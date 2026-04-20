using Assets.Scripts.Components;
using Spine;
using Spine.Unity;
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// ParatrooperWeaponSystem (Combat Execution Layer)
/// </summary>
/// <remarks>
/// Handles all weapon-related logic for the Paratrooper entity.
///
/// Input Sources:
/// - Controller (AI intent: wants to shoot)
/// - Spine events (animation timing: when to shoot)
///
/// Responsibilities:
/// - Executes shooting logic (raycast or projectile)
/// - Handles cooldowns
/// - Validates if shooting is allowed
///
/// Constraints:
/// - MUST NOT decide when to shoot (Controller does that)
/// - MUST NOT depend on animation logic directly
/// - MUST remain deterministic
/// </remarks>
namespace iStick2War_V2
{
    public class ParatrooperWeaponSystem_V2 : MonoBehaviour
    {
        private ParatrooperModel_V2 _model;
        private SkeletonAnimation _skeletonAnimation;
        private Bone _aimPointBone;
        private Bone _crossHairBone;
        private Bone _grenadeBone;
        private HeroModel_V2 _heroModel;
        private Hero_V2 _heroRoot;

        [Header("Shooting")]
        [SerializeField] private float _fireCooldown = 0.14f;
        [SerializeField] private float _range = 100f;
        [SerializeField] private int _baseDamage = 9;
        [SerializeField] private LayerMask _whatToHit = 0;
        [Header("Debug")]
        [Tooltip("Hard-block MP40 shot execution (useful when testing grenade-only flow).")]
        [SerializeField] private bool _debugDisableMp40Shooting;
        [Header("Grenade (Potatomasher)")]
        [SerializeField] private GameObject _potatomasherProjectilePrefab;
        [SerializeField] private Transform _grenadeThrowPoint;
        [SpineBone(dataField: "_skeletonAnimation")]
        [SerializeField] private string _grenadeBoneName = "grenadeBone";
        [SerializeField] private float _grenadeThrowSpeed = 18f;
        [Tooltip("Hard minimum throw speed used at runtime (protects against stale prefab overrides).")]
        [SerializeField] private float _minimumRuntimeGrenadeThrowSpeed = 18f;
        [SerializeField] private float _grenadeFuseSeconds = 2.25f;
        [SerializeField] private int _grenadeBaseDamage = 24;
        [SerializeField] private float _grenadeExplosionRadius = 1.6f;
        [SerializeField] private float _grenadeCooldown = 3.25f;
        [Tooltip("When enabled, computes a ballistic arc using gravity so grenades can reach distant bunker targets more reliably.")]
        [SerializeField] private bool _useBallisticGrenadeAim = true;
        [Tooltip("Prefer the higher ballistic arc (more lob) when both low/high arc are possible.")]
        [SerializeField] private bool _preferHighArcGrenadeThrow = true;
        [Tooltip("Minimum horizontal speed ratio for ballistic throws. If high-arc becomes too vertical, low-arc is used.")]
        [SerializeField, Range(0.05f, 0.95f)] private float _minHorizontalSpeedRatio = 0.35f;
        [SerializeField] private bool _debugGrenadeThrowLogs = true;
        [Header("Bunker cover")]
        [Tooltip("Colliders on this mask should use BunkerHitbox_V2 (e.g. bunkerFront). Tries layer 'Bunker' when unset.")]
        [SerializeField] private LayerMask _bunkerShotBlockMask;
        [SerializeField] private bool _respectBunkerCover = true;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private string _aimPointBoneName = "mp40-aim";
        [SerializeField] private string _crossHairBoneName = "crosshair";

