using iStick2War;
using Spine;
using Spine.Unity;
using System;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>Optional shoot clip selection for <see cref="MechRobotBossView_V2"/> when body state is Shoot.</summary>
    public enum MechRobotBossShootPresentation
    {
        Default = 0,
        MachineGun = 1,
        Cannon = 2,
        Missile = 3,
    }

    /// <summary>
    /// Mech boss weapons: machine-gun bursts, telegraphed cannon hitscan, and homing missiles (phase 2).
    /// Only one attack mode is active at a time; driven by <see cref="TickAttackPattern"/> while in range.
    /// </summary>
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
        [Tooltip("Below this HP fraction (current/max), boss uses phase-2 loop (missile volley after cannon, and may open with missiles).")]
        [SerializeField] [Range(0.05f, 0.95f)] private float _phaseTwoHpFraction = 0.5f;
        [Tooltip("If true, missile volleys are used even above Phase Two HP (cannon is still followed by missiles). Phase Two still adds the stronger entry pattern when HP is low.")]
        [SerializeField] private bool _missilesInPhaseOne;

        [Header("Machine gun")]
        [SerializeField] private int _machineGunDamage = 5;
        [SerializeField] private float _machineGunShotInterval = 0.09f;
        [SerializeField] private float _machineGunBurstDuration = 2.2f;
        [SerializeField] private float _afterMachineGunCooldown = 3.5f;

        [Header("Cannon (hitscan)")]
        [SerializeField] private int _cannonDamage = 56;
        [SerializeField] private float _cannonTelegraphSeconds = 1f;
        [SerializeField] private float _afterCannonCooldown = 7f;
        [SerializeField] private Color _cannonTelegraphColor = new Color(1f, 0.15f, 0.1f, 0.92f);
        [SerializeField] private float _telegraphLineWidth = 0.065f;
        [SerializeField] private float _telegraphDrawDistance = 24f;

        [Header("Missiles (phase 2)")]
        [SerializeField] private GameObject _missilePrefab;
        [SerializeField] private int _missileDamage = 28;
        [SerializeField] private float _missileSpeed = 5.5f;
        [SerializeField] private float _missileLifetime = 10f;
        [SerializeField] private int _missilesPerVolley = 3;
        [SerializeField] private float _missileSpawnSpacing = 0.18f;
        [SerializeField] private float _afterMissileVolleyCooldown = 2.2f;
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

        private float _lastFireTime = -999f;
        private Collider2D _cachedHeroCollider;

        private PatternSegment _segment = PatternSegment.None;
        private float _segmentStartedAt;
        private float _nextMachineGunShotTime;
        private int _missilesSpawnedInVolley;
        private float _nextMissileSpawnTime;

        private LineRenderer _telegraphLine;
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
        }

        public void ResetForSpawn()
        {
            _lastFireTime = -999f;
            _segment = PatternSegment.None;
            _missilesSpawnedInVolley = 0;
            ClearTelegraph();
            _heroRoot = FindAnyObjectByType<Hero_V2>();
            _heroModel = _heroRoot != null ? _heroRoot.GetComponent<HeroModel_V2>() : FindAnyObjectByType<HeroModel_V2>();
            CacheHeroCollider();
            CacheScaledDamages();
        }

        /// <summary>Advance attack loop while the boss is in combat range. Safe to call every <see cref="FixedUpdate"/>.</summary>
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
            if (IsPhaseTwo())
            {
                EnterSegment(PatternSegment.MissileVolley);
            }
            else
            {
                EnterSegment(PatternSegment.MachineGunBurst);
            }
        }

        private bool IsPhaseTwo()
        {
            if (_model == null || _model.maxHealth <= 0.01f)
            {
                return false;
            }

            return _model.health / _model.maxHealth <= _phaseTwoHpFraction;
        }

        /// <summary>Missile volleys after cannon (and skipped volley if prefab missing) use this gate.</summary>
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
            Transform sp = _missileSpawnPoint != null ? _missileSpawnPoint : _firePoint;
            Vector3 pos = sp != null ? sp.position : transform.position;
            GameObject go = Instantiate(_missilePrefab, pos, Quaternion.identity);
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
                _heroRoot != null ? _heroRoot.transform : null);
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
