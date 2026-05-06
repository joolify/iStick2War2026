using Assets.Scripts.Components;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class HelicopterSpineEventForwarder_V2 : MonoBehaviour
    {
        private HelicopterController_V2 _controller;
        private SkeletonAnimation _skeletonAnimation;
        private EventData _flyStartedEventData;
        private bool _initialized;

        [Tooltip("Optional. Leave empty when helicopter skeleton has no events.")]
        [SpineEvent] public string flyStartedEventName = "";

        public void Init(HelicopterController_V2 controller, SkeletonAnimation skeletonAnimation)
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
            // Helicopter currently has no authored Spine events; this forwarder is intentionally optional
            // and only forwards if a mapping was explicitly configured in the inspector.
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
