using Assets.Scripts.Components;
using iStick2War;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    [RequireComponent(typeof(Collider2D))]
    public class HeroRocketProjectile_V2 : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private float _defaultSpeed = 14f;
        [SerializeField] private float _defaultLifetime = 5f;
        [SerializeField] private float _defaultDamage = 80f;
        [Header("Flight")]
        [SerializeField] private bool _forceStraightFlight = true;
        [Header("Explosion")]
        [SerializeField] private float _explosionRadius = 2.8f;
        [SerializeField] [Range(0f, 1f)] private float _minFalloffMultiplier = 0.35f;
        [SerializeField] private LayerMask _explosionMask = Physics2D.DefaultRaycastLayers;
        [SerializeField] private GameObject _explosionEffectPrefab;
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
                    _rb.bodyType = RigidbodyType2D.Kinematic;
                    _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                }

                _rb.linearVelocity = _travelDirection * _travelSpeed;
                _rb.WakeUp();
                _useManualMovement = !_rb.simulated || _rb.bodyType == RigidbodyType2D.Static;
            }

            CancelInvoke(nameof(ExplodeFromTimeout));
            Invoke(nameof(ExplodeFromTimeout), _lifetime);
        }

        private void Awake()
        {
            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody2D>();
            }
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
            TryExplode(other);
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
                TryExplode(collision.collider);
            }
        }

        private void ExplodeFromTimeout()
        {
            TryExplode(null);
        }

        private void TryExplode(Collider2D impactCollider)
        {
            if (_hasExploded)
            {
                return;
            }

            if (impactCollider != null && impactCollider.GetComponentInParent<Hero_V2>() != null)
            {
                return;
            }

            _hasExploded = true;
            SpawnExplosionEffect();
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

                Destroy(gameObject);
                return;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(explosionCenter, Mathf.Max(0.1f, _explosionRadius), _explosionMask);
            HashSet<ParatrooperDamageReceiver_V2> damagedParatroopers = new HashSet<ParatrooperDamageReceiver_V2>();
            HashSet<Explodable> damagedExplodables = new HashSet<Explodable>();
            HashSet<AircraftHealth_V2> damagedAircraft = new HashSet<AircraftHealth_V2>();

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
                    !IsWorldPointVisibleForExplosionDamage(clipCam, closestOnHit))
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
                    DamageInfo damageInfo = new DamageInfo
                    {
                        BaseDamage = finalDamage,
                        BodyPart = BodyPartType.Torso,
                        HitPoint = explosionCenter,
                        IsExplosive = true,
                        ExplosionForce = Mathf.Lerp(3.5f, 9f, damageMultiplier),
                        SourceWeapon = WeaponType.Bazooka
                    };
                    receiver.TakeDamage(damageInfo);
                    continue;
                }

                Explodable explodable = hit.GetComponentInParent<Explodable>();
                if (explodable != null && !damagedExplodables.Contains(explodable))
                {
                    damagedExplodables.Add(explodable);
                    explodable.TakeDamage(finalDamage);
                    continue;
                }

                AircraftHealth_V2 aircraft =
                    hit.GetComponent<AircraftHealth_V2>() ??
                    hit.GetComponentInParent<AircraftHealth_V2>();
                if (aircraft != null && !damagedAircraft.Contains(aircraft))
                {
                    damagedAircraft.Add(aircraft);
                    aircraft.ApplyDamage(finalAircraftDamage);
                }
            }

            if (_debugExplosion)
            {
                Debug.Log(
                    $"[HeroRocketProjectile_V2] Explosion center={explosionCenter}, radius={_explosionRadius:0.00}, " +
                    $"paratroopers={damagedParatroopers.Count}, explodables={damagedExplodables.Count}, aircraft={damagedAircraft.Count}");
            }

            Destroy(gameObject);
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
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                Vector3 c = cam.transform.position;
                float inset = Mathf.Min(_explosionCameraRectInset, Mathf.Max(0f, Mathf.Min(halfW, halfH) - 0.001f));
                float minX = c.x - halfW + inset;
                float maxX = c.x + halfW - inset;
                float minY = c.y - halfH + inset;
                float maxY = c.y + halfH - inset;
                return world.x >= minX && world.x <= maxX && world.y >= minY && world.y <= maxY;
            }

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var b = new Bounds(new Vector3(world.x, world.y, cam.transform.position.z), new Vector3(0.05f, 0.05f, 0.25f));
            return GeometryUtility.TestPlanesAABB(planes, b);
        }

        private void SpawnExplosionEffect()
        {
            if (_explosionEffectPrefab == null)
            {
                return;
            }

            GameObject effect = Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);
            float effectLifetime = Mathf.Max(0.05f, _explosionEffectLifetime);
            Destroy(effect, effectLifetime);
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
