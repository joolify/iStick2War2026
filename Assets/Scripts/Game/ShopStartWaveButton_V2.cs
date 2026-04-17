using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// World-space control to leave the shop and start the next wave (SpriteRenderer + Collider2D).
    /// Prefer calling <see cref="WaveManager_V2.StartNextWaveFromShop"/> so behaviour matches keyboard Continue
    /// and the top-bar wave intro always runs (panel <c>OnStartNextWaveClicked</c> is optional / legacy).
    /// </summary>
    [AddComponentMenu("iStick2War/Shop Start Wave Button V2")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class ShopStartWaveButton_V2 : MonoBehaviour
    {
        [SerializeField] private WaveManager_V2 _waveManager;
        [Tooltip("Optional fallback if WaveManager is not assigned and cannot be found.")]
        [SerializeField] private ShopPanel_V2 _shopPanel;
        [SerializeField] private bool _debugLogs;

        private void OnMouseDown()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>();
            }

            if (_waveManager != null)
            {
                if (_debugLogs)
                {
                    Debug.Log($"[ShopStartWaveButton_V2] '{name}' OnMouseDown -> WaveManager.StartNextWaveFromShop");
                }

                _waveManager.StartNextWaveFromShop();
                return;
            }

            if (_shopPanel == null)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning($"[ShopStartWaveButton_V2] '{name}': assign WaveManager_V2 or ShopPanel_V2.");
                }

                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[ShopStartWaveButton_V2] '{name}' OnMouseDown -> ShopPanel.OnStartNextWaveClicked (fallback)");
            }

            _shopPanel.OnStartNextWaveClicked();
        }
    }
}
