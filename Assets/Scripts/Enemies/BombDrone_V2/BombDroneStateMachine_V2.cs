using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombDroneStateMachine_V2 (State Transition Rules Engine)
 *
 * PURPOSE:
 * Owns the current BombDroneState_V2 and mirrors it onto BombDroneModel_V2.
 * Raises OnStateChanged so BombDroneView_V2 can swap Spine tracks without polling.
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * Defines WHAT state is active and WHEN transitions are accepted,
 * not HOW the drone flies or when the bomb releases (Controller owns that).
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Initialize / reset with the Model
 * - ChangeState with guards (duplicate no-op; Die is sticky)
 * - Broadcast (previous, next) to listeners
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Query BunkerHitbox_V2 or Camera
 * - Spawn bombs or move transforms
 */
    public sealed class BombDroneStateMachine_V2 : AircraftStateMachineBase_V2<BombDroneState_V2, BombDroneModel_V2>
    {
        protected override BombDroneState_V2 IdleState => BombDroneState_V2.Idle;

        protected override BombDroneState_V2 TerminalState => BombDroneState_V2.Die;
    }
}
