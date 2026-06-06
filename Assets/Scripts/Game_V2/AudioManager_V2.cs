using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * AudioManager_V2 (Centralized SFX + music router)
     *
     * PURPOSE:
     * Owns runtime audio playback for gameplay, UI, impacts, and music loops.
     * Callers trigger semantic methods (PlayWeaponShot, PlayPurchaseSuccess, etc.) rather than handling clips directly.
     */
    [DisallowMultipleComponent]
    public sealed class AudioManager_V2 : MonoBehaviour
    {
        private static AudioManager_V2 s_instance;

        [Header("Aircraft")]
        [SerializeField] private AudioClip _bombPlaneLoop;
        [SerializeField] private AudioClip _droneLoop;
        [SerializeField] private AudioClip _helicopterLoop;

        [Header("Explosions")]
        [SerializeField] private AudioClip _grenadeExplosion;
        [SerializeField] private AudioClip _missileExplosion;

        [Header("Impacts")]
        [SerializeField] private AudioClip _bulletHitDirt;
        [SerializeField] private AudioClip _bulletHitFlesh;
        [SerializeField] private AudioClip _bulletHitMetal;

        [Header("Menu")]
        [SerializeField] private AudioClip _clickMenu;
        [SerializeField] private AudioClip _failure;
        [SerializeField] private AudioClip _levelDone;
        [SerializeField] private AudioClip _purchaseSuccess;
        [SerializeField] private AudioClip _settingsSuccess;

        [Header("Music")]
        [SerializeField] private AudioClip _bossMusic;
        [SerializeField] private AudioClip _gameMusic;
        [SerializeField] private AudioClip _menuMusic;

        [Header("Weapons")]
        [SerializeField] private AudioClip _bazookaShot;
        [SerializeField] private AudioClip _flamethrowerShot;
        [SerializeField] private AudioClip _machineGunShot;
        [SerializeField] private AudioClip _pistolShot;
        [SerializeField] private AudioClip _shotgunShot;
        [SerializeField] private AudioClip _reloadGun;
        [SerializeField] private AudioClip _reloadMachineGun;
        [SerializeField] private AudioClip _teslaShot;
        [SerializeField] private AudioClip _outOfAmmo;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _worldLoopVolume = 0.22f;

        // Keeps aircraft loop loudness proportional to the SFX slider default mix.
        private const float WorldLoopFromSfxRatio = 0.22f / 0.9f;

        private AudioSource _sfxSource;
        private AudioSource _musicSource;
        private AudioSource _aircraftSource;
        private AudioSource _droneSource;
        private AudioSource _continuousWeaponSource;
        private float _nextAircraftLoopRefreshAt;
        private float _nextMusicRefreshAt;
        private string _activeMusicKey = string.Empty;
        private bool _warnedNoMenuMusic;
        private bool _warnedNoGameMusic;
        private bool _warnedNoBossMusic;
        private bool _warnedNoSfxSource;
        private bool _warnedNoMusicSource;
        private bool _warnedNoOutOfAmmoClip;
        private bool _loggedStartupState;
        private static AudioManagerClipDefaults_V2 s_cachedClipDefaults;
        private float _nextMissileExplosionAllowedAt;
        private WeaponType _activeContinuousWeapon = WeaponType.None;

        private enum ImpactKind
        {
            Dirt,
            Flesh,
            Metal,
        }

        public static AudioManager_V2 EnsureInstance()
        {
            EnsureAudioListenerPresent();
            if (s_instance != null)
            {
                return s_instance;
            }

            s_instance = FindAnyObjectByType<AudioManager_V2>(FindObjectsInactive.Include);
            if (s_instance != null)
            {
                s_instance.EnsureSources();
                s_instance.LoadDefaultClipsIfMissing();
                return s_instance;
            }

            GameObject go = new GameObject("AudioManager_V2");
            s_instance = go.AddComponent<AudioManager_V2>();
            s_instance.EnsureSources();
            Debug.LogWarning(
                "[AudioManager_V2] No scene-bound AudioManager_V2 found. " +
                "Created runtime fallback object; assign one in the scene to edit clips in Inspector.");
            return s_instance;
        }

        public static void PlayMenuClick()
        {
            AudioManager_V2 audio = EnsureInstance();
            audio.EnsureClipsLoaded();
            audio.PlayOneShot(audio._clickMenu);
        }
        public static void PlayFailure() { AudioManager_V2 a = EnsureInstance(); a.EnsureClipsLoaded(); a.PlayOneShot(a._failure); }
        public static void PlayWaveComplete() { AudioManager_V2 a = EnsureInstance(); a.EnsureClipsLoaded(); a.PlayOneShot(a._levelDone); }
        public static void PlayPurchaseSuccess() { AudioManager_V2 a = EnsureInstance(); a.EnsureClipsLoaded(); a.PlayOneShot(a._purchaseSuccess); }
        public static void PlaySettingsSuccess() { AudioManager_V2 a = EnsureInstance(); a.EnsureClipsLoaded(); a.PlayOneShot(a._settingsSuccess); }
        public static void PlayGrenadeExplosion() { AudioManager_V2 a = EnsureInstance(); a.EnsureClipsLoaded(); a.PlayOneShot(a._grenadeExplosion); }
        public static void PlayMissileExplosion()
        {
            AudioManager_V2 a = EnsureInstance();
            a.EnsureClipsLoaded();
            a.PlayMissileExplosionOncePerImpactWindow();
        }

        public static void SetMenuMusic() { AudioManager_V2 a = EnsureInstance(); a.EnsureClipsLoaded(); a.PlayMusic(a._menuMusic); }
        public static void SetGameplayMusic() { AudioManager_V2 a = EnsureInstance(); a.EnsureClipsLoaded(); a.PlayMusic(a._gameMusic); }
        public static void SetBossMusic() { AudioManager_V2 a = EnsureInstance(); a.EnsureClipsLoaded(); a.PlayMusic(a._bossMusic); }

        public static void PlayWeaponShot(WeaponType weaponType)
        {
            AudioManager_V2 audio = EnsureInstance();
            audio.EnsureClipsLoaded();

            if (weaponType == WeaponType.Tesla || weaponType == WeaponType.Flamethrower)
            {
                audio.PlayContinuousWeaponLoop(weaponType);
                return;
            }

            AudioClip clip = weaponType switch
            {
                WeaponType.Colt45 => audio._pistolShot,
                WeaponType.Ithaca => audio._shotgunShot != null ? audio._shotgunShot : audio._pistolShot,
                WeaponType.Thompson => audio._machineGunShot,
                WeaponType.Bazooka => audio._bazookaShot,
                WeaponType.Tesla => audio._teslaShot,
                WeaponType.Flamethrower => audio._flamethrowerShot,
                _ => null,
            };

            audio.PlayOneShot(clip);
        }

        public static void StopContinuousWeaponShot(WeaponType weaponType)
        {
            AudioManager_V2 audio = EnsureInstance();
            if (weaponType == WeaponType.None)
            {
                audio.StopContinuousWeaponLoop(WeaponType.Tesla);
                audio.StopContinuousWeaponLoop(WeaponType.Flamethrower);
                return;
            }

            audio.StopContinuousWeaponLoop(weaponType);
        }

        public static void PlayWeaponReload(WeaponType weaponType)
        {
            AudioManager_V2 audio = EnsureInstance();
            audio.EnsureClipsLoaded();
            AudioClip clip = weaponType == WeaponType.Thompson || weaponType == WeaponType.Ithaca
                ? audio._reloadMachineGun
                : audio._reloadGun;
            audio.PlayOneShot(clip);
        }

        public static void PlayOutOfAmmo()
        {
            AudioManager_V2 audio = EnsureInstance();
            audio.EnsureClipsLoaded();
            AudioClip clip = audio._outOfAmmo != null ? audio._outOfAmmo : audio._failure;
            if (clip == null)
            {
                if (!audio._warnedNoOutOfAmmoClip)
                {
                    audio._warnedNoOutOfAmmoClip = true;
                    Debug.LogWarning("[AudioManager_V2] outOfAmmo clip is not assigned (no failure fallback).");
                }

                return;
            }

            audio.PlayOneShot(clip);
        }

        public static void PlayImpactForCollider(Collider2D collider)
        {
            if (collider == null)
            {
                return;
            }

            AudioManager_V2 audio = EnsureInstance();
            audio.EnsureClipsLoaded();
            ImpactKind kind = audio.ResolveImpactKind(collider);
            AudioClip clip = kind switch
            {
                ImpactKind.Flesh => audio._bulletHitFlesh,
                ImpactKind.Metal => audio._bulletHitMetal,
                _ => audio._bulletHitDirt,
            };
            audio.PlayOneShot(clip);
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            EnsureSources();
            ForceGlobalAudioEnabled();
            LoadDefaultClipsIfMissing();
            GameSettings_V2.LoadAndApplyAll();
            LogMissingClipSummaryOnce();
            LogStartupState();
        }

        public void ApplyVolumeSettings(float masterVolume, float musicVolume, float sfxVolume)
        {
            _masterVolume = Mathf.Clamp01(masterVolume);
            _musicVolume = Mathf.Clamp01(musicVolume);
            _sfxVolume = Mathf.Clamp01(sfxVolume);
            _worldLoopVolume = _sfxVolume * WorldLoopFromSfxRatio;
            RefreshAllSourceVolumes();
        }

        private float EffectiveMasterVolume => Mathf.Clamp01(_masterVolume);
        private float EffectiveMusicVolume => _musicVolume * EffectiveMasterVolume;
        private float EffectiveSfxVolume => _sfxVolume * EffectiveMasterVolume;
        private float EffectiveWorldLoopVolume => _worldLoopVolume * EffectiveMasterVolume;

        private void RefreshAllSourceVolumes()
        {
            if (_musicSource != null)
            {
                _musicSource.volume = EffectiveMusicVolume;
            }

            if (_continuousWeaponSource != null)
            {
                _continuousWeaponSource.volume = EffectiveSfxVolume;
            }

            RefreshAircraftLoops();
        }

        private void Start()
        {
            LoadDefaultClipsIfMissing();
            _loggedStartupState = false;
            LogStartupState();
        }

        private void Update()
        {
            ForceGlobalAudioEnabled();

            if (Time.unscaledTime >= _nextMusicRefreshAt)
            {
                _nextMusicRefreshAt = Time.unscaledTime + 0.5f;
                RefreshMusicByGameState();
            }

            if (Time.unscaledTime < _nextAircraftLoopRefreshAt)
            {
                return;
            }

            _nextAircraftLoopRefreshAt = Time.unscaledTime + 1f;
            RefreshAircraftLoops();
        }

        private void EnsureSources()
        {
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
                _sfxSource.spatialBlend = 0f;
                _sfxSource.ignoreListenerPause = true;
                _sfxSource.ignoreListenerVolume = true;
            }

            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.playOnAwake = false;
                _musicSource.loop = true;
                _musicSource.spatialBlend = 0f;
                _musicSource.ignoreListenerPause = true;
                _musicSource.ignoreListenerVolume = true;
                _musicSource.volume = EffectiveMusicVolume;
            }

            if (_aircraftSource == null)
            {
                _aircraftSource = gameObject.AddComponent<AudioSource>();
                _aircraftSource.playOnAwake = false;
                _aircraftSource.loop = true;
                _aircraftSource.spatialBlend = 0f;
                _aircraftSource.ignoreListenerPause = true;
                _aircraftSource.ignoreListenerVolume = true;
                _aircraftSource.volume = EffectiveWorldLoopVolume;
            }

            if (_droneSource == null)
            {
                _droneSource = gameObject.AddComponent<AudioSource>();
                _droneSource.playOnAwake = false;
                _droneSource.loop = true;
                _droneSource.spatialBlend = 0f;
                _droneSource.ignoreListenerPause = true;
                _droneSource.ignoreListenerVolume = true;
                _droneSource.volume = EffectiveWorldLoopVolume * 0.9f;
            }

            if (_continuousWeaponSource == null)
            {
                _continuousWeaponSource = gameObject.AddComponent<AudioSource>();
                _continuousWeaponSource.playOnAwake = false;
                _continuousWeaponSource.loop = true;
                _continuousWeaponSource.spatialBlend = 0f;
                _continuousWeaponSource.ignoreListenerPause = true;
                _continuousWeaponSource.ignoreListenerVolume = true;
                _continuousWeaponSource.volume = EffectiveSfxVolume;
            }
        }

        private void ForceGlobalAudioEnabled()
        {
            EnsureAudioListenerPresent();
            // Some editor/game-view states can leave listener volume muted.
            AudioListener.pause = false;
            AudioListener.volume = 1f;
        }

        // MainMenuScene boot and runtime AudioManager fallback must never leave the scene without a listener.
        private static void EnsureAudioListenerPresent()
        {
            if (FindAnyObjectByType<AudioListener>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
                if (cameras.Length > 0)
                {
                    mainCamera = cameras[0];
                }
            }

            if (mainCamera != null)
            {
                if (mainCamera.GetComponent<AudioListener>() == null)
                {
                    mainCamera.gameObject.AddComponent<AudioListener>();
                }

                return;
            }

            GameObject listenerRoot = new GameObject("AudioListener_V2");
            listenerRoot.AddComponent<AudioListener>();
        }

        private void EnsureClipsLoaded()
        {
            LoadDefaultClipsIfMissing();
        }

        private void LoadDefaultClipsIfMissing()
        {
            TryApplyDefaultsFromResources();
#if UNITY_EDITOR
            TryApplyDefaultsFromEditorAssetDatabase();
#endif
        }

        private bool HasCoreClipsAssigned()
        {
            return _menuMusic != null && _gameMusic != null && _bossMusic != null && _clickMenu != null;
        }

        private void TryApplyDefaultsFromResources()
        {
            if (s_cachedClipDefaults == null)
            {
                s_cachedClipDefaults = Resources.Load<AudioManagerClipDefaults_V2>(AudioManagerClipDefaults_V2.ResourcesPath);
            }

            if (s_cachedClipDefaults == null)
            {
                return;
            }

            AudioManagerClipDefaults_V2 d = s_cachedClipDefaults;
            _bombPlaneLoop ??= d.bombPlaneLoop;
            _droneLoop ??= d.droneLoop;
            _helicopterLoop ??= d.helicopterLoop;
            _grenadeExplosion ??= d.grenadeExplosion;
            _missileExplosion ??= d.missileExplosion;
            _bulletHitDirt ??= d.bulletHitDirt;
            _bulletHitFlesh ??= d.bulletHitFlesh;
            _bulletHitMetal ??= d.bulletHitMetal;
            _clickMenu ??= d.clickMenu;
            _failure ??= d.failure;
            _levelDone ??= d.levelDone;
            _purchaseSuccess ??= d.purchaseSuccess;
            _settingsSuccess ??= d.settingsSuccess;
            _bossMusic ??= d.bossMusic;
            _gameMusic ??= d.gameMusic;
            _menuMusic ??= d.menuMusic;
            _bazookaShot ??= d.bazookaShot;
            _flamethrowerShot ??= d.flamethrowerShot;
            _machineGunShot ??= d.machineGunShot;
            _pistolShot ??= d.pistolShot;
            _shotgunShot ??= d.shotgunShot;
            _reloadGun ??= d.reloadGun;
            _reloadMachineGun ??= d.reloadMachineGun;
            _teslaShot ??= d.teslaShot;
            _outOfAmmo ??= d.outOfAmmo;
        }

