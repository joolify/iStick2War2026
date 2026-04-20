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
    private float _forcedShootFallbackAtTime = -1f;
    private float _forcedGrenadeAttemptAtTime = -1f;
    [Header("Ground combat behavior")]
    [Tooltip("If true, try to start grenade immediately when landing (if possible).")]
    [SerializeField] private bool _tryImmediateGrenadeOnLand = true;
    [Tooltip("Fallback delay: if no grenade started on landing, force one grenade attempt after this many seconds.")]
    [SerializeField] private float _forcedGrenadeDelayAfterLandingSeconds = 1.25f;
    [Tooltip("Failsafe: if grenade finished event is missed, force transition to Shoot after this many seconds.")]
    [SerializeField] private float _grenadeToShootFallbackSeconds = 0.9f;
    [Header("Debug")]
    [Tooltip("When enabled, Paratrooper only uses grenade behavior and never fires MP40.")]
    [SerializeField] private bool _debugGrenadeOnlyMode = false;

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
                else if (_debugGrenadeOnlyMode)
                {
                    if (_weaponSystem != null && !_weaponSystem.CanThrowGrenade())
                    {
                        string reason = _weaponSystem.GetGrenadeBlockReason();
                        Debug.LogWarning($"[ParatrooperController_V2] Grenade-only mode active but CanThrowGrenade() is false ({reason ?? "unknown reason"}). Staying on grenade behavior and suppressing shoot fallback.");
                    }
                    _stateMachine.ChangeState(StickmanBodyState.Grenade);
                }
                else
                {
                    bool canGrenade = _weaponSystem != null && _weaponSystem.CanThrowGrenade();
                    bool didStartImmediateGrenade = _tryImmediateGrenadeOnLand && canGrenade;
                    Log($"[ParatrooperController_V2] LandFinished event: _tryImmediateGrenadeOnLand={_tryImmediateGrenadeOnLand}, canGrenade={canGrenade}. Will start {(didStartImmediateGrenade ? "Grenade" : "Shoot")}. Grenade block reason: {(canGrenade ? "N/A" : (_weaponSystem != null ? _weaponSystem.GetGrenadeBlockReason() : "WeaponSystem missing"))}");

                    _stateMachine.ChangeState(didStartImmediateGrenade ? StickmanBodyState.Grenade : StickmanBodyState.Shoot);

                    if (didStartImmediateGrenade)
                    {
                        _forcedGrenadeAttemptAtTime = -1f;
                    }
                    else
                    {
                        float delay = Mathf.Max(0.05f, _forcedGrenadeDelayAfterLandingSeconds);
                        _forcedGrenadeAttemptAtTime = Time.time + delay;
                    }
                }

                break;
            case AnimationEventType.ShootStarted:
                _forcedShootFallbackAtTime = -1f;
                if (_debugGrenadeOnlyMode)
                {
                    _isShootWindowOpen = false;
                    _stateMachine.ChangeState(StickmanBodyState.Grenade);

                    break;
                }

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
                    bool didThrow = _weaponSystem.TryThrowGrenadeAtHero();
                    if (didThrow && !_debugGrenadeOnlyMode)
                    {
                        _forcedGrenadeAttemptAtTime = -1f;
                        _forcedShootFallbackAtTime = Time.time + Mathf.Max(0.05f, _grenadeToShootFallbackSeconds);
                    }
                    if (!didThrow && _debugGrenadeOnlyMode)
                    {
                        string reason = _weaponSystem.GetGrenadeBlockReason();
                        Debug.LogWarning($"[ParatrooperController_V2] GrenadeThrow event received but throw failed in grenade-only mode ({reason ?? "unknown reason"}).");
                    }
                }
                break;
            case AnimationEventType.GrenadeFinished:
                if (_stateMachine.CurrentState == StickmanBodyState.Grenade)
                {
                    _forcedShootFallbackAtTime = -1f;
                    _forcedGrenadeAttemptAtTime = -1f;
                    if (_debugGrenadeOnlyMode)
                    {
                        _stateMachine.ChangeState(StickmanBodyState.Grenade);
                    }
                    else
                    {
                        _stateMachine.ChangeState(StickmanBodyState.Shoot);
                    }
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

        // Enforce one grenade attempt after landing if immediate grenade did not start.
        if (!_debugGrenadeOnlyMode &&
            _stateMachine.CurrentState == StickmanBodyState.Shoot &&
            _forcedGrenadeAttemptAtTime > 0f &&
            Time.time >= _forcedGrenadeAttemptAtTime)
        {
            _forcedGrenadeAttemptAtTime = -1f;
            bool canGrenadeNow = _weaponSystem.CanThrowGrenade();
            if (canGrenadeNow)
            {
                Log("[ParatrooperController_V2] Forced delayed grenade attempt is due. Switching Shoot -> Grenade.");
                _stateMachine.ChangeState(StickmanBodyState.Grenade);
            }
            else
            {
                Log($"[ParatrooperController_V2] Forced delayed grenade attempt was due but blocked: {_weaponSystem.GetGrenadeBlockReason()}");
            }
        }

        // Fail-safe in case the Spine "grenade_finished" event is missing for this clip/setup.
        if (!_debugGrenadeOnlyMode &&
            _stateMachine.CurrentState == StickmanBodyState.Grenade &&
            _forcedShootFallbackAtTime > 0f &&
            Time.time >= _forcedShootFallbackAtTime)
        {
            _forcedShootFallbackAtTime = -1f;
            _stateMachine.ChangeState(StickmanBodyState.Shoot);
        }
    }

    public void SetDebugGrenadeOnlyMode(bool enabled)
    {
        _debugGrenadeOnlyMode = enabled;
    }

    private static void Log(string message)
    {
        Debug.Log(message);
    }

    internal void OnAnimationEvent(object reloadStarted)
    {
        throw new NotImplementedException();
    }
}
}
