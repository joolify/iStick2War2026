using iStick2War;
using System;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * ParatrooperStateMachine_V2 (StickmanBodyState rules + notifications)
 *
 * PURPOSE:
 * Owns the current iStick2War.StickmanBodyState, mirrors it onto ParatrooperModel_V2, and raises
 * OnStateChanged so Controller / View can react without polling.
 *
 * ---------------------------------------------------------
 * STATE SET:
 *
 * The enum is shared StickmanBodyState (Deploy, Glide, GlideDie, Shoot, Grenade, Die, …).
 * Keep this file’s documentation in sync with Assets/Scripts/Components/StickmanBodyState.cs — do not
 * invent parallel state names here.
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Initialize / ResetForSpawn, ChangeState with CanTransition / Enter / Exit hooks
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT:
 *
 * - Implement AI targeting, weapon raycasts, or Spine track selection
 */
public class ParatrooperStateMachine_V2 : MonoBehaviour
{
    /* Current active state (mirrored to model when changed). */
    StickmanBodyState _currentState;

    private ParatrooperModel_V2 _model;

    /* Fired as (fromState, toState) after each successful ChangeState. */
    public event Action<StickmanBodyState, StickmanBodyState> OnStateChanged;
    public StickmanBodyState CurrentState => _currentState;

    public void Initialize(ParatrooperModel_V2 model)
    {
        _model = model;

        _currentState = StickmanBodyState.Idle;

        _model.currentState = _currentState;
    }

    public void ResetForSpawn()
    {
        _currentState = StickmanBodyState.Idle;
        if (_model != null)
        {
            _model.currentState = _currentState;
        }
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
        if (_model != null)
        {
            _model.currentState = _currentState;
        }

        EnterState(_currentState);

        OnStateChanged?.Invoke(previousState, newState);
    }

    private bool CanTransition(StickmanBodyState from, StickmanBodyState to)
    {
        // Simple rules (expand later)
        if (from == StickmanBodyState.Die)
            return false;

        if (_model != null &&
            _model.heroDeathStandDownActive &&
            (to == StickmanBodyState.Shoot || to == StickmanBodyState.Grenade))
        {
            return false;
        }

        return true;
    }

    /*
    Controller
       ↓
    StateMachine.ChangeState()
       ↓
    EnterState()
       ↓
    OnStateChanged event
       ↓
    View.PlayAnimation()
       ↓
    Spine animation plays
    */
    private void EnterState(StickmanBodyState state)
    {
        // ONLY gameplay hooks, NOT visuals
        switch (state)
        {
            case StickmanBodyState.Idle:
                // setup idle
                break;

            case StickmanBodyState.Glide:
                // start gliding. // adjust movement physics
                break;

            case StickmanBodyState.Run:
                // start running
                break;

            case StickmanBodyState.Die:
                // trigger death flow
                break;

            case StickmanBodyState.Deploy:
                // start deploy from parachute. enable gravity, disable shooting, etc.
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
}
