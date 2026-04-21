using UnityEngine;

namespace iStick2War_V2
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class BombProjectile_V2 : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private float _gravityScale = 2.4f;
        [SerializeField] private float _lifetimeSeconds = 9f;
        [SerializeField] private int _damage = 30;
        [SerializeField] private float _explosionRadius = 1.9f;
        [SerializeField] private GameObject _explosionEffectPrefab;
        [SerializeField] private float _explosionEffectLifetime = 1.4f;
        [Header("Impact filtering")]
        [Tooltip("If empty, defaults to Ground + Bunker + Player.")]
        [SerializeField] private LayerMask _triggerImpactLayers;
        [SerializeField] private bool _debugImpactLogs;

        private bool _exploded;

        public void Initialize(Vector2 inheritedVelocity, int damage, float explosionRadius)
        {
            _damage = Mathf.Max(1, damage);
            _explosionRadius = Mathf.Max(0.2f, explosionRadius);

            if (_rigidbody2D == null)
            {
                _rigidbody2D = GetComponent<Rigidbody2D>();
            }

            if (_rigidbody2D != null)
            {
                _rigidbody2D.gravityScale = _gravityScale;
                _rigidbody2D.linearVelocity = inheritedVelocity;
            }

            Destroy(gameObject, Mathf.Max(0.5f, _lifetimeSeconds));
        }

        private void Awake()
        {
            if (_rigidbody2D == null)
            {
                _rigidbody2D = GetComponent<Rigidbody2D>();
            }

            if (_triggerImpactLayers.value == 0)
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                int bunkerLayer = LayerMask.NameToLayer("Bunker");
                int playerLayer = LayerMask.NameToLayer("Player");
                int mask = 0;
                if (groundLayer >= 0) mask |= 1 << groundLayer;
                if (bunkerLayer >= 0) mask |= 1 << bunkerLayer;
                if (playerLayer >= 0) mask |= 1 << playerLayer;
                _triggerImpactLayers = mask;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null)
            {
                return;
            }

            Explode();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.GetComponentInParent<AircraftHealth_V2>() != null)
            {
                return;
            }

            if (!ShouldExplodeOnTrigger(other))
            {
                if (_debugImpactLogs)
                {
                    Debug.Log($"[BombProjectile_V2] Ignored trigger '{other.name}' on layer '{LayerMask.LayerToName(other.gameObject.layer)}'.");
                }

                return;
            }

            Explode();
        }

        private bool ShouldExplodeOnTrigger(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            int otherLayerMaskBit = 1 << other.gameObject.layer;
            if ((_triggerImpactLayers.value & otherLayerMaskBit) != 0)
            {
                return true;
            }

            if (other.GetComponentInParent<BunkerHitbox_V2>() != null)
            {
                return true;
            }

            if (other.GetComponentInParent<Hero_V2>() != null)
            {
                return true;
            }

            return false;
        }

        private void Explode()
        {
            if (_exploded)
            {
                return;
            }

            _exploded = true;
            Vector2 center = transform.position;

            WaveManager_V2 waveManager = FindAnyObjectByType<WaveManager_V2>();
            if (waveManager != null)
            {
                waveManager.ApplyBunkerDamage(_damage);
            }

            Hero_V2 hero = FindAnyObjectByType<Hero_V2>();
            if (hero != null && !hero.IsDead() && IsHeroWithinExplosionRadius(center, hero, _explosionRadius))
            {
                // Bunker safe zone blocks infantry fire to hero HP; bomb splash ignores that (and has no effect once bunker HP is 0).
                hero.ReceiveDamage(_damage, ignoreBunkerSafeZone: true);
            }

            if (_explosionEffectPrefab != null)
            {
                GameObject fx = Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);
                Destroy(fx, Mathf.Max(0.05f, _explosionEffectLifetime));
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Uses child colliders ClosestPoint so tall sprites / offset roots still catch AoE (transform-only distance often misses).
        /// </summary>
        private static bool IsHeroWithinExplosionRadius(Vector2 center, Hero_V2 hero, float radius)
        {
            if (radius <= 0f || hero == null)
            {
                return false;
            }

            float r2 = radius * radius;
            Collider2D[] cols = hero.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                Collider2D col = cols[i];
                if (col == null || !col.enabled)
                {
                    continue;
                }

                Vector2 closest = col.ClosestPoint(center);
                if (((Vector2)closest - center).sqrMagnitude <= r2)
                {
                    return true;
                }
            }

            Vector2 root = hero.transform.position;
            return (root - center).sqrMagnitude <= r2;
        }
    }
}
