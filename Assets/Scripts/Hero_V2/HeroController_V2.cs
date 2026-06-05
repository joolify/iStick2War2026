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
 *     - HeroMovementSystem_V2
 *     - HeroWeaponSystem_V2
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
        private const bool DebugDrawShotRay = false;
        private static readonly bool DebugCombatLogs = false;
        private bool _isShootLoopActive;
        private bool _outOfAmmoLatched;
        private float _nextOutOfAmmoFeedbackAt;
        private float _nextFlamethrowerDebugLogAt;

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
            _weaponSystem.OnReloadCompleted += HandleWeaponReloadCompleted;
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

        internal void SetCombatPaused(bool paused)
        {
            _isShootLoopActive = false;
            _outOfAmmoLatched = false;
            _nextOutOfAmmoFeedbackAt = 0f;
            _view.StopShoot();
            AudioManager_V2.StopContinuousWeaponShot(WeaponType.None);

            if (paused)
            {
                HeroState currentState = _stateMachine.CurrentState;
                if (currentState == HeroState.Shooting || currentState == HeroState.Reloading)
                {
                    _stateMachine.ChangeState(HeroState.Idle);
                }

                return;
            }

            if (_model.isDead)
            {
                return;
            }

            ExitDeadStateIfAlive();
        }

        private void ExitDeadStateIfAlive()
        {
            if (_model.isDead || _stateMachine.CurrentState != HeroState.Dead)
            {
                return;
            }

            _stateMachine.ForceState(HeroState.Idle);
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
            // Life retry clears model.isDead before the state machine can leave Dead via ChangeState.
            ExitDeadStateIfAlive();

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

            // Reload timer can finish while the machine is still in Reloading; resume beam weapons same frame.
            if (_stateMachine.CurrentState == HeroState.Reloading)
            {
                if (_isShootLoopActive &&
                    _input.IsShootingHeld &&
                    _weaponSystem.HasRoundsReadyToFire())
                {
                    _stateMachine.ForceState(HeroState.Shooting);
                    return;
                }

                _stateMachine.ChangeState(HeroState.Idle);
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
            if (_weaponSystem.IsCombatDisabled)
            {
                if (_isShootLoopActive || _stateMachine.CurrentState == HeroState.Shooting)
                {
                    SetCombatPaused(true);
                }

                return;
            }

            // Programmatic weapon switches (e.g. AutoHero_V2) bypass HandleWeaponSwitchInput, so _outOfAmmoLatched
            // can stay true after a dry-fire on the previous weapon even when the new weapon has a loaded magazine.
            if (_outOfAmmoLatched && _weaponSystem.HasRoundsReadyToFire())
            {
                _outOfAmmoLatched = false;
                _nextOutOfAmmoFeedbackAt = 0f;
            }

            if (_model.isReloadPressed && _weaponSystem.CanReload())
            {
                _isShootLoopActive = false;
                if (_weaponSystem.StartReload())
                {
                    AudioManager_V2.StopContinuousWeaponShot(_model.currentWeaponType);
                    AudioManager_V2.PlayWeaponReload(_model.currentWeaponType);
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
                _nextOutOfAmmoFeedbackAt = 0f;
            }

            if (_input.IsShootingHeld &&
                !_weaponSystem.HasRoundsReadyToFire() &&
                !_weaponSystem.IsReloading())
            {
                if (!_outOfAmmoLatched)
                {
                    _outOfAmmoLatched = true;
                }

                TryDryFireOutOfAmmoFeedback();

                if (_isShootLoopActive)
                {
                    _isShootLoopActive = false;
                    AudioManager_V2.StopContinuousWeaponShot(_model.currentWeaponType);
                    _stateMachine.ChangeState(HeroState.Idle);
                    _view.StopShoot();
                }
            }
            else if (_input.IsShootingHeld &&
                     !_isShootLoopActive &&
                     _weaponSystem.HasRoundsReadyToFire())
            {
                _outOfAmmoLatched = false;
                _isShootLoopActive = true;
                if (_stateMachine.CurrentState == HeroState.Reloading)
                {
                    _stateMachine.ForceState(HeroState.Shooting);
                }
                else
                {
                    _stateMachine.ChangeState(HeroState.Shooting);
                }

                _view.PlayShoot();
                if (ShouldPlayShotAudioNow(_model.currentWeaponType))
                {
                    AudioManager_V2.PlayWeaponShot(_model.currentWeaponType);
                }
            }

            if (!_input.IsShootingHeld && _isShootLoopActive)
            {
                _isShootLoopActive = false;
                AudioManager_V2.StopContinuousWeaponShot(_model.currentWeaponType);
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
            _nextOutOfAmmoFeedbackAt = 0f;
            AudioManager_V2.StopContinuousWeaponShot(WeaponType.None);
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
                    // Beam weapons only; bazooka and hitscan fire from Spine start_shoot (jump shoot overlay on track 2).
                    if (_isShootLoopActive && _input.IsShootingHeld && ShootingStateTicksWeaponShots())
                    {
                        TryShootNow();
                    }
                    break;

                case HeroState.Shooting:
                    // Allow strafe/run while the shoot loop is active.
                    _movementSystem.Move(_model.moveInput, deltaTime);
                    _view.UpdateShootLocomotion(_model.moveInput != Vector2.zero);
                    // Beam weapons and projectiles (bazooka) resolve shots here so a missing/irregular Spine
                    // "ShootStarted" on looped clips cannot silently drop rounds.
                    // Bazooka uses Spine start_shoot / stop_shoot only (see ShootingStateTicksWeaponShots).
                    // TryShootNow is cheap when CanShoot() is false (fire-rate gate inside Shoot / ShootProjectile).
                    if (_isShootLoopActive &&
                        _input.IsShootingHeld &&
                        ShootingStateTicksWeaponShots())
                    {
                        TryShootNow();
                    }
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
                    if (_weaponSystem.IsCombatDisabled || !_isShootLoopActive)
                    {
                        LogCombat("[HeroController_V2] ShootStarted ignored: shoot loop inactive.");
                        return;
                    }

                    // Jumping: beam tick only; bazooka / hitscan use Spine start_shoot on the jump shoot overlay.
                    if (_stateMachine.CurrentState == HeroState.Jumping && ShootingStateTicksWeaponShots())
                    {
                        return;
                    }

                    // Tesla / flamethrower: fire from Shooting-state tick, not Spine ShootStarted.
                    if (ShootingStateTicksWeaponShots())
                    {
                        return;
                    }

                    if (!_weaponSystem.HasRoundsReadyToFire())
                    {
                        LogCombat("[HeroController_V2] ShootStarted cancelled: out of ammo.");
                        _isShootLoopActive = false;
                        _stateMachine.ChangeState(HeroState.Idle);
                        _view.StopShoot();
                        if (!_outOfAmmoLatched)
                        {
                            _outOfAmmoLatched = true;
                        }

                        TryDryFireOutOfAmmoFeedback();
                        return;
                    }

                    TryShootNow();
                    break;

                case AnimationEventType.ShootFinished:
                    if (!_input.IsShootingHeld)
                    {
                        _isShootLoopActive = false;
                        AudioManager_V2.StopContinuousWeaponShot(_model.currentWeaponType);
                        _stateMachine.ChangeState(HeroState.Idle);
                    }
                    break;
            }
        }

        private static bool UsesContinuousShootTickResolution(WeaponType weaponType)
        {
            return weaponType == WeaponType.Tesla || weaponType == WeaponType.Flamethrower;
        }

        /// <summary>
        /// When true, <see cref="TryShootNow"/> is driven from the Shooting-state tick (not only Spine ShootStarted).
        /// </summary>
        private bool ShootingStateTicksWeaponShots()
        {
            return UsesContinuousShootTickResolution(_model.currentWeaponType);
        }

        private float GetOutOfAmmoFeedbackIntervalSeconds()
        {
            return Mathf.Max(0.08f, _model.fireRate);
        }

        private void TryDryFireOutOfAmmoFeedback()
        {
            if (Time.time < _nextOutOfAmmoFeedbackAt)
            {
                return;
            }

            _nextOutOfAmmoFeedbackAt = Time.time + GetOutOfAmmoFeedbackIntervalSeconds();
            AudioManager_V2.PlayOutOfAmmo();
            _view.PlayOutOfAmmo();
        }

        private void TryShootNow()
        {
            if (!_weaponSystem.HasRoundsReadyToFire())
            {
                if (_input.IsShootingHeld &&
                    !_weaponSystem.IsReloading())
                {
                    TryDryFireOutOfAmmoFeedback();
                }

                return;
            }

            bool isFlamethrower = _model.currentWeaponType == WeaponType.Flamethrower;

            if (!_view.TryGetAimData(out var aimPos, out var direction, out var aimTarget))
            {
                Debug.LogWarning("[HeroController_V2] TryShootNow: TryGetAimData failed.");
                if (isFlamethrower && Time.time >= _nextFlamethrowerDebugLogAt)
                {
                    _nextFlamethrowerDebugLogAt = Time.time + 0.2f;
                    Debug.LogWarning("[HeroController_V2] Flamethrower debug: aim data invalid this tick.");
                }
                return;
            }

            HeroShotContext_V2 shotContext = _weaponSystem.CreateShotContext(
                aimPos,
                direction,
                DebugDrawShotRay,
                _view != null ? _view.FlamethrowerViewReachFraction : -1f);

            if (_weaponSystem.ActiveWeaponUsesProjectile())
            {
                bool allowCarrierPassthrough =
                    aimTarget.y <= HeroView_V2.ProjectileGroundTargetMaxWorldY;
                bool didShootProjectile = _weaponSystem.ShootProjectile(
                    aimPos,
                    direction,
                    allowCarrierPassthrough);
                if (didShootProjectile &&
                    ShouldPlayMuzzleFlashForWeapon(_model.currentWeaponType))
                {
                    PlayHeroMuzzleFlash(aimPos, direction);
                }

                if (didShootProjectile && ShouldPlayShotAudioNow(_model.currentWeaponType))
                {
                    AudioManager_V2.PlayWeaponShot(_model.currentWeaponType);
                    _view?.PlayVisualRecoil(_model.currentWeaponType, direction);
                }
                return;
            }

            if (_weaponSystem.Shoot(shotContext, out var shotResult))
            {
                if (ShouldPlayShotAudioNow(_model.currentWeaponType))
                {
                    AudioManager_V2.PlayWeaponShot(_model.currentWeaponType);
                }
                if (_model.currentWeaponType == WeaponType.Ithaca)
                {
                    PlayIthacaPelletImpacts(aimPos, shotContext.Range);
                }
                else if (shotResult.DidHit)
                {
                    BulletImpactVfx_V2.PlayIfSurfaceHit(shotResult.Hit, direction);
                    AudioManager_V2.PlayImpactForCollider(shotResult.Hit.collider);
                }
                else
                {
                    BulletImpactVfx_V2.PlayFirstSurfaceHitAlongRay(
                        aimPos,
                        direction,
                        Mathf.Max(0.1f, shotContext.Range),
                        includeBunker: false,
                        alignToHitNormalOverride: false);
                }

                bool usedTeslaBolt = _model.currentWeaponType == WeaponType.Tesla &&
                    _view.TryPlayTeslaLightningForShot(aimPos, shotResult.FinalPos);
                if (!usedTeslaBolt && !isFlamethrower)
                {
                    if (_model.currentWeaponType == WeaponType.Ithaca)
                    {
                        PlayIthacaPelletShotTrails(aimPos);
                    }
                    else
                    {
                        Vector2 shotVisualEnd = shotResult.FinalPos;
                        if (!shotResult.DidHit &&
                            TryGetMainCameraEdgePoint(aimPos, direction, out Vector2 cameraEdgePoint))
                        {
                            shotVisualEnd = cameraEdgePoint;
                        }

                        _view.PlayShotTrail(aimPos, shotVisualEnd);
                    }
                }

                if (ShouldPlayMuzzleFlashForWeapon(_model.currentWeaponType))
                {
                    PlayHeroMuzzleFlash(aimPos, direction);
                }
                _view.TryEjectShellCasing(_model.currentWeaponType, aimPos, direction);
                _view?.PlayVisualRecoil(_model.currentWeaponType, direction);

                if (isFlamethrower && Time.time >= _nextFlamethrowerDebugLogAt)
                {
                    _nextFlamethrowerDebugLogAt = Time.time + 0.2f;
                    string hitName = shotResult.DidHit && shotResult.Hit.collider != null
                        ? shotResult.Hit.collider.name
                        : "none";
                    Debug.Log(
                        $"[HeroController_V2] Flamethrower debug: shot committed. didHit={shotResult.DidHit}, " +
                        $"hitCollider={hitName}, aimPos={aimPos}, dir={direction}, ammo={_model.currentAmmo}");
                }
            }
        }

        private void PlayIthacaPelletShotTrails(Vector2 aimPos)
        {
            for (int pelletIndex = 0; pelletIndex < HeroWeaponSystem_V2.IthacaPelletCount; pelletIndex++)
            {
                IthacaPelletVisualSnapshot_V2 pellet = _weaponSystem.GetIthacaPelletVisual(pelletIndex);
                Vector2 trailEnd = pellet.FinalPos;
                if (!pellet.DidHit &&
                    TryGetMainCameraEdgePoint(aimPos, pellet.Direction, out Vector2 cameraEdgePoint))
                {
                    trailEnd = cameraEdgePoint;
                }

                _view.PlayShotTrail(aimPos, trailEnd);
            }
        }

        private void PlayIthacaPelletImpacts(Vector2 aimPos, float range)
        {
            bool anyPelletHit = false;
            for (int pelletIndex = 0; pelletIndex < HeroWeaponSystem_V2.IthacaPelletCount; pelletIndex++)
            {
                IthacaPelletVisualSnapshot_V2 pellet = _weaponSystem.GetIthacaPelletVisual(pelletIndex);
                if (!pellet.DidHit)
                {
                    continue;
                }

                anyPelletHit = true;
                BulletImpactVfx_V2.PlayIfSurfaceHit(pellet.Hit, pellet.Direction);
                AudioManager_V2.PlayImpactForCollider(pellet.Hit.collider);
            }

            if (!anyPelletHit)
            {
                IthacaPelletVisualSnapshot_V2 centerPellet =
                    _weaponSystem.GetIthacaPelletVisual(HeroWeaponSystem_V2.IthacaPelletCount / 2);
                BulletImpactVfx_V2.PlayFirstSurfaceHitAlongRay(
                    aimPos,
                    centerPellet.Direction,
                    Mathf.Max(0.1f, range),
                    includeBunker: false,
                    alignToHitNormalOverride: false);
            }
        }

        private void PlayHeroMuzzleFlash(Vector2 origin, Vector2 direction)
        {
            if (_view != null && _view.TryPlayMuzzleFlash(origin, direction))
            {
                return;
            }

            MuzzleFlash_V2.Play(origin, direction);
        }

        private static bool ShouldPlayMuzzleFlashForWeapon(WeaponType weaponType)
        {
            switch (weaponType)
            {
                case WeaponType.None:
                case WeaponType.Flamethrower:
                case WeaponType.Tesla:
                case WeaponType.MagicStaff:
                case WeaponType.Mk2:
                case WeaponType.Potatomasher:
                    return false;
                default:
                    return true;
            }
        }

        private bool ShouldPlayShotAudioNow(WeaponType weaponType)
        {
            // Continuous weapon audio should only run while fire input is actively held.
            if (weaponType == WeaponType.Tesla || weaponType == WeaponType.Flamethrower)
            {
                return _input.IsShootingHeld;
            }

            return true;
        }

        private static bool TryGetMainCameraEdgePoint(Vector2 origin, Vector2 direction, out Vector2 edgePoint)
        {
            if (HeroCombatCameraReach_V2.TryGetReachPoint(
                    Camera.main,
                    origin,
                    direction,
                    1f,
                    out edgePoint,
                    out _))
            {
                return true;
            }

            edgePoint = origin;
            return false;
        }

        private void HandleWeaponReloadCompleted(WeaponType weaponType)
        {
            _outOfAmmoLatched = false;
            _nextOutOfAmmoFeedbackAt = 0f;

            if (_weaponSystem.IsCombatDisabled || _model.isDead || !_input.IsShootingHeld)
            {
                return;
            }

            if (!_weaponSystem.HasRoundsReadyToFire())
            {
                return;
            }

            _isShootLoopActive = true;
            if (_stateMachine.CurrentState != HeroState.Shooting)
            {
                _stateMachine.ForceState(HeroState.Shooting);
            }

            _view.PlayShoot();
            if (ShouldPlayShotAudioNow(weaponType))
            {
                AudioManager_V2.PlayWeaponShot(weaponType);
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
