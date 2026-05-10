using UnityEngine;

namespace iStick2War_V2
{
    /*
 * ShopStartWaveButton_V2 (Leave shop → next wave)
 *
 * PURPOSE:
 * Collider2D + OnMouseDown resolves WaveManager_V2 (direct reference or FindAnyObjectByType) and calls
 * StartNextWaveFromShop so behaviour matches keyboard Continue and top-bar wave intro. Optional ShopPanel_V2 fallback.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Encode wave difficulty (WaveManager_V2 + configs).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Next wave + shop exit → WaveManager_V2.cs (StartNextWaveFromShop)
 * Optional fallback → ShopPanel_V2.cs
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Thin bridge so world-space CONTINUE art does not depend on legacy ShopPanel.OnStartNextWaveClicked alone.
 */
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