#if UNITY_EDITOR
        private void TryApplyDefaultsFromEditorAssetDatabase()
        {
            _bombPlaneLoop ??= LoadEditorClip("Aircraft/bombplane.mp3");
            _droneLoop ??= LoadEditorClip("Aircraft/drone.wav");
            _helicopterLoop ??= LoadEditorClip("Aircraft/helicopter.mp3");
            _grenadeExplosion ??= LoadEditorClip("Explosions/grenadeExplosion.mp3");
            _missileExplosion ??= LoadEditorClip("Explosions/missileExplosion.wav");
            _bulletHitDirt ??= LoadEditorClip("Impact/bulletHitDirt.mp3");
            _bulletHitFlesh ??= LoadEditorClip("Impact/bulletHitFlesh.mp3");
            _bulletHitMetal ??= LoadEditorClip("Impact/bulletHitMetal2.mp3", "Impact/bulletHitMetal.mp3");
            _clickMenu ??= LoadEditorClip("Menu/clickMenu.wav");
            _failure ??= LoadEditorClip("Menu/failure.mp3");
            _levelDone ??= LoadEditorClip("Menu/levelDone.mp3");
            _purchaseSuccess ??= LoadEditorClip("Menu/purchaseSuccess2.mp3", "Menu/purchaseSuccess.mp3");
            _settingsSuccess ??= LoadEditorClip("Menu/settingsSuccess.mp3");
            _bossMusic ??= LoadEditorClip("Music/bossMusic2.mp3", "Music/bossMusic.mp3");
            _gameMusic ??= LoadEditorClip("Music/gameMusic.mp3");
            _menuMusic ??= LoadEditorClip("Music/menuMusic.mp3");
            _bazookaShot ??= LoadEditorClip("Weapons/bazooka.wav");
            _flamethrowerShot ??= LoadEditorClip("Weapons/flamethrower.wav");
            _machineGunShot ??= LoadEditorClip("Weapons/machineGun.wav");
            _pistolShot ??= LoadEditorClip("Weapons/pistol.wav");
            _shotgunShot ??= LoadEditorClip("Weapons/shotgun.mp3");
            _shotgunShot ??= LoadEditorClip("Weapons/shotGun.wav");
            _shotgunShot ??= LoadEditorClip("Weapons/pistol.wav");
            _reloadGun ??= LoadEditorClip("Weapons/reloadGun.mp3");
            _reloadMachineGun ??= LoadEditorClip("Weapons/reloadMachineGun.mp3");
            _teslaShot ??= LoadEditorClip("Weapons/teslaGun.mp3");
            _outOfAmmo ??= LoadEditorClip("Weapons/outOfAmmo.mp3");
        }

        private static AudioClip LoadEditorClip(string primaryRelativePath, string alternateRelativePath = null)
        {
            AudioClip clip = LoadEditorClipAtRelativePath(primaryRelativePath);
            if (clip != null || string.IsNullOrEmpty(alternateRelativePath))
            {
                return clip;
            }

            return LoadEditorClipAtRelativePath(alternateRelativePath);
        }

        private static AudioClip LoadEditorClipAtRelativePath(string relativePath)
        {
            string soundsPath = $"Assets/Sounds/Audio/{relativePath}";
            AudioClip fromSounds = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(soundsPath);
            if (fromSounds != null)
            {
                return fromSounds;
            }

            string audioPath = $"Assets/Audio/{relativePath}";
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
        }
