using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * AircraftSpineEventForwarderBase_V2
 *
 * Subscribes / unsubscribes Spine AnimationState.Event; subclasses map raw events to gameplay.
 */
    public abstract class AircraftSpineEventForwarderBase_V2 : MonoBehaviour
    {
        private SkeletonAnimation _skeletonAnimation;
        private bool _initialized;

        public void Init(SkeletonAnimation skeletonAnimation)
        {
            if (_initialized && _skeletonAnimation != null && _skeletonAnimation.AnimationState != null)
            {
                _skeletonAnimation.AnimationState.Event -= OnSpineEvent;
                _initialized = false;
            }

            _skeletonAnimation = skeletonAnimation;
            if (_skeletonAnimation != null && _skeletonAnimation.AnimationState != null)
            {
                _skeletonAnimation.AnimationState.Event += OnSpineEvent;
                _initialized = true;
            }
        }

        private void OnDestroy()
        {
            if (_initialized && _skeletonAnimation != null && _skeletonAnimation.AnimationState != null)
            {
                _skeletonAnimation.AnimationState.Event -= OnSpineEvent;
            }
        }

        private void OnSpineEvent(TrackEntry trackEntry, Spine.Event e)
        {
            OnSpineAnimationEvent(trackEntry, e);
        }

        protected abstract void OnSpineAnimationEvent(TrackEntry trackEntry, Spine.Event e);
    }
}
