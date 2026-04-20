using Assets.Scripts.Components;
using iStick2War;
using System;
using UnityEngine;

/// <summary>
/// ParatrooperController acts as the AI brain for the Paratrooper entity.
/// It is responsible for high-level decision making and orchestrating core gameplay systems.
/// </summary>
/// <remarks>
/// Responsibilities:
/// - Updates AI logic on a fixed or frame-based tick
/// - Evaluates and triggers state transitions via the StateMachine
/// - Communicates intent and data updates to the Model
///
/// Restrictions:
/// - MUST NOT contain physics logic
/// - MUST NOT directly control animations
/// - Acts purely as a decision-making layer
/// </remarks>
namespace iStick2War_V2
{
public class ParatrooperController_V2 : MonoBehaviour
{
    ParatrooperModel_V2 _model;
    ParatrooperStateMachine_V2 _stateMachine;
    ParatrooperDamageReceiver_V2 _damageReceiver;
    ParatrooperWeaponSystem_V2 _weaponSystem;
    private bool _isShootWindowOpen;
    [Header("Ground combat behavior")]
    [SerializeField] [Range(0f, 1f)] private float _grenadeChanceAfterLanding = 0.35f;

    /// <summary>True while Spine shoot window is open (read keeps field from CS0414).</summary>
    internal bool IsShootWindowOpen => _isShootWindowOpen;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public void Initialize(
        ParatrooperModel_V2 model,
        ParatrooperStateMachine_V2 stateMachine,
        ParatrooperDamageReceiver_V2 damageReceiver,
        ParatrooperWeaponSystem_V2 weaponSystem)
    {
        _model = model;
        _stateMachine = stateMachine;
        _damageReceiver = damageReceiver;
        _weaponSystem = weaponSystem;
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void StartGame()
    {
        OnDeploy();
    }

    public void OnAnimationEvent(AnimationEventType eventName)
    {
        switch (eventName)
        {
            case AnimationEventType.DeployFinished:
                _stateMachine.ChangeState(StickmanBodyState.Glide);
                break;
            case AnimationEventType.LandFinished:
                if (_stateMachine.CurrentState != StickmanBodyState.Land)
                {
                    Debug.Log($"[ParatrooperController_V2] Ignored LandFinished in state {_stateMachine.CurrentState}.");
                    break;
                }

                if (_model != null && _model.pendingDieAfterLandAnim)
                {
                    _model.pendingDieAfterLandAnim = false;
                    _stateMachine.ChangeState(StickmanBodyState.Die);
                }
                else
                {
                    bool canGrenade = _weaponSystem != null && _weaponSystem.CanThrowGrenade();
                    bool shouldGrenade = canGrenade && UnityEngine.Random.value <= Mathf.Clamp01(_grenadeChanceAfterLanding);
                    _stateMachine.ChangeState(shouldGrenade ? StickmanBodyState.Grenade : StickmanBodyState.Shoot);
                }

                break;
            case AnimationEventType.ShootStarted:
                if (_stateMachine.CurrentState == StickmanBodyState.Shoot && _weaponSystem != null)
                {
                    _isShootWindowOpen = true;
                    _weaponSystem.TryAutoShootAtHero();
                }
                break;
            case AnimationEventType.ShootFinished:
                _isShootWindowOpen = false;
                break;
            case AnimationEventType.GrenadeThrow:
                if (_stateMachine.CurrentState == StickmanBodyState.Grenade && _weaponSystem != null)
                {
                    _weaponSystem.TryThrowGrenadeAtHero();
                }
                break;
            case AnimationEventType.GrenadeFinished:
                if (_stateMachine.CurrentState == StickmanBodyState.Grenade)
                {
                    _stateMachine.ChangeState(StickmanBodyState.Shoot);
                }
                break;
        }
    }

    public void OnDeploy()
    {
        _stateMachine.ChangeState(StickmanBodyState.Deploy);
    }

    public void OnLanded()
    {
        _stateMachine.ChangeState(StickmanBodyState.Shoot);
    }

    internal void Tick(float deltaTime)
    {
        if (_stateMachine == null || _weaponSystem == null)
        {
            return;
        }

        // Shooting is event-driven by Spine shoot events.
        // Keep Tick free from fire calls to avoid double-triggering.
        if (_stateMachine.CurrentState != StickmanBodyState.Shoot)
        {
            _isShootWindowOpen = false;
        }
    }

    internal void OnAnimationEvent(object reloadStarted)
    {
        throw new NotImplementedException();
    }
}
}
