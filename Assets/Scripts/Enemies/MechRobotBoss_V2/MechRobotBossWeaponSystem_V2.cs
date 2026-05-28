using iStick2War;
using Spine;
using Spine.Unity;
using System;
using System.Collections;
using UnityEngine;

namespace iStick2War_V2
{
    // Optional shoot clip selection for MechRobotBossView_V2 when body state is Shoot.
    public enum MechRobotBossShootPresentation
    {
        Default = 0,
        MachineGun = 1,
        Cannon = 2,
        Missile = 3,
    }

    /*
 * MechRobotBossWeaponSystem_V2 (Combat execution)
 *
 * PURPOSE:
 * Runs mech boss weapons while the controller keeps the boss in range: machine-gun bursts, telegraphed cannon
 * hit-scan, and homing missile volleys. Exposes TickAttackPattern / presentation hooks for the view; only one
 * pattern segment is active at a time.
 *
 * ---------------------------------------------------------
 * INPUT SOURCES
 *
 * - MechRobotBossModel_V2 (HP, flags), hero / bunker context from controller-driven calls
 * - MechRobotBossShootPresentation from controller when choosing shoot presentation
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Own high-level movement or when to enter Aim (MechRobotBossController_V2)
 * - Select Spine tracks (MechRobotBossView_V2)
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Weapon execution isolated from locomotion; mirrors the split between ParatrooperWeaponSystem_V2 and controller AI.
 */
    public sealed class MechRobotBossWeaponSystem_V2 : MonoBehaviour
    {
        private enum PatternSegment
        {
            None = 0,
            MachineGunBurst = 1,
            PostMachineGunCooldown = 2,
            CannonTelegraph = 3,
            CannonFire = 4,
            PostCannonCooldown = 5,
            MissileVolley = 6,
            PostMissileCooldown = 7,
        }

        private MechRobotBossModel_V2 _model;
        private SkeletonAnimation _skeletonAnimation;
        private Bone _aimBone;
        private Bone _crossHairBone;
        private HeroModel_V2 _heroModel;
        private Hero_V2 _heroRoot;

        [Header("Attack pattern")]
        [SerializeField] private bool _attackPatternEnabled = true;
        [Tooltip("Below this HP fraction (current/max), missiles are enabled even when phase-one missiles are off.")]
        [SerializeField] [Range(0.05f, 0.95f)] private float _phaseTwoHpFraction = 0.5f;
        [Tooltip("If true, the boss uses the full MG -> cannon -> missile loop from phase one.")]
        [SerializeField] private bool _missilesInPhaseOne;

        [Header("Machine gun")]
        [SerializeField] private int _machineGunDamage = 5;
        [SerializeField] private float _machineGunShotInterval = 0.11f;
        [SerializeField] private float _machineGunBurstDuration = 1.4f;
        [SerializeField] private float _afterMachineGunCooldown = 3.2f;

        [Header("Cannon (hitscan)")]
        [SerializeField] private int _cannonDamage = 56;
        [SerializeField] private float _cannonTelegraphSeconds = 1.4f;
        [SerializeField] private float _afterCannonCooldown = 3.2f;
        [SerializeField] private Color _cannonTelegraphColor = new Color(1f, 0.15f, 0.1f, 0.92f);
        [SerializeField] private float _telegraphLineWidth = 0.065f;
        [SerializeField] private float _telegraphDrawDistance = 24f;

        [Header("Missiles (phase 2)")]
        [SerializeField] private GameObject _missilePrefab;
        [SerializeField] private int _missileDamage = 28;
        [SerializeField] private float _missileSpeed = 5.5f;
        [SerializeField] private float _missileLifetime = 10f;
        [SerializeField] private int _missilesPerVolley = 3;
        [SerializeField] private float _missileSpawnSpacing = 0.35f;
        [SerializeField] private float _afterMissileVolleyCooldown = 4.5f;
        [SerializeField] private float _missileArcDurationSeconds = 1.15f;
        [SerializeField] private float _missileArcHeightWorld = 3.2f;
        [SerializeField] private string[] _missileSpawnBoneNames =
        {
            "missile-slot-1",
            "missile-slot-2",
            "missile-slot-3",
        };
        [SerializeField] private Transform _missileSpawnPoint;

        [Header("Legacy / shared")]
        [SerializeField] private float _range = 100f;
        [SerializeField] private int _baseDamage = 14;
        [SerializeField] private LayerMask _whatToHit;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private string _aimBoneName = "gun";
        [Tooltip("Spine bone driven toward the hero (same pattern as Hero_V2 / Paratrooper); moves cannon IK when set up in Spine.")]
        [SerializeField] private string _crossHairBoneName = "crosshair";
        [Header("Bunker cover")]
        [SerializeField] private LayerMask _bunkerShotBlockMask;
        [SerializeField] private bool _respectBunkerCover = true;
        [Tooltip("Combat ray aims at this height on the hero collider (0=feet, 1=head).")]
        [SerializeField] [Range(0f, 1f)] private float _heroCombatAimHeightLerp = 0.42f;
        [SerializeField] private bool _debugDrawShotRay = true;

