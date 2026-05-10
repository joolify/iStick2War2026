using UnityEngine;

namespace iStick2War_V2
{
    public sealed class HelicopterView_V2 : AircraftSingleClipSpineViewBase_V2<HelicopterState_V2>
    {
        protected override HelicopterState_V2 IdleStateValue => HelicopterState_V2.Idle;

        public void Initialize(HelicopterStateMachine_V2 stateMachine)
        {
            base.Initialize(stateMachine);
        }
    }
}
