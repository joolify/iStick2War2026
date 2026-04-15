using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// World-space BUY button for SpriteRenderer objects.
    /// Uses Collider2D + OnMouseDown instead of Unity UI Button.
    /// </summary>
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
