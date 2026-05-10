using System;

namespace iStick2War_V2
{
    /*
 * IAircraftStateChangedSource_V2
 *
 * Implemented by aircraft state machines so Views can subscribe without a concrete machine type.
 */
    public interface IAircraftStateChangedSource_V2<TState>
        where TState : struct, System.Enum
    {
        event Action<TState, TState> OnStateChanged;

        TState CurrentState { get; }
    }
}
