using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace iStick2War_V2
{
    public enum HealthBarCanvasBindMode
    {
        Hero,
        Paratrooper,
        Bunker
    }

    /// <summary>
    /// Drives UI Images (green fill = Filled type; optional red damage strip) from hero, paratrooper, or bunker HP.
    /// For world placement over units, add <see cref="WorldHealthBarFollower_V2"/> and use a World Space canvas.
    /// </summary>
    public class HealthBarCanvas_V2 : MonoBehaviour
    {
        [SerializeField] private HealthBarCanvasBindMode _bindMode = HealthBarCanvasBindMode.Hero;

        [Tooltip("Optional for Hero mode when WaveManager has no Hero yet. WaveManager.Hero wins when assigned.")]
        [SerializeField] private Hero_V2 _hero;

        [Tooltip("Paratrooper mode: leave empty to use GetComponentInParent<ParatrooperModel_V2>().")]
        [SerializeField] private ParatrooperModel_V2 _paratrooperModel;

        [Tooltip("Bunker mode: optional explicit WaveManager (e.g. drag from scene). When set, overrides FindAnyObjectByType.")]
        [SerializeField] private WaveManager_V2 _waveManager;

        [Header("Images")]
        [Tooltip("Green bar. Inspector: Image Type = Filled, Fill Method = Horizontal (usually).")]
        [SerializeField] private Image _healthFill;

        [Tooltip("Optional. If assigned and Sync Damage Fill is on: Filled image for the lost-HP strip (1 − ratio).")]
        [SerializeField] private Image _damageFill;

        [SerializeField] private bool _syncDamageFill = true;

        [FormerlySerializedAs("_zeroBarWhenHeroDead")]
        [Tooltip("When bound unit is dead (hero or paratrooper), force empty green / full damage fill.")]
        [SerializeField] private bool _zeroBarWhenDead = true;

        private WaveManager_V2 _cachedWaveManager;
        private bool _warnedHealthImageNotFilled;
        private bool _warnedParatrooperModelMissing;
        private bool _warnedBunkerWaveManagerMissing;
        private bool _paratrooperModelFromExternal;

        /// <summary>
        /// Use when this canvas is not parented under the paratrooper (unparented world bar avoids scale shear).
        /// </summary>
        public void SetParatrooperModelExternal(ParatrooperModel_V2 model)
        {
            _paratrooperModel = model;
            _paratrooperModelFromExternal = model != null;
        }

        protected virtual void Awake()
        {
            ResolveSourcesIfNeeded();
        }

        private void LateUpdate()
        {
            if (_healthFill == null)
            {
                return;
            }

            if (!_warnedHealthImageNotFilled && _healthFill.type != Image.Type.Filled)
            {
                _warnedHealthImageNotFilled = true;
                Debug.LogWarning(
                    "[HealthBarCanvas_V2] Health Fill must use Image Type = Filled. " +
                    "With Simple, changing fillAmount does not change what you see on screen.",
                    this);
            }

            ResolveSourcesIfNeeded();

            if (!TryGetHealthRatio(out float ratio, out bool dead))
            {
                return;
            }

            if (_zeroBarWhenDead && dead)
            {
                _healthFill.fillAmount = 0f;
                if (_syncDamageFill && _damageFill != null)
                {
                    _damageFill.fillAmount = 1f;
                }

                return;
            }

            _healthFill.fillAmount = ratio;
            if (_syncDamageFill && _damageFill != null)
            {
                _damageFill.fillAmount = 1f - ratio;
            }
        }

        private bool TryGetHealthRatio(out float ratio, out bool dead)
        {
            ratio = 0f;
            dead = false;

            switch (_bindMode)
            {
                case HealthBarCanvasBindMode.Hero:
                    if (_hero == null)
                    {
                        return false;
                    }

                    dead = _hero.IsDead();
                    if (dead)
                    {
                        return true;
                    }

                    int maxH = Mathf.Max(1, _hero.GetMaxHealth());
                    ratio = Mathf.Clamp01((float)_hero.GetCurrentHealth() / maxH);
                    return true;

                case HealthBarCanvasBindMode.Paratrooper:
                    if (_paratrooperModel == null)
                    {
                        if (!_warnedParatrooperModelMissing)
                        {
                            _warnedParatrooperModelMissing = true;
                            Debug.LogWarning(
                                "[HealthBarCanvas_V2] Paratrooper mode: no ParatrooperModel_V2 found. " +
                                "Keep this canvas under the paratrooper root (same prefab hierarchy) so the model can be resolved.",
                                this);
                        }

                        return false;
                    }

                    dead = _paratrooperModel.IsDead();
                    if (dead)
                    {
                        return true;
                    }

                    float maxP = Mathf.Max(1f, _paratrooperModel.maxHealth);
                    ratio = Mathf.Clamp01(_paratrooperModel.health / maxP);
                    return true;

                case HealthBarCanvasBindMode.Bunker:
                {
                    WaveManager_V2 wm = ResolveWaveManagerForBunker();
                    if (wm == null)
                    {
                        if (!_warnedBunkerWaveManagerMissing)
                        {
                            _warnedBunkerWaveManagerMissing = true;
                            Debug.LogWarning(
                                "[HealthBarCanvas_V2] Bunker mode: WaveManager_V2 not found in the loaded scene.",
                                this);
                        }

                        return false;
                    }

                    int maxB = Mathf.Max(1, wm.BunkerMaxHealth);
                    ratio = Mathf.Clamp01((float)wm.BunkerHealth / maxB);
                    dead = false;
                    return true;
                }

                default:
                    return false;
            }
        }

        private void ResolveSourcesIfNeeded()
        {
            switch (_bindMode)
            {
                case HealthBarCanvasBindMode.Hero:
                    ResolveHeroIfNeeded();
                    break;

                case HealthBarCanvasBindMode.Paratrooper:
                {
                    if (!_paratrooperModelFromExternal)
                    {
                        ParatrooperModel_V2 fromParents = GetComponentInParent<ParatrooperModel_V2>();
                        if (fromParents != null)
                        {
                            _paratrooperModel = fromParents;
                        }
                    }

                    break;
                }

                case HealthBarCanvasBindMode.Bunker:
                    _cachedWaveManager = ResolveWaveManagerForBunker();
                    break;
            }
        }

        private WaveManager_V2 ResolveWaveManagerForBunker()
        {
            if (_waveManager != null)
            {
                return _waveManager;
            }

            if (_cachedWaveManager == null)
            {
                _cachedWaveManager = FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Include);
            }

            return _cachedWaveManager;
        }

        private void ResolveHeroIfNeeded()
        {
            if (_cachedWaveManager == null)
            {
                _cachedWaveManager = FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Include);
            }

            Hero_V2 fromWave = _cachedWaveManager != null ? _cachedWaveManager.Hero : null;
            if (fromWave != null)
            {
                _hero = fromWave;
                return;
            }

            if (_hero == null || !IsHeroInstanceInLoadedScene(_hero))
            {
                _hero = FindAnyObjectByType<Hero_V2>(FindObjectsInactive.Include);
            }
        }

        private static bool IsHeroInstanceInLoadedScene(Hero_V2 hero)
        {
            if (hero == null)
            {
                return false;
            }

            UnityEngine.SceneManagement.Scene s = hero.gameObject.scene;
            return s.IsValid() && s.isLoaded;
        }
    }
}
