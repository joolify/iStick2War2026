using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    public enum WorldShakeImpulseKind_V2
    {
        Colt45Shot = 0,
        ThompsonShot = 1,
        BazookaExplosion = 2,
        BunkerHit = 3,
        MechMachineGun = 4,
        MechCannon = 5,
    }

    /*
 * WorldShake_V2 (Ground / hero world shake)
 *
 * PURPOSE:
 * Applies short trauma-based local-position offsets to configured world targets (for example a
 * GroundShakeRoot parent and/or the Hero visual root). This is intentionally separate from camera shake:
 * aircraft and UI can stay stable while ground, bunker props, and hero react together.
 *
 * ---------------------------------------------------------
 * INPUT SOURCES
 *
 * - HeroWeaponSystem_V2: Colt45 / Thompson shot micro-shake.
 * - HeroRocketProjectile_V2: bazooka explosion impact.
 * - WaveManager_V2: bunker HP damage.
 * - MechRobotBossWeaponSystem_V2: machine-gun / cannon fire.
 *
 * ---------------------------------------------------------
 * MUST NOT
 *
 * - Own camera movement.
 * - Move unconfigured scene objects automatically (designer assigns the safe root/targets).
 */
    [DefaultExecutionOrder(900)]
    public sealed class WorldShake_V2 : MonoBehaviour
    {
        private struct ShakeTarget
        {
            public Transform Transform;
            public Vector3 OriginalLocalPosition;
        }

        private static WorldShake_V2 s_instance;
        private static bool s_warnedNoInstance;

        [Header("Targets")]
        [Tooltip("Preferred: parent containing ground visuals / bunker visuals / hero visual root. Leave null if using Additional Shake Targets.")]
        [SerializeField] private Transform _shakeRoot;
        [Tooltip("Optional extra targets that should receive the exact same offset as Shake Root.")]
        [SerializeField] private Transform[] _additionalShakeTargets;

        [Header("Motion")]
        [SerializeField] private float _maxOffsetWorld = 1f;
        [SerializeField] private float _frequency = 35f;
        [SerializeField] private float _decayPerSecond = 2.8f;
        [SerializeField] private float _returnSpeed = 25f;
        [SerializeField] private bool _useUnscaledTime;
        [SerializeField] private bool _warnWhenNoTargets = true;

        [Header("Impulse Amounts")]
        [SerializeField] [Range(0f, 1f)] private float _colt45ShotShake = 0.045f;
        [SerializeField] [Range(0f, 1f)] private float _thompsonShotShake = 0.06f;
        [SerializeField] [Range(0f, 1f)] private float _bazookaExplosionShake = 0.35f;
        [SerializeField] [Range(0f, 1f)] private float _bunkerHitShake = 0.2f;
        [SerializeField] [Range(0f, 1f)] private float _mechMachineGunShake = 0.1f;
        [SerializeField] [Range(0f, 1f)] private float _mechCannonShake = 0.45f;

        private readonly List<ShakeTarget> _targets = new List<ShakeTarget>(4);
        private float _trauma;
        private float _peakOffsetWorld;
        private float _seed;

        public static void AddImpulse(WorldShakeImpulseKind_V2 kind)
        {
            if (!GameSettings_V2.ScreenShakeEnabled)
            {
                return;
            }

            if (s_instance == null)
            {
                if (!s_warnedNoInstance)
                {
                    s_warnedNoInstance = true;
                    Debug.LogWarning("[WorldShake_V2] No WorldShake_V2 instance in scene; world shake impulse ignored.");
                }

                return;
            }

            s_instance.AddShake(s_instance.ResolveImpulseAmount(kind));
        }

        public void AddShake(float amount)
        {
            if (!GameSettings_V2.ScreenShakeEnabled)
            {
                return;
            }

            float impulse = Mathf.Max(0f, amount);
            _trauma = Mathf.Clamp01(_trauma + impulse);
            _peakOffsetWorld = Mathf.Max(_peakOffsetWorld, impulse);
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Debug.LogWarning("[WorldShake_V2] Multiple instances found; latest instance will receive static impulses.");
            }

            s_instance = this;
            s_warnedNoInstance = false;
            _seed = Random.value * 100f;
            CacheTargets();
        }

        private void OnDisable()
        {
            ResetTargetsToOriginal();
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void LateUpdate()
        {
            if (_targets.Count == 0)
            {
                return;
            }

            float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = _useUnscaledTime ? Time.unscaledTime : Time.time;
            Vector3 offset = Vector3.zero;

            if (_trauma > 0.01f)
            {
                float x = (Mathf.PerlinNoise(_seed, t * Mathf.Max(0.1f, _frequency)) - 0.5f) * 2f;
                float y = (Mathf.PerlinNoise(_seed + 10f, t * Mathf.Max(0.1f, _frequency)) - 0.5f) * 2f;
                float amplitude = Mathf.Min(Mathf.Max(0f, _maxOffsetWorld), _peakOffsetWorld);
                offset = new Vector3(x, y, 0f) * amplitude;
                _trauma = Mathf.Clamp01(_trauma - dt * Mathf.Max(0f, _decayPerSecond));
                _peakOffsetWorld = Mathf.Lerp(_peakOffsetWorld, 0f, 1f - Mathf.Exp(-Mathf.Max(0f, _decayPerSecond) * Mathf.Max(0f, dt)));
            }

            ApplyOffset(offset, dt);
        }

        private void CacheTargets()
        {
            _targets.Clear();
            AddTargetIfValid(_shakeRoot);
            if (_additionalShakeTargets != null)
            {
                for (int i = 0; i < _additionalShakeTargets.Length; i++)
                {
                    AddTargetIfValid(_additionalShakeTargets[i]);
                }
            }

            if (_targets.Count == 0 && _warnWhenNoTargets)
            {
                Debug.LogWarning("[WorldShake_V2] No shake targets assigned. Add a GroundShakeRoot or Additional Shake Targets.");
            }
        }

        private void AddTargetIfValid(Transform target)
        {
            if (target == null)
            {
                return;
            }

            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i].Transform == target)
                {
                    return;
                }
            }

            _targets.Add(new ShakeTarget
            {
                Transform = target,
                OriginalLocalPosition = target.localPosition
            });
        }

        private void ApplyOffset(Vector3 offset, float dt)
        {
            float lerp = 1f - Mathf.Exp(-Mathf.Max(0f, _returnSpeed) * Mathf.Max(0f, dt));
            for (int i = 0; i < _targets.Count; i++)
            {
                ShakeTarget target = _targets[i];
                if (target.Transform == null)
                {
                    continue;
                }

                Vector3 desired = target.OriginalLocalPosition + offset;
                target.Transform.localPosition = offset.sqrMagnitude > 0.000001f
                    ? desired
                    : Vector3.Lerp(target.Transform.localPosition, target.OriginalLocalPosition, lerp);
            }
        }

        private void ResetTargetsToOriginal()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                ShakeTarget target = _targets[i];
                if (target.Transform != null)
                {
                    target.Transform.localPosition = target.OriginalLocalPosition;
                }
            }
        }

        private float ResolveImpulseAmount(WorldShakeImpulseKind_V2 kind)
        {
            switch (kind)
            {
                case WorldShakeImpulseKind_V2.Colt45Shot:
                    return _colt45ShotShake;
                case WorldShakeImpulseKind_V2.ThompsonShot:
                    return _thompsonShotShake;
                case WorldShakeImpulseKind_V2.BazookaExplosion:
                    return _bazookaExplosionShake;
                case WorldShakeImpulseKind_V2.BunkerHit:
                    return _bunkerHitShake;
                case WorldShakeImpulseKind_V2.MechMachineGun:
                    return _mechMachineGunShake;
                case WorldShakeImpulseKind_V2.MechCannon:
                    return _mechCannonShake;
                default:
                    return 0f;
            }
        }

        [ContextMenu("Test Medium Shake")]
        private void TestMediumShake()
        {
            AddShake(0.35f);
        }

        [ContextMenu("Test Big Shake")]
        private void TestBigShake()
        {
            AddShake(0.75f);
        }
    }
}