#endif

        private void LogStartupState()
        {
            if (_loggedStartupState)
            {
                return;
            }

            _loggedStartupState = true;
            string defaultsSource = s_cachedClipDefaults != null ? "Resources" : "none";
            Debug.Log(
                "[AudioManager_V2] Ready. " +
                $"listenerPause={AudioListener.pause}, listenerVolume={AudioListener.volume:0.00}, " +
                $"defaults={defaultsSource}, " +
                $"clips(menu/game/boss/click)=({_menuMusic != null}/{_gameMusic != null}/{_bossMusic != null}/{_clickMenu != null}).");
        }

        private void PlayMusic(AudioClip clip)
        {
            if (_musicSource == null)
            {
                if (!_warnedNoMusicSource)
                {
                    _warnedNoMusicSource = true;
                    Debug.LogWarning("[AudioManager_V2] Missing music AudioSource.");
                }

                return;
            }

            if (clip == null)
            {
                return;
            }

            if (_musicSource.clip == clip && _musicSource.isPlaying)
            {
                return;
            }

            _musicSource.clip = clip;
            _musicSource.volume = EffectiveMusicVolume;
            _musicSource.Play();
        }

        private void RefreshMusicByGameState()
        {
            WaveManager_V2 waveManager = FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Include);
            if (waveManager == null)
            {
                SetMusicByKey("menu", _menuMusic, ref _warnedNoMenuMusic);
                return;
            }

            bool bossActive = IsBossActive();
            if (bossActive)
            {
                SetMusicByKey("boss", _bossMusic, ref _warnedNoBossMusic);
                return;
            }

            switch (waveManager.State)
            {
                case WaveLoopState_V2.Preparing:
                case WaveLoopState_V2.InWave:
                case WaveLoopState_V2.Shop:
                    SetMusicByKey("game", _gameMusic, ref _warnedNoGameMusic);
                    break;
                default:
                    SetMusicByKey("menu", _menuMusic, ref _warnedNoMenuMusic);
                    break;
            }
        }

        private bool IsBossActive()
        {
            MechRobotBossModel_V2 boss = FindAnyObjectByType<MechRobotBossModel_V2>(FindObjectsInactive.Exclude);
            return boss != null && !boss.IsDead();
        }

        private void SetMusicByKey(string key, AudioClip clip, ref bool warnedMissingClip)
        {
            if (_activeMusicKey == key && _musicSource != null && _musicSource.isPlaying)
            {
                return;
            }

            _activeMusicKey = key ?? string.Empty;
            if (clip == null)
            {
                if (!warnedMissingClip)
                {
                    warnedMissingClip = true;
                    Debug.LogWarning($"[AudioManager_V2] Missing music clip for key '{_activeMusicKey}'.");
                }

                return;
            }

            warnedMissingClip = false;
            PlayMusic(clip);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (_sfxSource == null)
            {
                if (!_warnedNoSfxSource)
                {
                    _warnedNoSfxSource = true;
                    Debug.LogWarning("[AudioManager_V2] Missing SFX AudioSource.");
                }

                return;
            }

            if (clip == null)
            {
                return;
            }

            _sfxSource.PlayOneShot(clip, EffectiveSfxVolume);
        }

        private void PlayMissileExplosionOncePerImpactWindow()
        {
            // Prevent duplicate blast sounds when the same explosion can reach multiple callbacks
            // in a single physics/update window.
            float now = Time.unscaledTime;
            if (now < _nextMissileExplosionAllowedAt)
            {
                return;
            }

            _nextMissileExplosionAllowedAt = now + 0.08f;
            PlayOneShot(_missileExplosion);
        }

        private void PlayContinuousWeaponLoop(WeaponType weaponType)
        {
            if (_continuousWeaponSource == null)
            {
                return;
            }

            AudioClip clip = weaponType switch
            {
                WeaponType.Tesla => _teslaShot,
                WeaponType.Flamethrower => _flamethrowerShot,
                _ => null,
            };

            if (clip == null)
            {
                return;
            }

            _continuousWeaponSource.volume = EffectiveSfxVolume;
            if (_activeContinuousWeapon == weaponType &&
                _continuousWeaponSource.clip == clip &&
                _continuousWeaponSource.isPlaying)
            {
                return;
            }

            _activeContinuousWeapon = weaponType;
            _continuousWeaponSource.clip = clip;
            _continuousWeaponSource.loop = true;
            _continuousWeaponSource.Play();
        }

        private void StopContinuousWeaponLoop(WeaponType weaponType)
        {
            if (_continuousWeaponSource == null)
            {
                return;
            }

            if (weaponType != WeaponType.None && _activeContinuousWeapon != weaponType)
            {
                return;
            }

            if (_continuousWeaponSource.isPlaying)
            {
                _continuousWeaponSource.Stop();
            }

            _continuousWeaponSource.clip = null;
            _activeContinuousWeapon = WeaponType.None;
        }

        private void LogMissingClipSummaryOnce()
        {
            if (_menuMusic == null)
            {
                Debug.LogWarning("[AudioManager_V2] menuMusic clip is not assigned.");
            }

            if (_gameMusic == null)
            {
                Debug.LogWarning("[AudioManager_V2] gameMusic clip is not assigned.");
            }

            if (_bossMusic == null)
            {
                Debug.LogWarning("[AudioManager_V2] bossMusic2 clip is not assigned.");
            }

            if (_clickMenu == null)
            {
                Debug.LogWarning("[AudioManager_V2] clickMenu clip is not assigned.");
            }

            if (_outOfAmmo == null)
            {
                Debug.LogWarning("[AudioManager_V2] outOfAmmo clip is not assigned.");
            }
        }

        private void RefreshAircraftLoops()
        {
            bool hasBombPlane = FindAnyObjectByType<Bombplane_V2>(FindObjectsInactive.Exclude) != null;
            bool hasHelicopter = FindAnyObjectByType<Helicopter_V2>(FindObjectsInactive.Exclude) != null;
            bool hasDrone =
                FindAnyObjectByType<BombDrone_V2>(FindObjectsInactive.Exclude) != null ||
                FindAnyObjectByType<KamikazeDrone_V2>(FindObjectsInactive.Exclude) != null;

            AudioClip aircraftClip = hasBombPlane ? _bombPlaneLoop : hasHelicopter ? _helicopterLoop : null;
            UpdateLoopSource(_aircraftSource, aircraftClip, EffectiveWorldLoopVolume);
            UpdateLoopSource(_droneSource, hasDrone ? _droneLoop : null, EffectiveWorldLoopVolume * 0.9f);
        }

        private static void UpdateLoopSource(AudioSource source, AudioClip clip, float volume)
        {
            if (source == null)
            {
                return;
            }

            if (clip == null)
            {
                if (source.isPlaying)
                {
                    source.Stop();
                }
                source.clip = null;
                return;
            }

            source.volume = volume;
            if (source.clip != clip)
            {
                source.clip = clip;
            }

            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private ImpactKind ResolveImpactKind(Collider2D collider)
        {
            if (collider.GetComponentInParent<Hero_V2>() != null ||
                collider.GetComponentInParent<ParatrooperModel_V2>() != null)
            {
                return ImpactKind.Flesh;
            }

            if (collider.GetComponentInParent<AircraftHealth_V2>() != null ||
                collider.GetComponentInParent<MechRobotBossModel_V2>() != null)
            {
                return ImpactKind.Metal;
            }

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0 && collider.gameObject.layer == groundLayer)
            {
                return ImpactKind.Dirt;
            }

            return collider.GetComponentInParent<BunkerHitbox_V2>() != null ? ImpactKind.Metal : ImpactKind.Dirt;
        }

    }
}
