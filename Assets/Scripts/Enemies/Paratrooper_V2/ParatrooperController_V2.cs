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
public class ParatrooperController_V2 : MonoBehaviour
{
    ParatrooperModel_V2 _model;
    ParatrooperStateMachine_V2 _stateMachine;
    ParatrooperDamageReceiver_V2 _damageReceiver;
    ParatrooperWeaponSystem_V2 _weaponSystem;
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

    public void OnAnimationEvent(AnimationEventType eventName)
    {
        switch (eventName)
        {
            case AnimationEventType.Shoot:
                _weaponSystem.Shoot();
                break;

            case AnimationEventType.Grenade:
                _weaponSystem.Grenade();
                break;
        }
    }

    internal void Tick(float deltaTime)
    {
        throw new NotImplementedException();
    }

    internal void EnterInitialState()
    {
        throw new NotImplementedException();
    }
}
