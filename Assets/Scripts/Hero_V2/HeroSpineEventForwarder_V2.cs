using Assets.Scripts.Components;
using Spine;
using Spine.Unity;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    /// <summary>
    /// HeroSpineEventForwarder_V2 (Animation Event Bridge)
    /// </summary>
    /// <remarks>
    /// Acts as a bridge between Spine animation events and the gameplay systems.
    /// This component listens to Spine events and forwards them to the Controller
    /// without interpreting their meaning.
    ///
    /// ---------------------------------------------------------
    /// EVENT FLOW:
    ///
    /// Spine → HeroSpineEventForwarder_V2 → HeroController_V2 → Gameplay Systems
    ///
    /// ---------------------------------------------------------
    /// RESPONSIBILITIES:
    ///
    /// - Listens to Spine animation events
    /// - Forwards raw event identifiers to HeroController_V2
    ///
    /// ---------------------------------------------------------
    /// CONSTRAINTS:
    ///
    /// - MUST NOT contain gameplay logic
    /// - MUST NOT interpret event meaning
    /// - MUST NOT trigger systems directly (movement, combat, state changes)
    /// - MUST remain a thin forwarding layer only
    ///
    /// ---------------------------------------------------------
    /// ARCHITECTURAL ROLE:
    ///
    /// - Part of the View layer (acts as a "sensor")
    /// - Keeps animation system fully decoupled from gameplay logic
    /// - Allows animators/designers to trigger gameplay events safely via Spine
    ///
    /// ---------------------------------------------------------
    /// VISUAL FLOW:
    ///
    /// Animator (Spine)
    ///    ↓
    /// Event("Attack", "Jump", "Land", etc.)
    ///    ↓
    /// HeroSpineEventForwarder_V2
    ///    ↓
    /// HeroController_V2
    ///    ↓
    /// Gameplay Systems
    /// </remarks>
    internal class HeroSpineEventForwarder_V2 : MonoBehaviour
    {
        private HeroController_V2 _controller;
        private SkeletonAnimation _skeletonAnimation;

        [SpineEvent] public string shootStartedEventName;
        [SpineEvent] public string shootFinishedEventName;

        private EventData _shootStartedEventData;
        private EventData _shootFinishedEventData;

        private bool _initialized;

        public void Init(HeroController_V2 controller, SkeletonAnimation skeletonAnimation)
        {
            _controller = controller;
            _skeletonAnimation = skeletonAnimation;

            _shootStartedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(shootStartedEventName);
            _shootFinishedEventData = _skeletonAnimation.Skeleton.Data.FindEvent(shootFinishedEventName);

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

            Debug.Log($"HeroSpineEventForwarder: Received event '{e.Data.Name}' at time {e.Time} (int: {e.Int}, float: {e.Float}, string: '{e.String}')");

            // ✅ Use EventData instead of string compare (faster & safer)
            if (e.Data == _shootStartedEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.ShootStarted);
            }
            else if (e.Data == _shootFinishedEventData)
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
