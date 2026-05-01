using UnityEngine;

namespace iStick2War_V2
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class BombProjectile_V2 : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private float _gravityScale = 2.4f;
        [SerializeField] private float _lifetimeSeconds = 9f;
        [Tooltip("Ignores collision/trigger impacts for a short time right after spawn to avoid instant self-collisions.")]
        [SerializeField] private float _armingDelaySeconds = 0.08f;
        [SerializeField] private int _damage = 30;
        [SerializeField] private float _explosionRadius = 1.9f;
        [SerializeField] private GameObject _explosionEffectPrefab;
        [SerializeField] private float _explosionEffectLifetime = 1.4f;
        [Header("Impact filtering")]
        [Tooltip("If empty, defaults to Ground + Bunker + Player.")]
        [SerializeField] private LayerMask _triggerImpactLayers;
        [SerializeField] private bool _debugImpactLogs;

        private bool _exploded;
        private float _expireAt;
        private float _armedAt;

        public void Initialize(Vector2 inheritedVelocity, int damage, float explosionRadius)
        {
            _damage = Mathf.Max(1, damage);
            _explosionRadius = Mathf.Max(0.2f, explosionRadius);
            _exploded = false;
            _expireAt = Time.time + Mathf.Max(0.5f, _lifetimeSeconds);
            _armedAt = Time.time + Mathf.Max(0f, _armingDelaySeconds);

            if (_rigidbody2D == null)
            {
                _rigidbody2D = GetComponent<Rigidbody2D>();
            }

            if (_rigidbody2D != null)
            {
                _rigidbody2D.gravityScale = _gravityScale;
                _rigidbody2D.linearVelocity = inheritedVelocity;
            }
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

            if (Time.time < _armedAt)
            {
                return;
            }

            Explode();
        }

        private void Update()
        {
            if (_exploded)
            {
                return;
            }

            if (Time.time < _armedAt)
            {
                return;
            }

            if (_expireAt > 0f && Time.time >= _expireAt)
            {
                DespawnSelf();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.GetComponentInParent<AircraftHealth_V2>() != null)
            {
                return;
            }

            if (Time.time < _armedAt)
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
            int bunkerHpBefore = waveManager != null ? waveManager.BunkerHealth : 0;
            int absorbedByBunker = Mathf.Min(_damage, Mathf.Max(0, bunkerHpBefore));
            if (absorbedByBunker > 0 && waveManager != null)
            {
                waveManager.ApplyBunkerDamage(absorbedByBunker);
            }

            int heroDamage = _damage - absorbedByBunker;
            Hero_V2 hero = FindAnyObjectByType<Hero_V2>();
            if (heroDamage > 0 &&
                hero != null &&
                !hero.IsDead() &&
                IsHeroWithinExplosionRadius(center, hero, _explosionRadius))
            {
                // Remaining splash only after bunker cover is breached (same hit can overflow if bunker HP was low).
                Vector2 toHero = (Vector2)hero.transform.position - center;
                Vector2 shotDir = toHero.sqrMagnitude > 0.0001f ? toHero.normalized : Vector2.left;
                hero.ReceiveDamage(heroDamage, ignoreBunkerSafeZone: true, incomingShotWorldDirection: shotDir);
            }

            if (_explosionEffectPrefab != null)
            {
                GameObject fx = SimplePrefabPool_V2.Spawn(_explosionEffectPrefab, transform.position, Quaternion.identity);
                if (fx != null)
                {
                    PooledAutoDespawn_V2 timer = fx.GetComponent<PooledAutoDespawn_V2>();
                    if (timer == null)
                    {
                        timer = fx.AddComponent<PooledAutoDespawn_V2>();
                    }

                    timer.Arm(Mathf.Max(0.05f, _explosionEffectLifetime));
                }
            }

            DespawnSelf();
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

        private void DespawnSelf()
        {
            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.angularVelocity = 0f;
            }

            _exploded = false;
            _expireAt = 0f;
            _armedAt = 0f;
            SimplePrefabPool_V2.Despawn(gameObject);
        }
    }
}
