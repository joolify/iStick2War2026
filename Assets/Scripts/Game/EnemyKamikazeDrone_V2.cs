using UnityEngine;

namespace iStick2War_V2
{
    [DisallowMultipleComponent]
    public sealed class EnemyKamikazeDrone_V2 : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] private float _horizontalCruiseSpeed = 7f;
        [SerializeField] private float _diveAttackSpeed = 12f;
        [Tooltip("Begin dive when drone is this close to bunker in X (world units).")]
        [SerializeField] private float _startDiveWhenWithinBunkerX = 1.2f;
        [Tooltip("Extra height above bunker collider used as fixed dive entry point.")]
        [SerializeField] private float _diveEntryHeightAboveBunker = 0.4f;
        [SerializeField] private float _turnLerpSpeed = 5f;
        [Tooltip("Rotate sprite nose towards flight direction. Disable if sprite should stay upright.")]
        [SerializeField] private bool _rotateToFlightDirection;
        [SerializeField] private float _maxLifetimeSeconds = 25f;
        [SerializeField] private float _hitDetectionPadding = 0.05f;

        [Header("Explosion")]
        [SerializeField] private int _explosionDamage = 35;
        [SerializeField] private float _heroExplosionRadius = 1.8f;
        [SerializeField] private GameObject _explosionEffectPrefab;
        [SerializeField] private float _explosionEffectLifetime = 1.2f;

        private WaveManager_V2 _waveManager;
        private Hero_V2 _hero;
        private BunkerHitbox_V2 _bunkerHitbox;
        private Collider2D _bunkerCollider;
        private readonly Collider2D[] _bunkerColliders = new Collider2D[16];
        private int _bunkerColliderCount;
        private float _expireAt;
        private bool _started;
        private bool _detonated;
        private AttackPhase _phase;
        private float _cruiseWorldY;
        private Vector2 _diveEntryPoint;
        private readonly Collider2D[] _selfColliders = new Collider2D[8];

        private enum AttackPhase
        {
            HorizontalApproach,
            DiveToEntryPoint,
            VerticalPlunge
        }

        public void BeginRun()
        {
            _waveManager = FindAnyObjectByType<WaveManager_V2>();
            _hero = FindAnyObjectByType<Hero_V2>();
            _bunkerHitbox = FindAnyObjectByType<BunkerHitbox_V2>(FindObjectsInactive.Include);
            _bunkerCollider = _bunkerHitbox != null ? _bunkerHitbox.GetComponent<Collider2D>() : null;
            CacheBunkerColliders();
            _expireAt = Time.time + Mathf.Max(2f, _maxLifetimeSeconds);
            _detonated = false;
            _phase = AttackPhase.HorizontalApproach;
            _cruiseWorldY = transform.position.y;
            _diveEntryPoint = Vector2.zero;
            CacheSelfColliders();
            IgnoreHeroCollisions();
            _started = true;
        }

        private void Start()
        {
            if (!_started)
            {
                BeginRun();
            }
        }

        private void Update()
        {
            if (!_started || _detonated)
            {
                return;
            }

            if (Time.time >= _expireAt)
            {
                DespawnSelf();
                return;
            }

            if (_bunkerHitbox == null)
            {
                _bunkerHitbox = FindAnyObjectByType<BunkerHitbox_V2>(FindObjectsInactive.Include);
                if (_bunkerHitbox == null)
                {
                    return;
                }

                _bunkerCollider = _bunkerHitbox.GetComponent<Collider2D>();
                CacheBunkerColliders();
            }

            if (HasReachedBunkerCollision())
            {
                Explode();
                return;
            }

            Vector2 from = transform.position;
            Vector2 bunkerPos = _bunkerCollider != null
                ? (Vector2)_bunkerCollider.bounds.center
                : (Vector2)_bunkerHitbox.transform.position;

            float bunkerDx = Mathf.Abs(bunkerPos.x - from.x);
            if (_phase == AttackPhase.HorizontalApproach &&
                bunkerDx <= Mathf.Max(0.15f, _startDiveWhenWithinBunkerX))
            {
                if (_bunkerCollider != null)
                {
                    Bounds b = _bunkerCollider.bounds;
                    float x = Mathf.Clamp(from.x, b.min.x, b.max.x);
                    float y = b.max.y + Mathf.Max(0f, _diveEntryHeightAboveBunker);
                    _diveEntryPoint = new Vector2(x, y);
                }
                else
                {
                    _diveEntryPoint = bunkerPos + Vector2.up * Mathf.Max(0f, _diveEntryHeightAboveBunker);
                }

                _phase = AttackPhase.DiveToEntryPoint;
            }

            Vector2 stepDir;
            float speed;
            if (_phase == AttackPhase.HorizontalApproach)
            {
                // Stage 1: approach bunker horizontally at a stable cruise altitude.
                float dirX = Mathf.Sign(bunkerPos.x - from.x);
                if (Mathf.Abs(dirX) < 0.001f)
                {
                    dirX = 1f;
                }

                stepDir = new Vector2(dirX, 0f);
                speed = Mathf.Max(0.1f, _horizontalCruiseSpeed);
                transform.position = new Vector3(
                    from.x + stepDir.x * speed * Time.deltaTime,
                    _cruiseWorldY,
                    transform.position.z);
            }
            else if (_phase == AttackPhase.DiveToEntryPoint)
            {
                // Stage 2: move to a stable, fixed point above bunker before plunging.
                Vector2 diveDir = _diveEntryPoint - from;
                float diveDist = diveDir.magnitude;
                if (diveDist <= 0.05f)
                {
                    _phase = AttackPhase.VerticalPlunge;
                    stepDir = Vector2.down;
                }
                else
                {
                    stepDir = diveDir / diveDist;
                }
                speed = Mathf.Max(0.1f, _diveAttackSpeed);
                transform.position = from + stepDir * (speed * Time.deltaTime);
            }
            else
            {
                // Stage 3: vertical attack pass toward bunker collider.
                stepDir = Vector2.down;
                speed = Mathf.Max(0.1f, _diveAttackSpeed);
                transform.position = from + stepDir * (speed * Time.deltaTime);
            }

            if (HasReachedBunkerCollision())
            {
                Explode();
                return;
            }

            if (_rotateToFlightDirection)
            {
                float targetAngle = Mathf.Atan2(stepDir.y, stepDir.x) * Mathf.Rad2Deg;
                Quaternion targetRot = Quaternion.AngleAxis(targetAngle, Vector3.forward);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    Mathf.Clamp01(Mathf.Max(0.1f, _turnLerpSpeed) * Time.deltaTime));
            }

            Physics2D.SyncTransforms();
        }

        private void Explode()
        {
            if (_detonated)
            {
                return;
            }

            _detonated = true;
            Vector2 center = transform.position;
            int damage = Mathf.Max(1, _explosionDamage);
            int bunkerHpBefore = _waveManager != null ? _waveManager.BunkerHealth : 0;
            int absorbedByBunker = Mathf.Min(damage, Mathf.Max(0, bunkerHpBefore));
            if (absorbedByBunker > 0 && _waveManager != null)
            {
                _waveManager.ApplyBunkerDamage(absorbedByBunker);
            }

            int heroDamage = damage - absorbedByBunker;
            if (heroDamage > 0 && _hero != null && !_hero.IsDead())
            {
                float radius = Mathf.Max(0f, _heroExplosionRadius);
                if (radius > 0f && ((Vector2)_hero.transform.position - center).sqrMagnitude <= radius * radius)
                {
                    Vector2 toHero = (Vector2)_hero.transform.position - center;
                    Vector2 shotDir = toHero.sqrMagnitude > 0.0001f ? toHero.normalized : Vector2.left;
                    _hero.ReceiveDamage(heroDamage, ignoreBunkerSafeZone: true, incomingShotWorldDirection: shotDir);
                }
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

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || _detonated)
            {
                return;
            }

            if (IsBunkerCollider(other))
            {
                Explode();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_detonated || collision == null)
            {
                return;
            }

            Collider2D other = collision.collider;
            if (IsBunkerCollider(other))
            {
                Explode();
            }
        }

        private bool IsBunkerCollider(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            if (_bunkerCollider != null && other == _bunkerCollider)
            {
                return true;
            }

            return other.GetComponentInParent<BunkerHitbox_V2>() != null;
        }

        private void CacheBunkerColliders()
        {
            _bunkerColliderCount = 0;
            for (int i = 0; i < _bunkerColliders.Length; i++)
            {
                _bunkerColliders[i] = null;
            }

            if (_bunkerHitbox == null)
            {
                return;
            }

            Collider2D[] cols = _bunkerHitbox.GetComponentsInChildren<Collider2D>(true);
            if ((cols == null || cols.Length == 0) && _bunkerHitbox.transform.parent != null)
            {
                cols = _bunkerHitbox.transform.parent.GetComponentsInChildren<Collider2D>(true);
            }

            if (cols == null || cols.Length == 0)
            {
                return;
            }

            int n = Mathf.Min(_bunkerColliders.Length, cols.Length);
            for (int i = 0; i < n; i++)
            {
                _bunkerColliders[i] = cols[i];
            }

            _bunkerColliderCount = n;
            if (_bunkerCollider == null)
            {
                _bunkerCollider = _bunkerColliders[0];
            }
        }

        private void CacheSelfColliders()
        {
            Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
            int n = Mathf.Min(_selfColliders.Length, cols != null ? cols.Length : 0);
            for (int i = 0; i < _selfColliders.Length; i++)
            {
                _selfColliders[i] = i < n ? cols[i] : null;
            }
        }

        private void IgnoreHeroCollisions()
        {
            if (_hero == null)
            {
                return;
            }

            Collider2D[] heroCols = _hero.GetComponentsInChildren<Collider2D>(true);
            if (heroCols == null || heroCols.Length == 0)
            {
                return;
            }

            for (int i = 0; i < _selfColliders.Length; i++)
            {
                Collider2D selfCol = _selfColliders[i];
                if (selfCol == null)
                {
                    continue;
                }

                for (int h = 0; h < heroCols.Length; h++)
                {
                    Collider2D heroCol = heroCols[h];
                    if (heroCol == null)
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(selfCol, heroCol, true);
                }
            }
        }

        private bool HasReachedBunkerCollision()
        {
            if (_bunkerColliderCount <= 0)
            {
                CacheBunkerColliders();
                if (_bunkerColliderCount <= 0)
                {
                    return _bunkerCollider != null && _bunkerCollider.OverlapPoint(transform.position);
                }
            }

            float pad = Mathf.Max(0f, _hitDetectionPadding);
            for (int i = 0; i < _selfColliders.Length; i++)
            {
                Collider2D selfCol = _selfColliders[i];
                if (selfCol == null || !selfCol.enabled || !selfCol.gameObject.activeInHierarchy)
                {
                    continue;
                }

                for (int b = 0; b < _bunkerColliderCount; b++)
                {
                    Collider2D bunkerCol = _bunkerColliders[b];
                    if (bunkerCol == null || !bunkerCol.enabled || !bunkerCol.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    ColliderDistance2D dist = selfCol.Distance(bunkerCol);
                    if (dist.isOverlapped || dist.distance <= pad)
                    {
                        return true;
                    }
                }
            }

            // Fallback when no self collider is available: keep previous root-point check.
            if (_bunkerCollider != null && _bunkerCollider.OverlapPoint(transform.position))
            {
                return true;
            }

            for (int b = 0; b < _bunkerColliderCount; b++)
            {
                Collider2D bunkerCol = _bunkerColliders[b];
                if (bunkerCol != null && bunkerCol.enabled && bunkerCol.gameObject.activeInHierarchy &&
                    bunkerCol.OverlapPoint(transform.position))
                {
                    return true;
                }
            }

            return false;
        }

        private void DespawnSelf()
        {
            _started = false;
            _detonated = false;
            _expireAt = 0f;
            _phase = AttackPhase.HorizontalApproach;
            _diveEntryPoint = Vector2.zero;
            SimplePrefabPool_V2.Despawn(gameObject);
        }
    }
}
