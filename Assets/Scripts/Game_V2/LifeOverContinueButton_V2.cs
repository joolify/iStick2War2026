using UnityEngine;

namespace iStick2War_V2
{
    /*
 * LifeOverContinueButton_V2 (Life lost — continue hit target)
 *
 * PURPOSE:
 * Collider2D + OnMouseDown on e.g. TextBTN_MediumStartNewGame restarts the current wave after the player lost a life.
 *
 * NAVIGATION: WaveManager_V2.TryContinueAfterLifeLost; shown while WaveLoopState_V2.LifeOver.
 */
    [AddComponentMenu("iStick2War/Life Over Continue Button V2")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class LifeOverContinueButton_V2 : MonoBehaviour
    {
        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private bool _debugLogs;

        private void Awake()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Exclude);
            }
        }

        private void OnMouseDown()
        {
            if (LifeOverUiClickRouting_V2.IsBlanketLifeOverRoot(gameObject))
            {
                return;
            }

            if (LifeOverUiClickRouting_V2.IsPointerOverGoToShopButton())
            {
                return;
            }

            AudioManager_V2.PlayMenuClick();
            if (_waveManager == null)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning($"[LifeOverContinueButton_V2] '{name}': WaveManager_V2 not found.");
                }

                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[LifeOverContinueButton_V2] '{name}' -> TryContinueAfterLifeLost");
            }

            _waveManager.TryContinueAfterLifeLost();
        }

        public void TriggerAutomationClick()
        {
            OnMouseDown();
        }
    }
}
