using Assets.Scripts.Components;
using iStick2War;
using Spine;
using Spine.Unity;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Enemies.Paratrooper_V2
{
    /// <summary>
    /// SpineEventForwarder (Animation Event Bridge)
    /// </summary>
    /// <remarks>
    /// Acts as a bridge between Spine animation events and the gameplay systems.
    /// This component listens to Spine events and forwards them to the Controller
    /// without interpreting their meaning.
    ///
    /// Event Flow:
    /// Spine → SpineEventForwarder → Controller → Gameplay Systems
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
    public class SpineEventForwarder : MonoBehaviour
    {
        private ParatrooperController_V2 _controller;
        private SkeletonAnimation _skeletonAnimation;

        [SpineEvent] public string shootEventName;
        [SpineEvent] public string grenadeEventName;

        private EventData _shootEventData;
        private EventData _grenadeEventData;

        private bool _initialized;

        public void Initialize(ParatrooperController_V2 controller, SkeletonAnimation skeletonAnimation)
        {
            _controller = controller;
            _skeletonAnimation = skeletonAnimation;

            _shootEventData = _skeletonAnimation.Skeleton.Data.FindEvent(shootEventName);
            _grenadeEventData = _skeletonAnimation.Skeleton.Data.FindEvent(grenadeEventName);

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

            // ✅ Use EventData instead of string compare (faster & safer)
            if (e.Data == _shootEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.Shoot);
            }
            else if (e.Data == _grenadeEventData)
            {
                _controller.OnAnimationEvent(AnimationEventType.Grenade);
            }
            else
            {
                // fallback (optional)
                _controller.OnAnimationEvent(AnimationEventType.None);
            }
        }
    }
}
