using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using iStick2War;

namespace iStick2War_V2
{
    /// <summary>
    /// Writes one JSON document per session: <c>{ "events": [ ... ] }</c> (pretty-printed).
    /// Tracks combat, bunker time, bunker damage, and shop spend between waves (attributed to the wave that just ended).
    /// <c>session_begin</c> is written one frame after enable so <see cref="WaveManager_V2"/> economy/bunker has initialized.
    /// </summary>
    public sealed class WaveRunTelemetry_V2 : MonoBehaviour
    {
        public static WaveRunTelemetry_V2 ActiveInstance { get; private set; }

        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private bool _telemetryEnabled = true;
        [SerializeField] private string _fileNamePrefix = "wave_run";
        [SerializeField] private bool _logFilePathOnce = true;

        private string _sessionId;
        private string _filePath;
        private WaveLoopState_V2 _lastState;
        private float _waveStartedRealtime;
        private bool _runEndWritten;
        private bool _sessionBeginWritten;
        private Coroutine _sessionBeginCoroutine;

        private Hero_V2 _subscribedHero;

        // --- Per-wave combat (reset when entering InWave) ---
        private int _heroDamageTakenDuringWave;
        private int _heroHealedDuringWave;
        private int _bunkerDamageTakenDuringWave;
        private float _timeInBunkerUnscaledDuringWave;
        private int _shotsFiredDuringWave;
        private int _rayHitsDuringWave;
        private int _rayMissesDuringWave;
        private int _projectileLaunchesDuringWave;
        private int _reloadsDuringWave;

        // Snapshots at wave start (InWave)
        private int _heroHpAtWaveStart;
        private int _bunkerHpAtWaveStart;
        private int _currencyAtWaveStart;

        // Shop intermission before the wave that just cleared (set when leaving Shop → Preparing)
        private int _shopPurchasesPriorToWaveJustCleared;
        private int _shopCurrencySpentPriorToWaveJustCleared;

        private int _intermissionShopPurchaseCount;
        private int _intermissionShopCurrencySpent;

        [Serializable]
        private sealed class TelemetryFileRoot
        {
            public TelemetryEvent[] events;
        }

        [Serializable]
        private sealed class TelemetryEvent
        {
            public string kind;
            public string sessionId;
            public float realtimeSinceStartup;
            public int wave;
            public float waveDurationSec;
            public int heroHp;
            public int heroMaxHp;
            public int bunkerHp;
            public int bunkerMaxHp;
            public int currency;
            public int enemiesKilled;
            public string weapon;
            public string endReason;

            public int damageTakenHero;
            public int healingHero;
            public int damageTakenBunker;
            public float timeInBunkerSec;
            public int shotsFired;
            public int rayHits;
            public int rayMisses;
            public int projectileLaunches;
            public int reloads;
            public int shopPurchasesPrior;
            public int shopCurrencySpentPrior;
            public int heroHpWaveStart;
            public int bunkerHpWaveStart;
            public int currencyWaveStart;
            public string weaponType;
            /// <summary><see cref="AutoHeroTestProfileKind_V2"/> on the hero when present; empty if no bot / disabled.</summary>
            public string autoHeroTestProfile;
        }

        private void Awake()
        {
            _sessionId = Guid.NewGuid().ToString("N");
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            ResolveWaveManager();
            if (_waveManager == null)
            {
                return;
            }

            _lastState = _waveManager.State;
            _waveManager.OnStateChanged += OnWaveStateChanged;
            if (_waveManager.State == WaveLoopState_V2.InWave)
            {
                _waveStartedRealtime = Time.realtimeSinceStartup;
            }

            SubscribeHeroIfPossible();

            if (_telemetryEnabled && !_sessionBeginWritten)
            {
                if (_sessionBeginCoroutine != null)
                {
                    StopCoroutine(_sessionBeginCoroutine);
                }

                _sessionBeginCoroutine = StartCoroutine(WriteSessionBeginAfterManagersInitialized());
            }
        }

        private IEnumerator WriteSessionBeginAfterManagersInitialized()
        {
            yield return null;
            _sessionBeginCoroutine = null;
            SubscribeHeroIfPossible();
            TryWriteSessionBegin();
        }

        private void Update()
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            Hero_V2 hero = FindHero();
            if (hero != null && _waveManager.IsHeroInsideBunker(hero))
            {
                _timeInBunkerUnscaledDuringWave += Time.unscaledDeltaTime;
            }
        }

        private void OnDisable()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            if (_sessionBeginCoroutine != null)
            {
                StopCoroutine(_sessionBeginCoroutine);
                _sessionBeginCoroutine = null;
            }

