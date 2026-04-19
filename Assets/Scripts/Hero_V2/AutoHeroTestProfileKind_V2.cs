namespace iStick2War_V2
{
    /// <summary>
    /// Preset bot behaviors for balance runs and telemetry. Does not fake hit rolls — only aim, timing,
    /// and optional suboptimal decisions so runs better resemble human play than a perfect agent.
    /// </summary>
    public enum AutoHeroTestProfileKind_V2
    {
        Perfect = 0,

        /// <summary>Imperfect aim, short engagement delay; still tries to play well.</summary>
        HumanLike = 1,

        /// <summary>Slower to engage, larger aim error, skips optimal weapon switches often, slightly shorter self-imposed shoot range.</summary>
        Struggling = 2,
    }
}
