using Assets.Scripts.Components;
using iStick2War;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    [RequireComponent(typeof(Collider2D))]
    public class HeroRocketProjectile_V2 : MonoBehaviour
    {
        /// <summary>
        /// Which enemy family was damaged by the blast (used to pick per-type explosion VFX). Order is not important;
        /// <see cref="SpawnExplosionEffectsForKinds"/> uses <see cref="VfxSpawnPriorityOrder"/> for spawn order.
        /// </summary>
        private enum RocketExplosionVfxKind
        {
            Paratrooper,
            Explodable,
            BombPlane,
            KamikazeDrone,
            BombDrone,
            /// <summary>Helicopter and other <see cref="AircraftHealth_V2"/> not matched above.</summary>
            HelicopterOrGenericAircraft
        }

        private static readonly RocketExplosionVfxKind[] VfxSpawnPriorityOrder =
        {
            RocketExplosionVfxKind.Paratrooper,
            RocketExplosionVfxKind.Explodable,
            RocketExplosionVfxKind.BombPlane,
            RocketExplosionVfxKind.KamikazeDrone,
            RocketExplosionVfxKind.BombDrone,
            RocketExplosionVfxKind.HelicopterOrGenericAircraft
        };

        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private float _defaultSpeed = 14f;
        [SerializeField] private float _defaultLifetime = 5f;
        [SerializeField] private float _defaultDamage = 80f;
        [Header("Flight")]
        [SerializeField] private bool _forceStraightFlight = true;
        [Header("Impact filtering")]
        [Tooltip("Ignore collisions/triggers until this long after spawn (avoids instant pops from spawn overlap).")]
        [SerializeField] private float _armingDelaySeconds = 0.08f;
        [Tooltip(
            "Trigger colliders only detonate the rocket if they are on these layers OR carry a damage/bunker component " +
            "(paratrooper, aircraft, explodable, BunkerHitbox). Empty = Ground, Bunker, Enemy, EnemyBodyPart, Aircraft.")]
        [SerializeField] private LayerMask _detonateOnTriggerLayers;
        [SerializeField] private bool _debugImpactLogs;
        [Tooltip("Logs every successful detonation: how it triggered and what collider was hit (use to trace mid-air pops).")]
        [SerializeField] private bool _logExplosionDetonation = true;
        [Header("Explosion")]
        [SerializeField] private float _explosionRadius = 2.8f;
        [SerializeField] [Range(0f, 1f)] private float _minFalloffMultiplier = 0.35f;
        [SerializeField] private LayerMask _explosionMask = Physics2D.DefaultRaycastLayers;
        [Header("Explosion VFX")]
        [Tooltip(
            "Fallback: terrain / no valid target / blast center off-screen (damage skipped), or when a damaged type has no typed prefab assigned.")]
        [SerializeField] private GameObject _explosionEffectPrefab;
        [SerializeField] private GameObject _explosionParatrooperEffectPrefab;
        [SerializeField] private GameObject _explosionExplodableEffectPrefab;
        [Tooltip("Bombing aircraft with Bombplane_V2.")]
        [SerializeField] private GameObject _explosionBombPlaneEffectPrefab;
        [SerializeField] private GameObject _explosionKamikazeDroneEffectPrefab;
        [SerializeField] private GameObject _explosionBombDroneEffectPrefab;
        [Tooltip("Helicopter and other AircraftHealth_V2 not covered by bomb plane / drone prefabs above.")]
        [SerializeField] private GameObject _explosionHelicopterEffectPrefab;
        [SerializeField] private float _explosionEffectLifetime = 1.5f;
        [SerializeField] private bool _debugExplosion = false;
        [Header("Explosion vs camera")]
        [Tooltip(
            "When enabled, explosion damage only applies if the blast center is on-screen and each hit's " +
            "closest point to the center lies inside this camera's view. Prevents off-screen enemies from " +
            "receiving bazooka knockback/damage from a visible hit. If unset, uses Camera.main.")]
        [SerializeField] private bool _clipExplosionDamageToCamera = true;
        [SerializeField] private Camera _explosionVisibilityCamera;
        [Tooltip("Inset inside the camera rect (world units) so edge hits are still valid.")]
        [SerializeField] private float _explosionCameraRectInset = 0.02f;

        private float _damage;
        private float _explosionDamageVsAircraft;
        private bool _isInitialized;
        private float _lifetime;
        private bool _hasExploded;
        private Vector2 _travelDirection = Vector2.right;
        private float _travelSpeed = 14f;
        private bool _useManualMovement;
        private float _armedAt;

        public void Initialize(Vector2 direction, float speed, float lifetime, float damage, float explosionDamageVsAircraft = -1f)
        {
            _isInitialized = true;
            _damage = Mathf.Max(0f, damage);
            _explosionDamageVsAircraft = explosionDamageVsAircraft >= 0f ? explosionDamageVsAircraft : _damage;
            _lifetime = Mathf.Max(0.1f, lifetime);

            _travelDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _travelSpeed = Mathf.Max(0.1f, speed);
            _useManualMovement = _rb == null;

            if (_rb != null)
            {
                _rb.gravityScale = 0f;
                _rb.linearDamping = 0f;
                _rb.angularDamping = 0f;
                _rb.angularVelocity = 0f;

                if (_forceStraightFlight)
                {
                    // Match pre–Hero_V2 migration behavior (e.g. 25e9a7e): kinematic straight flight avoids the
                    // dynamic solver pushing the rocket out of spawn overlap (~1m "pop"). Kinematic vs kinematic
                    // aircraft still needs full kinematic contacts (also enabled on AircraftHealth_V2).
                    _rb.bodyType = RigidbodyType2D.Kinematic;
                    _rb.gravityScale = 0f;
                    _rb.linearDamping = 0f;
                    _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    _rb.useFullKinematicContacts = true;
                }

                _rb.linearVelocity = _travelDirection * _travelSpeed;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.WakeUp();
                _useManualMovement = !_rb.simulated || _rb.bodyType == RigidbodyType2D.Static;
            }

            CancelInvoke(nameof(ExplodeFromTimeout));
            Invoke(nameof(ExplodeFromTimeout), _lifetime);
            _armedAt = Time.time + Mathf.Max(0f, _armingDelaySeconds);
        }

        private void Awake()
        {
            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody2D>();
            }

            EnsureDetonateOnTriggerLayerMask();
            EnsureExplosionOverlapMaskIncludesAirLayers();
        }

        private void Start()
        {
            if (!_isInitialized)
            {
                Initialize(Vector2.right, _defaultSpeed, _defaultLifetime, _defaultDamage);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryExplode(other, nameof(OnTriggerEnter2D));
        }

        private void Update()
        {
            if (_hasExploded)
            {
                return;
            }

            if (_useManualMovement)
            {
                transform.position += (Vector3)(_travelDirection * _travelSpeed * Time.deltaTime);
                return;
            }

            // Keep speed stable even if external physics/settings damp it.
            if (_rb != null && _rb.simulated && _rb.bodyType != RigidbodyType2D.Static)
            {
                _rb.gravityScale = 0f;
                _rb.linearVelocity = _travelDirection * _travelSpeed;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision != null)
            {
                TryExplode(collision.collider, nameof(OnCollisionEnter2D));
            }
        }

        private void ExplodeFromTimeout()
        {
            TryExplode(null, nameof(ExplodeFromTimeout));
        }

        private void TryExplode(Collider2D impactCollider, string detonationVia)
        {
            if (_hasExploded)
            {
                return;
            }

            if (impactCollider != null && impactCollider.GetComponentInParent<Hero_V2>() != null)
            {
                return;
            }

            if (impactCollider != null)
            {
                if (Time.time < _armedAt)
                {
                    if (_debugImpactLogs)
                    {
                        Debug.Log(
                            $"[HeroRocketProjectile_V2] Ignored impact '{impactCollider.name}' (not armed yet, " +
                            $"t={Time.time:0.###} < armedAt={_armedAt:0.###}).");
                    }

                    return;
                }

                if (impactCollider.isTrigger && !ShouldDetonateOnTrigger(impactCollider))
                {
                    if (_debugImpactLogs)
                    {
                        Debug.Log(
                            $"[HeroRocketProjectile_V2] Ignored trigger '{impactCollider.name}' on layer " +
                            $"'{LayerMask.LayerToName(impactCollider.gameObject.layer)}' (no detonation rule).");
                    }

                    return;
                }
            }

            _hasExploded = true;
            if (_logExplosionDetonation)
            {
                LogDetonationCommitted(impactCollider, detonationVia);
            }

            StopRocketMotion();

            Vector2 explosionCenter = transform.position;
            Camera clipCam = ResolveExplosionClipCamera();
            if (_clipExplosionDamageToCamera &&
                clipCam != null &&
                !IsWorldPointVisibleForExplosionDamage(clipCam, explosionCenter))
            {
                if (_debugExplosion)
                {
                    Debug.Log(
                        "[HeroRocketProjectile_V2] Explosion center off-screen; skipping all explosion damage " +
                        $"(center={explosionCenter}).");
                }

                SpawnExplosionEffectsForKinds(explosionCenter, null);
                Destroy(gameObject);
                return;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(explosionCenter, Mathf.Max(0.1f, _explosionRadius), _explosionMask);
            HashSet<ParatrooperDamageReceiver_V2> damagedParatroopers = new HashSet<ParatrooperDamageReceiver_V2>();
            HashSet<MechRobotBossDamageReceiver_V2> damagedMechBosses = new HashSet<MechRobotBossDamageReceiver_V2>();
            HashSet<Explodable> damagedExplodables = new HashSet<Explodable>();
            HashSet<AircraftHealth_V2> damagedAircraft = new HashSet<AircraftHealth_V2>();
            HashSet<RocketExplosionVfxKind> damagedKinds = new HashSet<RocketExplosionVfxKind>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                Vector2 closestOnHit = hit.bounds.ClosestPoint(explosionCenter);
                if (_clipExplosionDamageToCamera &&
                    clipCam != null &&
                    !IsExplosionDamageTargetOnScreen(clipCam, hit, closestOnHit))
                {
                    continue;
                }

                float dist = Vector2.Distance(explosionCenter, closestOnHit);
                float normalized = Mathf.Clamp01(dist / Mathf.Max(0.1f, _explosionRadius));
                float damageMultiplier = Mathf.Lerp(1f, Mathf.Clamp01(_minFalloffMultiplier), normalized);
                float finalDamage = Mathf.Max(0f, _damage * damageMultiplier);
                float finalAircraftDamage = Mathf.Max(0f, _explosionDamageVsAircraft * damageMultiplier);

                ParatrooperBodyPart_V2 bodyPart = hit.GetComponent<ParatrooperBodyPart_V2>();
                if (bodyPart != null)
                {
                    ParatrooperDamageReceiver_V2 receiver = bodyPart.GetComponentInParent<ParatrooperDamageReceiver_V2>();
                    if (receiver == null || damagedParatroopers.Contains(receiver))
                    {
                        continue;
                    }

                    damagedParatroopers.Add(receiver);
                    damagedKinds.Add(RocketExplosionVfxKind.Paratrooper);
                    Vector2 toTarget = closestOnHit - explosionCenter;
                    Vector2 shotDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
                    DamageInfo damageInfo = new DamageInfo
                    {
                        BaseDamage = finalDamage,
                        BodyPart = BodyPartType.Torso,
                        HitPoint = closestOnHit,
                        ShotDirection = shotDir,
                        IsExplosive = true,
                        ExplosionForce = Mathf.Lerp(3.5f, 9f, damageMultiplier),
                        SourceWeapon = WeaponType.Bazooka
                    };
                    receiver.TakeDamage(damageInfo);
                    continue;
                }

                MechRobotBossBodyPart_V2 mechPart = hit.GetComponent<MechRobotBossBodyPart_V2>();
                if (mechPart != null)
                {
                    MechRobotBossDamageReceiver_V2 mechReceiver =
                        mechPart.GetComponentInParent<MechRobotBossDamageReceiver_V2>();
                    if (mechReceiver == null || damagedMechBosses.Contains(mechReceiver))
                    {
                        continue;
                    }

                    damagedMechBosses.Add(mechReceiver);
                    damagedKinds.Add(RocketExplosionVfxKind.Paratrooper);
                    Vector2 toTarget = closestOnHit - explosionCenter;
                    Vector2 shotDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
                    DamageInfo damageInfo = new DamageInfo
                    {
                        BaseDamage = finalDamage,
                        BodyPart = BodyPartType.Torso,
                        HitPoint = closestOnHit,
                        ShotDirection = shotDir,
                        IsExplosive = true,
                        ExplosionForce = Mathf.Lerp(3.5f, 9f, damageMultiplier),
                        SourceWeapon = WeaponType.Bazooka
                    };
                    mechReceiver.TakeDamage(damageInfo);
                    continue;
                }

                Explodable explodable = hit.GetComponentInParent<Explodable>();
                if (explodable != null && !damagedExplodables.Contains(explodable))
                {
                    damagedExplodables.Add(explodable);
                    damagedKinds.Add(RocketExplosionVfxKind.Explodable);
                    explodable.TakeDamage(finalDamage);
                    continue;
                }

                AircraftHealth_V2 aircraft =
                    hit.GetComponent<AircraftHealth_V2>() ??
                    hit.GetComponentInParent<AircraftHealth_V2>();
                if (aircraft != null && !damagedAircraft.Contains(aircraft))
                {
                    damagedAircraft.Add(aircraft);
                    damagedKinds.Add(ClassifyAircraftExplosionVfxKind(aircraft));
                    aircraft.ApplyDamage(finalAircraftDamage);
                }
            }

            SpawnExplosionEffectsForKinds(explosionCenter, damagedKinds);

            if (_debugExplosion)
            {
                Debug.Log(
                    $"[HeroRocketProjectile_V2] Explosion center={explosionCenter}, radius={_explosionRadius:0.00}, " +
                    $"paratroopers={damagedParatroopers.Count}, explodables={damagedExplodables.Count}, aircraft={damagedAircraft.Count}");
            }

            Destroy(gameObject);
        }

        private void LogDetonationCommitted(Collider2D impactCollider, string detonationVia)
        {
            Vector2 p = transform.position;
            string hitDetail;
            if (impactCollider == null)
            {
                hitDetail = "impactCollider=null (typical: lifetime expired)";
            }
            else
            {
                string impactClass = ClassifyImpactForDebugLog(impactCollider);
                hitDetail =
                    $"class={impactClass} impact='{impactCollider.name}' path='{BuildTransformHierarchyPath(impactCollider.transform)}' " +
                    $"layer={LayerMask.LayerToName(impactCollider.gameObject.layer)}({impactCollider.gameObject.layer}) " +
                    $"isTrigger={impactCollider.isTrigger} " +
                    $"rbType={(impactCollider.attachedRigidbody != null ? impactCollider.attachedRigidbody.bodyType.ToString() : "none")}";
            }

            float age = Mathf.Max(0f, Time.time - (_armedAt - Mathf.Max(0f, _armingDelaySeconds)));
            Debug.Log(
                $"[HeroRocketProjectile_V2] DETONATE via={detonationVia} age={age:0.###}s worldPos=({p.x:0.###},{p.y:0.###}) " +
                $"travelDir=({_travelDirection.x:0.###},{_travelDirection.y:0.###}) speed={_travelSpeed:0.###} {hitDetail}");
        }

        /// <summary>
        /// Short tag for detonation logs so "mid-air pop" reports are easy to read (e.g. aircraft vs stray trigger).
        /// </summary>
        private static string ClassifyImpactForDebugLog(Collider2D impactCollider)
        {
            if (impactCollider == null)
            {
                return "unknown";
            }

            if (impactCollider.GetComponentInParent<Hero_V2>() != null)
            {
                return "Hero";
            }

            if (impactCollider.GetComponent<ParatrooperBodyPart_V2>() != null ||
                impactCollider.GetComponentInParent<ParatrooperDamageReceiver_V2>() != null ||
                impactCollider.GetComponent<MechRobotBossBodyPart_V2>() != null ||
                impactCollider.GetComponentInParent<MechRobotBossDamageReceiver_V2>() != null)
            {
                return "Paratrooper";
            }

            if (impactCollider.GetComponent<AircraftHealth_V2>() != null ||
                impactCollider.GetComponentInParent<AircraftHealth_V2>() != null)
            {
                return "Aircraft";
            }

            if (impactCollider.GetComponentInParent<Explodable>() != null)
            {
                return "Explodable";
            }

            if (impactCollider.GetComponentInParent<BunkerHitbox_V2>() != null)
            {
                return "BunkerHitbox";
            }

            return "Other";
        }

        private static string BuildTransformHierarchyPath(Transform t)
        {
            if (t == null)
            {
                return "";
            }

            var parts = new List<string>(8);
            for (Transform walk = t; walk != null; walk = walk.parent)
            {
                parts.Add(walk.name);
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private void EnsureDetonateOnTriggerLayerMask()
        {
            if (_detonateOnTriggerLayers.value != 0)
            {
                return;
            }

            int mask = 0;
            string[] layerNames = { "Ground", "Bunker", "Enemy", "EnemyBodyPart", "Aircraft" };
            for (int i = 0; i < layerNames.Length; i++)
            {
                int layer = LayerMask.NameToLayer(layerNames[i]);
                if (layer >= 0)
                {
                    mask |= 1 << layer;
                }
            }

            _detonateOnTriggerLayers = mask;
        }

        /// <summary>
        /// Ensures <see cref="Physics2D.OverlapCircleAll"/> can see aircraft hitboxes on the <c>Aircraft</c> layer
        /// (some prefabs use <c>EnemyBodyPart</c> already; others only Aircraft).
        /// </summary>
        private void EnsureExplosionOverlapMaskIncludesAirLayers()
        {
            int aircraftLayer = LayerMask.NameToLayer("Aircraft");
            if (aircraftLayer >= 0)
            {
                _explosionMask |= 1 << aircraftLayer;
            }
        }

        /// <summary>
        /// Non-trigger colliders always detonate (after arming). Triggers must be gameplay surfaces or damage targets —
        /// otherwise large sensor volumes cause mid-air pops.
        /// </summary>
        private bool ShouldDetonateOnTrigger(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            if (other.GetComponent<ParatrooperBodyPart_V2>() != null ||
                other.GetComponentInParent<ParatrooperDamageReceiver_V2>() != null ||
                other.GetComponent<MechRobotBossBodyPart_V2>() != null ||
                other.GetComponentInParent<MechRobotBossDamageReceiver_V2>() != null)
            {
                return true;
            }

            if (other.GetComponent<AircraftHealth_V2>() != null ||
                other.GetComponentInParent<AircraftHealth_V2>() != null)
            {
                return true;
            }

            if (other.GetComponentInParent<Explodable>() != null)
            {
                return true;
            }

            if (other.GetComponentInParent<BunkerHitbox_V2>() != null)
            {
                return true;
            }

            int bit = 1 << other.gameObject.layer;
            return (_detonateOnTriggerLayers.value & bit) != 0;
        }

        private static RocketExplosionVfxKind ClassifyAircraftExplosionVfxKind(AircraftHealth_V2 aircraft)
        {
            if (aircraft == null)
            {
                return RocketExplosionVfxKind.HelicopterOrGenericAircraft;
            }

            if (aircraft.GetComponentInParent<EnemyKamikazeDrone_V2>() != null)
            {
                return RocketExplosionVfxKind.KamikazeDrone;
            }

            if (aircraft.GetComponentInParent<EnemyBombDrone_V2>() != null)
            {
                return RocketExplosionVfxKind.BombDrone;
            }

            if (aircraft.GetComponentInParent<Bombplane_V2>() != null)
            {
                return RocketExplosionVfxKind.BombPlane;
            }

            return RocketExplosionVfxKind.HelicopterOrGenericAircraft;
        }

        /// <summary>
        /// Spawns typed prefabs for each damaged <paramref name="kinds"/> (in fixed priority order). If <paramref name="kinds"/>
        /// is null or empty, or no typed prefab is assigned for the damaged set, spawns <see cref="_explosionEffectPrefab"/> once.
        /// </summary>
        private void SpawnExplosionEffectsForKinds(Vector2 center, HashSet<RocketExplosionVfxKind> kinds)
        {
            bool spawnedTyped = false;
            if (kinds != null && kinds.Count > 0)
            {
                for (int i = 0; i < VfxSpawnPriorityOrder.Length; i++)
                {
                    RocketExplosionVfxKind kind = VfxSpawnPriorityOrder[i];
                    if (!kinds.Contains(kind))
                    {
                        continue;
                    }

                    GameObject prefab = GetExplosionPrefabForKind(kind);
                    if (prefab == null)
                    {
                        continue;
                    }

                    SpawnOneExplosionInstance(prefab, center);
                    spawnedTyped = true;
                }
            }

            if (!spawnedTyped && _explosionEffectPrefab != null)
            {
                SpawnOneExplosionInstance(_explosionEffectPrefab, center);
            }
        }

        private GameObject GetExplosionPrefabForKind(RocketExplosionVfxKind kind)
        {
            switch (kind)
            {
                case RocketExplosionVfxKind.Paratrooper:
                    return _explosionParatrooperEffectPrefab;
                case RocketExplosionVfxKind.Explodable:
                    return _explosionExplodableEffectPrefab;
                case RocketExplosionVfxKind.BombPlane:
                    return _explosionBombPlaneEffectPrefab;
                case RocketExplosionVfxKind.KamikazeDrone:
                    return _explosionKamikazeDroneEffectPrefab;
                case RocketExplosionVfxKind.BombDrone:
                    return _explosionBombDroneEffectPrefab;
                case RocketExplosionVfxKind.HelicopterOrGenericAircraft:
                    return _explosionHelicopterEffectPrefab;
                default:
                    return null;
            }
        }

        private void SpawnOneExplosionInstance(GameObject prefab, Vector2 center)
        {
            GameObject effect = Instantiate(prefab, center, Quaternion.identity);
            float effectLifetime = Mathf.Max(0.05f, _explosionEffectLifetime);
            Destroy(effect, effectLifetime);
        }

        private Camera ResolveExplosionClipCamera()
        {
            if (_explosionVisibilityCamera != null)
            {
                return _explosionVisibilityCamera;
            }

            return Camera.main;
        }

        /// <summary>
        /// True if <paramref name="world"/> lies inside the clip camera's view (orthographic rect with inset, else frustum AABB test).
        /// </summary>
        private bool IsWorldPointVisibleForExplosionDamage(Camera cam, Vector2 world)
        {
            if (cam == null || !cam.isActiveAndEnabled)
            {
                return true;
            }

            if (cam.orthographic)
            {
                TryGetOrthographicExplosionClipRect(cam, out float minX, out float maxX, out float minY, out float maxY);
                return world.x >= minX && world.x <= maxX && world.y >= minY && world.y <= maxY;
            }

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var b = new Bounds(new Vector3(world.x, world.y, cam.transform.position.z), new Vector3(0.05f, 0.05f, 0.25f));
            return GeometryUtility.TestPlanesAABB(planes, b);
        }

        /// <summary>
        /// Per-target clip: closest point alone can lie off-screen for tall air targets (helicopter above the view)
        /// even when part of the collider is visible — allow damage if the collider bounds intersect the clip rect.
        /// </summary>
        private bool IsExplosionDamageTargetOnScreen(Camera cam, Collider2D hit, Vector2 closestOnHit)
        {
            if (cam == null || !cam.isActiveAndEnabled || hit == null)
            {
                return true;
            }

            if (IsWorldPointVisibleForExplosionDamage(cam, closestOnHit))
            {
                return true;
            }

            Bounds b = hit.bounds;
            if (cam.orthographic)
            {
                TryGetOrthographicExplosionClipRect(cam, out float minX, out float maxX, out float minY, out float maxY);
                return !(b.max.x < minX || b.min.x > maxX || b.max.y < minY || b.min.y > maxY);
            }

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            return GeometryUtility.TestPlanesAABB(planes, b);
        }

        private void TryGetOrthographicExplosionClipRect(
            Camera cam,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            float inset = Mathf.Min(_explosionCameraRectInset, Mathf.Max(0f, Mathf.Min(halfW, halfH) - 0.001f));
            minX = c.x - halfW + inset;
            maxX = c.x + halfW - inset;
            minY = c.y - halfH + inset;
            maxY = c.y + halfH - inset;
        }

        private void StopRocketMotion()
        {
            if (_rb == null)
            {
                return;
            }

            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.simulated = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, _explosionRadius));
        }
    }
}