        [Header("Shot line effect")]
        [SerializeField] private LineRenderer _shotLineRenderer;
        [Tooltip("How long one mech hitscan tracer stays visible.")]
        [SerializeField] private float _shotLineVisibleDuration = 0.12f;
        [SerializeField] private float _shotLineWidth = 0.08f;
        [SerializeField] private Color _shotLineColor = new Color(1f, 0.95f, 0.5f, 1f);
        [SerializeField] private bool _overrideShotLineColor = false;
        [SerializeField] private int _shotLineSortingOrder = 5000;
        [Tooltip("If set, tracer uses this sorting layer. Empty = highest sorting layer in project.")]
        [SerializeField] private string _shotLineSortingLayerName = "";
        [SerializeField] private bool _preferUrpUnlitShotLineMaterial = true;
        [SerializeField] private bool _debugShotLineLogs = false;

        private float _lastFireTime = -999f;
        private Collider2D _cachedHeroCollider;
        private Bone[] _missileSpawnBones;

        private PatternSegment _segment = PatternSegment.None;
        private float _segmentStartedAt;
        private float _nextMachineGunShotTime;
        private int _missilesSpawnedInVolley;
        private float _nextMissileSpawnTime;

        private LineRenderer _telegraphLine;
        private Coroutine _shotLineCoroutine;
        private int _shotLineSortingLayerId = -1;
        private Material _shotLineMaterial;
        private static bool s_warnedMissilePrefab;

        private int _machineGunDamageScaled = 5;
        private int _cannonDamageScaled = 56;
        private int _missileDamageScaled = 28;
        private int _legacyDamageScaled = 14;

        private MechRobotBossShootPresentation _shootPresentation = MechRobotBossShootPresentation.Default;

        public bool AttackPatternEnabled => _attackPatternEnabled;

        public MechRobotBossShootPresentation CurrentShootPresentation => _shootPresentation;

        public bool IgnoresSpineShootFinishedForStateMachine => _attackPatternEnabled;

        public bool ShouldUseShootTrack =>
            _attackPatternEnabled &&
            (_segment == PatternSegment.MachineGunBurst ||
             _segment == PatternSegment.CannonTelegraph ||
             _segment == PatternSegment.CannonFire ||
             _segment == PatternSegment.MissileVolley);

        public void ApplyWaveDamageMultiplier(float multiplier)
        {
            if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
            {
                return;
            }

            _machineGunDamage = Mathf.Max(1, Mathf.RoundToInt(_machineGunDamage * multiplier));
            _cannonDamage = Mathf.Max(1, Mathf.RoundToInt(_cannonDamage * multiplier));
            _missileDamage = Mathf.Max(1, Mathf.RoundToInt(_missileDamage * multiplier));
            _baseDamage = Mathf.Max(1, Mathf.RoundToInt(_baseDamage * multiplier));
            CacheScaledDamages();
        }

        private void CacheScaledDamages()
        {
            _machineGunDamageScaled = _machineGunDamage;
            _cannonDamageScaled = _cannonDamage;
            _missileDamageScaled = _missileDamage;
            _legacyDamageScaled = _baseDamage;
        }

        public void Initialize(MechRobotBossModel_V2 model)
        {
            _model = model;
            CacheScaledDamages();
            _skeletonAnimation = GetComponent<SkeletonAnimation>();
            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
            }

            ResolveAimBone();
            ResolveCrossHairBone();
            ResolveMissileSpawnBones();

            if (_firePoint == null)
            {
                _firePoint = transform;
            }

            if (_missileSpawnPoint == null)
            {
                _missileSpawnPoint = _firePoint;
            }

