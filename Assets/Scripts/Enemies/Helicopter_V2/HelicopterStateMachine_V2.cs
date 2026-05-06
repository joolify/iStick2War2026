using System;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class HelicopterStateMachine_V2 : MonoBehaviour
    {
        private HelicopterState_V2 _currentState;
        private HelicopterModel_V2 _model;

        public event Action<HelicopterState_V2, HelicopterState_V2> OnStateChanged;

        public HelicopterState_V2 CurrentState => _currentState;

        public void Initialize(HelicopterModel_V2 model)
        {
            _model = model;
            _currentState = HelicopterState_V2.Idle;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }
        }

        public void ResetForSpawn()
        {
            _currentState = HelicopterState_V2.Idle;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }
        }

        public void ChangeState(HelicopterState_V2 newState)
        {
            if (newState == _currentState || _currentState == HelicopterState_V2.Die)
            {
                return;
            }

            HelicopterState_V2 previous = _currentState;
            _currentState = newState;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }

            OnStateChanged?.Invoke(previous, newState);
        }
    }
}
