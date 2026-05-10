using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombDroneView_V2 (Presentation Layer)
 *
 * PURPOSE:
 * Spine presentation for the bomb drone: looping fly clip, optional one-shot dropBomb when
 * state becomes DropBomb (default clip name matches typical Spine export).
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * The View MUST NOT decide bunker alignment or when the bomb spawns.
 * It only reacts to BombDroneStateMachine_V2.OnStateChanged.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Reference BunkerHitbox_V2 or Camera
 * - Call SimplePrefabPool_V2 or BombProjectile_V2
 *
 * ---------------------------------------------------------
 * ✅ RESPONSIBILITIES:
 *
 * - Resolve SkeletonAnimation (serialized or GetComponent / InChildren)
 * - Subscribe to state changes and call SetAnimation for fly / dropBomb
 *
 * ---------------------------------------------------------
 * ARCHITECTURE NOTE:
 *
 * Often lives on the same object or child as SkeletonAnimation; composition root may add a
 * duplicate component if none exists on the root—prefer assigning the View on the drone prefab.
 */
    public sealed class BombDroneView_V2 : AircraftDualClipSpineViewBase_V2<BombDroneState_V2>
    {
        protected override BombDroneState_V2 IdleStateValue => BombDroneState_V2.Idle;

        protected override BombDroneState_V2 DropBombStateValue => BombDroneState_V2.DropBomb;
    }
}
