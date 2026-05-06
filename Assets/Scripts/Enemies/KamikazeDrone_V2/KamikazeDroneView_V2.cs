using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class KamikazeDroneView_V2 : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [Tooltip("Kamikaze drone currently has one Spine clip.")]
        [SerializeField] private string _singleAnim = "fly";

        private KamikazeDroneStateMachine_V2 _stateMachine;

        public SkeletonAnimation SkeletonAnimation => _skeletonAnimation;

        public void Initialize(KamikazeDroneStateMachine_V2 stateMachine)
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

            PlayForState(_stateMachine != null ? _stateMachine.CurrentState : KamikazeDroneState_V2.Idle);
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
            PlayForState(KamikazeDroneState_V2.Idle);
        }

        private void HandleStateChanged(KamikazeDroneState_V2 from, KamikazeDroneState_V2 to)
        {
            PlayForState(to);
        }

        private void PlayForState(KamikazeDroneState_V2 state)
        {
            if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null || string.IsNullOrWhiteSpace(_singleAnim))
            {
                return;
            }

            _skeletonAnimation.AnimationState.SetAnimation(0, _singleAnim, true);
        }
    }
}
