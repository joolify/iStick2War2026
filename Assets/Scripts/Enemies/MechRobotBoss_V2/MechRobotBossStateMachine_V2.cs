using System;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class MechRobotBossStateMachine_V2 : MonoBehaviour
    {
        MechRobotBossBodyState _currentState;
        private MechRobotBossModel_V2 _model;

        public event Action<MechRobotBossBodyState, MechRobotBossBodyState> OnStateChanged;
        public MechRobotBossBodyState CurrentState => _currentState;

        public void Initialize(MechRobotBossModel_V2 model)
        {
            _model = model;
            _currentState = MechRobotBossBodyState.Idle;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }
        }

        public void ResetForSpawn()
        {
            _currentState = MechRobotBossBodyState.Idle;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }
        }

        public void ChangeState(MechRobotBossBodyState newState)
        {
            if (newState == _currentState)
            {
                return;
            }

            if (_currentState == MechRobotBossBodyState.Die)
            {
                return;
            }

            MechRobotBossBodyState previous = _currentState;
            _currentState = newState;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }

            OnStateChanged?.Invoke(previous, newState);
        }
    }
}
