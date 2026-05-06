using UnityEngine;
using System;

namespace iStick2War_V2
{
    /// <summary>
    /// Simple HP for aircraft hit by the hero hit-scan and rocket explosion.
    /// Use the same layer as paratrooper hitboxes (<c>EnemyBodyPart</c>) so the hero ray mask hits this collider.
    /// </summary>
    public sealed class AircraftHealth_V2 : MonoBehaviour
    {
        [SerializeField] private float _maxHealth = 120f;
        [SerializeField] private bool _destroyRootWhenDead = true;
        [SerializeField] private GameObject _airExplosionEffectPrefab;
        [SerializeField] private float _airExplosionEffectLifetime = 1.4f;

        private float _currentHealth;
        private bool _isDead;

        public event Action<AircraftHealth_V2> OnDestroyed;

        /// <summary>Current HP (telemetry / weapon-vs-enemy test range).</summary>
        public float CurrentHealth => _currentHealth;

        /// <summary>Configured max HP at spawn.</summary>
        public float MaxHealthConfigured => _maxHealth;

        /// <summary>True after fatal damage or while despawning.</summary>
        public bool IsDefeated => _isDead || _currentHealth <= 0f;

        private void Awake()
        {
            _currentHealth = Mathf.Max(1f, _maxHealth);
            EnsureKinematicAircraftCollidesWithHeroProjectiles();
        }

        /// <summary>
        /// Kinematic vs kinematic contacts are off by default; hero bazooka uses a kinematic or dynamic RB.
        /// Enable full kinematic contacts so Fa_223-style helicopters always register hits without relying on spawn order.
        /// </summary>
        private void EnsureKinematicAircraftCollidesWithHeroProjectiles()
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb == null || !rb.simulated || rb.bodyType != RigidbodyType2D.Kinematic)
            {
                return;
            }

            rb.useFullKinematicContacts = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void OnEnable()
        {
            _isDead = false;
            _currentHealth = Mathf.Max(1f, _maxHealth);
        }

        /// <summary>Apply damage from hero weapons (per-weapon values come from <see cref="HeroWeaponDefinition_V2"/>).</summary>
        public void ApplyDamage(float damage)
        {
            if (damage <= 0f || _currentHealth <= 0f || _isDead)
            {
                return;
            }

            _currentHealth -= damage;
            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;
            OnDestroyed?.Invoke(this);
            if (_airExplosionEffectPrefab != null)
            {
                GameObject fx = SimplePrefabPool_V2.Spawn(_airExplosionEffectPrefab, transform.position, Quaternion.identity);
                if (fx != null)
                {
                    PooledAutoDespawn_V2 timer = fx.GetComponent<PooledAutoDespawn_V2>();
                    if (timer == null)
                    {
                        timer = fx.AddComponent<PooledAutoDespawn_V2>();
                    }

                    timer.Arm(Mathf.Max(0.05f, _airExplosionEffectLifetime));
                }
            }

            if (_destroyRootWhenDead)
            {
                SimplePrefabPool_V2.Despawn(gameObject);
            }
        }
    }
}
