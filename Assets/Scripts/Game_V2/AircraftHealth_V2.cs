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
 * - Own flight paths or AI (controllers / KamikazeDroneDriver_V2 style drivers).
 * - Apply damage to bunkers or heroes directly (weapon systems route through WaveManager / DamageInfo paths).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2 + aircraft)
 *
 * Pool despawn / explosions → SimplePrefabPool_V2.cs, PooledAutoDespawn_V2.cs
 * Composition roots using this → Enemies/Helicopter_V2, BombPlane_V2, BombDrone_V2, KamikazeDrone_V2, …
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

        // suppressDeathExplosionVfx: bazooka detonation spawns typed blast VFX at the rocket; skip duplicate at aircraft root.
        public void ApplyDamage(float damage, bool suppressDeathExplosionVfx = false)
        {
            if (damage <= 0f || _currentHealth <= 0f || _isDead)
            {
                return;
            }

            _currentHealth -= damage;
            if (_currentHealth <= 0f)
            {
                Die(suppressDeathExplosionVfx);
            }
        }

        private void Die(bool suppressDeathExplosionVfx)
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;
            OnDestroyed?.Invoke(this);
            if (!suppressDeathExplosionVfx)
            {
                // Hit-scan (Colt, Thompson, …): same typed/fallback VFX as bazooka aircraft kills.
                if (!AircraftExplosionVfx_V2.TrySpawnForAircraftDeath(this) &&
                    _airExplosionEffectPrefab != null)
                {
                    Vector3 explosionPoint = AircraftExplosionVfx_V2.ResolveDeathWorldPoint(this);
                    SpawnLegacyAirExplosion(_airExplosionEffectPrefab, explosionPoint, _airExplosionEffectLifetime);
                }
            }

            if (_destroyRootWhenDead)
            {
                SimplePrefabPool_V2.Despawn(gameObject);
            }
        }

        private static void SpawnLegacyAirExplosion(GameObject prefab, Vector3 worldPosition, float lifetimeSeconds)
        {
            GameObject fx = SimplePrefabPool_V2.Spawn(prefab, worldPosition, Quaternion.identity);
            if (fx == null)
            {
                return;
            }

            AudioManager_V2.PlayMissileExplosion();

            PooledAutoDespawn_V2 timer = fx.GetComponent<PooledAutoDespawn_V2>();
            if (timer == null)
            {
                timer = fx.AddComponent<PooledAutoDespawn_V2>();
            }

            timer.Arm(Mathf.Max(0.05f, lifetimeSeconds));
        }
    }
}
