using UnityEngine;

namespace iStick2War_V2
{
    public sealed class MechRobotBossModel_V2 : MonoBehaviour
    {
        [SerializeField] private float _health = 420f;

        /// <summary>Spawn/max HP for UI ratio; kept in sync with <see cref="_health"/> on reset.</summary>
        [SerializeField] private float _maxHealth = 420f;

        public MechRobotBossBodyState currentState = MechRobotBossBodyState.Idle;

        public float health
        {
            get => _health;
            set => _health = value;
        }

        public float maxHealth
        {
            get => _maxHealth;
            set => _maxHealth = value;
        }

        private void Awake()
        {
            if (_maxHealth < _health)
            {
                _maxHealth = _health;
            }
        }

        public void ResetForSpawn()
        {
            _health = _maxHealth;
            currentState = MechRobotBossBodyState.Idle;
        }

        public void ApplyWaveHealthMultiplier(float multiplier)
        {
            if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
            {
                return;
            }

            _health *= multiplier;
            _maxHealth *= multiplier;
        }

        public float ApplyDamage(float damage)
        {
            _health -= damage;
            if (_health < 0f)
            {
                _health = 0f;
            }

            return _health;
        }

        public bool IsDead()
        {
            return _health <= 0f;
        }
    }
}
