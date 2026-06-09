namespace iStick2War_V2
{
    /*
     * SwedishPlaneState_V2 (neutral supply aircraft)
     *
     * Idle — before BeginSupplyRun.
     * Fly — horizontal pass; Controller may drop survival powerups.
     * Complete — terminal; pass ended (despawn / pool return).
     */
    public enum SwedishPlaneState_V2
    {
        Idle,
        Fly,
        Complete
    }
}
