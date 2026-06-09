using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SwedishPlaneView_V2 — looping "fly" Spine clip for the neutral supply plane.
     */
    public sealed class SwedishPlaneView_V2 : AircraftSingleClipSpineViewBase_V2<SwedishPlaneState_V2>
    {
        protected override SwedishPlaneState_V2 IdleStateValue => SwedishPlaneState_V2.Idle;
    }
}
