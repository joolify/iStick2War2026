using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Temporary world-space button to leave the shop and start the next wave.
    /// Same as ShopPanel_V2.OnStartNextWaveClicked (works with SpriteRenderer + Collider2D).
    /// </summary>
    [AddComponentMenu("iStick2War/Shop Start Wave Button V2")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class ShopStartWaveButton_V2 : MonoBehaviour
    {
        [SerializeField] private ShopPanel_V2 _shopPanel;
        [SerializeField] private bool _debugLogs;

        private void OnMouseDown()
        {
            if (_shopPanel == null)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning($"[ShopStartWaveButton_V2] '{name}': assign ShopPanel_V2.");
                }

                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[ShopStartWaveButton_V2] '{name}' OnMouseDown -> StartNextWave");
            }

            _shopPanel.OnStartNextWaveClicked();
        }
    }
}
