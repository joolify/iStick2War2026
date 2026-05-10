using UnityEngine;

namespace iStick2War_V2
{
    public sealed class HelicopterStateMachine_V2 : AircraftStateMachineBase_V2<HelicopterState_V2, HelicopterModel_V2>
    {
        protected override HelicopterState_V2 IdleState => HelicopterState_V2.Idle;

        protected override HelicopterState_V2 TerminalState => HelicopterState_V2.Die;
    }
}
