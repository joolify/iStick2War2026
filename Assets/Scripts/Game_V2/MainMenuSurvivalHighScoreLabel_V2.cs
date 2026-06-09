using TMPro;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * MainMenuSurvivalHighScoreLabel_V2 (Menu high-score line)
     *
     * PURPOSE:
     * Optional TMP label on the main menu showing survival bests from SurvivalHighScoreService_V2.
     * Assign _label or leave unset to resolve txt_mainmenu_survivalHighScore at runtime.
     *
     * NAVIGATION: SurvivalHighScoreService_V2.cs; wire on MainMenuScene canvas.
     */
    [AddComponentMenu("iStick2War/Main Menu Survival High Score Label V2")]
    public sealed class MainMenuSurvivalHighScoreLabel_V2 : MonoBehaviour
    {
        private const string DefaultLabelName = "txt_mainmenu_survivalHighScore";

        [SerializeField] private TMP_Text _label;
        [SerializeField] private string _noScoreText = "Survival best: —";
        [SerializeField] private string _scoreFormat = "Survival best: Wave {0} · {1} kills";

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            ResolveLabelIfNeeded();
            if (_label == null)
            {
                return;
            }

            int bestWave = SurvivalHighScoreService_V2.BestWaveReached;
            int bestKills = SurvivalHighScoreService_V2.BestTotalKills;
            if (bestWave <= 0 && bestKills <= 0)
            {
                _label.text = _noScoreText;
                return;
            }

            _label.text = string.Format(_scoreFormat, bestWave, bestKills);
        }

        private void ResolveLabelIfNeeded()
        {
            if (_label != null)
            {
                return;
            }

            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && text.gameObject.name.Equals(DefaultLabelName, System.StringComparison.Ordinal))
                {
                    _label = text;
                    return;
                }
            }

            _label = GetComponent<TMP_Text>();
        }
    }
}
