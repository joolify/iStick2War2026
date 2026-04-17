using Assets.Scripts.Components;
using iStick2War;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace iStick2War_V2
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
    internal class HeroController_V2
    {
        private readonly HeroModel_V2 _model;
        private readonly HeroView_V2 _view;

        private readonly HeroInput_V2 _input;
        private readonly HeroStateMachine_V2 _stateMachine;
        private readonly HeroMovementSystem_V2 _movementSystem;
        private readonly HeroWeaponSystem_V2 _weaponSystem;
        private const bool DebugDrawShotRay = true;
        private static readonly bool DebugCombatLogs = false;
        private bool _isShootLoopActive;
        private bool _outOfAmmoLatched;

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
            HandleWeaponSwitchInput();
            _weaponSystem.Tick();
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
            _model.isShootingPressed = _input.IsShootingHeld;
            _model.isReloadPressed = _input.IsReloadPressed;
            _model.isJumpPressed = _input.IsJumpPressed;

            if (_model.isJumpPressed)
            {
                LogCombat($"[HeroController_V2] Jump input detected. moveInput={_model.moveInput}");
            }
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

            if (_model.isJumpPressed && _movementSystem.CanJump())
            {
                LogCombat("[HeroController_V2] Jump transition accepted.");
                _movementSystem.Jump();
                _stateMachine.ChangeState(HeroState.Jumping);
                return;
            }

            if (_stateMachine.CurrentState == HeroState.Jumping && !_movementSystem.IsGrounded())
            {
                _stateMachine.ChangeState(HeroState.Jumping);
                return;
            }

            if (_weaponSystem.IsReloading())
            {
                _stateMachine.ChangeState(HeroState.Reloading);
                return;
            }

            // Keep shooting state while shoot loop is active and button is still held.
            if (_isShootLoopActive && _input.IsShootingHeld)
            {
                _stateMachine.ChangeState(HeroState.Shooting);
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
            if (_model.isReloadPressed && _weaponSystem.CanReload())
            {
                _isShootLoopActive = false;
                if (_weaponSystem.StartReload())
                {
                    _outOfAmmoLatched = false;
                    _view.StopShoot();
                    _view.PlayReload();
                    _stateMachine.ChangeState(HeroState.Reloading);
                    return;
                }
            }

            // Require release before re-entering shooting after dry fire.
            if (!_input.IsShootingHeld && _outOfAmmoLatched)
            {
                _outOfAmmoLatched = false;
            }

            if (_input.IsShootingHeld && !_isShootLoopActive && _model.currentAmmo > 0 && !_outOfAmmoLatched)
            {
                _isShootLoopActive = true;
                _stateMachine.ChangeState(HeroState.Shooting);
                _view.PlayShoot();
            }
            else if (_input.IsShootingHeld && !_isShootLoopActive && _model.currentAmmo <= 0 && !_outOfAmmoLatched)
            {
                _outOfAmmoLatched = true;
                _view.PlayOutOfAmmo();
            }

            if (!_input.IsShootingHeld && _isShootLoopActive)
            {
                _isShootLoopActive = false;
                _stateMachine.ChangeState(HeroState.Idle);
                _view.StopShoot();
            }

        }

        private void HandleWeaponSwitchInput()
        {
            bool switched = false;
            if (_input.DirectWeaponSlot >= 0)
            {
                switched = _weaponSystem.TrySwitchToSlot(_input.DirectWeaponSlot);
            }
            else if (_input.IsSwitchNextWeaponPressed)
            {
                switched = _weaponSystem.TrySwitchToNextWeapon();
            }
            else if (_input.IsSwitchPreviousWeaponPressed)
            {
                switched = _weaponSystem.TrySwitchToPreviousWeapon();
            }

            if (!switched)
            {
                return;
            }

            _isShootLoopActive = false;
            _outOfAmmoLatched = false;
            _view.StopShoot();
            _view.RefreshWeaponVisualsForCurrentState();
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

                case HeroState.Jumping:
                    _movementSystem.Move(_model.moveInput, deltaTime);
                    _view.UpdateJumpCombatOverlay(_isShootLoopActive && _input.IsShootingHeld);
                    if (_isShootLoopActive && _input.IsShootingHeld)
                    {
                        TryShootNow();
                    }
                    break;

                case HeroState.Shooting:
                    // Allow strafe/run while the shoot loop is active.
                    _movementSystem.Move(_model.moveInput, deltaTime);
                    _view.UpdateShootLocomotion(_model.moveInput != Vector2.zero);
                    break;

                case HeroState.Reloading:
                    _movementSystem.Stop();
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
                case AnimationEventType.ShootStarted:
                    LogCombat("OnAnimationEvent.ShootStarted");
                    if (!_isShootLoopActive)
                    {
                        LogCombat("[HeroController_V2] ShootStarted ignored: shoot loop inactive.");
                        return;
                    }

                    // While jumping we resolve fire directly in Tick to keep jump animation on base track.
                    if (_stateMachine.CurrentState == HeroState.Jumping)
                    {
                        return;
                    }

                    if (_model.currentAmmo <= 0)
                    {
                        LogCombat("[HeroController_V2] ShootStarted cancelled: out of ammo.");
                        _isShootLoopActive = false;
                        _outOfAmmoLatched = true;
                        _stateMachine.ChangeState(HeroState.Idle);
                        _view.StopShoot();
                        _view.PlayOutOfAmmo();
                        return;
                    }

                    TryShootNow();
                    break;

                case AnimationEventType.ShootFinished:
                    if (!_input.IsShootingHeld)
                    {
                        _isShootLoopActive = false;
                        _stateMachine.ChangeState(HeroState.Idle);
                    }
                    break;
            }
        }

        private void TryShootNow()
        {
            if (_model.currentAmmo <= 0)
            {
                return;
            }

            if (!_view.TryGetAimData(out var aimPos, out var direction))
            {
                Debug.LogWarning("[HeroController_V2] TryShootNow: TryGetAimData failed.");
                return;
            }

            HeroShotContext_V2 shotContext = _weaponSystem.CreateShotContext(aimPos, direction, DebugDrawShotRay);

            if (_weaponSystem.ActiveWeaponUsesProjectile())
            {
                _weaponSystem.ShootProjectile(aimPos, direction);
                return;
            }

            if (_weaponSystem.Shoot(shotContext, out var shotResult))
            {
                _view.PlayShotTrail(aimPos, shotResult.FinalPos);
            }
        }

        private static void LogCombat(string message)
        {
            if (DebugCombatLogs)
            {
                Debug.Log(message);
            }
        }
    }
}
