using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// World-space shop arrows using SpriteRenderer: uses Collider2D + OnMouseDown (UI Button does not raycast sprites).
    /// </summary>
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
