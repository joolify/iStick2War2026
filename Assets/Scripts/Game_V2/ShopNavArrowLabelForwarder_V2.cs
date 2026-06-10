using UnityEngine;
using UnityEngine.EventSystems;

namespace iStick2War_V2
{
    /*
     * Forwards canvas label clicks (txt_shop_*Item / txt_shop_startNewGame) to ShopNavArrowUiButton_V2.
     * Pressed visual only when a canvas Button also handles the click (avoids double carousel steps).
     */
    [AddComponentMenu("iStick2War/Shop Nav Arrow Label Forwarder V2")]
    [DefaultExecutionOrder(-49)]
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

        private void Update()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || _navButton == null)
            {
                return;
            }

            TryHandleDirectPointerClick();
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

        private void TryHandleDirectPointerClick()
        {
            RectTransform labelRect = transform as RectTransform;
            if (labelRect == null)
            {
                return;
            }

            Camera eventCamera = ResolveEventCamera();

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Ended)
                {
                    continue;
                }

                if (!RectTransformUtility.RectangleContainsScreenPoint(labelRect, touch.position, eventCamera))
                {
                    continue;
                }

                _navButton.TriggerDirectPointerClick();
                return;
            }

            if (Input.touchCount == 0 &&
                Input.GetMouseButtonUp(0) &&
                RectTransformUtility.RectangleContainsScreenPoint(labelRect, Input.mousePosition, eventCamera))
            {
                _navButton.TriggerDirectPointerClick();
            }
        }

        private Camera ResolveEventCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }
    }
}
