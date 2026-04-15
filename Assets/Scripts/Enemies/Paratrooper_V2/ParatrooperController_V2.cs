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
                if (_stateMachine.CurrentState == StickmanBodyState.Land)
                {
                    _stateMachine.ChangeState(StickmanBodyState.Shoot);
                }
                else
                {
                    Debug.Log($"[ParatrooperController_V2] Ignored LandFinished in state {_stateMachine.CurrentState}.");
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
