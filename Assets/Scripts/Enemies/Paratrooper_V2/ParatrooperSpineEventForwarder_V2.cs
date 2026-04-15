using Assets.Scripts.Components;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Assets.Scripts.Enemies.Paratrooper_V2
{
    /// <summary>
    /// ParatrooperSpineEventForwarder (Animation Event Bridge)
    /// </summary>
    /// <remarks>
    /// Acts as a bridge between Spine animation events and the gameplay systems.
    /// This component listens to Spine events and forwards them to the Controller
    /// without interpreting their meaning.
    ///
    /// Event Flow:
    /// Spine → ParatrooperSpineEventForwarder → Controller → Gameplay Systems
    ///
    /// Responsibilities:
    /// - Listens to Spine animation events
    /// - Forwards event identifiers to the ParatrooperController
    ///
    /// Constraints:
    /// - MUST NOT contain gameplay logic
    /// - MUST NOT interpret event meaning
    /// - MUST remain a thin forwarding layer only
    ///
    /// Notes:
    /// - Part of the View layer (acts as a “sensor”)
    /// - Keeps animation system decoupled from gameplay logic
    /// - Enables designers/animators to trigger gameplay via Spine safely
    /// 
    /// Animator (Spine)
    ///    ↓
    /// Event("Shoot")
    ///    ↓
    /// Forwarder
    ///    ↓
    /// Controller
    ///    ↓
    /// Gameplay systems
    /// 
    /// </remarks>
    public class ParatrooperSpineEventForwarder_V2 : MonoBehaviour
    {
        private ParatrooperController_V2 _controller;
        private SkeletonAnimation _skeletonAnimation;

        [SpineEvent] public string deployStartedEventName;
        [SpineEvent] public string deployFinishedEventName;
        [SpineEvent] public string grenadeStartedEventName;
        [SpineEvent] public string grenadeFinishedEventName;
        [SpineEvent] public string grenadeThrowEventName;
        [SpineEvent] public string landStartedEventName;
        [SpineEvent] public string landFinishedEventName;
        [SpineEvent] public string reloadStartedEventName;
        [SpineEvent] public string reloadFinishedEventName;
        [SpineEvent] public string shootStartedEventName;
        [SpineEvent] public string shootFinishedEventName;
        [SerializeField] private bool _debugEventLogs = false;

        private EventData _deployStartedEventData;
        private EventData _deployFinishedEventData;
        private EventData _grenadeStartedEventData;
        private EventData _grenadeFinishedEventData;
        private EventData _grenadeThrowEventData;
        private EventData _landStartedEventData;
        private EventData _landFinishedEventData;
        private EventData _reloadStartedEventData;
        private EventData _reloadFinishedEventData;
        private EventData _shootStartedEventData;
        private EventData _shootFinishedEventData;

        private bool _initialized;

        public void Init(ParatrooperController_V2 controller, SkeletonAnimation skeletonAnimation)
        {
            _controller = controller;
            _skeletonAnimation = skeletonAnimation;

            _deployStartedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(deployStartedEventName);
            _deployFinishedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(deployFinishedEventName);
            _grenadeStartedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(grenadeStartedEventName);
            _grenadeFinishedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(grenadeFinishedEventName);
            _grenadeThrowEventData = _skeletonAnimation.Skeleton.Data.FindEvent(grenadeThrowEventName);
            _landStartedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(landStartedEventName);
            _landFinishedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(landFinishedEventName);
            _reloadStartedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(reloadStartedEventName);
            _reloadFinishedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(reloadFinishedEventName);
            _shootStartedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(shootStartedEventName);
            _shootFinishedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(shootFinishedEventName);

            if (_shootStartedEventData == null)
            {
                Debug.LogWarning("[ParatrooperSpineEventForwarder_V2] shootStartedEventName is not mapped in SkeletonData. Fallback name matching will be used.");
            }
            if (_shootFinishedEventData == null)
            {
                Debug.LogWarning("[ParatrooperSpineEventForwarder_V2] shootFinishedEventName is not mapped in SkeletonData. Fallback name matching will be used.");
            }

            _skeletonAnimation.AnimationState.Event += OnSpineEvent;

            _initialized = true;
        }

        private void OnDestroy()
        {
            if (_initialized && _skeletonAnimation != null)
            {
                _skeletonAnimation.AnimationState.Event -= OnSpineEvent;
            }
        }

        /// <summary>
        /// Called by Spine when an animation event is fired.
        /// Forwards the event name to the Controller for interpretation.
        /// </summary>
        public void OnSpineEvent(Spine.TrackEntry trackEntry, Spine.Event e)
        {
            if (_controller == null)
                return;

            if (_debugEventLogs)
            {
                Debug.Log($"ParatrooperSpineEventForwarder: Received event '{e.Data.Name}' at time {e.Time} (int: {e.Int}, float: {e.Float}, string: '{e.String}')");
            }
            string eventName = e.Data != null ? e.Data.Name : string.Empty;
            string normalized = string.IsNullOrEmpty(eventName) ? string.Empty : eventName.Trim().ToLowerInvariant();

            // ✅ Use EventData instead of string compare (faster & safer)
            if (e.Data == _deployStartedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.DeployStarted);
            }
            else if (e.Data == _deployFinishedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.DeployFinished);
            }
            else if (e.Data == _grenadeStartedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.GrenadeStarted);
            }
            else if (e.Data == _grenadeFinishedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.GrenadeFinished);
            }
            else if (e.Data == _grenadeThrowEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.GrenadeThrow);
            }
            else if (e.Data == _landStartedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.LandStarted);
            }
            else if (e.Data == _landFinishedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.LandFinished);
            }
            else if (e.Data == _reloadStartedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.ReloadStarted);
            }
            else if (e.Data == _reloadFinishedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.ReloadFinished);
            }
            else if (e.Data == _shootStartedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.ShootStarted);
            }
            else if (e.Data == _shootFinishedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.ShootFinished);
            }
            else if (normalized == "start_shoot" || normalized == "shoot_started")
            {
                _controller.OnAnimationEvent(AnimationEventType.ShootStarted);
            }
            else if (normalized == "stop_shoot" || normalized == "shoot_finished")
            {
                _controller.OnAnimationEvent(AnimationEventType.ShootFinished);
            }
            else
            {
                // fallback (optional) FIXME
                _controller.OnAnimationEvent(AnimationEventType.None);
            }
        }
    }
}
