using Assets.Scripts.Components;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class KamikazeDroneController_V2 : MonoBehaviour
    {
        private KamikazeDroneStateMachine_V2 _stateMachine;

        public void Initialize(KamikazeDroneModel_V2 model, KamikazeDroneStateMachine_V2 stateMachine)
        {
            _stateMachine = stateMachine;
        }

        public void StartFlight()
        {
            _stateMachine?.ChangeState(KamikazeDroneState_V2.Fly);
        }

        public void OnDestroyed()
        {
            _stateMachine?.ChangeState(KamikazeDroneState_V2.Die);
        }

        public void OnAnimationEvent(AnimationEventType eventType)
        {
            if (_stateMachine == null)
            {
                return;
            }

            if (eventType == AnimationEventType.DeployStarted)
            {
                _stateMachine.ChangeState(KamikazeDroneState_V2.Fly);
            }
        }
    }
}
