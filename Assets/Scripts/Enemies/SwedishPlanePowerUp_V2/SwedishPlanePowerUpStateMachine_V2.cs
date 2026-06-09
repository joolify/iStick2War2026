using UnityEngine;

namespace iStick2War_V2
{
    public sealed class SwedishPlanePowerUpStateMachine_V2 :
        AircraftStateMachineBase_V2<SwedishPlanePowerUpState_V2, SwedishPlanePowerUpModel_V2>
    {
        protected override SwedishPlanePowerUpState_V2 IdleState => SwedishPlanePowerUpState_V2.Idle;

        protected override SwedishPlanePowerUpState_V2 TerminalState => SwedishPlanePowerUpState_V2.PickedUp;
    }
}
