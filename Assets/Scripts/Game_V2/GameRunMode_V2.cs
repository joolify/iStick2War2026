namespace iStick2War_V2
{
    /*
     * GameRunMode_V2 (Campaign vs Survival)
     *
     * PURPOSE:
     * Selects which run rules WaveManager_V2 applies: fixed 15-wave campaign with Game Won, or endless
     * survival with escalating pressure and persistent high scores.
     *
     * NAVIGATION: bootstrap from MainMenu_V2 → GameRunModeBootstrap_V2.cs → WaveManager_V2.cs
     */
    public enum GameRunMode_V2
    {
        Campaign = 0,
        Survival = 1
    }
}
