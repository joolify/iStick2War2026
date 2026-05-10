namespace iStick2War_V2
{
    /*
 * IAircraftStateMirror_V2
 *
 * Implemented by aircraft Models so a generic state machine can mirror CurrentState into runtime data.
 */
    public interface IAircraftStateMirror_V2<TState>
        where TState : struct, System.Enum
    {
        TState currentState { get; set; }
    }
}
