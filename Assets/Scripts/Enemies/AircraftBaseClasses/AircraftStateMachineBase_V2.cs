using System;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * AircraftStateMachineBase_V2
 *
 * Generic state owner for V2 aircraft stacks: mirrors CurrentState onto TModel, raises OnStateChanged,
 * and treats TerminalState as sticky (no further transitions).
 */
    public abstract class AircraftStateMachineBase_V2<TState, TModel> : MonoBehaviour,
        IAircraftStateChangedSource_V2<TState>
        where TState : struct, Enum
        where TModel : MonoBehaviour, IAircraftStateMirror_V2<TState>
    {
        private TState _currentState;
        private TModel _model;

        public event Action<TState, TState> OnStateChanged;

        public TState CurrentState => _currentState;

        protected abstract TState IdleState { get; }

        protected abstract TState TerminalState { get; }

        public void Initialize(TModel model)
        {
            _model = model;
            ResetForSpawn();
        }

        public virtual void ResetForSpawn()
        {
            _currentState = IdleState;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }
        }

        public void ChangeState(TState newState)
        {
            if (EqualityComparer<TState>.Default.Equals(newState, _currentState))
            {
                return;
            }

            if (EqualityComparer<TState>.Default.Equals(_currentState, TerminalState))
            {
                return;
            }

            TState previous = _currentState;
            _currentState = newState;
            if (_model != null)
            {
                _model.currentState = _currentState;
            }

            OnStateChanged?.Invoke(previous, newState);
        }
    }
}
