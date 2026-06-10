using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace iStick2War_V2
{
    /*
     * Forwards txt_lifeOver_* canvas label clicks to LifeOver wave actions.
     * Hand-placed TMP labels on LifeOver-canvas often sit over TextBTN_Medium* sprites whose
     * Collider2D hitboxes stay offset — wire labels so clicks on the text still work.
     */
    [AddComponentMenu("iStick2War/Life Over Label UI Button V2")]
    [DefaultExecutionOrder(-50)]
    public sealed class LifeOverLabelUiButton_V2 : MonoBehaviour, IPointerClickHandler
    {
        public enum LifeOverLabelAction
        {
            ContinueAfterLifeLost,
            GoToShopAfterLifeLost,
            GoToMainMenuAfterLifeLost
        }

        [SerializeField] private LifeOverLabelAction _action;

        private WaveManager_V2 _waveManager;
        private int _lastHandledClickFrame = -1;

        public void Configure(LifeOverLabelAction action)
        {
            _action = action;
        }

        private void Awake()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Exclude);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TryExecuteLifeOverAction();
        }

        private void Update()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            TryHandleDirectPointerClick();
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

                TryExecuteLifeOverAction();
                return;
            }

            if (Input.touchCount == 0 &&
                Input.GetMouseButtonUp(0) &&
                RectTransformUtility.RectangleContainsScreenPoint(labelRect, Input.mousePosition, eventCamera))
            {
                TryExecuteLifeOverAction();
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

        private void TryExecuteLifeOverAction()
        {
            if (_lastHandledClickFrame == Time.frameCount)
            {
                return;
            }

            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Exclude);
            }

            if (_waveManager == null || _waveManager.State != WaveLoopState_V2.LifeOver)
            {
                return;
            }

            _lastHandledClickFrame = Time.frameCount;
            AudioManager_V2.PlayMenuClick();
            switch (_action)
            {
                case LifeOverLabelAction.ContinueAfterLifeLost:
                    _waveManager.TryContinueAfterLifeLost();
                    break;
                case LifeOverLabelAction.GoToShopAfterLifeLost:
                    _waveManager.TryGoToShopAfterLifeLost();
                    break;
                case LifeOverLabelAction.GoToMainMenuAfterLifeLost:
                    _waveManager.TryGoToMainMenuAfterLifeLost();
                    break;
            }
        }

        internal static void EnsureOnLabel(TMP_Text label, LifeOverLabelAction action)
        {
            if (label == null)
            {
                return;
            }

            label.raycastTarget = true;
            LifeOverLabelUiButton_V2 handler = label.GetComponent<LifeOverLabelUiButton_V2>();
            if (handler == null)
            {
                handler = label.gameObject.AddComponent<LifeOverLabelUiButton_V2>();
            }

            handler.Configure(action);
        }

        internal static void EnsureInfoLabelNonBlocking(TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            label.raycastTarget = false;
            LifeOverLabelUiButton_V2 handler = label.GetComponent<LifeOverLabelUiButton_V2>();
            if (handler != null)
            {
                Destroy(handler);
            }
        }
    }
}
