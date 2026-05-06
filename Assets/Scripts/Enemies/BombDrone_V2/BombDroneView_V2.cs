using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class BombDroneView_V2 : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [SerializeField] private string _flyAnim = "fly";
        [SerializeField] private string _dropBombAnim = "dropBomb";

        private BombDroneStateMachine_V2 _stateMachine;

        public SkeletonAnimation SkeletonAnimation => _skeletonAnimation;

        public void Initialize(BombDroneStateMachine_V2 stateMachine)
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

            PlayForState(_stateMachine != null ? _stateMachine.CurrentState : BombDroneState_V2.Idle);
        }

        public void ResetVisualStateForSpawn()
        {
            PlayForState(BombDroneState_V2.Idle);
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(BombDroneState_V2 from, BombDroneState_V2 to)
        {
            PlayForState(to);
        }

        private void PlayForState(BombDroneState_V2 state)
        {
            if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null)
            {
                return;
            }

            if (state == BombDroneState_V2.DropBomb && !string.IsNullOrWhiteSpace(_dropBombAnim))
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
