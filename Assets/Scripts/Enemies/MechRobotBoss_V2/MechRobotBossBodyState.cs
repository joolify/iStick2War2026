namespace iStick2War_V2
{
    /*
 * MechRobotBossBodyState (High-level body / animation states)
 *
 * PURPOSE:
 * Discrete states mirrored to MechRobotBossModel_V2 and consumed by MechRobotBossView_V2 for Spine playback.
 * Matches the boss skeleton export: idle, walk, run, aim, shoot, plus terminal Die.
 */
    public enum MechRobotBossBodyState
    {
        Idle,
        Walk,
        Run,
        Aim,
        Shoot,
        Die,
    }
}
