using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class BombPlaneView_V2 : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [SerializeField] private string _flyAnim = "fly";
        [SerializeField] private string _dropBombAnim = "";

        private BombPlaneStateMachine_V2 _stateMachine;

        public SkeletonAnimation SkeletonAnimation => _skeletonAnimation;

        public void Initialize(BombPlaneStateMachine_V2 stateMachine)
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

            PlayForState(_stateMachine != null ? _stateMachine.CurrentState : BombPlaneState_V2.Idle);
        }

        public void ResetVisualStateForSpawn()
        {
            PlayForState(BombPlaneState_V2.Idle);
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(BombPlaneState_V2 from, BombPlaneState_V2 to)
        {
            PlayForState(to);
        }

        private void PlayForState(BombPlaneState_V2 state)
        {
            if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null)
            {
                return;
            }

            if (state == BombPlaneState_V2.DropBomb && !string.IsNullOrWhiteSpace(_dropBombAnim))
            {
                _skeletonAnimation.AnimationState.SetAnimation(0, _dropBombAnim, false);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_flyAnim))
            {
                _skeletonAnimation.AnimationState.SetAnimation(0, _flyAnim, true);
            }
        }
    }
}
