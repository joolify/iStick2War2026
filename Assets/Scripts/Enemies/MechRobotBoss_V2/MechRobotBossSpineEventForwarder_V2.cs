using Assets.Scripts.Components;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * MechRobotBossSpineEventForwarder_V2 (Animation event bridge)
 *
 * PURPOSE:
 * Subscribes to Spine AnimationState events and maps configured shoot started/finished event names to
 * MechRobotBossController_V2 so MG-style damage can be gated to the open shoot window.
 *
 * ---------------------------------------------------------
 * EVENT FLOW
 *
 * Spine → MechRobotBossSpineEventForwarder_V2 → MechRobotBossController_V2 (shoot window flags)
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Encode weapon damage or ray tests (MechRobotBossWeaponSystem_V2)
 * - Change body state directly (controller / state machine own transitions)
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Thin sensor at the animation boundary, same role shape as other *_SpineEventForwarder_V2 components.
 */
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