        [Header("Line Renderer Effect")]
        [SerializeField] private LineRenderer _shotLineRenderer;
        [SerializeField] private Transform _shotTrailPrefab;
        [Tooltip("URP: too short + wrong sorting layer often looks like 'no trail'.")]
        [SerializeField] private float _lineVisibleDuration = 0.2f;
        [SerializeField] private bool _debugDrawShotRay = true;
        [SerializeField] private bool _debugShotLineLogs = false;
        [SerializeField] private bool _debugCombatLogs = false;
        [SerializeField] private bool _debugAimFallbackLogs = true;
        [Tooltip("Reject aim-bone reads that are implausibly far from the SkeletonAnimation transform (not entity root).")]
        [SerializeField] private float _maxAimOriginDistanceFromRoot = 3.5f;
        [Tooltip("Combat ray aims at this height on the hero collider (0=feet, 1=head). Lower = more likely to intersect bunker cover.")]
        [SerializeField][Range(0f, 1f)] private float _heroCombatAimHeightLerp = 0.42f;
        [SerializeField] private float _lineWidth = 0.06f;
        [SerializeField] private Color _lineColor = new Color(1f, 0.95f, 0.5f, 1f);
        [Tooltip("When false, force neutral white trail color at runtime.")]
        [SerializeField] private bool _overrideLineColor = false;
        [SerializeField] private int _lineSortingOrder = 5000;
        [Tooltip("If set, trail uses this sorting layer. Empty = highest sorting layer in project.")]
        [SerializeField] private string _shotLineSortingLayerName = "";
        [Tooltip("When true, assign a URP-friendly Unlit material (Sprites/Default is unreliable for LineRenderer under URP).")]
        [SerializeField] private bool _preferUrpUnlitLineMaterial = true;

        private float _lastFireTime = -999f;
        private float _lastGrenadeTime = -999f;
        private Coroutine _lineCoroutine;
        private int _lineSortingLayerId = -1;
        private Collider2D _cachedHeroCollider;

        public void ApplyWaveDamageMultiplier(float multiplier)
        {
            if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
            {
                return;
            }

            _baseDamage = Mathf.Max(1, Mathf.RoundToInt(_baseDamage * multiplier));
        }

        public void Initialize(ParatrooperModel_V2 model)
        {
            _model = model;
            _skeletonAnimation = GetComponent<SkeletonAnimation>();
            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
            }
            ResolveAimBones();

            if (_firePoint == null)
            {
                _firePoint = transform;
            }
            else
            {
                // Guard against wrong inspector drag-drop (e.g. Hero fire point assigned to Paratrooper).
                ValidateFirePointOwnership();
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

            // If a prefab was assigned to _shotLineRenderer in inspector, treat it as a trail prefab.
            if (_shotTrailPrefab == null && _shotLineRenderer != null && !_shotLineRenderer.gameObject.scene.IsValid())
            {
                _shotTrailPrefab = _shotLineRenderer.transform;
                _shotLineRenderer = null;
            }

            EnsureShotLineRenderer();
            CacheLineSortingLayer();

            _heroRoot = FindAnyObjectByType<Hero_V2>();
            _heroModel = _heroRoot != null ? _heroRoot.GetComponent<HeroModel_V2>() : FindAnyObjectByType<HeroModel_V2>();
            CacheHeroCollider();
        }

        /// <summary>
        /// Called by Controller to indicate intent to shoot.
        /// </summary>
        public bool CanShoot()
        {
            if (_debugDisableMp40Shooting)
            {
                return false;
            }

            if (_model == null)
                return false;

            if (Time.time < _lastFireTime + _fireCooldown)
                return false;

            if (_model.currentState == iStick2War.StickmanBodyState.Die || _model.currentState == iStick2War.StickmanBodyState.GlideDie)
                return false;

            return true;
        }

        public void SetDebugDisableMp40Shooting(bool enabled)
        {
            _debugDisableMp40Shooting = enabled;
        }

        public bool CanThrowGrenade()
        {
            return GetGrenadeBlockReason() == null;
        }

        public string GetGrenadeBlockReason()
        {
            if (_model == null)
            {
                return "model missing";
            }

            if (_potatomasherProjectilePrefab == null)
            {
                return "potatomasher prefab missing";
            }

            float cooldown = Mathf.Max(0.1f, _grenadeCooldown);
            float nextReadyAt = _lastGrenadeTime + cooldown;
            if (Time.time < nextReadyAt)
            {
                return $"grenade cooldown active ({(nextReadyAt - Time.time):0.00}s left)";
            }

            if (_model.currentState == iStick2War.StickmanBodyState.Die ||
                _model.currentState == iStick2War.StickmanBodyState.GlideDie)
            {
                return $"invalid state {_model.currentState}";
            }

            return null;
        }

