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
 * Physics-driven grenade arc with timed fuse; on detonation applies area damage and spawns optional VFX.
 * Spawned by ParatrooperWeaponSystem_V2 when the AI / Spine window commits a throw.
 *
 * ❌ MUST NOT: decide throw timing (Controller + weapon system + Spine events own that).
 */
[RequireComponent(typeof(Collider2D))]
public sealed class PotatomasherProjectile_V2 : MonoBehaviour
{
        // Same VFX prefab as hero bazooka rockets (HeroRocketProjectile_V2 / bazookaRocket.prefab).
        private const string BazookaExplosionPrefabAssetPath =
            "Assets/Prefabs/Explosions/Enemies/Paratrooper_Exp.prefab";

        private static GameObject s_cachedBazookaStyleExplosionPrefab;

        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private float _gravityScale = 1.75f;
        [SerializeField] private GameObject _explosionEffectPrefab;
        [SerializeField] private float _explosionEffectLifetime = 1.5f;
        [Tooltip("When false, grenade ignores collisions with paratrooper colliders/body parts.")]
        [SerializeField] private bool _canExplodeOnParatrooperCollision = false;

        private float _fuseSeconds = 2.25f;
        private int _damage = 24;
        private float _radius = 1.6f;
        private bool _hasExploded;

        public void Initialize(Vector2 initialVelocity, float fuseSeconds, int damage, float explosionRadius)
        {
            _fuseSeconds = Mathf.Max(0.1f, fuseSeconds);
            _damage = Mathf.Max(1, damage);
            _radius = Mathf.Max(0.1f, explosionRadius);

            if (_rigidbody2D == null)
            {
                _rigidbody2D = GetComponent<Rigidbody2D>();
            }

            if (_rigidbody2D != null)
            {
                _rigidbody2D.gravityScale = _gravityScale;
                _rigidbody2D.linearVelocity = initialVelocity;
            }

            CancelInvoke(nameof(ExplodeFromFuse));
            Invoke(nameof(ExplodeFromFuse), _fuseSeconds);
        }

        private void Awake()
        {
            if (_rigidbody2D == null)
            {
                _rigidbody2D = GetComponent<Rigidbody2D>();
            }

            EnsureExplosionEffectPrefab();
        }

        private void Start()
        {
            if (!IsInvoking(nameof(ExplodeFromFuse)))
            {
                Invoke(nameof(ExplodeFromFuse), _fuseSeconds);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || _hasExploded)
            {
                return;
            }

            Collider2D other = collision.collider;
            if (!_canExplodeOnParatrooperCollision && IsParatrooperCollider(other))
            {
                return;
            }

            Explode();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasExploded || other == null)
            {
                return;
            }

            if (!_canExplodeOnParatrooperCollision && IsParatrooperCollider(other))
            {
                return;
            }

            Explode();
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

        private static bool IsParatrooperCollider(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            if (other.GetComponentInParent<Paratrooper>() != null)
            {
                return true;
            }

            if (other.GetComponent<ParatrooperBodyPart_V2>() != null)
            {
                return true;
            }

            return false;
        }
    }
}
