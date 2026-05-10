using Assets.Scripts.Components;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * AircraftFlyStartedSpineEventForwarderBase_V2
 *
 * Optional Spine event → DeployStarted mapping shared by kamikaze drone and helicopter stacks.
 */
    public abstract class AircraftFlyStartedSpineEventForwarderBase_V2<TController> : MonoBehaviour
        where TController : MonoBehaviour, IAircraftSpineAnimationCommandReceiver_V2
    {
        private TController _controller;
        private SkeletonAnimation _skeletonAnimation;
        private EventData _flyStartedEventData;
        private bool _initialized;

        [Tooltip("Optional. Leave empty when skeleton has no matching event.")]
        [SpineEvent] public string flyStartedEventName = "";

        public void Init(TController controller, SkeletonAnimation skeletonAnimation)
        {
            _controller = controller;
            _skeletonAnimation = skeletonAnimation;

            if (_skeletonAnimation != null && _skeletonAnimation.Skeleton != null && _skeletonAnimation.Skeleton.Data != null)
            {
                _flyStartedEventData = string.IsNullOrWhiteSpace(flyStartedEventName)
                    ? null
                    : _skeletonAnimation.Skeleton.Data.FindEvent(flyStartedEventName);
                _skeletonAnimation.AnimationState.Event += OnSpineEvent;
                _initialized = true;
            }
        }

        private void OnDestroy()
        {
            if (_initialized && _skeletonAnimation != null)
            {
                _skeletonAnimation.AnimationState.Event -= OnSpineEvent;
            }
        }

        private void OnSpineEvent(TrackEntry trackEntry, Spine.Event e)
        {
            if (_controller == null || e == null || e.Data == null)
            {
                return;
            }

            if (_flyStartedEventData == null)
            {
                return;
            }

            if (e.Data == _flyStartedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.DeployStarted);
            }
        }
    }
}
