namespace iStick2War_V2
{
    /*
 * BombPlaneState_V2 (Gameplay State Enum)
 *
 * PURPOSE:
 * Named phases for the bomb plane’s simple lifecycle.
 *
 * ---------------------------------------------------------
 * STATES:
 *
 * - Idle   — not running a pass (or reset before spawn)
 * - Fly    — active pass; horizontal motion when enabled
 * - DropBomb — transient visual cue; controller may pulse this around each drop
 * - Die    — terminal; used before despawn / pool return
 *
 * ---------------------------------------------------------
 * NOTE:
 *
 * Transition rules live in BombPlaneStateMachine_V2; this enum is data only.
 */
    public enum BombPlaneState_V2
    {
        Idle,
        Fly,
        DropBomb,
        Die
    }
}
