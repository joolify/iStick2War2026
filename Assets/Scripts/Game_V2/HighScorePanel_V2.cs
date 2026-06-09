using TMPro;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * HighScorePanel_V2 (Survival high-score overlay)
     *
     * PURPOSE:
     * Lives on the HighScore V2 root in MainMenuScene. Refreshes TMP labels from SurvivalHighScoreService_V2
     * whenever the panel is shown.
     *
     * NAVIGATION: MainMenu_V2.HandleShowHighScore / HandleHideHighScore; SurvivalHighScoreService_V2.cs
     */
    [AddComponentMenu("iStick2War/High Score Panel V2")]
    public sealed class HighScorePanel_V2 : MonoBehaviour
    {
        private const string DefaultBestWaveLabelName = "txt_highscore_bestWave";
        private const string DefaultBestKillsLabelName = "txt_highscore_bestKills";
        private const string DefaultSummaryLabelName = "txt_highscore_summary";

        [SerializeField] private TMP_Text _bestWaveText;
        [SerializeField] private TMP_Text _bestKillsText;
        [SerializeField] private TMP_Text _summaryText;
        [SerializeField] private string _noScoreText = "No survival run recorded yet.";
        [SerializeField] private string _waveFormat = "Best wave: {0}";
        [SerializeField] private string _killsFormat = "Best kills: {0}";
        [SerializeField] private string _summaryFormat = "Best: Wave {0} · {1} kills";

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            ResolveReferencesIfNeeded();

            int bestWave = SurvivalHighScoreService_V2.BestWaveReached;
            int bestKills = SurvivalHighScoreService_V2.BestTotalKills;
            bool hasScore = bestWave > 0 || bestKills > 0;

            if (_summaryText != null)
            {
                _summaryText.text = hasScore
                    ? string.Format(_summaryFormat, bestWave, bestKills)
                    : _noScoreText;
            }

            if (_bestWaveText != null)
            {
                _bestWaveText.text = hasScore
                    ? string.Format(_waveFormat, bestWave)
                    : _noScoreText;
            }

            if (_bestKillsText != null)
            {
                _bestKillsText.text = hasScore
                    ? string.Format(_killsFormat, bestKills)
                    : string.Empty;
            }
        }

        private void ResolveReferencesIfNeeded()
        {
            if (_bestWaveText == null)
            {
                _bestWaveText = FindTmpInHierarchy(transform, DefaultBestWaveLabelName);
            }

            if (_bestKillsText == null)
            {
                _bestKillsText = FindTmpInHierarchy(transform, DefaultBestKillsLabelName);
            }

            if (_summaryText == null)
            {
                _summaryText = FindTmpInHierarchy(transform, DefaultSummaryLabelName);
            }
        }

        private static TMP_Text FindTmpInHierarchy(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && text.gameObject.name.Equals(objectName, System.StringComparison.Ordinal))
                {
                    return text;
                }
            }

            return null;
        }
    }
}
