using UnityEngine;

namespace iStick2War_V2
{
    /*
 * ShopNavArrow_V2 (Carousel previous/next hit target)
 *
 * PURPOSE:
 * Collider2D + OnMouseDown cycles ShopPanel_V2 offers (Previous/Next). Same world-space input pattern as ShopBuyButton_V2.
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Carousel owner → ShopPanel_V2.cs
 * Purchase click → ShopBuyButton_V2.cs
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Direction enum keeps wiring obvious in Inspector; no direct WaveManager coupling.
 */
    [AddComponentMenu("iStick2War/Shop Nav Arrow V2")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class ShopNavArrow_V2 : MonoBehaviour
    {
        public enum ArrowDirection
        {
            Previous,
            Next
        }

        [SerializeField] private ShopPanel_V2 _shopPanel;
        [SerializeField] private ArrowDirection _direction = ArrowDirection.Previous;
        [SerializeField] private bool _debugLogs;

        private void OnMouseDown()
        {
            if (_shopPanel == null)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning($"[ShopNavArrow_V2] '{name}': assign ShopPanel_V2.");
                }

                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[ShopNavArrow_V2] '{name}' OnMouseDown -> {_direction}");
            }

            if (_direction == ArrowDirection.Previous)
            {
                _shopPanel.OnShopArrowPreviousClicked();
            }
            else
            {
                _shopPanel.OnShopArrowNextClicked();
            }
        }
    }
}
