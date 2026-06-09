using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SurvivalHighScoreService_V2 (Persistent survival bests)
     *
     * PURPOSE:
     * Stores best wave reached and best total kills for Survival mode in PlayerPrefs (separate from
     * mid-run run_save.json). Updated when a survival run ends in Game Over.
     *
     * NAVIGATION: WaveManager_V2.cs on Game Over; MainMenuSurvivalHighScoreLabel_V2.cs on menu.
     */
    public static class SurvivalHighScoreService_V2
    {
        private const string BestWaveKey = "iStick2War_V2.SurvivalBestWave";
        private const string BestKillsKey = "iStick2War_V2.SurvivalBestKills";

        public static int BestWaveReached => PlayerPrefs.GetInt(BestWaveKey, 0);

        public static int BestTotalKills => PlayerPrefs.GetInt(BestKillsKey, 0);

        public static bool TrySubmitRun(int waveReached, int totalKills, out bool newBestWave, out bool newBestKills)
        {
            waveReached = Mathf.Max(0, waveReached);
            totalKills = Mathf.Max(0, totalKills);

            int previousBestWave = BestWaveReached;
            int previousBestKills = BestTotalKills;

            newBestWave = waveReached > previousBestWave;
            newBestKills = totalKills > previousBestKills;

            if (newBestWave)
            {
                PlayerPrefs.SetInt(BestWaveKey, waveReached);
            }

            if (newBestKills)
            {
                PlayerPrefs.SetInt(BestKillsKey, totalKills);
            }

            if (newBestWave || newBestKills)
            {
                PlayerPrefs.Save();
            }

            return newBestWave || newBestKills;
        }
    }
}
