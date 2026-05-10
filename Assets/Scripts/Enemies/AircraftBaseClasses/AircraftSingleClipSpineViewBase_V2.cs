using System;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * AircraftSingleClipSpineViewBase_V2
 *
 * One looping Spine clip for all animation-facing states (kamikaze, helicopter).
 */
    public abstract class AircraftSingleClipSpineViewBase_V2<TState> : MonoBehaviour
        where TState : struct, Enum
    {
        [SerializeField] protected SkeletonAnimation _skeletonAnimation;
        [SerializeField] protected string _singleAnim = "fly";

        private IAircraftStateChangedSource_V2<TState> _stateMachine;
        private Action<TState, TState> _stateChangedHandler;

        public SkeletonAnimation SkeletonAnimation => _skeletonAnimation;

        protected abstract TState IdleStateValue { get; }

        public void Initialize(IAircraftStateChangedSource_V2<TState> stateMachine)
        {
            if (_stateMachine != null && _stateChangedHandler != null)
            {
                _stateMachine.OnStateChanged -= _stateChangedHandler;
            }

            _stateMachine = stateMachine;

            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponent<SkeletonAnimation>();
                if (_skeletonAnimation == null)
                {
                    _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
                }
            }

            _stateChangedHandler = HandleStateChanged;
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged += _stateChangedHandler;
            }

            PlayForState(_stateMachine != null ? _stateMachine.CurrentState : IdleStateValue);
        }

        public void ResetVisualStateForSpawn()
        {
            PlayForState(IdleStateValue);
        }

        private void OnDestroy()
        {
            if (_stateMachine != null && _stateChangedHandler != null)
            {
                _stateMachine.OnStateChanged -= _stateChangedHandler;
            }
        }

        private void HandleStateChanged(TState from, TState to)
        {
            PlayForState(to);
        }

        private void PlayForState(TState state)
        {
            if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null ||
                string.IsNullOrWhiteSpace(_singleAnim))
            {
                return;
            }

            _skeletonAnimation.AnimationState.SetAnimation(0, _singleAnim, true);
        }
    }
}
