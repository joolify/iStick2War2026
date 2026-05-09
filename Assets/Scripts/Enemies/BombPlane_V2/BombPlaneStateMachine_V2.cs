using System;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombPlaneStateMachine_V2 (State Transition Rules Engine)
 *
 * PURPOSE:
 * Owns the current BombPlaneState_V2 and mirrors it onto BombPlaneModel_V2.
 * Raises OnStateChanged so the View can swap Spine tracks without polling.
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * Defines WHAT state is active and WHEN transitions are accepted,
 * not HOW flight or bombs execute (that belongs to the Controller).
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Initialize / reset with the Model
 * - ChangeState with simple guards (no-op on duplicate; Die is sticky)
 * - Broadcast (previous, next) to listeners
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Instantiate bombs or move the transform
 * - Read spawner or camera data
 * - Encode bomb cadence or off-screen despawn rules
 */
    public sealed class BombPlaneStateMachine_V2 : MonoBehaviour
    {
        private BombPlaneState_V2 _currentState = BombPlaneState_V2.Idle;
        private BombPlaneModel_V2 _model;

        public event Action<BombPlaneState_V2, BombPlaneState_V2> OnStateChanged;
        public BombPlaneState_V2 CurrentState => _currentState;

        public void Initialize(BombPlaneModel_V2 model)
        {
            _model = model;
            ResetForSpawn();
        }

        public void ResetForSpawn()
        {
            _currentState = BombPlaneState_V2.Idle;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }
        }

        public void ChangeState(BombPlaneState_V2 newState)
        {
            if (newState == _currentState || _currentState == BombPlaneState_V2.Die)
            {
                return;
            }

            BombPlaneState_V2 previous = _currentState;
            _currentState = newState;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }

            OnStateChanged?.Invoke(previous, newState);
        }
    }
}
