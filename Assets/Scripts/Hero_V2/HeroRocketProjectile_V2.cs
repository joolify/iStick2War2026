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

        private float _damage;
        private bool _isInitialized;
        private float _lifetime;
        private bool _hasExploded;
        private Vector2 _travelDirection = Vector2.right;
        private float _travelSpeed = 14f;
        private bool _useManualMovement;

        public void Initialize(Vector2 direction, float speed, float lifetime, float damage)
        {
            _isInitialized = true;
            _damage = Mathf.Max(0f, damage);
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
            Collider2D[] hits = Physics2D.OverlapCircleAll(explosionCenter, Mathf.Max(0.1f, _explosionRadius), _explosionMask);
            HashSet<ParatrooperDamageReceiver_V2> damagedParatroopers = new HashSet<ParatrooperDamageReceiver_V2>();
            HashSet<Explodable> damagedExplodables = new HashSet<Explodable>();

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                float dist = Vector2.Distance(explosionCenter, hit.bounds.ClosestPoint(explosionCenter));
                float normalized = Mathf.Clamp01(dist / Mathf.Max(0.1f, _explosionRadius));
                float damageMultiplier = Mathf.Lerp(1f, Mathf.Clamp01(_minFalloffMultiplier), normalized);
                float finalDamage = Mathf.Max(0f, _damage * damageMultiplier);

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
                        ExplosionForce = Mathf.Lerp(3.5f, 9f, damageMultiplier)
                    };
                    receiver.TakeDamage(damageInfo);
                    continue;
                }

                Explodable explodable = hit.GetComponentInParent<Explodable>();
                if (explodable != null && !damagedExplodables.Contains(explodable))
                {
                    damagedExplodables.Add(explodable);
                    explodable.TakeDamage(finalDamage);
                }
            }

            if (_debugExplosion)
            {
                Debug.Log($"[HeroRocketProjectile_V2] Explosion center={explosionCenter}, radius={_explosionRadius:0.00}, paratroopers={damagedParatroopers.Count}, explodables={damagedExplodables.Count}");
            }

            Destroy(gameObject);
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
