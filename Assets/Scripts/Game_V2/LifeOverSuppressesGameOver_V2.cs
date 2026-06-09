using UnityEngine;

namespace iStick2War_V2
{
    /*
     * LifeOverSuppressesGameOver_V2 (LifeOver V2 — hide GameOver chrome on show)
     *
     * PURPOSE:
     * When LifeOver V2 is enabled in Play Mode, deactivates GameOver / GameOver V2 / LifeOver V2 old
     * chrome so only one death menu appears. Keep enabled even when LifeOverRuntimeLayout_V2 is disabled.
     *
     * ---------------------------------------------------------
     * NAVIGATION (Game_V2)
     *
     * WaveManager hide path → WaveManager_V2.HideGameOverChromeCompletely
     */
    [DisallowMultipleComponent]
    public sealed class LifeOverSuppressesGameOver_V2 : MonoBehaviour
    {
        private void OnEnable()
        {
            if (Application.isPlaying &&
                gameObject.name.Equals("LifeOver V2", System.StringComparison.OrdinalIgnoreCase))
            {
                LifeOverRuntimeLayout_V2.SuppressGameOverChrome();
            }
        }
    }
}
