using UnityEngine;

namespace iStick2War_V2
{
    /*
 * GameOverContinueButton_V2 (World-space game over continue hit target)
 *
 * PURPOSE:
 * Collider2D + OnMouseDown forwards to WaveManager_V2.TryChooseDeathContinue for checkpoint, clutch, or restart.
 *
 * NAVIGATION: WaveManager_V2 death continue tiers; pair with GameOverContinueUi_V2 keyboard fallback.
 */
    [AddComponentMenu("iStick2War/Game Over Continue Button V2")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class GameOverContinueButton_V2 : MonoBehaviour
    {
        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private DeathContinueTier_V2 _tier = DeathContinueTier_V2.CheckpointContinue;
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
            AudioManager_V2.PlayMenuClick();
            if (_waveManager == null)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning($"[GameOverContinueButton_V2] '{name}': WaveManager_V2 not found.");
                }

                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[GameOverContinueButton_V2] '{name}' -> {_tier}");
            }

            _waveManager.TryChooseDeathContinue(_tier);
        }
    }
}
