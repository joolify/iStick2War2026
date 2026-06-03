using UnityEngine;
using UnityEngine.EventSystems;

namespace iStick2War_V2
{
    /*
     * Forwards canvas label clicks (txt_shop_previous / txt_shop_prev / txt_shop_next) to ShopNavArrowUiButton_V2.
     * Pressed visual only when a canvas Button also handles the click (avoids double carousel steps).
     */
    [AddComponentMenu("iStick2War/Shop Nav Arrow Label Forwarder V2")]
    public sealed class ShopNavArrowLabelForwarder_V2 :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        private ShopNavArrowUiButton_V2 _navButton;

        internal void Configure(ShopNavArrowUiButton_V2 navButton)
        {
            _navButton = navButton;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _navButton?.ForwardLabelPointerDown();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _navButton?.ForwardLabelPointerUp();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _navButton?.ForwardLabelPointerExit();
        }
    }
}
