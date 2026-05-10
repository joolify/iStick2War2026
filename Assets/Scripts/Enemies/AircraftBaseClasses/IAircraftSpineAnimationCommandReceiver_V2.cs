using Assets.Scripts.Components;

namespace iStick2War_V2
{
    /*
 * IAircraftSpineAnimationCommandReceiver_V2
 *
 * Controllers that accept mapped Spine animation events (via a forwarder) implement this contract.
 */
    public interface IAircraftSpineAnimationCommandReceiver_V2
    {
        void OnAnimationEvent(AnimationEventType eventType);
    }
}
