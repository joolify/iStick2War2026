using UnityEngine;

namespace iStick2War_V2
{
    /*
 * MechRobotBossModel_V2 (Pure Data Layer)
 *
 * PURPOSE:
 * Authoritative runtime numbers for the mech boss: health / max health, mirrored MechRobotBossBodyState,
 * and simple mutators (ApplyDamage, wave multipliers, reset for spawn). No Update loops — mutation only.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Run per-frame gameplay orchestration (MechRobotBossController_V2)
 * - Resolve hit-scan or projectile hits (MechRobotBossDamageReceiver_V2 / weapon systems)
 * - Drive Spine or animation (MechRobotBossView_V2)
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Single source of truth for boss HP and high-level body state snapshot used by controller, view, and receivers.
 */
    public sealed class MechRobotBossModel_V2 : MonoBehaviour
    {
        [SerializeField] private float _health = 1680f;

        /// <summary>Spawn/max HP for UI ratio; kept in sync with <see cref="_health"/> on reset.</summary>
        [SerializeField] private float _maxHealth = 1680f;

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
