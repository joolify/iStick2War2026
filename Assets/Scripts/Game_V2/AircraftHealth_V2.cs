using UnityEngine;
using System;

namespace iStick2War_V2
{
    /*
 * AircraftHealth_V2 (Aircraft HP + defeat gate)
 *
 * PURPOSE:
 * Tracks current/max HP for helicopters, bombers, drones, and other flying props; raises OnDestroyed when HP hits zero
 * or when despawn is triggered. Hero hit-scan and rockets consume HP through the same contract as infantry hitboxes.
 *
 * ---------------------------------------------------------
 * SETUP
 *
 * - Prefer EnemyBodyPart layer on colliders so hero LayerMasks match ParatrooperBodyPart_V2 expectations.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Own flight paths or AI (controllers / EnemyKamikazeDrone_V2 style drivers).
 * - Apply damage to bunkers or heroes directly (weapon systems route through WaveManager / DamageInfo paths).
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Small reusable health component shared by multiple aircraft prefabs; optional explosion VFX then destroy root.
 */
    public sealed class AircraftHealth_V2 : MonoBehaviour
    {
        [SerializeField] private float _maxHealth = 120f;
        [SerializeField] private bool _destroyRootWhenDead = true;
        [SerializeField] private GameObject _airExplosionEffectPrefab;
        [SerializeField] private float _airExplosionEffectLifetime = 1.4f;

        private float _currentHealth;
        private bool _isDead;

        public event Action<AircraftHealth_V2> OnDestroyed;

        // Current HP (telemetry / weapon-vs-enemy test range).
        public float CurrentHealth => _currentHealth;

        // Configured max HP at spawn.
        public float MaxHealthConfigured => _maxHealth;

        // True after fatal damage or while despawning.
        public bool IsDefeated => _isDead || _currentHealth <= 0f;

        private void Awake()
        {
            _currentHealth = Mathf.Max(1f, _maxHealth);
            EnsureKinematicAircraftCollidesWithHeroProjectiles();
        }

        // Kinematic vs kinematic contacts are off by default; hero bazooka uses a kinematic or dynamic RB.
        // Enable full kinematic contacts so Fa_223-style helicopters always register hits without relying on spawn order.
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

        // Apply damage from hero weapons (per-weapon values come from ).
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
