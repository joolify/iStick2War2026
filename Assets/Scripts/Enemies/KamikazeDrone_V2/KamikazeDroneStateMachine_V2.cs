using UnityEngine;

namespace iStick2War_V2
{
    /*
 * KamikazeDroneStateMachine_V2 (State Transition Rules Engine)
 *
 * PURPOSE:
 * Owns KamikazeDroneState_V2, mirrors it to KamikazeDroneModel_V2, and notifies KamikazeDroneView_V2
 * via OnStateChanged (so Spine can stay in sync without polling).
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * Defines WHAT animation-facing state is active, not HOW the drone navigates the bunker
 * (KamikazeDroneDriver_V2 owns movement and detonation).
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Initialize / ResetForSpawn with the Model
 * - ChangeState with guards (duplicate no-op; Die is sticky)
 * - Broadcast (previous, next)
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Damage WaveManager / hero, spawn VFX, or despawn the prefab
 */
    public sealed class KamikazeDroneStateMachine_V2 : AircraftStateMachineBase_V2<KamikazeDroneState_V2, KamikazeDroneModel_V2>
    {
        protected override KamikazeDroneState_V2 IdleState => KamikazeDroneState_V2.Idle;

        protected override KamikazeDroneState_V2 TerminalState => KamikazeDroneState_V2.Die;
    }
}
