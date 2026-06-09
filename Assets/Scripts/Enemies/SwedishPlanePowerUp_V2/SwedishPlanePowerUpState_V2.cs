namespace iStick2War_V2
{
    /*
     * SwedishPlanePowerUpState_V2 (parachute survival drop)
     *
     * Deploy — parachute + crate descent (one-shot Spine).
     * Land — grounded pickup idle (loop until hero collects).
     * PickedUp — terminal; despawn / pool return.
     */
    public enum SwedishPlanePowerUpState_V2
    {
        Idle,
        Deploy,
        Land,
        PickedUp
    }
}