            if (_whatToHit.value == 0)
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0)
                {
                    _whatToHit = 1 << playerLayer;
                }
            }

            if (_bunkerShotBlockMask.value == 0)
            {
                int bunkerLayer = LayerMask.NameToLayer("Bunker");
                if (bunkerLayer >= 0)
                {
                    _bunkerShotBlockMask = 1 << bunkerLayer;
                }
            }

            _heroRoot = FindAnyObjectByType<Hero_V2>();
            _heroModel = _heroRoot != null ? _heroRoot.GetComponent<HeroModel_V2>() : FindAnyObjectByType<HeroModel_V2>();
            CacheHeroCollider();
            EnsureTelegraphLine();
            EnsureShotLineRenderer();
        }

        public void ResetForSpawn()
        {
            _lastFireTime = -999f;
            _segment = PatternSegment.None;
            _missilesSpawnedInVolley = 0;
            ClearTelegraph();
            ClearShotLine();
            _heroRoot = FindAnyObjectByType<Hero_V2>();
            _heroModel = _heroRoot != null ? _heroRoot.GetComponent<HeroModel_V2>() : FindAnyObjectByType<HeroModel_V2>();
            CacheHeroCollider();
            CacheScaledDamages();
            ResolveMissileSpawnBones();
        }

        // Advance attack loop while the boss is in combat range. Safe to call every FixedUpdate.
        public void TickAttackPattern(bool inCombatRange)
        {
            if (!_attackPatternEnabled || _model == null || _model.IsDead())
            {
                ClearTelegraph();
                _segment = PatternSegment.None;
                return;
            }

            if (!inCombatRange)
            {
                ClearTelegraph();
                _segment = PatternSegment.None;
                return;
            }

            if (_segment == PatternSegment.None)
            {
                StartPatternFromEntry();
            }

            switch (_segment)
            {
                case PatternSegment.MachineGunBurst:
                    TickMachineGunBurst();
                    break;
                case PatternSegment.PostMachineGunCooldown:
                    TickTimedSegment(_afterMachineGunCooldown, AdvanceAfterMachineGunCooldown);
                    break;
                case PatternSegment.CannonTelegraph:
                    TickCannonTelegraph();
                    break;
                case PatternSegment.CannonFire:
                    FireCannonOnce();
                    EnterSegment(PatternSegment.PostCannonCooldown);
                    break;
                case PatternSegment.PostCannonCooldown:
                    TickTimedSegment(_afterCannonCooldown, AdvanceAfterCannonCooldown);
                    break;
                case PatternSegment.MissileVolley:
                    TickMissileVolley();
                    break;
                case PatternSegment.PostMissileCooldown:
                    TickTimedSegment(_afterMissileVolleyCooldown, AdvanceAfterMissileCooldown);
                    break;
            }
        }

        private void StartPatternFromEntry()
        {
            EnterSegment(PatternSegment.MachineGunBurst);
        }

        private bool IsPhaseTwo()
        {
            if (_model == null || _model.maxHealth <= 0.01f)
            {
                return false;
            }

            return _model.health / _model.maxHealth <= _phaseTwoHpFraction;
        }

        // Missile volleys after cannon (and skipped volley if prefab missing) use this gate.
        private bool ShouldRunMissileVolleysAfterCannon()
        {
            return _missilesInPhaseOne || IsPhaseTwo();
        }

        private void EnterSegment(PatternSegment seg)
        {
            _segment = seg;
            _segmentStartedAt = Time.time;

            if (seg == PatternSegment.MachineGunBurst)
            {
                _nextMachineGunShotTime = Time.time;
                _shootPresentation = MechRobotBossShootPresentation.MachineGun;
            }
            else if (seg == PatternSegment.MissileVolley)
            {
                _missilesSpawnedInVolley = 0;
                _nextMissileSpawnTime = Time.time;
                _shootPresentation = MechRobotBossShootPresentation.Missile;
            }
            else if (seg == PatternSegment.CannonTelegraph || seg == PatternSegment.CannonFire)
            {
                _shootPresentation = MechRobotBossShootPresentation.Cannon;
            }
            else if (seg == PatternSegment.PostMachineGunCooldown ||
                     seg == PatternSegment.PostCannonCooldown ||
                     seg == PatternSegment.PostMissileCooldown)
            {
                _shootPresentation = MechRobotBossShootPresentation.Default;
            }

            if (seg != PatternSegment.CannonTelegraph)
            {
                ClearTelegraph();
            }
        }

        private void TickMachineGunBurst()
        {
            float burstElapsed = Time.time - _segmentStartedAt;
            if (burstElapsed >= _machineGunBurstDuration)
            {
                EnterSegment(PatternSegment.PostMachineGunCooldown);
                return;
            }

            if (Time.time >= _nextMachineGunShotTime)
            {
                _nextMachineGunShotTime = Time.time + _machineGunShotInterval;
                ApplyHitscanDamage(_machineGunDamageScaled);
                WorldShake_V2.AddImpulse(WorldShakeImpulseKind_V2.MechMachineGun);
            }
        }

        private void TickTimedSegment(float duration, Action onComplete)
        {
            if (Time.time - _segmentStartedAt >= duration)
            {
                onComplete?.Invoke();
            }
        }

        private void AdvanceAfterMachineGunCooldown()
        {
            EnterSegment(PatternSegment.CannonTelegraph);
        }

        private void AdvanceAfterCannonCooldown()
        {
            if (ShouldRunMissileVolleysAfterCannon())
            {
                EnterSegment(PatternSegment.MissileVolley);
            }
            else
            {
                EnterSegment(PatternSegment.MachineGunBurst);
            }
        }

        private void AdvanceAfterMissileCooldown()
        {
            EnterSegment(PatternSegment.MachineGunBurst);
        }

        private void TickCannonTelegraph()
        {
            UpdateTelegraphVisual();
            if (Time.time - _segmentStartedAt >= _cannonTelegraphSeconds)
            {
                ClearTelegraph();
                EnterSegment(PatternSegment.CannonFire);
            }
        }

        private void FireCannonOnce()
        {
            ApplyHitscanDamage(_cannonDamageScaled);
            WorldShake_V2.AddImpulse(WorldShakeImpulseKind_V2.MechCannon);
            HitStop_V2.Request(HitStopKind_V2.MechCannon);
        }

        private void TickMissileVolley()
        {
            if (_missilePrefab == null)
            {
                if (!s_warnedMissilePrefab)
                {
                    s_warnedMissilePrefab = true;
                    Debug.LogWarning(
                        "[MechRobotBossWeaponSystem_V2] Missile prefab is not assigned; missile volley is skipped. " +
                        "Assign a prefab with MechRobotBossMissileProjectile_V2 + trigger collider.");
                }

                EnterSegment(PatternSegment.PostMissileCooldown);
                return;
            }

            if (_missilesSpawnedInVolley < _missilesPerVolley && Time.time >= _nextMissileSpawnTime)
            {
                SpawnOneMissile();
                _missilesSpawnedInVolley++;
                _nextMissileSpawnTime = Time.time + _missileSpawnSpacing;
            }

            if (_missilesSpawnedInVolley >= _missilesPerVolley &&
                Time.time >= _nextMissileSpawnTime + 0.05f)
            {
                EnterSegment(PatternSegment.PostMissileCooldown);
            }
        }

        private void SpawnOneMissile()
        {
            Vector3 pos = GetMissileSpawnPosition();
            Vector2 target = ResolveMissileTargetWorldPoint(pos, out bool targetIsBunker);
            GameObject go = Instantiate(_missilePrefab, pos, Quaternion.Euler(0f, 0f, 90f));
            MechRobotBossMissileProjectile_V2 missile = go.GetComponent<MechRobotBossMissileProjectile_V2>();
            if (missile == null)
            {
                missile = go.AddComponent<MechRobotBossMissileProjectile_V2>();
            }

            EnsureHeroReferences();
            missile.Launch(
                _missileDamageScaled,
                _missileSpeed,
                _missileLifetime,
                _respectBunkerCover,
                _heroRoot != null ? _heroRoot.transform : null,
                _missileArcDurationSeconds,
                _missileArcHeightWorld,
                target,
                targetIsBunker);
        }

        public bool CanShoot()
        {
            if (_model == null)
            {
                return false;
            }

            if (Time.time < _lastFireTime + _machineGunShotInterval)
            {
                return false;
            }

            if (_model.currentState == MechRobotBossBodyState.Die || _model.IsDead())
            {
                return false;
            }

            return true;
        }

        public void TryAutoShootAtHero()
        {
            if (_attackPatternEnabled)
            {
                return;
            }

            if (!CanShoot())
            {
                return;
            }

            _lastFireTime = Time.time;
            ApplyHitscanDamage(_legacyDamageScaled);
        }

        private void LateUpdate()
        {
            if (_model == null || _model.IsDead() || _model.currentState == MechRobotBossBodyState.Die)
            {
                return;
            }

            if (_skeletonAnimation == null || _crossHairBone == null)
            {
                return;
            }

            EnsureHeroReferences();
            if (_heroModel != null && _heroModel.isDead)
            {
                return;
            }

            if (_heroRoot == null && _heroModel == null)
            {
                return;
            }

            SyncCrosshairToHeroCombatPoint();
        }

        private void SyncCrosshairToHeroCombatPoint()
        {
            if (_skeletonAnimation == null || _crossHairBone == null)
            {
                return;
            }

            Vector2 worldTarget = GetHeroCombatAimWorldPoint();
            Vector3 skeletonSpacePoint = _skeletonAnimation.transform.InverseTransformPoint(worldTarget);
            skeletonSpacePoint.x *= _skeletonAnimation.Skeleton.ScaleX;
            skeletonSpacePoint.y *= _skeletonAnimation.Skeleton.ScaleY;
            _crossHairBone.SetLocalPosition(skeletonSpacePoint);
        }

        private void EnsureHeroReferences()
        {
            if (_heroRoot == null)
            {
                _heroRoot = FindAnyObjectByType<Hero_V2>();
            }

            if (_heroModel == null)
            {
                _heroModel = _heroRoot != null ? _heroRoot.GetComponent<HeroModel_V2>() : FindAnyObjectByType<HeroModel_V2>();
            }
        }

        private void ResolveAimBone()
        {
            _aimBone = null;
            if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null || string.IsNullOrEmpty(_aimBoneName))
            {
                return;
            }

            _aimBone = _skeletonAnimation.Skeleton.FindBone(_aimBoneName);
        }

        private void ResolveCrossHairBone()
        {
            _crossHairBone = null;
            if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_crossHairBoneName))
            {
                _crossHairBone = _skeletonAnimation.Skeleton.FindBone(_crossHairBoneName);
            }

            if (_crossHairBone == null)
            {
                _crossHairBone = _skeletonAnimation.Skeleton.FindBone("crosshair");
            }
        }

        private void ResolveMissileSpawnBones()
        {
            _missileSpawnBones = null;
            if (_skeletonAnimation == null ||
                _skeletonAnimation.Skeleton == null ||
                _missileSpawnBoneNames == null ||
                _missileSpawnBoneNames.Length == 0)
            {
                return;
            }

            Bone[] resolved = new Bone[_missileSpawnBoneNames.Length];
            int resolvedCount = 0;
            for (int i = 0; i < _missileSpawnBoneNames.Length; i++)
            {
                string boneName = _missileSpawnBoneNames[i];
                if (string.IsNullOrWhiteSpace(boneName))
                {
                    continue;
                }

                Bone bone = _skeletonAnimation.Skeleton.FindBone(boneName);
                if (bone == null)
                {
                    continue;
                }

                resolved[resolvedCount] = bone;
                resolvedCount++;
            }

            if (resolvedCount <= 0)
            {
                return;
            }

            if (resolvedCount != resolved.Length)
            {
                Array.Resize(ref resolved, resolvedCount);
            }

            _missileSpawnBones = resolved;
        }

        private Vector3 GetMissileSpawnPosition()
        {
            Bone spawnBone = GetMissileSpawnBoneForNextShot();
            if (spawnBone != null && _skeletonAnimation != null)
            {
                return _skeletonAnimation.transform.TransformPoint(new Vector3(spawnBone.WorldX, spawnBone.WorldY, 0f));
            }

            Transform sp = _missileSpawnPoint != null ? _missileSpawnPoint : _firePoint;
            return sp != null ? sp.position : transform.position;
        }

        private Bone GetMissileSpawnBoneForNextShot()
        {
            if (_missileSpawnBones == null || _missileSpawnBones.Length == 0)
            {
                ResolveMissileSpawnBones();
            }

            if (_missileSpawnBones == null || _missileSpawnBones.Length == 0)
            {
                return null;
            }

            int index = Mathf.Abs(_missilesSpawnedInVolley) % _missileSpawnBones.Length;
            return _missileSpawnBones[index];
        }

        private Vector2 ResolveMissileTargetWorldPoint(Vector3 missileSpawnPosition, out bool targetIsBunker)
        {
            if (TryResolveBunkerTargetWorldPoint(out Vector2 bunkerTarget))
            {
                targetIsBunker = true;
                return bunkerTarget;
            }

            Vector2 heroPoint = GetHeroCombatAimWorldPoint();
            if (heroPoint.sqrMagnitude > 0.0001f)
            {
                targetIsBunker = false;
                return heroPoint;
            }

            targetIsBunker = false;
            return (Vector2)missileSpawnPosition + Vector2.left;
        }

        private static bool TryResolveBunkerTargetWorldPoint(out Vector2 target)
        {
            target = default;

            BunkerHitbox_V2 bunkerHitbox = FindAnyObjectByType<BunkerHitbox_V2>(FindObjectsInactive.Include);
            if (bunkerHitbox != null && TryGetCombinedColliderBounds(bunkerHitbox.transform, out Bounds hitboxBounds))
            {
                target = hitboxBounds.center;
                return true;
            }

            GameObject bunkerRoot = GameObject.Find("BunkerRoot");
            if (bunkerRoot != null)
            {
                if (TryGetCombinedColliderBounds(bunkerRoot.transform, out Bounds rootBounds))
                {
                    target = rootBounds.center;
                    return true;
                }

                target = bunkerRoot.transform.position;
                return true;
            }

            return false;
        }

        private static bool TryGetCombinedColliderBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
            {
                return false;
            }

            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
            bool hasBounds = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null || !col.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }

            return hasBounds;
        }

        private void CacheHeroCollider()
        {
            _cachedHeroCollider = null;
            if (_heroRoot != null)
            {
                _cachedHeroCollider = _heroRoot.GetComponent<Collider2D>();
                if (_cachedHeroCollider == null)
                {
                    _cachedHeroCollider = _heroRoot.GetComponentInChildren<Collider2D>(true);
                }
            }
        }

        private Vector2 GetHeroCombatAimWorldPoint()
        {
            if (_cachedHeroCollider == null)
            {
                CacheHeroCollider();
            }

            if (_cachedHeroCollider == null)
            {
                return _heroModel != null ? (Vector2)_heroModel.transform.position : Vector2.zero;
            }

            Bounds b = _cachedHeroCollider.bounds;
            float t = Mathf.Clamp01(_heroCombatAimHeightLerp);
            return new Vector2(b.center.x, Mathf.Lerp(b.min.y, b.max.y, t));
        }

        private Vector2 GetShotOrigin()
        {
            if (_aimBone != null && _skeletonAnimation != null)
            {
                Vector2 world = _skeletonAnimation.transform.TransformPoint(new Vector3(_aimBone.WorldX, _aimBone.WorldY, 0f));
                return world;
            }

            return _firePoint != null ? (Vector2)_firePoint.position : (Vector2)transform.position;
        }

        private bool TryGetShotOriginAndDirection(out Vector2 origin, out Vector2 direction)
        {
            origin = GetShotOrigin();
            direction = default;

            SyncCrosshairToHeroCombatPoint();

            if (_aimBone != null && _crossHairBone != null && _skeletonAnimation != null)
            {
                Vector2 aimPos = _skeletonAnimation.transform.TransformPoint(
                    new Vector3(_aimBone.WorldX, _aimBone.WorldY, 0f));
                Vector2 crossPos = _skeletonAnimation.transform.TransformPoint(
                    new Vector3(_crossHairBone.WorldX, _crossHairBone.WorldY, 0f));
                Vector2 dir = crossPos - aimPos;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    origin = aimPos;
                    direction = dir.normalized;
                    return true;
                }
            }

            Vector2 aimTarget = GetHeroCombatAimWorldPoint();
            Vector2 toHero = aimTarget - origin;
            if (toHero.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            direction = toHero.normalized;
            return true;
        }

        private void ApplyHitscanDamage(int damage)
        {
            if (_heroModel == null)
            {
                _heroRoot = FindAnyObjectByType<Hero_V2>();
                _heroModel = _heroRoot != null ? _heroRoot.GetComponent<HeroModel_V2>() : FindAnyObjectByType<HeroModel_V2>();
            }

            if (_heroRoot == null && _heroModel != null)
            {
                _heroRoot = _heroModel.GetComponentInParent<Hero_V2>();
            }

            CacheHeroCollider();

            if (_heroModel == null || _heroModel.isDead)
            {
                return;
            }

            if (!TryGetShotOriginAndDirection(out Vector2 origin, out Vector2 direction))
            {
                return;
            }

            MuzzleFlash_V2.Play(origin, direction);

            bool prevHitTriggers = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = true;
            RaycastHit2D[] hits;
            try
            {
                hits = Physics2D.RaycastAll(origin, direction, _range, ~0);
            }
            finally
            {
                Physics2D.queriesHitTriggers = prevHitTriggers;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            WaveManager_V2 waveManager = FindAnyObjectByType<WaveManager_V2>();
            Hero_V2 heroRefForShelter = _heroRoot;
            if (heroRefForShelter == null && _heroModel != null)
            {
                heroRefForShelter = _heroModel.GetComponentInParent<Hero_V2>();
            }

            RaycastHit2D firstBunkerAlongRay = default;
            bool foundBunkerAlongRay = false;
            RaycastHit2D firstHeroAlongRay = default;
            bool foundHeroAlongRay = false;

            for (int scan = 0; scan < hits.Length; scan++)
            {
                RaycastHit2D h = hits[scan];
                if (h.collider == null)
                {
                    continue;
                }

                if (!foundBunkerAlongRay && _respectBunkerCover && IsBunkerCoverHit(h.collider))
                {
                    firstBunkerAlongRay = h;
                    foundBunkerAlongRay = true;
                }

                if (!foundHeroAlongRay)
                {
                    if (h.collider.GetComponentInParent<Hero_V2>() != null ||
                        h.collider.GetComponentInParent<HeroModel_V2>() != null)
                    {
                        firstHeroAlongRay = h;
                        foundHeroAlongRay = true;
                    }
                }

                if (foundBunkerAlongRay && foundHeroAlongRay)
                {
                    break;
                }
            }

            bool bunkerAlive = waveManager != null && waveManager.BunkerHealth > 0;
            bool heroSheltered =
                bunkerAlive &&
                heroRefForShelter != null &&
                waveManager != null &&
                waveManager.IsHeroInsideBunker(heroRefForShelter);

            RaycastHit2D damageHit = default;
            bool didApplyDamage = false;

            if (bunkerAlive && _respectBunkerCover && foundBunkerAlongRay)
            {
                bool heroIsCloser =
                    foundHeroAlongRay && firstHeroAlongRay.distance < firstBunkerAlongRay.distance;
                if (!heroIsCloser || heroSheltered)
                {
                    waveManager?.ApplyBunkerDamage(damage);
                    didApplyDamage = true;
                    damageHit = firstBunkerAlongRay;
                    BulletImpactVfx_V2.PlayIfSurfaceHit(damageHit, direction);
                }
            }

            if (!didApplyDamage)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit2D h = hits[i];
                    if (h.collider == null)
                    {
                        continue;
                    }

                    if (_respectBunkerCover && IsBunkerCoverHit(h.collider))
                    {
                        if (waveManager != null && waveManager.BunkerHealth <= 0)
                        {
                            continue;
                        }

                        waveManager?.ApplyBunkerDamage(damage);
                        didApplyDamage = true;
                        damageHit = h;
                        BulletImpactVfx_V2.PlayIfSurfaceHit(damageHit, direction);
                        break;
                    }

                    Hero_V2 heroRoot = h.collider.GetComponentInParent<Hero_V2>();
                    if (heroRoot != null)
                    {
                        if (waveManager != null && waveManager.IsHeroInsideBunker(heroRoot))
                        {
                            continue;
                        }

                        heroRoot.ReceiveDamage(damage, incomingShotWorldDirection: direction);
                        didApplyDamage = true;
                        damageHit = h;
                        break;
                    }

                    HeroModel_V2 heroModelHit = h.collider.GetComponentInParent<HeroModel_V2>();
                    if (heroModelHit != null)
                    {
                        Hero_V2 heroForZone = heroModelHit.GetComponentInParent<Hero_V2>();
                        if (heroForZone == null)
                        {
                            heroForZone = _heroRoot;
                        }

                        bool heroProtected = waveManager != null &&
                            (heroForZone != null ? waveManager.IsHeroInsideBunker(heroForZone) : waveManager.IsHeroInsideBunker());
                        if (heroProtected)
                        {
                            continue;
                        }

                        if (heroForZone != null)
                        {
                            heroForZone.ReceiveDamage(damage, incomingShotWorldDirection: direction);
                        }
                        else
                        {
                            heroModelHit.TakeDamage(damage);
                        }

                        didApplyDamage = true;
                        damageHit = h;
                        break;
                    }
                }
            }

            Vector2 finalPos;
            if (didApplyDamage && damageHit.collider != null)
            {
                finalPos = damageHit.point;
            }
            else if (hits.Length > 0 && hits[0].collider != null)
            {
                finalPos = hits[0].point;
            }
            else
            {
                finalPos = origin + direction * _range;
            }

            if (_debugDrawShotRay)
            {
                Debug.DrawLine(origin, finalPos, Color.magenta, 0.45f);
            }

            PlayShotLine(origin, finalPos);
        }

        private void EnsureTelegraphLine()
        {
            if (_telegraphLine != null)
            {
                return;
            }

            _telegraphLine = GetComponent<LineRenderer>();
            if (_telegraphLine == null)
            {
                var child = new GameObject("CannonTelegraph");
                child.transform.SetParent(transform, false);
                _telegraphLine = child.AddComponent<LineRenderer>();
                _telegraphLine.positionCount = 2;
                _telegraphLine.useWorldSpace = true;
                _telegraphLine.sortingOrder = 32000;
                _telegraphLine.material = new Material(Shader.Find("Sprites/Default"));
            }

            _telegraphLine.enabled = false;
            _telegraphLine.startWidth = _telegraphLineWidth;
            _telegraphLine.endWidth = _telegraphLineWidth;
            _telegraphLine.startColor = _cannonTelegraphColor;
            _telegraphLine.endColor = _cannonTelegraphColor;
        }

        private void UpdateTelegraphVisual()
        {
            EnsureTelegraphLine();
            if (_telegraphLine == null || !TryGetShotOriginAndDirection(out Vector2 origin, out Vector2 direction))
            {
                return;
            }

            _telegraphLine.enabled = true;
            Vector3 a = origin;
            Vector3 b = origin + direction * _telegraphDrawDistance;
            _telegraphLine.SetPosition(0, a);
            _telegraphLine.SetPosition(1, b);
        }

        private void ClearTelegraph()
        {
            if (_telegraphLine != null)
            {
                _telegraphLine.enabled = false;
            }
        }

        private void PlayShotLine(Vector2 from, Vector2 to)
        {
            if (_shotLineRenderer == null)
            {
                EnsureShotLineRenderer();
            }

            if (_shotLineRenderer == null)
            {
                return;
            }

            ConfigureAndRenderShotLine(_shotLineRenderer, from, to);
            if (_debugShotLineLogs)
            {
                Debug.Log($"[MechRobotBossWeaponSystem_V2] Shot line rendered. from={from}, to={to}, width={_shotLineRenderer.widthMultiplier:0.000}, sortingOrder={_shotLineRenderer.sortingOrder}");
            }

            if (_shotLineCoroutine != null)
            {
                StopCoroutine(_shotLineCoroutine);
            }

            _shotLineCoroutine = StartCoroutine(HideShotLineAfterDelay());
        }

        private void EnsureShotLineRenderer()
        {
            if (_shotLineRenderer == null)
            {
                Transform child = transform.Find("MechShotLine");
                if (child == null)
                {
                    GameObject lineGo = new GameObject("MechShotLine");
                    lineGo.transform.SetParent(transform, false);
                    child = lineGo.transform;
                }

                _shotLineRenderer = child.GetComponent<LineRenderer>();
                if (_shotLineRenderer == null)
                {
                    _shotLineRenderer = child.gameObject.AddComponent<LineRenderer>();
                }
            }

            _shotLineRenderer.useWorldSpace = true;
            _shotLineRenderer.enabled = false;
            _shotLineRenderer.positionCount = 2;
            _shotLineRenderer.widthMultiplier = Mathf.Max(0.01f, _shotLineWidth);
            _shotLineRenderer.numCapVertices = 2;
            _shotLineRenderer.numCornerVertices = 0;
            _shotLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shotLineRenderer.receiveShadows = false;
            _shotLineRenderer.textureMode = LineTextureMode.Stretch;
            _shotLineRenderer.alignment = LineAlignment.View;
            ApplyShotLineMaterial(_shotLineRenderer, _overrideShotLineColor ? _shotLineColor : Color.white);
        }

        private void ConfigureAndRenderShotLine(LineRenderer line, Vector2 from, Vector2 to)
        {
            if (line == null)
            {
                return;
            }

            Color tint = _overrideShotLineColor ? _shotLineColor : Color.white;
            line.enabled = false;
            line.transform.localScale = Vector3.one;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = Mathf.Max(0.01f, _shotLineWidth);
            line.startWidth = line.widthMultiplier;
            line.endWidth = line.widthMultiplier;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.sortingOrder = Mathf.Max(5000, _shotLineSortingOrder);
            CacheShotLineSortingLayer();
            line.sortingLayerID = _shotLineSortingLayerId;
            line.startColor = new Color(tint.r, tint.g, tint.b, 1f);
            line.endColor = new Color(tint.r, tint.g, tint.b, 1f);

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(tint, 0f),
                    new GradientColorKey(tint, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            line.colorGradient = gradient;

            ApplyShotLineMaterial(line, tint);

            float z = _firePoint != null ? _firePoint.position.z : transform.position.z;
            line.SetPosition(0, new Vector3(from.x, from.y, z));
            line.SetPosition(1, new Vector3(to.x, to.y, z));
            line.enabled = true;
        }

        private void CacheShotLineSortingLayer()
        {
            if (_shotLineSortingLayerId >= 0)
            {
                return;
            }

            if (TryResolveSortingLayerId(_shotLineSortingLayerName, out int forcedId))
            {
                _shotLineSortingLayerId = forcedId;
                return;
            }

            _shotLineSortingLayerId = GetTopSortingLayerId();
        }

        private static bool TryResolveSortingLayerId(string layerName, out int id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return false;
            }

            SortingLayer[] layers = SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == layerName)
                {
                    id = layers[i].id;
                    return true;
                }
            }

            return false;
        }

        private static int GetTopSortingLayerId()
        {
            SortingLayer[] layers = SortingLayer.layers;
            if (layers == null || layers.Length == 0)
            {
                return SortingLayer.NameToID("Default");
            }

            int topId = layers[0].id;
            int topValue = layers[0].value;
            for (int i = 1; i < layers.Length; i++)
            {
                if (layers[i].value > topValue)
                {
                    topValue = layers[i].value;
                    topId = layers[i].id;
                }
            }

            return topId;
        }

        private void ApplyShotLineMaterial(LineRenderer line, Color tint)
        {
            if (line == null)
            {
                return;
            }

            if (_shotLineMaterial == null)
            {
                Shader shader = null;
                if (_preferUrpUnlitShotLineMaterial)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                if (shader != null)
                {
                    _shotLineMaterial = new Material(shader);
                }
            }

            ConfigureShotLineMaterial(_shotLineMaterial, tint);
            if (_shotLineMaterial != null)
            {
                line.sharedMaterial = _shotLineMaterial;
            }
        }

        private static void ConfigureShotLineMaterial(Material mat, Color tint)
        {
            if (mat == null)
            {
                return;
            }

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", Texture2D.whiteTexture);
            }

            Color solidTint = new Color(tint.r, tint.g, tint.b, 1f);
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", solidTint);
            }
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", solidTint);
            }
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 0f);
            }
            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f);
            }
            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }
            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            }
            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetFloat("_ZWrite", 1f);
            }
            if (mat.HasProperty("_ZTest"))
            {
                mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            }

            mat.renderQueue = 5000;
        }

        private void ClearShotLine()
        {
            if (_shotLineCoroutine != null)
            {
                StopCoroutine(_shotLineCoroutine);
                _shotLineCoroutine = null;
            }

            if (_shotLineRenderer != null)
            {
                _shotLineRenderer.enabled = false;
            }
        }

        private IEnumerator HideShotLineAfterDelay()
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, _shotLineVisibleDuration));
            if (_shotLineRenderer != null)
            {
                _shotLineRenderer.enabled = false;
            }

            _shotLineCoroutine = null;
        }

        private static bool IsBunkerCoverHit(Collider2D collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.GetComponentInParent<BunkerHitbox_V2>() != null)
            {
                return true;
            }

            Transform t = collider.transform;
            while (t != null)
            {
                string n = t.name;
                if (!string.IsNullOrWhiteSpace(n) &&
                    n.IndexOf("bunker", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                t = t.parent;
            }

            return false;
        }
    }
}
