using System;
using UnityEngine;

namespace iStick2War_V2
{
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
