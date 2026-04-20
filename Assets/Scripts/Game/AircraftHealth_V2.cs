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

        private void Awake()
        {
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
                GameObject fx = Instantiate(_airExplosionEffectPrefab, transform.position, Quaternion.identity);
                Destroy(fx, Mathf.Max(0.05f, _airExplosionEffectLifetime));
            }

            if (_destroyRootWhenDead)
            {
                Destroy(gameObject);
            }
        }
    }
}
