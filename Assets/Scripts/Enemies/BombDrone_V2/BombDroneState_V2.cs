namespace iStick2War_V2
{
    /*
 * BombDroneState_V2 (Gameplay State Enum)
 *
 * PURPOSE:
 * Named phases for the drone’s short lifecycle: approach, optional drop pulse, despawn path.
 *
 * ---------------------------------------------------------
 * STATES:
 *
 * - Idle    — not flying a pass (or reset before spawn)
 * - Fly     — horizontal motion; Controller watches bunker X to begin the over-bunker sequence
 * - DropBomb — transient; View plays one-shot dropBomb clip; Controller returns to Fly after a timer
 * - Die     — terminal; set when aircraft is destroyed before pool despawn
 * - HoverOverBunker — paused over bunker (default 1s) before DropBomb + payload spawn (enum appended so Die keeps value 3 for saved data)
 *
 * ---------------------------------------------------------
 * NOTE:
 *
 * Transition rules live in BombDroneStateMachine_V2; this enum is data only.
 * C# does not support enum inheritance; shared transition logic lives on AircraftStateMachineBase_V2.
 */
    public enum BombDroneState_V2
    {
        Idle,
        Fly,
        DropBomb,
        Die,
        HoverOverBunker
    }
}
