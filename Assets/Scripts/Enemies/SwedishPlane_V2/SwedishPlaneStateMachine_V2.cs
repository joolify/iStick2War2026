using UnityEngine;

namespace iStick2War_V2
{
    public sealed class SwedishPlaneStateMachine_V2 :
        AircraftStateMachineBase_V2<SwedishPlaneState_V2, SwedishPlaneModel_V2>
    {
        protected override SwedishPlaneState_V2 IdleState => SwedishPlaneState_V2.Idle;

        protected override SwedishPlaneState_V2 TerminalState => SwedishPlaneState_V2.Complete;
    }
}
