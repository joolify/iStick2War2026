namespace iStick2War_V2
{
    /*
     * GameRunModeBootstrap_V2 (Pending mode across scene loads)
     *
     * PURPOSE:
     * Carries the selected GameRunMode_V2 from MainMenu_V2 (or restart-run reload) into SampleScene
     * before WaveManager_V2 Start runs. Continue restores mode from run_save.json instead.
     *
     * NAVIGATION: MainMenu_V2.cs sets pending mode; WaveManager_V2.cs consumes on fresh run.
     */
    public static class GameRunModeBootstrap_V2
    {
        private static bool _hasPendingMode;
        private static GameRunMode_V2 _pendingMode = GameRunMode_V2.Campaign;

        public static bool HasPendingNewRunMode => _hasPendingMode;

        public static void SetPendingNewRunMode(GameRunMode_V2 mode)
        {
            _pendingMode = mode;
            _hasPendingMode = true;
        }

        // Fresh run or scene reload after Game Over restart in the same mode.
        public static GameRunMode_V2 ConsumePendingNewRunMode()
        {
            if (!_hasPendingMode)
            {
                return GameRunMode_V2.Campaign;
            }

            _hasPendingMode = false;
            return _pendingMode;
        }

        public static void CarryModeForSceneReload(GameRunMode_V2 mode)
        {
            SetPendingNewRunMode(mode);
        }
    }
}
