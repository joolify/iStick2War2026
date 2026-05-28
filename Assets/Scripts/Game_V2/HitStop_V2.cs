using UnityEngine;

namespace iStick2War_V2
{
    public enum HitStopKind_V2
    {
        BazookaExplosion = 0,
        MechCannon = 1,
        LargeExplosion = 2,
    }

    /*
 * HitStop_V2 (Global short freeze service)
 *
 * PURPOSE:
 * Applies very short global freeze windows for heavy impacts to increase game feel.
 * Uses unscaled time internally so pauses remain consistent even while Time.timeScale is modified.
 *
 * ---------------------------------------------------------
 * INPUT SOURCES
 *
 * - HeroRocketProjectile_V2: bazooka explosion
 * - MechRobotBossWeaponSystem_V2: cannon fire
 * - MechRobotBossMissileProjectile_V2: heavy missile impact (large explosion feel)
 */
    [DefaultExecutionOrder(1000)]
    public sealed class HitStop_V2 : MonoBehaviour
    {
        private static HitStop_V2 s_instance;

        [Header("Durations (seconds)")]
        [SerializeField] [Range(0f, 0.2f)] private float _bazookaExplosionSeconds = 0.05f;
        [SerializeField] [Range(0f, 0.2f)] private float _mechCannonSeconds = 0.04f;
        [SerializeField] [Range(0f, 0.2f)] private float _largeExplosionSeconds = 0.045f;

        [Header("Scale")]
        [Tooltip("Usually 0 for hard freeze. Use tiny >0 value for soft freeze.")]
        [SerializeField] [Range(0f, 0.2f)] private float _freezeTimeScale = 0f;
        [SerializeField] private bool _debugLogs;

        private bool _isFreezing;
        private float _freezeUntilUnscaled;
        private float _restoreTimeScale = 1f;
        private float _restoreFixedDeltaTime = 0.02f;

        public static void Request(HitStopKind_V2 kind)
        {
            HitStop_V2 instance = GetOrCreateInstance();
            if (instance == null)
            {
                return;
            }

            instance.RequestInternal(kind);
        }

        private static HitStop_V2 GetOrCreateInstance()
        {
            if (s_instance != null)
            {
                return s_instance;
            }

            s_instance = FindAnyObjectByType<HitStop_V2>(FindObjectsInactive.Include);
            if (s_instance != null)
            {
                return s_instance;
            }

            GameObject go = new GameObject("HitStop_V2");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<HitStop_V2>();
            return s_instance;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
        }

        private void Update()
        {
            if (!_isFreezing)
            {
                return;
            }

            if (Time.unscaledTime < _freezeUntilUnscaled)
            {
                return;
            }

            _isFreezing = false;
            Time.timeScale = _restoreTimeScale;
            Time.fixedDeltaTime = _restoreFixedDeltaTime;
            if (_debugLogs)
            {
                Debug.Log($"[HitStop_V2] Restore timeScale={_restoreTimeScale:0.###}");
            }
        }

        private void RequestInternal(HitStopKind_V2 kind)
        {
            if (Time.timeScale <= 0f)
            {
                // Respect existing full pause states (main menu, hard pause).
                return;
            }

            float duration = ResolveDuration(kind);
            if (duration <= 0f)
            {
                return;
            }

            if (!_isFreezing)
            {
                _restoreTimeScale = Time.timeScale;
                _restoreFixedDeltaTime = Time.fixedDeltaTime;
            }

            _isFreezing = true;
            _freezeUntilUnscaled = Mathf.Max(_freezeUntilUnscaled, Time.unscaledTime + duration);

            float frozenScale = Mathf.Clamp01(_freezeTimeScale);
            Time.timeScale = frozenScale;
            float baseFixed = _restoreFixedDeltaTime > 0f ? _restoreFixedDeltaTime : 0.02f;
            Time.fixedDeltaTime = Mathf.Max(0.0001f, baseFixed * Mathf.Max(0.01f, frozenScale > 0f ? frozenScale : 0.01f));

            if (_debugLogs)
            {
                Debug.Log($"[HitStop_V2] kind={kind}, duration={duration:0.###}, freezeScale={frozenScale:0.###}");
            }
        }

        private float ResolveDuration(HitStopKind_V2 kind)
        {
            switch (kind)
            {
                case HitStopKind_V2.BazookaExplosion:
                    return _bazookaExplosionSeconds;
                case HitStopKind_V2.MechCannon:
                    return _mechCannonSeconds;
                case HitStopKind_V2.LargeExplosion:
                    return _largeExplosionSeconds;
                default:
                    return 0f;
            }
        }
    }
}
