using System;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class KamikazeDroneStateMachine_V2 : MonoBehaviour
    {
        private KamikazeDroneState_V2 _currentState;
        private KamikazeDroneModel_V2 _model;

        public event Action<KamikazeDroneState_V2, KamikazeDroneState_V2> OnStateChanged;

        public KamikazeDroneState_V2 CurrentState => _currentState;

        public void Initialize(KamikazeDroneModel_V2 model)
        {
            _model = model;
            _currentState = KamikazeDroneState_V2.Idle;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }
        }

        public void ResetForSpawn()
        {
            _currentState = KamikazeDroneState_V2.Idle;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }
        }

        public void ChangeState(KamikazeDroneState_V2 newState)
        {
            if (newState == _currentState || _currentState == KamikazeDroneState_V2.Die)
            {
                return;
            }

            KamikazeDroneState_V2 previous = _currentState;
            _currentState = newState;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }

            OnStateChanged?.Invoke(previous, newState);
        }
    }
}
