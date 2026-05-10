using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombDroneSpineEventForwarder_V2 (Animation Event Bridge)
 *
 * PURPOSE:
 * Subscribes to Spine AnimationState events for the bomb-drone skeleton.
 * Placeholder for future designer-driven hooks (e.g. exact frame bomb release) without hard-wiring Spine inside Update.
 *
 * ---------------------------------------------------------
 * EVENT FLOW (when mapped):
 *
 * Spine → BombDroneSpineEventForwarder_V2 → BombDroneController_V2.OnAnimationEvent
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Register / unregister Spine Event callback in Init / OnDestroy
 * - Remain a thin listener; handler currently intentionally empty
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT:
 *
 * - Implement bunker targeting or movement
 * - Spawn bombs independently of Controller rules
 *
 * ---------------------------------------------------------
 * ARCHITECTURAL ROLE:
 *
 * Same “sensor at the animation boundary” role as BombPlaneSpineEventForwarder_V2 / HeroSpineEventForwarder_V2.
 */
    public sealed class BombDroneSpineEventForwarder_V2 : AircraftSpineEventForwarderBase_V2
    {
        private BombDroneController_V2 _controller;

        public void Init(BombDroneController_V2 controller, SkeletonAnimation skeletonAnimation)
        {
            _controller = controller;
            base.Init(skeletonAnimation);
        }

        protected override void OnSpineAnimationEvent(TrackEntry trackEntry, Spine.Event e)
        {
            if (_controller == null)
            {
                return;
            }

            // Intentionally no Spine event mapping yet for BombDrone.
        }
    }
}
