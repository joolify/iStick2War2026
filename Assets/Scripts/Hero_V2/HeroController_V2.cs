using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    /*
     * HeroController_V2 (extra viktigt)

Den blir din “brain router”:

input → actions
state validation
weapon triggers
movement permission

    CORE PRINCIPER (innan kod)

Din controller ska:

Läsa input (via HeroInput_V2)
Fråga state machine: får vi göra detta?
Skicka vidare till:
MovementSystem
WeaponSystem
Uppdatera Model (inte View direkt)

❌ INTE:

Ingen fysik
Ingen animation
Ingen logik per feature

    */
    internal class HeroController_V2
    {
        private readonly HeroModel_V2 _model;

        private readonly HeroInput_V2 _input;
        private readonly HeroStateMachine_V2 _stateMachine;
        private readonly HeroMovementSystem_V2 _movementSystem;
        private readonly HeroWeaponSystem_V2 _weaponSystem;

        public HeroController_V2(
            HeroModel_V2 model,
            HeroInput_V2 input,
            HeroStateMachine_V2 stateMachine,
            HeroMovementSystem_V2 movementSystem,
            HeroWeaponSystem_V2 weaponSystem)
        {
            _model = model;
            _input = input;
            _stateMachine = stateMachine;
            _movementSystem = movementSystem;
            _weaponSystem = weaponSystem;
        }

        public void Tick(float deltaTime)
        {
            ReadInput();
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
                _stateMachine.SetState(HeroState.Dead);
                return;
            }

            // Reload
            if (_model.isReloadPressed && _weaponSystem.CanReload())
            {
                _stateMachine.SetState(HeroState.Reloading);
                return;
            }

            // Shooting
            if (_model.isShootingPressed && _weaponSystem.CanShoot())
            {
                _stateMachine.SetState(HeroState.Shooting);
                return;
            }

            // Movement
            if (_model.moveInput != Vector2.zero)
            {
                _stateMachine.SetState(HeroState.Moving);
                return;
            }

            // Default
            _stateMachine.SetState(HeroState.Idle);
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
    }
}
