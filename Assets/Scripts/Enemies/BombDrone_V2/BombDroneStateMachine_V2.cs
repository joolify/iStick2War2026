using System;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombDroneStateMachine_V2 (State Transition Rules Engine)
 *
 * PURPOSE:
 * Owns the current BombDroneState_V2 and mirrors it onto BombDroneModel_V2.
 * Raises OnStateChanged so BombDroneView_V2 can swap Spine tracks without polling.
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * Defines WHAT state is active and WHEN transitions are accepted,
 * not HOW the drone flies or when the bomb releases (Controller owns that).
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Initialize / reset with the Model
 * - ChangeState with guards (duplicate no-op; Die is sticky)
 * - Broadcast (previous, next) to listeners
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Query BunkerHitbox_V2 or Camera
 * - Spawn bombs or move transforms
 */
    public sealed class BombDroneStateMachine_V2 : MonoBehaviour
    {
        private BombDroneState_V2 _currentState = BombDroneState_V2.Idle;
        private BombDroneModel_V2 _model;

        public event Action<BombDroneState_V2, BombDroneState_V2> OnStateChanged;
        public BombDroneState_V2 CurrentState => _currentState;

        public void Initialize(BombDroneModel_V2 model)
        {
            _model = model;
            ResetForSpawn();
        }

        public void ResetForSpawn()
        {
            _currentState = BombDroneState_V2.Idle;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }
        }

        public void ChangeState(BombDroneState_V2 newState)
        {
            if (newState == _currentState || _currentState == BombDroneState_V2.Die)
            {
                return;
            }

            BombDroneState_V2 previous = _currentState;
            _currentState = newState;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }

            OnStateChanged?.Invoke(previous, newState);
        }
    }
}
