using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    /*
 * HeroDamageReceiver_V2 (Gatekeeper Component)
 *
 * DESIGN INTENT:
 * The HeroDamageReceiver_V2 acts as the central entry point for all incoming damage.
 * It validates, processes, and forwards damage events into the system.
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Receives all incoming hit data (bullets, enemies, explosions)
 * - Validates and filters damage
 * - Applies damage to the Model
 * - Emits events for downstream systems (View, VFX, Audio, etc.)
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Play animations
 * - Decide gameplay state transitions (except death via model state)
 * - Contain weapon logic or combat behaviour
 *
 * ---------------------------------------------------------
 * FLOW:
 *
 * Hit (bullet / enemy / explosion)
 *        ↓
 * HeroDamageReceiver_V2
 *        ↓
 * Model.TakeDamage()
 *        ↓
 * Event dispatched
 *        ↓
 * View / VFX / Audio react
 *
 * ---------------------------------------------------------
 * ARCHITECTURAL ROLE:
 *
 * This component acts as a "gatekeeper" between raw damage input
 * and the internal game state.
 */
    public class HeroDamageReceiver_V2 
    {
        private readonly HeroModel_V2 _model;

        // Events (extremt viktigt för clean architecture)
        public event Action<int> OnDamageTaken;
        public event Action OnDeath;

        public HeroDamageReceiver_V2(HeroModel_V2 model)
        {
            _model = model;
        }

        internal void Init(HeroModel_V2 model, HeroStateMachine_V2 stateMachine, HeroDeathHandler_V2 deathHandler)
        {
            //FIXME
        }

        // -------------------------
        // PUBLIC API
        // -------------------------
        public void ApplyDamage(int damage)
        {
            if (!CanTakeDamage()) return;

            int previousHealth = _model.currentHealth;

            _model.TakeDamage(damage);

            int actualDamage = previousHealth - _model.currentHealth;
            Debug.Log($"[HeroDamageReceiver_V2] Damage applied. in={damage}, actual={actualDamage}, hp={previousHealth}->{_model.currentHealth}, dead={_model.isDead}");

            if (actualDamage > 0)
            {
                OnDamageTaken?.Invoke(actualDamage);
            }

            if (_model.isDead)
            {
                Debug.Log("[HeroDamageReceiver_V2] OnDeath emitted.");
                OnDeath?.Invoke();
            }
        }

        // -------------------------
        // VALIDATION
        // -------------------------
        private bool CanTakeDamage()
        {
            if (_model.isDead) return false;

            // future:
            // if (model.isInvincible) return false;
            // if (model.isDodging) return false;

            return true;
        }
    }
}
