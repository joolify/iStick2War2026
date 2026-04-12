using iStick2War;
using System;
using UnityEngine;
using static UnityEngine.CullingGroup;

/// <summary>
/// ParatrooperStateMachine
/// </summary>
/// <remarks>
/// Controls the current state of the Paratrooper entity and manages transitions between states.
/// Acts as the central authority for state flow and ensures consistency between systems.
///
/// Responsibilities:
/// - Stores and updates the current state
/// - Handles state transitions in a controlled manner
/// - Notifies or drives dependent systems (Controller and View) on state changes
///
/// Constraints:
/// - SHOULD NOT contain AI decision logic (handled by Controller)
/// - SHOULD NOT contain rendering or animation logic directly (handled by View)
/// - SHOULD remain lightweight and focused on state flow only
///
/// States:
/// - Idle
/// - Drop
/// - Combat
/// - HitReaction
/// - Dead
///
///stateMachine.OnStateChanged += (from, to) =>
///{
///if (to == EnemyState.Dead)
///deathHandler.Die();
///};

/// </remarks>
public class ParatrooperStateMachine_V2 : MonoBehaviour
{
    /// <summary>
    /// Current active state.
    /// </summary>
    StickmanBodyState _currentState;

    private ParatrooperModel_V2 _model;

    /// <summary>
    /// Fired whenever the state changes.
    /// (fromState, toState)
    /// </summary>
    public event Action<StickmanBodyState, StickmanBodyState> OnStateChanged;

    public void Initialize(ParatrooperModel_V2 model)
    {
        _model = model;

        _currentState = StickmanBodyState.Idle;

        _model.currentState = _currentState;
    }

    public void ChangeState(StickmanBodyState newState)
    {
        if (newState == _currentState)
            return;

        if (!CanTransition(_currentState, newState))
            return;

        var previousState = _currentState;

        ExitState(_currentState);
        _currentState = newState;
        EnterState(_currentState);

        OnStateChanged?.Invoke(previousState, newState);
    }

    private bool CanTransition(StickmanBodyState from, StickmanBodyState to)
    {
        // Simple rules (expand later)
        if (from == StickmanBodyState.Die)
            return false;

        return true;
    }

    private void EnterState(StickmanBodyState state)
    {
        switch (state)
        {
            case StickmanBodyState.Idle:
                // setup idle
                break;

            case StickmanBodyState.Glide:
                // start gliding
                break;

            case StickmanBodyState.Run:
                // start running
                break;

            case StickmanBodyState.Die:
                // trigger death flow
                break;
        }
    }

    private void ExitState(StickmanBodyState state)
    {
        switch (state)
        {
            case StickmanBodyState.Shoot:
                // cleanup combat state
                break;

            case StickmanBodyState.Jump:
                // reset state
                break;
        }
    }
}
