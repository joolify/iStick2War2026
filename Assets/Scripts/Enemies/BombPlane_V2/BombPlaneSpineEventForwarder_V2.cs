using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class BombPlaneSpineEventForwarder_V2 : MonoBehaviour
    {
        private SkeletonAnimation _skeletonAnimation;
        private bool _initialized;

        public void Init(BombPlaneController_V2 controller, SkeletonAnimation skeletonAnimation)
        {
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
            // Intentionally no Spine event mapping yet for BombPlane.
        }
    }
}
