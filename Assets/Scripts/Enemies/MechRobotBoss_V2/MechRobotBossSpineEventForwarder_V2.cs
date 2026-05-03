using Assets.Scripts.Components;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>Forwards Spine animation events to <see cref="MechRobotBossController_V2"/> (shoot window only).</summary>
    public sealed class MechRobotBossSpineEventForwarder_V2 : MonoBehaviour
    {
        private MechRobotBossController_V2 _controller;
        private SkeletonAnimation _skeletonAnimation;

        [SpineEvent] public string shootStartedEventName;
        [SpineEvent] public string shootFinishedEventName;
        [SerializeField] private bool _debugEventLogs;

        private EventData _shootStartedEventData;
        private EventData _shootFinishedEventData;
        private bool _initialized;

        public void Init(MechRobotBossController_V2 controller, SkeletonAnimation skeletonAnimation)
        {
            _controller = controller;
            _skeletonAnimation = skeletonAnimation;

            if (_skeletonAnimation != null && _skeletonAnimation.Skeleton != null && _skeletonAnimation.Skeleton.Data != null)
            {
                _shootStartedEventData = string.IsNullOrEmpty(shootStartedEventName)
                    ? null
                    : _skeletonAnimation.Skeleton.Data.FindEvent(shootStartedEventName);
                _shootFinishedEventData = string.IsNullOrEmpty(shootFinishedEventName)
                    ? null
                    : _skeletonAnimation.Skeleton.Data.FindEvent(shootFinishedEventName);
            }

            if (_skeletonAnimation != null)
            {
                _skeletonAnimation.AnimationState.Event += OnSpineEvent;
            }

            _initialized = true;
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
            if (_controller == null || e.Data == null)
            {
                return;
            }

            if (_debugEventLogs)
            {
                Debug.Log($"[MechRobotBossSpineEventForwarder_V2] event='{e.Data.Name}'");
            }

            if (e.Data == _shootStartedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.ShootStarted);
                return;
            }

            if (e.Data == _shootFinishedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.ShootFinished);
                return;
            }

            string normalized = e.Data.Name.Trim().ToLowerInvariant();
            if (normalized == "start_shoot" || normalized == "shoot_started")
            {
                _controller.OnAnimationEvent(AnimationEventType.ShootStarted);
            }
            else if (normalized == "stop_shoot" || normalized == "shoot_finished")
            {
                _controller.OnAnimationEvent(AnimationEventType.ShootFinished);
            }
        }
    }
}
