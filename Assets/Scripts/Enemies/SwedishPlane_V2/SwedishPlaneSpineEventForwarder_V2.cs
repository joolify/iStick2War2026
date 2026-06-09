using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    // Reserved for future Spine drop cues; Swedish plane uses distance-based drops today.
    public sealed class SwedishPlaneSpineEventForwarder_V2 : AircraftSpineEventForwarderBase_V2
    {
        protected override void OnSpineAnimationEvent(TrackEntry trackEntry, Spine.Event e)
        {
        }
    }
}
