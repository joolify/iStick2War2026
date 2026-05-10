using UnityEngine;

namespace iStick2War_V2
{
    /*
 * KamikazeDroneView_V2 (Presentation Layer)
 *
 * PURPOSE:
 * Spine visuals for the kamikaze stack: one looping clip (default name "fly") regardless of
 * Idle/Fly/Die from the state machine’s perspective—state changes keep the hook ready if clips split later.
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * Presentation only. No bunker targeting, no explosion, no pooling.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Duplicate logic from KamikazeDroneDriver_V2 (rotation there is optional via its own fields)
 *
 * ---------------------------------------------------------
 * ✅ RESPONSIBILITIES:
 *
 * - Resolve SkeletonAnimation (serialized or search)
 * - Subscribe to KamikazeDroneStateMachine_V2.OnStateChanged and call SetAnimation
 *
 * ---------------------------------------------------------
 * ARCHITECTURE NOTE:
 *
 * Prefer placing this on the object that owns SkeletonAnimation; composition root may AddComponent
 * on the root if missing—assign references on the prefab when possible.
 */
    public sealed class KamikazeDroneView_V2 : AircraftSingleClipSpineViewBase_V2<KamikazeDroneState_V2>
    {
        protected override KamikazeDroneState_V2 IdleStateValue => KamikazeDroneState_V2.Idle;

        public void Initialize(KamikazeDroneStateMachine_V2 stateMachine)
        {
            base.Initialize(stateMachine);
        }
    }
}