            UnsubscribeHero();
            if (_waveManager != null)
            {
                _waveManager.OnStateChanged -= OnWaveStateChanged;
            }
        }

        private void OnApplicationQuit()
        {
            TryWriteSessionQuit();
        }

        /// <summary>Called from <see cref="WaveManager_V2"/> when bunker loses HP.</summary>
        public static void NotifyBunkerDamageTaken(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            ActiveInstance?.RegisterBunkerDamageTaken(amount);
        }

        /// <summary>Called from <see cref="WaveManager_V2"/> after a successful shop spend.</summary>
        public static void NotifyShopPurchase(string offerKind, int currencySpent)
        {
            ActiveInstance?.RegisterShopPurchase(offerKind, currencySpent);
        }

        private void RegisterBunkerDamageTaken(int amount)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _bunkerDamageTakenDuringWave += Mathf.Max(0, amount);
        }

        private void RegisterShopPurchase(string offerKind, int currencySpent)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.Shop)
            {
                return;
            }

            _intermissionShopPurchaseCount++;
            _intermissionShopCurrencySpent += Mathf.Max(0, currencySpent);
        }

        private void ResolveWaveManager()
        {
            if (_waveManager != null)
            {
                return;
            }

            _waveManager = FindAnyObjectByType<WaveManager_V2>();
            if (_waveManager == null)
            {
                Debug.LogWarning("[WaveRunTelemetry_V2] No WaveManager_V2 found; telemetry disabled.");
            }
        }

        private void SubscribeHeroIfPossible()
        {
            Hero_V2 hero = FindHero();
            if (hero == null)
            {
                UnsubscribeHero();
                return;
            }

            // First call can run before Hero_V2.Awake finishes (telemetry OnEnable vs script order).
            // Older logic bailed when hero == _subscribedHero, so WeaponSystem/DamageReceiver stayed
            // unsubscribed forever. Re-wire idempotently whenever the same hero gains systems.
            if (_subscribedHero != null && _subscribedHero != hero)
            {
                UnsubscribeHero();
            }

            _subscribedHero = hero;

            if (_subscribedHero.DamageReceiver != null)
            {
                _subscribedHero.DamageReceiver.OnDamageTaken -= OnHeroDamageTaken;
                _subscribedHero.DamageReceiver.OnDamageTaken += OnHeroDamageTaken;
            }

            _subscribedHero.OnHealed -= OnHeroHealed;
            _subscribedHero.OnHealed += OnHeroHealed;

            if (_subscribedHero.WeaponSystem != null)
            {
                _subscribedHero.WeaponSystem.OnCommittedAttack -= OnWeaponCommittedAttack;
                _subscribedHero.WeaponSystem.OnCommittedAttack += OnWeaponCommittedAttack;
                _subscribedHero.WeaponSystem.OnReloadCompleted -= OnWeaponReloadCompleted;
                _subscribedHero.WeaponSystem.OnReloadCompleted += OnWeaponReloadCompleted;
            }
        }

        private void UnsubscribeHero()
        {
            if (_subscribedHero == null)
            {
                return;
            }

            if (_subscribedHero.DamageReceiver != null)
            {
                _subscribedHero.DamageReceiver.OnDamageTaken -= OnHeroDamageTaken;
            }

            _subscribedHero.OnHealed -= OnHeroHealed;
            if (_subscribedHero.WeaponSystem != null)
            {
                _subscribedHero.WeaponSystem.OnCommittedAttack -= OnWeaponCommittedAttack;
                _subscribedHero.WeaponSystem.OnReloadCompleted -= OnWeaponReloadCompleted;
            }

            _subscribedHero = null;
        }

        private void OnHeroDamageTaken(int amount)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _heroDamageTakenDuringWave += Mathf.Max(0, amount);
        }

        private void OnHeroHealed(int amount)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _heroHealedDuringWave += Mathf.Max(0, amount);
        }

        private void OnWeaponCommittedAttack(WeaponType weaponType, bool isProjectile, bool rayDidHit)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _shotsFiredDuringWave++;
            if (isProjectile)
            {
                _projectileLaunchesDuringWave++;
            }
            else if (rayDidHit)
            {
                _rayHitsDuringWave++;
            }
            else
            {
                _rayMissesDuringWave++;
            }
        }

        private void OnWeaponReloadCompleted(WeaponType _)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _reloadsDuringWave++;
        }

        private void TryWriteSessionBegin()
        {
            if (!_telemetryEnabled || _waveManager == null || _sessionBeginWritten)
            {
                return;
            }

            _sessionBeginWritten = true;
            EnsureOutputPath();
            // session_begin is emitted before the first InWave transition, so wave-start fields would
            // otherwise stay default (0). Snapshot current economy/HP for a consistent first row.
            SnapshotWaveStartEconomy();
            AppendEvent(
                BuildTelemetryEvent(
                    "session_begin",
                    wave: _waveManager.CurrentWaveNumber,
                    waveDurationSec: 0f,
                    enemiesKilled: 0,
                    endReason: ""));
        }

        private void OnWaveStateChanged(WaveLoopState_V2 newState)
        {
            if (!_telemetryEnabled || _waveManager == null)
            {
                _lastState = newState;
                return;
            }

            if (_lastState == WaveLoopState_V2.Shop && newState == WaveLoopState_V2.Preparing)
            {
                _shopPurchasesPriorToWaveJustCleared = _intermissionShopPurchaseCount;
                _shopCurrencySpentPriorToWaveJustCleared = _intermissionShopCurrencySpent;
                _intermissionShopPurchaseCount = 0;
                _intermissionShopCurrencySpent = 0;
            }

            if (_lastState != WaveLoopState_V2.InWave && newState == WaveLoopState_V2.InWave)
            {
                _waveStartedRealtime = Time.realtimeSinceStartup;
                ResetWaveCombatAccumulators();
                SnapshotWaveStartEconomy();
                // Hero reference may point at an inactive duplicate; re-resolve before combat hooks.
                SubscribeHeroIfPossible();
            }

            if (_lastState == WaveLoopState_V2.InWave && newState == WaveLoopState_V2.Shop)
            {
                float duration = Mathf.Max(0f, Time.realtimeSinceStartup - _waveStartedRealtime);
                AppendEvent(
                    BuildTelemetryEvent(
                        "wave_cleared",
                        wave: _waveManager.CurrentWaveNumber,
                        waveDurationSec: duration,
                        enemiesKilled: _waveManager.EnemiesKilledThisWave,
                        endReason: ""));
            }

            if (newState == WaveLoopState_V2.GameOver)
            {
                WriteRunEnd(_lastState);
            }

            _lastState = newState;
        }

        private void ResetWaveCombatAccumulators()
        {
            _heroDamageTakenDuringWave = 0;
            _heroHealedDuringWave = 0;
            _bunkerDamageTakenDuringWave = 0;
            _timeInBunkerUnscaledDuringWave = 0f;
            _shotsFiredDuringWave = 0;
            _rayHitsDuringWave = 0;
            _rayMissesDuringWave = 0;
            _projectileLaunchesDuringWave = 0;
            _reloadsDuringWave = 0;
        }

        private void SnapshotWaveStartEconomy()
        {
            Hero_V2 h = FindHero();
            _heroHpAtWaveStart = h != null ? h.GetCurrentHealth() : -1;
            _bunkerHpAtWaveStart = _waveManager != null ? _waveManager.BunkerHealth : -1;
            _currencyAtWaveStart = _waveManager != null ? _waveManager.Currency : -1;
        }

        private TelemetryEvent BuildTelemetryEvent(
            string kind,
            int wave,
            float waveDurationSec,
            int enemiesKilled,
            string endReason)
        {
            Hero_V2 h = FindHero();
            int heroHp = h != null ? h.GetCurrentHealth() : -1;
            int heroMax = h != null ? h.GetMaxHealth() : -1;
            string weaponLabel = h != null ? h.GetCurrentWeaponDisplayName() : "";
            string weaponTypeStr = h != null ? h.CurrentWeaponType.ToString() : "";
            string autoHeroProfile = ResolveAutoHeroTestProfileLabel(h);

            return new TelemetryEvent
            {
                kind = kind,
                sessionId = _sessionId,
                realtimeSinceStartup = Time.realtimeSinceStartup,
                wave = wave,
                waveDurationSec = waveDurationSec,
                heroHp = heroHp,
                heroMaxHp = heroMax,
                bunkerHp = _waveManager != null ? _waveManager.BunkerHealth : -1,
                bunkerMaxHp = _waveManager != null ? _waveManager.BunkerMaxHealth : -1,
                currency = _waveManager != null ? _waveManager.Currency : -1,
                enemiesKilled = enemiesKilled,
                weapon = weaponLabel,
                endReason = endReason ?? "",
                damageTakenHero = _heroDamageTakenDuringWave,
                healingHero = _heroHealedDuringWave,
                damageTakenBunker = _bunkerDamageTakenDuringWave,
                timeInBunkerSec = _timeInBunkerUnscaledDuringWave,
                shotsFired = _shotsFiredDuringWave,
                rayHits = _rayHitsDuringWave,
                rayMisses = _rayMissesDuringWave,
                projectileLaunches = _projectileLaunchesDuringWave,
                reloads = _reloadsDuringWave,
                shopPurchasesPrior = _shopPurchasesPriorToWaveJustCleared,
                shopCurrencySpentPrior = _shopCurrencySpentPriorToWaveJustCleared,
                heroHpWaveStart = _heroHpAtWaveStart,
                bunkerHpWaveStart = _bunkerHpAtWaveStart,
                currencyWaveStart = _currencyAtWaveStart,
                weaponType = weaponTypeStr,
                autoHeroTestProfile = autoHeroProfile
            };
        }

        private static string ResolveAutoHeroTestProfileLabel(Hero_V2 hero)
        {
            if (hero == null)
            {
                return "";
            }

            AutoHero_V2 bot = hero.GetComponent<AutoHero_V2>();
            if (bot == null || !bot.isActiveAndEnabled)
            {
                return "";
            }

            return bot.TestProfile.ToString();
        }

        private void WriteRunEnd(WaveLoopState_V2 previousState)
        {
            if (_runEndWritten || !_telemetryEnabled || _waveManager == null)
            {
                return;
            }

            _runEndWritten = true;
            string reason = ResolveGameOverReason(previousState);
            AppendEvent(
                BuildTelemetryEvent(
                    "run_end",
                    wave: _waveManager.CurrentWaveNumber,
                    waveDurationSec: 0f,
                    enemiesKilled: _waveManager.EnemiesKilledThisWave,
                    endReason: reason));
        }

        private string ResolveGameOverReason(WaveLoopState_V2 previousState)
        {
            Hero_V2 hero = FindHero();
            if (hero != null && hero.IsDead())
            {
                return "hero_death";
            }

            if (previousState == WaveLoopState_V2.InWave)
            {
                return "wave_config_missing_or_empty";
            }

            if (previousState == WaveLoopState_V2.Preparing)
            {
                return "no_wave_config_at_wave_start";
            }

            return "game_over_other";
        }

        private void TryWriteSessionQuit()
        {
            if (!_telemetryEnabled || _waveManager == null || _runEndWritten)
            {
                return;
            }

            if (_waveManager.State != WaveLoopState_V2.GameOver)
            {
                AppendEvent(
                    BuildTelemetryEvent(
                        "session_quit",
                        wave: _waveManager.CurrentWaveNumber,
                        waveDurationSec: 0f,
                        enemiesKilled: _waveManager.EnemiesKilledThisWave,
                        endReason: "editor_or_app_quit"));
            }
        }

        private Hero_V2 FindHero()
        {
            if (_waveManager != null && _waveManager.Hero != null)
            {
                Hero_V2 assigned = _waveManager.Hero;
                if (assigned.gameObject.activeInHierarchy)
                {
                    return assigned;
                }
            }

            return FindAnyObjectByType<Hero_V2>(FindObjectsInactive.Exclude);
        }

        private void EnsureOutputPath()
        {
            if (!string.IsNullOrEmpty(_filePath))
            {
                return;
            }

            string dir = Path.Combine(Application.persistentDataPath, "iStick2WarTelemetry");
            Directory.CreateDirectory(dir);
            string safePrefix = string.IsNullOrWhiteSpace(_fileNamePrefix) ? "wave_run" : _fileNamePrefix.Trim();
            _filePath = Path.Combine(dir, $"{safePrefix}_{_sessionId}.json");

            if (_logFilePathOnce)
            {
                Debug.Log($"[WaveRunTelemetry_V2] Writing session log to: {_filePath}");
            }
        }

        private void AppendEvent(TelemetryEvent payload)
        {
            try
            {
                EnsureOutputPath();
                TelemetryFileRoot root = ReadRootOrNew();
                var list = new List<TelemetryEvent>(root.events ?? Array.Empty<TelemetryEvent>());
                list.Add(payload);
                root.events = list.ToArray();
                string json = JsonUtility.ToJson(root, prettyPrint: true);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WaveRunTelemetry_V2] Failed to write telemetry: {ex.Message}");
            }
        }

        private TelemetryFileRoot ReadRootOrNew()
        {
            if (!File.Exists(_filePath))
            {
                return new TelemetryFileRoot { events = Array.Empty<TelemetryEvent>() };
            }

            string text = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new TelemetryFileRoot { events = Array.Empty<TelemetryEvent>() };
            }

            try
            {
                TelemetryFileRoot root = JsonUtility.FromJson<TelemetryFileRoot>(text);
                if (root == null)
                {
                    return new TelemetryFileRoot { events = Array.Empty<TelemetryEvent>() };
                }

                if (root.events == null)
                {
                    root.events = Array.Empty<TelemetryEvent>();
                }

                return root;
            }
            catch (Exception)
            {
                return new TelemetryFileRoot { events = Array.Empty<TelemetryEvent>() };
            }
        }
    }
}
