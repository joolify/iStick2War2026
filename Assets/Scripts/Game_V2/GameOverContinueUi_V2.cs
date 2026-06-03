using UnityEngine;
using TMPro;

namespace iStick2War_V2
{
    /*
 * GameOverContinueUi_V2 (Game over continue input + prompt)
 *
 * PURPOSE:
 * Keyboard shortcuts for paid continue / restart while WaveManager_V2 is in GameOver after lives are exhausted.
 *
 * NAVIGATION: WaveManager_V2.TryChooseDeathContinue; optional GameOverContinueButton_V2 for world/UI colliders.
 */
    [AddComponentMenu("iStick2War/Game Over Continue UI V2")]
    public sealed class GameOverContinueUi_V2 : MonoBehaviour
    {
        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private TMP_Text _continuePromptText;
        [SerializeField] private bool _debugLogs;

        private void Awake()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Exclude);
            }

            if (_continuePromptText == null)
            {
                _continuePromptText = FindTmpByName("txt_topbar_gameOver_continue");
            }
        }

        private void Update()
        {
            if (_waveManager == null || _waveManager.State != WaveLoopState_V2.GameOver)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                TryContinue(DeathContinueTier_V2.CheckpointContinue);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                TryContinue(DeathContinueTier_V2.ClutchSave);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                TryContinue(DeathContinueTier_V2.RestartRun);
            }
        }

        public void RefreshPromptFromWaveManager()
        {
            if (_continuePromptText == null || _waveManager == null)
            {
                return;
            }

            _continuePromptText.text =
                $"Checkpoint {_waveManager.CheckpointContinueCost} [1]  |  " +
                $"Clutch {_waveManager.ClutchSaveCost} [2]  |  Restart run [R]";
        }

        private void TryContinue(DeathContinueTier_V2 tier)
        {
            if (_waveManager == null)
            {
                return;
            }

            bool ok = _waveManager.TryChooseDeathContinue(tier);
            if (_debugLogs)
            {
                Debug.Log($"[GameOverContinueUi_V2] {tier} -> {(ok ? "ok" : "failed")}");
            }
        }

        private static TMP_Text FindTmpByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && text.gameObject.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return null;
        }
    }
}
