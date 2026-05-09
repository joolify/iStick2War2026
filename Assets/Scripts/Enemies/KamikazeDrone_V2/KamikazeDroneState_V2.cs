namespace iStick2War_V2
{
    /*
 * KamikazeDroneState_V2 (Gameplay State Enum)
 *
 * PURPOSE:
 * High-level lifecycle for the Spine-facing stack. Actual attack phases (cruise / dive / plunge)
 * are internal to KamikazeDroneDriver_V2, not represented here.
 *
 * ---------------------------------------------------------
 * STATES:
 *
 * - Idle — before BeginFlight or after reset
 * - Fly  — active pass from the animation stack’s perspective (looping fly clip)
 * - Die  — terminal when AircraftHealth_V2 destroys the drone
 *
 * ---------------------------------------------------------
 * NOTE:
 *
 * Transition rules: KamikazeDroneStateMachine_V2. Optional Spine DeployStarted can force Fly.
 */
    public enum KamikazeDroneState_V2
    {
        Idle,
        Fly,
        Die
    }
}
