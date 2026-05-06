using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class HelicopterView_V2 : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [Tooltip("Helicopter has a single Spine clip in current content.")]
        [SerializeField] private string _singleAnim = "fly";

        private HelicopterStateMachine_V2 _stateMachine;

        public SkeletonAnimation SkeletonAnimation => _skeletonAnimation;

        public void Initialize(HelicopterStateMachine_V2 stateMachine)
        {
            _stateMachine = stateMachine;
            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponent<SkeletonAnimation>();
                if (_skeletonAnimation == null)
                {
                    _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
                }
            }

            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
                _stateMachine.OnStateChanged += HandleStateChanged;
            }

            PlayForState(_stateMachine != null ? _stateMachine.CurrentState : HelicopterState_V2.Idle);
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
            }
        }

        public void ResetVisualStateForSpawn()
        {
            PlayForState(HelicopterState_V2.Idle);
        }

        private void HandleStateChanged(HelicopterState_V2 from, HelicopterState_V2 to)
        {
            PlayForState(to);
        }

        private void PlayForState(HelicopterState_V2 state)
        {
            if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_singleAnim))
            {
                return;
            }

            // Current helicopter rig exposes one clip only ("fly"), so keep playback deterministic
            // across all gameplay states (idle/fly/die) to avoid missing-animation failures.
            _skeletonAnimation.AnimationState.SetAnimation(0, _singleAnim, true);
        }
    }
}
