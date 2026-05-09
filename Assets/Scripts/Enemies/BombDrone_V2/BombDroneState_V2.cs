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
 * - Fly     — horizontal motion; Controller watches bunker X for the single drop window
 * - DropBomb — transient; View may play a one-shot drop clip; Controller returns to Fly after a timer
 * - Die     — terminal; set when aircraft is destroyed before pool despawn
 *
 * ---------------------------------------------------------
 * NOTE:
 *
 * Transition rules live in BombDroneStateMachine_V2; this enum is data only.
 */
    public enum BombDroneState_V2
    {
        Idle,
        Fly,
        DropBomb,
        Die
    }
}
