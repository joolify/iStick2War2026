using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * PhoneWeaponArrowsCanvasLayout_V2 - bottom-left weapon switch buttons for mobile.
     * Expects PhoneWeaponArrowsCanvas with LeftArrowButton and RightArrowButton.
     * Direct touch/mouse on button rects when EventSystem input is misconfigured on device builds.
     *
     * NAVIGATION: MobileGameplayTouchInput_V2.cs, HeroInput_V2.cs
     */
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [ExecuteAlways]
    public sealed class PhoneWeaponArrowsCanvasLayout_V2 : MonoBehaviour
    {
        private const string CanvasObjectName = "PhoneWeaponArrowsCanvas";
        private const string LeftArrowButtonName = "LeftArrowButton";
        private const string RightArrowButtonName = "RightArrowButton";

        [SerializeField] private float _leftPadding = 48f;
        [SerializeField] private float _bottomPadding = 36f;
        [SerializeField] private float _buttonSize = 112f;
        [SerializeField] private float _buttonSpacing = 16f;
        // Keep visible on desktop so Left/Right arrow buttons can be clicked in Play Mode (mouse = touch on UI).
        [SerializeField] private bool _showOnDesktopForTesting = true;

        private bool _wired;
        private Hero_V2 _hero;
        private int _lastVisibleWeaponCount = -1;
        private RectTransform _leftArrowButtonRect;
        private RectTransform _rightArrowButtonRect;

        private void Awake()
        {
            ApplyIfNeeded();
        }

        private void OnEnable()
        {
            ApplyIfNeeded();
        }

        public void ApplyIfNeeded()
        {
            bool shouldShow = ShouldShowCanvas();
            gameObject.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            MobileGameplayTouchInput_V2.EnsureInstance();
            EnsureCanvasReady();
            LayoutButtons();
            WireButtonsIfNeeded();
            RefreshWeaponSwitchButtonVisibility();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || !ShouldShowCanvas())
            {
                return;
            }

            RefreshWeaponSwitchButtonVisibility();
        }

        private void Update()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !ShouldShowCanvas())
            {
                return;
            }

            TryHandleDirectWeaponSwitchPointer();
        }

        private bool ShouldShowCanvas()
        {
            return GamePlatform_V2.ShouldShowPhoneWeaponArrows || _showOnDesktopForTesting;
        }

        private void EnsureCanvasReady()
        {
            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            if (canvasRect.localScale.sqrMagnitude < 0.001f)
            {
                canvasRect.localScale = Vector3.one;
            }

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                GameplayHudLayoutUtility_V2.EnsureCanvasReceivesInput(canvas);
                if (canvas.sortingOrder < 250)
                {
                    canvas.sortingOrder = 250;
                }
            }

            StretchFull(canvasRect);
            EnsureSafeAreaRoot();
        }

        private void EnsureSafeAreaRoot()
        {
            Transform existing = transform.Find("SafeAreaRoot");
            RectTransform safeArea = existing as RectTransform;
            if (safeArea == null)
            {
                GameObject safeAreaGo = new GameObject("SafeAreaRoot", typeof(RectTransform), typeof(SafeAreaFitter));
                safeArea = safeAreaGo.GetComponent<RectTransform>();
                safeArea.SetParent(transform, false);
            }

            StretchFull(safeArea);
            ReparentButtonIfNeeded(transform, safeArea, LeftArrowButtonName);
            ReparentButtonIfNeeded(transform, safeArea, RightArrowButtonName);
        }

        private void LayoutButtons()
        {
            Transform safeArea = transform.Find("SafeAreaRoot");
            if (safeArea == null)
            {
                return;
            }

            RectTransform left = FindButtonRect(safeArea, LeftArrowButtonName);
            RectTransform right = FindButtonRect(safeArea, RightArrowButtonName);
            if (left == null || right == null)
            {
                return;
            }

            float half = _buttonSize * 0.5f;
            float leftX = _leftPadding + half;
            float rightX = leftX + _buttonSize + _buttonSpacing;
            float y = _bottomPadding + half;

            ApplyBottomLeftButtonLayout(left, leftX, y, _buttonSize);
            ApplyBottomLeftButtonLayout(right, rightX, y, _buttonSize);
            _leftArrowButtonRect = left;
            _rightArrowButtonRect = right;
        }

        private void RefreshWeaponSwitchButtonVisibility()
        {
            Transform safeArea = transform.Find("SafeAreaRoot");
            if (safeArea == null)
            {
                return;
            }

            ResolveHero();
            int weaponCount = _hero != null ? _hero.GetUnlockedWeaponCount() : 0;
            if (weaponCount == _lastVisibleWeaponCount)
            {
                return;
            }

            _lastVisibleWeaponCount = weaponCount;
            bool showButtons = weaponCount > 1;
            SetButtonActive(safeArea, LeftArrowButtonName, showButtons);
            SetButtonActive(safeArea, RightArrowButtonName, showButtons);
        }

        private void ResolveHero()
        {
            if (_hero != null)
            {
                return;
            }

            _hero = FindAnyObjectByType<Hero_V2>(FindObjectsInactive.Exclude);
        }

        private static void SetButtonActive(Transform root, string buttonName, bool active)
        {
            RectTransform buttonRect = FindButtonRect(root, buttonName);
            if (buttonRect == null)
            {
                return;
            }

            if (buttonRect.gameObject.activeSelf != active)
            {
                buttonRect.gameObject.SetActive(active);
            }
        }

        private void WireButtonsIfNeeded()
        {
            if (_wired)
            {
                return;
            }

            Transform safeArea = transform.Find("SafeAreaRoot");
            if (safeArea == null)
            {
                return;
            }

            WireButton(safeArea, LeftArrowButtonName, OnPreviousWeaponButtonClicked);
            WireButton(safeArea, RightArrowButtonName, OnNextWeaponButtonClicked);
            _wired = true;
        }

        private void OnPreviousWeaponButtonClicked()
        {
            ResolveHero();
            if (_hero != null)
            {
                _hero.RequestSwitchPreviousWeaponFromUi();
                return;
            }

            MobileGameplayTouchInput_V2.EnsureInstance();
            MobileGameplayTouchInput_V2.Instance?.QueueSwitchPreviousWeapon();
        }

        private void OnNextWeaponButtonClicked()
        {
            ResolveHero();
            if (_hero != null)
            {
                _hero.RequestSwitchNextWeaponFromUi();
                return;
            }

            MobileGameplayTouchInput_V2.EnsureInstance();
            MobileGameplayTouchInput_V2.Instance?.QueueSwitchNextWeapon();
        }

        private void TryHandleDirectWeaponSwitchPointer()
        {
            if (_leftArrowButtonRect == null || _rightArrowButtonRect == null)
            {
                Transform safeArea = transform.Find("SafeAreaRoot");
                if (safeArea != null)
                {
                    _leftArrowButtonRect = FindButtonRect(safeArea, LeftArrowButtonName);
                    _rightArrowButtonRect = FindButtonRect(safeArea, RightArrowButtonName);
                }
            }

            if (TryHandleDirectPointerOnButton(_leftArrowButtonRect, OnPreviousWeaponButtonClicked))
            {
                return;
            }

            TryHandleDirectPointerOnButton(_rightArrowButtonRect, OnNextWeaponButtonClicked);
        }

        private static bool TryHandleDirectPointerOnButton(RectTransform buttonRect, UnityEngine.Events.UnityAction onClicked)
        {
            if (buttonRect == null || !buttonRect.gameObject.activeInHierarchy)
            {
                return false;
            }

            Camera eventCamera = ResolveEventCamera(buttonRect);

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Ended)
                {
                    continue;
                }

                if (!RectTransformUtility.RectangleContainsScreenPoint(buttonRect, touch.position, eventCamera))
                {
                    continue;
                }

                onClicked?.Invoke();
                return true;
            }

            if (Input.touchCount == 0 &&
                Input.GetMouseButtonUp(0) &&
                RectTransformUtility.RectangleContainsScreenPoint(buttonRect, Input.mousePosition, eventCamera))
            {
                onClicked?.Invoke();
                return true;
            }

            return false;
        }

        private static Camera ResolveEventCamera(RectTransform buttonRect)
        {
            Canvas canvas = buttonRect != null ? buttonRect.GetComponentInParent<Canvas>() : null;
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        private static void WireButton(Transform root, string buttonName, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform buttonRect = FindButtonRect(root, buttonName);
            if (buttonRect == null)
            {
                return;
            }

            Button button = buttonRect.GetComponent<Button>();
            if (button == null)
            {
                button = buttonRect.gameObject.AddComponent<Button>();
            }

            button.onClick.RemoveListener(onClick);
            button.onClick.AddListener(onClick);
        }

        private static void ReparentButtonIfNeeded(Transform canvasRoot, RectTransform safeArea, string buttonName)
        {
            RectTransform buttonRect = FindButtonRect(canvasRoot, buttonName);
            if (buttonRect == null)
            {
                return;
            }

            if (buttonRect.parent != safeArea)
            {
                buttonRect.SetParent(safeArea, false);
            }
        }

        private static RectTransform FindButtonRect(Transform root, string objectName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    candidate.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate as RectTransform;
                }
            }

            return null;
        }

        private static void ApplyBottomLeftButtonLayout(RectTransform buttonRect, float x, float y, float buttonSize)
        {
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.zero;
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(buttonSize, buttonSize);
            buttonRect.anchoredPosition = new Vector2(x, y);
            buttonRect.localScale = Vector3.one;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        public static PhoneWeaponArrowsCanvasLayout_V2 EnsureFromScene()
        {
            Transform canvas = FindCanvasTransform();
            if (canvas == null)
            {
                return null;
            }

            PhoneWeaponArrowsCanvasLayout_V2 layout = canvas.GetComponent<PhoneWeaponArrowsCanvasLayout_V2>();
            if (layout == null)
            {
                layout = canvas.gameObject.AddComponent<PhoneWeaponArrowsCanvasLayout_V2>();
            }

            layout.ApplyIfNeeded();
            return layout;
        }

        private static Transform FindCanvasTransform()
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    candidate.name.Equals(CanvasObjectName, System.StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
