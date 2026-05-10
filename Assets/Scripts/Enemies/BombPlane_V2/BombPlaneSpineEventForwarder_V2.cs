using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombPlaneSpineEventForwarder_V2 (Animation Event Bridge)
 *
 * PURPOSE:
 * Subscribes to Spine AnimationState events for the bomb plane skeleton.
 * Reserved for future designer-driven hooks (e.g. keyed drops) without coupling Spine to the Controller.
 *
 * ---------------------------------------------------------
 * EVENT FLOW (when mapped):
 *
 * Spine → BombPlaneSpineEventForwarder_V2 → BombPlaneController_V2 (or other handlers)
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Register / unregister the Spine Event callback safely (Init / OnDestroy)
 * - Stay a thin listener; no gameplay decisions in the handler today
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT:
 *
 * - Own bomb timing or flight rules
 * - Replace BombPlaneView_V2’s animation selection
 *
 * ---------------------------------------------------------
 * ARCHITECTURAL ROLE:
 *
 * Same “sensor at the edge of the animation system” role as HeroSpineEventForwarder_V2.
 */
    public sealed class BombPlaneSpineEventForwarder_V2 : AircraftSpineEventForwarderBase_V2
    {
        private BombPlaneController_V2 _controller;

        public void Init(BombPlaneController_V2 controller, SkeletonAnimation skeletonAnimation)
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

            // Intentionally no Spine event mapping yet for BombPlane.
        }
    }
}
