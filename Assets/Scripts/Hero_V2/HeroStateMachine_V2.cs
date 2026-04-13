using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    /*
 * HeroStateMachine_V2 (State Transition Rules Engine)
 *
 * PURPOSE:
 * HeroStateMachine_V2 is a pure rules-based state machine responsible
 * for managing and validating the current gameplay state of the Hero.
 *
 * ---------------------------------------------------------
 * STATES:
 *
 * - Idle
 * - Run
 * - Jump
 * - Fall
 * - Shoot
 * - Reload
 * - Dead
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * The StateMachine is responsible for defining WHAT state is active,
 * not HOW that state is executed.
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Maintain current state
 * - Validate state transitions (simple or expandable rules)
 * - Notify other systems when state changes occur
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Decide input
 * - Play animations
 * - Implement movement logic
 * - Implement weapon logic
 *
 * ---------------------------------------------------------
 * ARCHITECTURE SEPARATION:
 *
 * System         Responsibility
 * -----------------------------------------
 * Input          What the player is doing
 * Controller     What should happen
 * StateMachine   What state is currently valid
 * Systems        How it is executed
 *
 * ---------------------------------------------------------
 * DESIGN GOAL:
 *
 * Keep state logic deterministic, simple, and decoupled from all
 * gameplay execution systems.
 */
    public class HeroStateMachine_V2 : MonoBehaviour
    {
        public HeroState CurrentState { get; private set; } = HeroState.Idle;

        public event Action<HeroState, HeroState> OnStateChanged;

        // -------------------------
        // MAIN ENTRY
        // -------------------------
        public void SetState(HeroState newState)
        {
            if (newState == CurrentState)
                return;

            if (!CanTransitionTo(newState))
                return;

            HeroState previousState = CurrentState;
            CurrentState = newState;

            OnStateChanged?.Invoke(previousState, CurrentState);
        }

        // -------------------------
        // TRANSITION RULES
        // -------------------------
        private bool CanTransitionTo(HeroState newState)
        {
            // Hard lock rules (highest priority)

            if (CurrentState == HeroState.Dead)
                return false; // dead is final state

            if (newState == HeroState.Dead)
                return true;

            // Example rules (kan byggas ut senare)

            switch (CurrentState)
            {
                case HeroState.Reloading:
                    if (newState == HeroState.Shooting)
                        return false;
                    break;

                case HeroState.Shooting:
                    if (newState == HeroState.Reloading)
                        return true;
                    break;
            }

            return true;
        }

        // -------------------------
        // FORCE STATE (for death / overrides)
        // -------------------------
        public void ForceState(HeroState newState)
        {
            HeroState previousState = CurrentState;

            CurrentState = newState;

            OnStateChanged?.Invoke(previousState, CurrentState);
        }
    }
}
