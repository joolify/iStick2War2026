using System;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * MechRobotBossStateMachine_V2 (Body state rules)
 *
 * PURPOSE:
 * Owns MechRobotBossBodyState transitions, mirrors current state onto MechRobotBossModel_V2, and raises
 * OnStateChanged so View, weapon system, and composition root can react without polling.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Encode locomotion or attack cadence (MechRobotBossController_V2)
 * - Apply damage or read colliders (MechRobotBossDamageReceiver_V2)
 * - Select Spine clips (MechRobotBossView_V2)
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Small deterministic state object living on a MonoBehaviour for Unity wiring; Die is terminal until reset.
 */
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
