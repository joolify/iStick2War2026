using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * AircraftDualClipSpineViewBase_V2
 *
 * Fly loop + optional one-shot drop clip when state matches DropBombStateValue (bomb drone, bomb plane).
 */
    public abstract class AircraftDualClipSpineViewBase_V2<TState> : MonoBehaviour
        where TState : struct, Enum
    {
        [SerializeField] protected SkeletonAnimation _skeletonAnimation;
        [SerializeField] protected string _flyAnim = "fly";
        [SerializeField] protected string _dropBombAnim = "dropBomb";

        private IAircraftStateChangedSource_V2<TState> _stateMachine;
        private Action<TState, TState> _stateChangedHandler;

        public SkeletonAnimation SkeletonAnimation => _skeletonAnimation;

        protected abstract TState IdleStateValue { get; }

        protected abstract TState DropBombStateValue { get; }

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
            if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null)
            {
                return;
            }

            if (EqualityComparer<TState>.Default.Equals(state, DropBombStateValue) &&
                !string.IsNullOrWhiteSpace(_dropBombAnim))
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