        public bool TryThrowGrenadeAtHero()
        {
            string blockedReason = GetGrenadeBlockReason();
            if (blockedReason != null)
            {
                if (_debugGrenadeThrowLogs)
                {
                    Debug.LogWarning($"[ParatrooperWeaponSystem_V2] grenade_throw blocked: {blockedReason}");
                }
                return false;
            }

            if (_heroModel == null)
            {
                _heroModel = FindAnyObjectByType<HeroModel_V2>();
            }

            if (_heroModel == null || _heroModel.isDead)
            {
                return false;
            }

            Vector2 origin;
            bool usingGrenadeBone = TryGetParatrooperGrenadeThrowOriginWorld(out origin);
            if (!usingGrenadeBone)
            {
                Transform spawnPoint = _grenadeThrowPoint != null ? _grenadeThrowPoint : (_firePoint != null ? _firePoint : transform);
                origin = spawnPoint.position;
            }
            Vector2 target = GetHeroCombatAimWorldPoint();
            Vector2 throwVelocity = ComputeGrenadeLaunchVelocity(origin, target);
            GameObject projectileGo = Instantiate(_potatomasherProjectilePrefab, origin, Quaternion.identity);
            PotatomasherProjectile_V2 projectile = projectileGo.GetComponent<PotatomasherProjectile_V2>();
            if (projectile != null)
            {
                projectile.Initialize(
                    throwVelocity,
                    Mathf.Max(0.5f, _grenadeFuseSeconds),
                    Mathf.Max(1, _grenadeBaseDamage),
                    Mathf.Max(0.2f, _grenadeExplosionRadius));
            }
            else
            {
                Rigidbody2D rb = projectileGo.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = throwVelocity;
                }
            }

            if (_debugGrenadeThrowLogs)
            {
                string source = usingGrenadeBone
                    ? $"SpineBone '{_grenadeBoneName}'"
                    : (_grenadeThrowPoint != null ? $"Transform '{_grenadeThrowPoint.name}'" : "fallback root/firePoint");
                Debug.Log(
                    $"[ParatrooperWeaponSystem_V2] grenade_throw origin={origin} source={source} target={target} velocity={throwVelocity}");
            }

            _lastGrenadeTime = Time.time;
            return true;
        }

        private Vector2 ComputeGrenadeLaunchVelocity(Vector2 origin, Vector2 target)
        {
            Vector2 toTarget = target - origin;
            float speed = Mathf.Max(0.1f, Mathf.Max(_grenadeThrowSpeed, _minimumRuntimeGrenadeThrowSpeed));
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return Vector2.right * speed;
            }

            float horizontalDistance = Mathf.Abs(toTarget.x);
            bool forceBallisticForLongRange = horizontalDistance > 4f;
            bool shouldUseBallistic = _useBallisticGrenadeAim || forceBallisticForLongRange;
            if (shouldUseBallistic && TryGetBallisticLaunchVelocity(origin, target, speed, out Vector2 ballisticVelocity))
            {
                return ballisticVelocity;
            }

