using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * KamikazeDroneSpineEventForwarder_V2 (Animation Event Bridge)
 *
 * PURPOSE:
 * Optional bridge from Spine AnimationState events to KamikazeDroneController_V2.OnAnimationEvent.
 * When flyStartedEventName is set and exists in skeleton data, matching events force state Fly
 * (e.g. after a deploy intro clip if the project adds one later).
 *
 * ---------------------------------------------------------
 * EVENT FLOW (when configured):
 *
 * Spine → KamikazeDroneSpineEventForwarder_V2 → KamikazeDroneController_V2.OnAnimationEvent
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Resolve EventData once in Init; subscribe / unsubscribe safely
 * - Remain a thin mapper; no gameplay simulation
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT:
 *
 * - Implement bunker approach or explosions (KamikazeDroneDriver_V2)
 *
 * ---------------------------------------------------------
 * ARCHITECTURAL ROLE:
 *
 * Same animation-boundary “sensor” role as HeroSpineEventForwarder_V2 / BombDroneSpineEventForwarder_V2.
 */
    public sealed class KamikazeDroneSpineEventForwarder_V2 : AircraftFlyStartedSpineEventForwarderBase_V2<KamikazeDroneController_V2>
    {
        public void Init(KamikazeDroneController_V2 controller, SkeletonAnimation skeletonAnimation)
        {
            base.Init(controller, skeletonAnimation);
        }
    }
}
