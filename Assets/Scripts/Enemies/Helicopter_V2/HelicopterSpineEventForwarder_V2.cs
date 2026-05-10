using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class HelicopterSpineEventForwarder_V2 : AircraftFlyStartedSpineEventForwarderBase_V2<HelicopterController_V2>
    {
        public void Init(HelicopterController_V2 controller, SkeletonAnimation skeletonAnimation)
        {
            base.Init(controller, skeletonAnimation);
        }
    }
}
