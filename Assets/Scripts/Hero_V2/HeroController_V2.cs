using Assets.Scripts.Components;
using iStick2War;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    /*
 * HeroController_V2 (Critical Component)
 *
 * This acts as the “brain router” of the Hero system.
 *
 * Responsibilities:
 * - Translates input into gameplay actions
 * - Validates actions against current state
 * - Triggers weapon actions
 * - Controls movement permissions
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLES (before implementation)
 *
 * The controller MUST:
 * - Read input from HeroInput_V2
 * - Query the state machine before executing actions
 * - Forward validated actions to:
 *     - MovementSystem
 *     - WeaponSystem
 * - Update the Model (not the View directly)
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 * - No physics handling
 * - No animation logic
 * - No feature-specific gameplay implementation
 *
 * The controller should remain a pure decision-making layer,
 * not a simulation or rendering system.
 */
    internal class HeroController_V2 : MonoBehaviour
    {
        private readonly HeroModel_V2 _model;
        private readonly HeroView_V2 _view;

        private readonly HeroInput_V2 _input;
        private readonly HeroStateMachine_V2 _stateMachine;
        private readonly HeroMovementSystem_V2 _movementSystem;
        private readonly HeroWeaponSystem_V2 _weaponSystem;

        public HeroController_V2(
            HeroModel_V2 model,
            HeroView_V2 view,
            HeroInput_V2 input,
            HeroStateMachine_V2 stateMachine,
            HeroMovementSystem_V2 movementSystem,
            HeroWeaponSystem_V2 weaponSystem)
        {
            _model = model;
            _view = view;
            _input = input;
            _stateMachine = stateMachine;
            _movementSystem = movementSystem;
            _weaponSystem = weaponSystem;
        }

        public void Tick(float deltaTime)
        {
            ReadInput();
            HandleCombat();
            HandleStateTransitions();
            ExecuteActions(deltaTime);
        }

        // -------------------------
        // INPUT
        // -------------------------
        private void ReadInput()
        {
            _model.moveInput = _input.MoveInput;
            _model.isShootingPressed = _input.IsShootingPressed;
            _model.isReloadPressed = _input.IsReloadPressed;
        }

        // -------------------------
        // STATE LOGIC
        // -------------------------
        private void HandleStateTransitions()
        {
            // Death override (highest priority)
            if (_model.isDead)
            {
                _stateMachine.ChangeState(HeroState.Dead);
                return;
            }

            //// Reload
            //if (_model.isReloadPressed && _weaponSystem.CanReload())
            //{
            //    _stateMachine.ChangeState(HeroState.Reloading);
            //    return;
            //}

            //// Shooting
            //if (_model.isShootingPressed && _weaponSystem.CanShoot())
            //{
            //    _stateMachine.ChangeState(HeroState.Shooting);
            //    return;
            //}

            // Movement
            if (_model.moveInput != Vector2.zero)
            {
                _stateMachine.ChangeState(HeroState.Moving);
                return;
            }

            // Default
            _stateMachine.ChangeState(HeroState.Idle);
        }

        private void HandleCombat()
        {
            Debug.Log("HandleCombat: " + _input.IsShootingReleased);
            if (_input.IsShootingPressed && _weaponSystem.CanShoot())
            {
                _weaponSystem.Shoot();

                _view.PlayShoot();
            }

            if (_input.IsShootingReleased)
            {
                _stateMachine.ChangeState(HeroState.Idle);

                _view.StopShoot();
            }

            if (_model.isReloadPressed && _weaponSystem.CanReload())
            {
                _weaponSystem.Reload();

                _view.PlayReload();
            }
        }

        // -------------------------
        // EXECUTION
        // -------------------------
        private void ExecuteActions(float deltaTime)
        {
            // Dead > Reload > Shoot > Move > Idle
            var currentState = _stateMachine.CurrentState;

            switch (currentState)
            {
                case HeroState.Idle:
                    _movementSystem.Stop();
                    break;

                case HeroState.Moving:
                    _movementSystem.Move(_model.moveInput, deltaTime);
                    break;

                case HeroState.Shooting:
                    _movementSystem.Stop(); // optional design choice
                    _weaponSystem.TryShoot();
                    break;

                case HeroState.Reloading:
                    _movementSystem.Stop();
                    _weaponSystem.Reload();
                    break;

                case HeroState.Dead:
                    _movementSystem.Disable();
                    _weaponSystem.Disable();
                    break;
            }
        }

        public void OnAnimationEvent(AnimationEventType eventName)
        {
            switch (eventName)
            {
                case AnimationEventType.ShootFinished:
                    _stateMachine.ChangeState(HeroState.Idle);
                    break;
            }
        }
    }
}