            return toTarget.normalized * speed;
        }

        private bool TryGetBallisticLaunchVelocity(Vector2 origin, Vector2 target, float launchSpeed, out Vector2 velocity)
        {
            velocity = default;
            float gravityScale = 1f;
            Rigidbody2D prefabRb = _potatomasherProjectilePrefab != null ? _potatomasherProjectilePrefab.GetComponent<Rigidbody2D>() : null;
            if (prefabRb != null)
            {
                gravityScale = Mathf.Max(0.0001f, prefabRb.gravityScale);
            }

            float g = Mathf.Abs(Physics2D.gravity.y) * gravityScale;
            if (g < 0.0001f)
            {
                return false;
            }

            Vector2 delta = target - origin;
            float dx = delta.x;
            float dy = delta.y;
            float x = Mathf.Abs(dx);
            if (x < 0.01f)
            {
                return false;
            }

            float speed2 = launchSpeed * launchSpeed;
            float speed4 = speed2 * speed2;
            float discriminant = speed4 - g * (g * x * x + 2f * dy * speed2);
            if (discriminant < 0f)
            {
                return false;
            }

            float sqrtDisc = Mathf.Sqrt(discriminant);
            float tanLow = (speed2 - sqrtDisc) / (g * x);
            float tanHigh = (speed2 + sqrtDisc) / (g * x);

            float angleLow = Mathf.Atan(tanLow);
            float angleHigh = Mathf.Atan(tanHigh);
            float minHorizontalSpeed = launchSpeed * Mathf.Clamp01(_minHorizontalSpeedRatio);
            float dirX = Mathf.Sign(dx);

            Vector2 lowVelocity = new Vector2(
                dirX * launchSpeed * Mathf.Cos(angleLow),
                launchSpeed * Mathf.Sin(angleLow));
            Vector2 highVelocity = new Vector2(
                dirX * launchSpeed * Mathf.Cos(angleHigh),
                launchSpeed * Mathf.Sin(angleHigh));

            bool highHasEnoughHorizontal = Mathf.Abs(highVelocity.x) >= minHorizontalSpeed;
            bool lowHasEnoughHorizontal = Mathf.Abs(lowVelocity.x) >= minHorizontalSpeed;

            if (_preferHighArcGrenadeThrow && highHasEnoughHorizontal)
            {
                velocity = highVelocity;
            }
            else if (!_preferHighArcGrenadeThrow && lowHasEnoughHorizontal)
            {
                velocity = lowVelocity;
            }
            else if (lowHasEnoughHorizontal)
            {
                // Avoid near-vertical throws that explode above the thrower.
                velocity = lowVelocity;
            }
            else if (highHasEnoughHorizontal)
            {
                velocity = highVelocity;
            }
            else
            {
                // Both arcs are steep; pick the one with better horizontal reach.
                velocity = Mathf.Abs(lowVelocity.x) >= Mathf.Abs(highVelocity.x) ? lowVelocity : highVelocity;
            }
            return true;
        }

        public void TryAutoShootAtHero()
        {
            if (_debugDisableMp40Shooting)
            {
                if (_debugCombatLogs)
                {
                    Debug.Log("[ParatrooperWeaponSystem_V2] MP40 shot suppressed by debug flag.");
                }
                return;
            }

            if (!CanShoot())
                return;

            _lastFireTime = Time.time;
            if (_debugCombatLogs)
            {
                Debug.Log("[ParatrooperWeaponSystem_V2] Auto-shoot triggered.");
            }
            ShootRaycastAtHero();
        }

        private void ShootRaycastAtHero()
        {
            if (_heroModel == null)
            {
                if (_heroRoot == null)
                {
                    _heroRoot = FindAnyObjectByType<Hero_V2>();
                }

                if (_heroRoot != null)
                {
                    _heroModel = _heroRoot.GetComponent<HeroModel_V2>();
                }
            }

            if (_heroModel == null)
            {
                _heroModel = FindAnyObjectByType<HeroModel_V2>();
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

            // Muzzle from Spine aim bone when valid; combat direction always toward hero body (not crosshair bone),
            // so shots intersect bunker cover instead of passing above it.
            if (!TryGetParatrooperMuzzleWorld(out Vector2 origin))
            {
                origin = GetShotOrigin();
            }

            Vector2 aimTarget = GetHeroCombatAimWorldPoint();
            Vector2 toHero = aimTarget - origin;
            if (toHero.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector2 direction = toHero.normalized;

            // Include triggers (bunker cover may use trigger colliders) and all layers so bunker is never skipped.
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

            // From the right, the hero collider can sort before the bunker wall; if the hero is in the bunker
            // zone, the shot should still strike cover when any bunker geometry lies on the same ray.
            if (bunkerAlive && _respectBunkerCover && foundBunkerAlongRay)
            {
                bool heroIsCloser =
                    foundHeroAlongRay && firstHeroAlongRay.distance < firstBunkerAlongRay.distance;
                if (!heroIsCloser || heroSheltered)
                {
                    if (waveManager != null)
                    {
                        waveManager.ApplyBunkerDamage(_baseDamage);
                    }

                    if (_debugCombatLogs)
                    {
                        Debug.Log(
                            $"[ParatrooperWeaponSystem_V2] Bunker absorbs shot (heroCloser={heroIsCloser}, sheltered={heroSheltered}) for {_baseDamage} dmg.");
                    }

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
                            if (_debugCombatLogs)
                            {
                                Debug.Log("[ParatrooperWeaponSystem_V2] Bunker cover hit ignored (bunker destroyed).");
                            }

                            continue;
                        }

                        if (waveManager != null)
                        {
                            waveManager.ApplyBunkerDamage(_baseDamage);
                        }

                        if (_debugCombatLogs)
                        {
                            Debug.Log($"[ParatrooperWeaponSystem_V2] Hit bunker cover for {_baseDamage} damage.");
                        }

                        didApplyDamage = true;
                        damageHit = h;
                        break;
                    }

                    Hero_V2 heroRoot = h.collider.GetComponentInParent<Hero_V2>();
                    if (heroRoot != null)
                    {
                        if (waveManager != null && waveManager.IsHeroInsideBunker(heroRoot))
                        {
                            if (_debugCombatLogs)
                            {
                                Debug.Log("[ParatrooperWeaponSystem_V2] Hero inside bunker — skipping HP damage, ray continues.");
                            }

                            continue;
                        }

                        if (_debugCombatLogs)
                        {
                            Debug.Log($"[ParatrooperWeaponSystem_V2] Hit Hero_V2 for {_baseDamage} damage.");
                        }

                        heroRoot.ReceiveDamage(_baseDamage);
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
                            if (_debugCombatLogs)
                            {
                                Debug.Log("[ParatrooperWeaponSystem_V2] Hero model hit ignored (bunker protection active).");
                            }

                            continue;
                        }

                        if (_debugCombatLogs)
                        {
                            Debug.Log($"[ParatrooperWeaponSystem_V2] Hit HeroModel_V2 for {_baseDamage} damage.");
                        }

                        if (heroForZone != null)
                        {
                            heroForZone.ReceiveDamage(_baseDamage);
                        }
                        else
                        {
                            heroModelHit.TakeDamage(_baseDamage);
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
                finalPos = ResolveHeroVisualTarget(origin, finalPos);
            }

            if (_debugDrawShotRay)
            {
                Debug.DrawLine(origin, finalPos, Color.green, 0.5f);
            }

            PlayShotLine(origin, finalPos);
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

            // Fallback for scenes where BunkerHitbox_V2 was not added yet.
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

        private void PlayShotLine(Vector2 from, Vector2 to)
        {
            if (_shotTrailPrefab != null)
            {
                Transform spawnedTrail = Instantiate(_shotTrailPrefab, from, Quaternion.identity);
                spawnedTrail.localScale = Vector3.one; // Ignore tiny prefab scale that can hide the line.
                LineRenderer spawnedLine = spawnedTrail.GetComponent<LineRenderer>();
                if (spawnedLine == null)
                {
                    spawnedLine = spawnedTrail.GetComponentInChildren<LineRenderer>(true);
                }
                if (spawnedLine != null)
                {
                    ConfigureAndRenderLine(spawnedLine, from, to);
                    if (_debugShotLineLogs)
                    {
                        Debug.Log($"[ParatrooperWeaponSystem_V2] Shot line rendered from TRAIL prefab. from={from}, to={to}, width={spawnedLine.widthMultiplier:0.000}, sortingOrder={spawnedLine.sortingOrder}, scale={spawnedLine.transform.lossyScale}, startColor={spawnedLine.startColor}, endColor={spawnedLine.endColor}, overrideLineColor={_overrideLineColor}, lineColor={_lineColor}");
                    }
                }
                else
                {
                    Debug.LogWarning("[ParatrooperWeaponSystem_V2] Shot trail prefab has no LineRenderer (root/children).");
                }
                Destroy(spawnedTrail.gameObject, Mathf.Max(0.01f, _lineVisibleDuration));
                return;
            }

            // If _shotLineRenderer points to a prefab asset, spawn an instance and render there.
            if (_shotLineRenderer != null && !_shotLineRenderer.gameObject.scene.IsValid())
            {
                LineRenderer spawnedLine = Instantiate(_shotLineRenderer, from, Quaternion.identity);
                ConfigureAndRenderLine(spawnedLine, from, to);
                if (_debugShotLineLogs)
                {
                    Debug.Log($"[ParatrooperWeaponSystem_V2] Shot line rendered from LINE prefab. from={from}, to={to}, width={spawnedLine.widthMultiplier:0.000}, sortingOrder={spawnedLine.sortingOrder}, startColor={spawnedLine.startColor}, endColor={spawnedLine.endColor}, overrideLineColor={_overrideLineColor}, lineColor={_lineColor}");
                }
                Destroy(spawnedLine.gameObject, Mathf.Max(0.01f, _lineVisibleDuration));
                return;
            }

            if (_shotLineRenderer == null)
            {
                EnsureShotLineRenderer();
            }

            if (_shotLineRenderer == null)
            {
                return;
            }

            _shotLineRenderer.positionCount = 2;
            ConfigureAndRenderLine(_shotLineRenderer, from, to);
            if (_debugShotLineLogs)
            {
                Debug.Log($"[ParatrooperWeaponSystem_V2] Shot line rendered from SCENE line. from={from}, to={to}, width={_shotLineRenderer.widthMultiplier:0.000}, sortingOrder={_shotLineRenderer.sortingOrder}, startColor={_shotLineRenderer.startColor}, endColor={_shotLineRenderer.endColor}, overrideLineColor={_overrideLineColor}, lineColor={_lineColor}");
            }

            if (_lineCoroutine != null)
            {
                StopCoroutine(_lineCoroutine);
            }

            _lineCoroutine = StartCoroutine(HideShotLineAfterDelay());
        }

        private void EnsureShotLineRenderer()
        {
            // Prefab-based trail mode: no persistent scene LineRenderer needed.
            if (_shotTrailPrefab != null)
            {
                return;
            }

            // If this is still a prefab asset reference, skip runtime mutation.
            if (_shotLineRenderer != null && !_shotLineRenderer.gameObject.scene.IsValid())
            {
                return;
            }

            if (_shotLineRenderer == null)
            {
                var child = transform.Find("ParatrooperShotLine");
                if (child == null)
                {
                    GameObject lineGo = new GameObject("ParatrooperShotLine");
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
            _shotLineRenderer.widthMultiplier = _lineWidth;
            _shotLineRenderer.numCapVertices = 2;
            _shotLineRenderer.numCornerVertices = 0;
            _shotLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shotLineRenderer.receiveShadows = false;
            _shotLineRenderer.textureMode = LineTextureMode.Stretch;
            _shotLineRenderer.alignment = LineAlignment.View;
            _shotLineRenderer.startColor = _lineColor;
            _shotLineRenderer.endColor = _lineColor;
            ApplyLineMaterial(_shotLineRenderer, _lineColor);
        }

        private void CacheLineSortingLayer()
        {
            if (_lineSortingLayerId >= 0)
            {
                return;
            }

            if (TryResolveSortingLayerId(_shotLineSortingLayerName, out int forcedId))
            {
                _lineSortingLayerId = forcedId;
                return;
            }

            _lineSortingLayerId = GetTopSortingLayerId();
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

        private void ConfigureAndRenderLine(LineRenderer line, Vector2 from, Vector2 to)
        {
            if (line == null)
            {
                return;
            }

            line.enabled = false;
            line.transform.localScale = Vector3.one;
            line.useWorldSpace = true;
            line.positionCount = 2;
            Color tint = _overrideLineColor ? _lineColor : Color.white;
            float runtimeWidth = Mathf.Max(0.08f, _lineWidth);
            line.widthMultiplier = runtimeWidth;
            line.startWidth = line.widthMultiplier;
            line.endWidth = line.widthMultiplier;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.sortingOrder = Mathf.Max(5000, _lineSortingOrder);
            CacheLineSortingLayer();
            line.sortingLayerID = _lineSortingLayerId;
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

            ApplyLineMaterial(line, tint);

            float z = _firePoint != null ? _firePoint.position.z : transform.position.z;
            line.SetPosition(0, new Vector3(from.x, from.y, z));
            line.SetPosition(1, new Vector3(to.x, to.y, z));
            line.enabled = true;
        }

        private void ApplyLineMaterial(LineRenderer line, Color tint)
        {
            if (line == null)
            {
                return;
            }

            if (!_preferUrpUnlitLineMaterial)
            {
                if (line.sharedMaterial == null)
                {
                    Shader spriteShader = Shader.Find("Sprites/Default");
                    if (spriteShader != null)
                    {
                        line.sharedMaterial = new Material(spriteShader);
                        ConfigureLineMaterial(line.sharedMaterial, tint);
                    }
                }

                return;
            }

            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpUnlit != null)
            {
                Material mat = new Material(urpUnlit);
                ConfigureLineMaterial(mat, tint);

                line.material = mat;
                return;
            }

            if (line.sharedMaterial == null)
            {
                Shader spriteShader = Shader.Find("Sprites/Default");
                if (spriteShader != null)
                {
                    Material mat = new Material(spriteShader);
                    ConfigureLineMaterial(mat, tint);
                    line.material = mat;
                }
            }
        }

        private static void ConfigureLineMaterial(Material mat, Color tint)
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

            // Force solid draw to avoid transparent blending with blue scene layers.
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

        private Vector2 GetShotOrigin()
        {
            ValidateFirePointOwnership();
            return _firePoint != null ? _firePoint.position : transform.position;
        }

        private void ResolveAimBones()
        {
            if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_aimPointBoneName))
            {
                _aimPointBone = _skeletonAnimation.Skeleton.FindBone(_aimPointBoneName);
            }
            if (_aimPointBone == null)
            {
                _aimPointBone = _skeletonAnimation.Skeleton.FindBone("mp40-aim");
            }

            if (!string.IsNullOrWhiteSpace(_crossHairBoneName))
            {
                _crossHairBone = _skeletonAnimation.Skeleton.FindBone(_crossHairBoneName);
            }
            if (_crossHairBone == null)
            {
                _crossHairBone = _skeletonAnimation.Skeleton.FindBone("crosshair");
            }

            if (!string.IsNullOrWhiteSpace(_grenadeBoneName))
            {
                _grenadeBone = _skeletonAnimation.Skeleton.FindBone(_grenadeBoneName);
            }
            if (_grenadeBone == null)
            {
                _grenadeBone = _skeletonAnimation.Skeleton.FindBone("grenadeBone");
            }
        }

        /// <summary>World position of the muzzle / mp40-aim bone when within sanity distance of the SkeletonAnimation transform.</summary>
        private bool TryGetParatrooperMuzzleWorld(out Vector2 origin)
        {
            origin = default;

            ResolveAimBones();
            if (_skeletonAnimation == null || _aimPointBone == null)
            {
                return false;
            }

            origin = _skeletonAnimation.transform.TransformPoint(
                new Vector3(_aimPointBone.WorldX, _aimPointBone.WorldY, 0f));

            Vector2 skeletonPivot = _skeletonAnimation.transform.position;
            float refDistance = Vector2.Distance(origin, skeletonPivot);
            if (refDistance > Mathf.Max(0.5f, _maxAimOriginDistanceFromRoot))
            {
                if (_debugAimFallbackLogs)
                {
                    Debug.LogWarning(
                        $"[ParatrooperWeaponSystem_V2] Aim origin too far from skeleton pivot ({refDistance:0.00}). " +
                        $"origin={origin}, skeleton={skeletonPivot}, entityRoot={transform.position}. Falling back to firePoint/root origin.");
                }

                origin = default;
                return false;
            }

            return true;
        }

        /// <summary>World position from the current Spine grenade bone pose (same frame as grenade_throw event).</summary>
        private bool TryGetParatrooperGrenadeThrowOriginWorld(out Vector2 origin)
        {
            origin = default;

            ResolveAimBones();
            if (_skeletonAnimation == null || _grenadeBone == null)
            {
                return false;
            }

            origin = _skeletonAnimation.transform.TransformPoint(
                new Vector3(_grenadeBone.WorldX, _grenadeBone.WorldY, 0f));
            return true;
        }

        private Vector2 GetHeroCombatAimWorldPoint()
        {
            CacheHeroCollider();
            if (_cachedHeroCollider != null)
            {
                Bounds b = _cachedHeroCollider.bounds;
                float t = Mathf.Clamp01(_heroCombatAimHeightLerp);
                float y = Mathf.Lerp(b.min.y, b.max.y, t);
                return new Vector2(b.center.x, y);
            }

            return _heroModel != null ? (Vector2)_heroModel.transform.position : Vector2.zero;
        }

        private void ValidateFirePointOwnership()
        {
            if (_firePoint == null)
            {
                return;
            }

            if (_firePoint.GetComponentInParent<Hero_V2>() != null)
            {
                if (_debugShotLineLogs)
                {
                    Debug.LogWarning($"[ParatrooperWeaponSystem_V2] FirePoint '{_firePoint.name}' belongs to Hero. Falling back to Paratrooper transform.");
                }

                _firePoint = transform;
                return;
            }

            // In this setup, fire point should be on this Paratrooper hierarchy.
            if (_firePoint != transform && !_firePoint.IsChildOf(transform))
            {
                if (_debugShotLineLogs)
                {
                    Debug.LogWarning($"[ParatrooperWeaponSystem_V2] FirePoint '{_firePoint.name}' is outside Paratrooper hierarchy. Falling back to root transform.");
                }

                _firePoint = transform;
            }
        }

        private void CacheHeroCollider()
        {
            if (_cachedHeroCollider != null)
            {
                return;
            }

            if (_heroRoot != null)
            {
                _cachedHeroCollider = _heroRoot.GetComponentInChildren<Collider2D>();
                if (_cachedHeroCollider != null)
                {
                    return;
                }
            }

            if (_heroModel != null)
            {
                _cachedHeroCollider = _heroModel.GetComponentInChildren<Collider2D>();
            }

            if (_debugShotLineLogs && _cachedHeroCollider != null)
            {
                Bounds b = _cachedHeroCollider.bounds;
                Debug.Log($"[ParatrooperWeaponSystem_V2] Cached Hero collider='{_cachedHeroCollider.name}', center={b.center}, size={b.size}");
            }
        }

        private Vector2 ResolveHeroVisualTarget(Vector2 origin, Vector2 fallback)
        {
            CacheHeroCollider();
            if (_cachedHeroCollider == null)
            {
                if (_debugShotLineLogs)
                {
                    Debug.LogWarning($"[ParatrooperWeaponSystem_V2] No Hero collider cached. Using fallback target={fallback}");
                }
                return fallback;
            }

            Bounds b = _cachedHeroCollider.bounds;
            float targetY = Mathf.Lerp(b.min.y, b.max.y, 0.78f); // upper torso/head area
            Vector2 target = new Vector2(b.center.x, targetY);
            if (_debugShotLineLogs)
            {
                Debug.Log($"[ParatrooperWeaponSystem_V2] ResolveHeroVisualTarget origin={origin}, target={target}, collider='{_cachedHeroCollider.name}', boundsMin={b.min}, boundsMax={b.max}");
            }
            return target;
        }

        private IEnumerator HideShotLineAfterDelay()
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, _lineVisibleDuration));
            if (_shotLineRenderer != null)
            {
                _shotLineRenderer.enabled = false;
            }
            _lineCoroutine = null;
        }

    }
}
