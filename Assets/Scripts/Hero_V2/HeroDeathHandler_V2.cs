using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * HeroDeathHandler_V2 (Death Orchestration Layer)
 *
 * ARCHITECTURAL ROLE:
 * HeroDeathHandler_V2 is responsible for handling the consequences
 * of the Hero's death event in a controlled and centralized way.
 *
 * ---------------------------------------------------------
 * SYSTEM FLOW:
 *
 * HeroModel_V2 determines death condition (e.g. health <= 0)
 *        ↓
 * HeroDamageReceiver_V2 triggers death event
 *        ↓
 * HeroDeathHandler_V2 handles death consequences
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Handles gameplay shutdown when the Hero dies
 * - Disables or stops relevant systems (movement, combat, input, etc.)
 * - Emits signals/events for other systems (View, GameManager, etc.)
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Play animations directly
 * - Control UI (e.g. Game Over screens)
 * - Handle respawn logic
 * - Process input
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE:
 *
 * This class is NOT responsible for visuals or UI.
 * It only orchestrates the transition from "alive gameplay state"
 * to "dead gameplay state" and notifies other systems.
 *
 * ---------------------------------------------------------
 * FLOW SUMMARY:
 *
 * Damage → Model (health <= 0)
 *        ↓
 * DamageReceiver.OnDeath
 *        ↓
 * DeathHandler.HandleDeath()
 *        ↓
 * Disable systems + emit death event
 *        ↓
 * View / GameManager reacts (animation, UI, restart, etc.)
 */
    public class HeroDeathHandler_V2 
    {
        private readonly HeroModel_V2 _model;
        private readonly HeroStateMachine_V2 _stateMachine;
        private readonly HeroMovementSystem_V2 _movementSystem;
        private readonly HeroWeaponSystem_V2 _weaponSystem;

        private bool hasHandledDeath;

        public event Action OnDeathHandled;

        public HeroDeathHandler_V2(
            HeroModel_V2 model,
            HeroStateMachine_V2 stateMachine,
            HeroMovementSystem_V2 movementSystem,
            HeroWeaponSystem_V2 weaponSystem)
        {
            _model = model;
            _stateMachine = stateMachine;
            _movementSystem = movementSystem;
            _weaponSystem = weaponSystem;
        }

        internal void Init(HeroModel_V2 model, HeroStateMachine_V2 stateMachine, HeroView_V2 view)
        {
            //FIXME: This is a bit of a code smell, but we need to subscribe to the View's death event to trigger our HandleDeath method.
        }

        // -------------------------
        // ENTRY POINT
        // -------------------------
        public void HandleDeath()
        {
            if (hasHandledDeath) return;
            hasHandledDeath = true;

            // 1. Sätt state (source of truth)
            _stateMachine.ChangeState(HeroState.Dead);

            // 2. Stäng av gameplay systems
            _movementSystem.Disable();
            _weaponSystem.Disable();

            // 3. Stoppa eventuell velocity
            _model.velocity = UnityEngine.Vector2.zero;

            // 4. Signalera till resten av spelet
            OnDeathHandled?.Invoke();
        }

        public void ResetAfterRevive()
        {
            hasHandledDeath = false;
            _movementSystem.Enable();
            _weaponSystem.Enable();
            _stateMachine.ChangeState(HeroState.Idle);
        }
    }
}
