using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace iStick2War_V2
{
    /*
 * PotatomasherProjectile_V2 (Paratrooper grenade projectile)
 *
 * PURPOSE:
 * Timed-fuse grenade arc with sub-stepped kinematic casts against Ground (solid) and Bunker (triggers).
 * Short spawn grace only suppresses Ground so throws from the paratrooper's feet do not detonate instantly.
 *
 * ❌ MUST NOT: decide throw timing (Controller + weapon system + Spine events own that).
 */
    [RequireComponent(typeof(Collider2D))]
    public sealed class PotatomasherProjectile_V2 : MonoBehaviour
    {
        private const string BazookaExplosionPrefabAssetPath =
            "Assets/Prefabs/Explosions/Enemies/Paratrooper_Exp.prefab";

        private static GameObject s_cachedBazookaStyleExplosionPrefab;

        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private float _gravityScale = 1.75f;
        [SerializeField] private GameObject _explosionEffectPrefab;
        [SerializeField] private float _explosionEffectLifetime = 1.5f;
        [Tooltip("Target world-space width of the grenade sprite (hand-sized).")]
        [SerializeField] private float _desiredWorldWidth = 0.34f;
        [SerializeField] private bool _useKinematicArcFlight = true;
        [SerializeField] private string _flightSortingLayerName = "Bombs";
        [SerializeField] private int _flightSortingOrder = 120;
        [SerializeField] private LayerMask _groundStopMask;
        [SerializeField] private LayerMask _bunkerStopMask;
        [SerializeField] private float _groundProbeRadius = 0.08f;
        [SerializeField] private float _groundSurfaceOffset = 0.06f;
        [SerializeField] private float _groundProbeDownDistance = 12f;
        [Tooltip("Max distance per physics sub-step so thin Platform colliders are not skipped.")]
        [SerializeField] private float _maxMoveStepDistance = 0.06f;
        [Tooltip("Ignore Ground contact until this many seconds after Initialize.")]
        [SerializeField] private float _groundContactGraceSeconds = 0.32f;
        [Tooltip("Ignore Ground contact until the grenade has traveled at least this far from spawn.")]
        [SerializeField] private float _minTravelBeforeGroundStop = 0.42f;
        [Tooltip("Bunker front colliders are triggers; shorter grace so lobs can detonate on cover.")]
        [SerializeField] private float _bunkerContactGraceSeconds = 0.12f;
        [SerializeField] private float _minTravelBeforeBunkerStop = 0.22f;
        [Tooltip("Lift spawn slightly so the first frame does not overlap the Platform collider.")]
        [SerializeField] private float _spawnHeightClearance = 0.28f;
        [SerializeField] private bool _explodeOnContact = true;

        private float _fuseSeconds = 2.25f;
        private int _damage = 24;
        private float _radius = 1.6f;
        private bool _hasExploded;
        private bool _kinematicFlying;
        private Vector2 _kinematicVelocity;
        private float _gravityAcceleration;
        private Collider2D[] _colliders;
        private Vector2 _spawnWorldPosition;
        private float _flightStartTime;

        public void Initialize(Vector2 initialVelocity, float fuseSeconds, int damage, float explosionRadius)
        {
            _fuseSeconds = Mathf.Max(0.1f, fuseSeconds);
            _damage = Mathf.Max(1, damage);
            _radius = Mathf.Max(0.1f, explosionRadius);
            _kinematicVelocity = initialVelocity;
            _gravityAcceleration = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.0001f, _gravityScale);
            _flightStartTime = Time.time;

            if (_rigidbody2D == null)
            {
                _rigidbody2D = GetComponent<Rigidbody2D>();
            }

            EnsureStopLayerMasks();
            SnapToPhysicsPlaneZ();
            ApplySpawnHeightClearance();
            ApplyVisualScaleFromSprite();
            EnsureFlightVisible();
            ConfigureFlightPhysics(initialVelocity);

            _spawnWorldPosition = transform.position;

            CancelInvoke(nameof(ExplodeFromFuse));
            Invoke(nameof(ExplodeFromFuse), _fuseSeconds);
        }

        private void Awake()
        {
            if (_rigidbody2D == null)
            {
                _rigidbody2D = GetComponent<Rigidbody2D>();
            }

            _colliders = GetComponents<Collider2D>();
            EnsureStopLayerMasks();
            EnsureExplosionEffectPrefab();
            SnapToPhysicsPlaneZ();
            ApplyVisualScaleFromSprite();
        }

        private void FixedUpdate()
        {
            if (!_kinematicFlying || _hasExploded)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            _kinematicVelocity.y -= _gravityAcceleration * dt;

            Vector2 from = transform.position;
            Vector2 totalMotion = _kinematicVelocity * dt;
            float totalDistance = totalMotion.magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(totalDistance / Mathf.Max(0.02f, _maxMoveStepDistance)));
            Vector2 stepDelta = totalMotion / steps;
            Vector2 pos = from;

            for (int step = 0; step < steps; step++)
            {
                Vector2 stepFrom = pos;
                Vector2 stepTo = stepFrom + stepDelta;

                if (_explodeOnContact && TryStopAtContact(stepFrom, stepTo, out Vector2 stopPos))
                {
                    transform.position = stopPos;
                    Explode();
                    return;
                }

                pos = stepTo;
            }

            transform.position = pos;
        }

        private bool TryStopAtContact(Vector2 from, Vector2 to, out Vector2 stopPos)
        {
            stopPos = to;
            float traveledFromSpawn = Vector2.Distance(from, _spawnWorldPosition);

            if (CanDetectBunkerContact(traveledFromSpawn) &&
                TryCastSegmentStop(from, to, _bunkerStopMask, queriesHitTriggers: true, minHitDistance: 0f, out stopPos))
            {
                return true;
            }

            if (!CanDetectGroundContact(traveledFromSpawn))
            {
                return false;
            }

            if (TryCastSegmentStop(from, to, _groundStopMask, queriesHitTriggers: false, minHitDistance: 0f, out stopPos))
            {
                return true;
            }

            if (_kinematicVelocity.y <= 0f && TryDownwardGroundStop(to, out stopPos))
            {
                return true;
            }

            return false;
        }

        private bool CanDetectGroundContact(float traveledFromSpawn)
        {
            if (Time.time - _flightStartTime < Mathf.Max(0.05f, _groundContactGraceSeconds))
            {
                return false;
            }

            return traveledFromSpawn >= Mathf.Max(0.08f, _minTravelBeforeGroundStop);
        }

        private bool CanDetectBunkerContact(float traveledFromSpawn)
        {
            if (Time.time - _flightStartTime < Mathf.Max(0.03f, _bunkerContactGraceSeconds))
            {
                return false;
            }

            return traveledFromSpawn >= Mathf.Max(0.08f, _minTravelBeforeBunkerStop);
        }

        private bool TryCastSegmentStop(
            Vector2 from,
            Vector2 to,
            LayerMask mask,
            bool queriesHitTriggers,
            float minHitDistance,
            out Vector2 stopPos)
        {
            stopPos = to;
            if (mask.value == 0)
            {
                return false;
            }

            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.00001f)
            {
                return false;
            }

            Vector2 direction = delta / distance;
            float probeRadius = Mathf.Max(0.02f, _groundProbeRadius);
            bool prevHitTriggers = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = queriesHitTriggers;
            try
            {
                RaycastHit2D castHit = Physics2D.CircleCast(
                    from,
                    probeRadius,
                    direction,
                    distance + probeRadius,
                    mask);

                if (castHit.collider != null && castHit.distance >= minHitDistance)
                {
                    stopPos = castHit.point + castHit.normal * _groundSurfaceOffset;
                    return true;
                }

                RaycastHit2D lineHit = Physics2D.Linecast(from, to, mask);
                if (lineHit.collider != null)
                {
                    stopPos = lineHit.point + lineHit.normal * _groundSurfaceOffset;
                    return true;
                }
            }
            finally
            {
                Physics2D.queriesHitTriggers = prevHitTriggers;
            }

            return false;
        }

        private bool TryDownwardGroundStop(Vector2 at, out Vector2 stopPos)
        {
            stopPos = at;
            if (_groundStopMask.value == 0)
            {
                return false;
            }

            Vector2 probeStart = new Vector2(at.x, at.y + 0.45f);
            RaycastHit2D downHit = Physics2D.Raycast(
                probeStart,
                Vector2.down,
                _groundProbeDownDistance,
                _groundStopMask);

            if (downHit.collider == null)
            {
                return false;
            }

            float floorY = downHit.point.y + _groundSurfaceOffset;
            if (at.y <= floorY + 0.04f)
            {
                stopPos = new Vector2(at.x, floorY);
                return true;
            }

            return false;
        }

        private void ApplySpawnHeightClearance()
        {
            if (_spawnHeightClearance <= 0f)
            {
                return;
            }

            Vector3 p = transform.position;
            p.y += _spawnHeightClearance;
            transform.position = p;
        }

        private void SnapToPhysicsPlaneZ()
        {
            Vector3 p = transform.position;
            p.z = 0f;
            transform.position = p;
        }

        private void EnsureStopLayerMasks()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int bunkerLayer = LayerMask.NameToLayer("Bunker");

            if (_groundStopMask.value == 0 && groundLayer >= 0)
            {
                _groundStopMask = 1 << groundLayer;
            }

            if (_bunkerStopMask.value == 0 && bunkerLayer >= 0)
            {
                _bunkerStopMask = 1 << bunkerLayer;
            }
        }

        private void ApplyVisualScaleFromSprite()
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                float fallback = Mathf.Max(0.01f, _desiredWorldWidth * 0.5f);
                transform.localScale = new Vector3(fallback, fallback, 1f);
                return;
            }

            float spriteWidth = spriteRenderer.sprite.bounds.size.x;
            if (spriteWidth < 0.001f)
            {
                return;
            }

            float uniformScale = _desiredWorldWidth / spriteWidth;
            transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        }

        private void ConfigureFlightPhysics(Vector2 initialVelocity)
        {
            SetCollidersEnabled(false);

            if (_rigidbody2D == null)
            {
                return;
            }

            if (_useKinematicArcFlight)
            {
                _kinematicFlying = true;
                _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
                _rigidbody2D.gravityScale = 0f;
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.angularVelocity = 0f;
                return;
            }

            _kinematicFlying = false;
            _rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody2D.gravityScale = _gravityScale;
            _rigidbody2D.WakeUp();
            _rigidbody2D.linearVelocity = initialVelocity;
        }

        private void EnsureFlightVisible()
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_flightSortingLayerName))
            {
                int layerId = SortingLayer.NameToID(_flightSortingLayerName);
                if (layerId != 0 || _flightSortingLayerName == "Default")
                {
                    spriteRenderer.sortingLayerID = layerId;
                }
            }

            spriteRenderer.sortingOrder = _flightSortingOrder;
            spriteRenderer.enabled = true;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (_colliders == null || _colliders.Length == 0)
            {
                _colliders = GetComponents<Collider2D>();
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = enabled;
                }
            }
        }

        private void ExplodeFromFuse()
        {
            Explode();
        }

        private void Explode()
        {
            if (_hasExploded)
            {
                return;
            }

            _hasExploded = true;
            _kinematicFlying = false;
            Vector2 center = transform.position;

            WaveManager_V2 waveManager = FindAnyObjectByType<WaveManager_V2>();
            int bunkerDamage = ComputeBunkerDamageAtPosition(center, waveManager);
            if (bunkerDamage > 0 && waveManager != null)
            {
                waveManager.ApplyBunkerDamage(bunkerDamage);
            }

            int heroDamage = _damage - bunkerDamage;
            Hero_V2 hero = FindAnyObjectByType<Hero_V2>();
            if (heroDamage > 0 &&
                hero != null &&
                !hero.IsDead())
            {
                float heroDist = Vector2.Distance(center, hero.transform.position);
                if (heroDist <= _radius)
                {
                    Vector2 toHero = (Vector2)hero.transform.position - center;
                    Vector2 shotDir = toHero.sqrMagnitude > 0.0001f ? toHero.normalized : Vector2.left;
                    hero.ReceiveDamage(heroDamage, ignoreBunkerSafeZone: true, incomingShotWorldDirection: shotDir);
                }
            }

            EnsureExplosionEffectPrefab();
            if (_explosionEffectPrefab != null)
            {
                GameObject fx = Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);
                Destroy(fx, Mathf.Max(0.05f, _explosionEffectLifetime));
            }

            Destroy(gameObject);
        }

        private int ComputeBunkerDamageAtPosition(Vector2 center, WaveManager_V2 waveManager)
        {
            if (waveManager == null || waveManager.BunkerHealth <= 0)
            {
                return 0;
            }

            BunkerHitbox_V2[] markers = FindObjectsByType<BunkerHitbox_V2>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            if (markers == null || markers.Length == 0)
            {
                return 0;
            }

            for (int i = 0; i < markers.Length; i++)
            {
                BunkerHitbox_V2 marker = markers[i];
                if (marker == null || !marker.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Collider2D col = marker.GetComponent<Collider2D>();
                if (col == null || !col.enabled)
                {
                    continue;
                }

                if (IsExplosionOverlappingCollider(center, col))
                {
                    return Mathf.Min(_damage, waveManager.BunkerHealth);
                }
            }

            return 0;
        }

        private bool IsExplosionOverlappingCollider(Vector2 center, Collider2D col)
        {
            if (col == null)
            {
                return false;
            }

            if (col.OverlapPoint(center))
            {
                return true;
            }

            Vector2 closest = col.ClosestPoint(center);
            return Vector2.Distance(center, closest) <= _radius;
        }

        private void EnsureExplosionEffectPrefab()
        {
            if (_explosionEffectPrefab != null)
            {
                return;
            }

            _explosionEffectPrefab = ResolveBazookaStyleExplosionPrefab();
        }

        private static GameObject ResolveBazookaStyleExplosionPrefab()
        {
            if (s_cachedBazookaStyleExplosionPrefab != null)
            {
                return s_cachedBazookaStyleExplosionPrefab;
            }

#if UNITY_EDITOR
            s_cachedBazookaStyleExplosionPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(BazookaExplosionPrefabAssetPath);
#endif

            return s_cachedBazookaStyleExplosionPrefab;
        }
    }
}
