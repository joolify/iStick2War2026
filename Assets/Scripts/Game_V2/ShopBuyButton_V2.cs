using UnityEngine;

namespace iStick2War_V2
{
    /*
 * ShopBuyButton_V2 (World-space shop purchase hit target)
 *
 * PURPOSE:
 * Collider2D + OnMouseDown forwards to ShopPanel_V2.OnPurchaseSelectedOfferClicked so sprite-based shop art works
 * without Unity UI raycasts.
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Shop logic → ShopPanel_V2.cs
 * Leave shop / next wave → ShopStartWaveButton_V2.cs → WaveManager_V2.cs
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Single-responsibility micro component; pair with ShopNavArrow_V2 and ShopStartWaveButton_V2 for full shop UX.
 */
    [AddComponentMenu("iStick2War/Shop Buy Button V2")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class ShopBuyButton_V2 : MonoBehaviour
    {
        [SerializeField] private ShopPanel_V2 _shopPanel;
        [SerializeField] private bool _debugLogs;

        private void OnMouseDown()
        {
            if (_shopPanel == null)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning($"[ShopBuyButton_V2] '{name}': assign ShopPanel_V2.");
                }

                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[ShopBuyButton_V2] '{name}' OnMouseDown -> BUY");
            }

            _shopPanel.OnPurchaseSelectedOfferClicked();
        }
    }
}
