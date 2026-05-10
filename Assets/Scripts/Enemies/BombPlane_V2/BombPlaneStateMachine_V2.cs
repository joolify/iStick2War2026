using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombPlaneStateMachine_V2 (State Transition Rules Engine)
 *
 * PURPOSE:
 * Owns the current BombPlaneState_V2 and mirrors it onto BombPlaneModel_V2.
 * Raises OnStateChanged so the View can swap Spine tracks without polling.
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * Defines WHAT state is active and WHEN transitions are accepted,
 * not HOW flight or bombs execute (that belongs to the Controller).
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Initialize / reset with the Model
 * - ChangeState with simple guards (no-op on duplicate; Die is sticky)
 * - Broadcast (previous, next) to listeners
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Instantiate bombs or move the transform
 * - Read spawner or camera data
 * - Encode bomb cadence or off-screen despawn rules
 */
    public sealed class BombPlaneStateMachine_V2 : AircraftStateMachineBase_V2<BombPlaneState_V2, BombPlaneModel_V2>
    {
        protected override BombPlaneState_V2 IdleState => BombPlaneState_V2.Idle;

        protected override BombPlaneState_V2 TerminalState => BombPlaneState_V2.Die;
    }
}
