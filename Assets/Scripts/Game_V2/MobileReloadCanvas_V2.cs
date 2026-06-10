using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * MobileReloadCanvas_V2 - center-screen reload button when the magazine is empty on mobile.
     * Expects ReloadCanvas with ReloadButton. Visibility is driven from MobileGameplayBootstrap_V2.
     * Also listens for direct touch on the button rect so reload works when UI EventSystem input is misconfigured.
     *
     * NAVIGATION: MobileGameplayTouchInput_V2.cs, HeroInput_V2.cs, Hero_V2.cs
     */
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class MobileReloadCanvas_V2 : MonoBehaviour
    {
        private const string CanvasObjectName = "ReloadCanvas";
        private const string ReloadButtonName = "ReloadButton";
        private const string PhoneReloadRootName = "PhoneReloadButton";

        [SerializeField] private float _buttonSize = 160f;
        // Visible on desktop Play Mode so ReloadButton can be clicked without a phone build.
        [SerializeField] private bool _showOnDesktopForTesting = true;

        private Hero_V2 _hero;
        private RectTransform _reloadButtonRect;
        private bool _prepared;
        private bool _wired;

        public void Prepare()
        {
            if (_prepared)
            {
                return;
            }

            MobileGameplayTouchInput_V2.EnsureInstance();
            EnsureCanvasReady();
            LayoutReloadButton();
            WireReloadButtonIfNeeded();
            _prepared = true;
        }

        private void Update()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !_prepared)
            {
                return;
            }

            if (!GamePlatform_V2.UseMobileGameplayRules)
            {
                return;
            }

            TryHandleDirectReloadPointer();
        }

        public void RefreshVisibility()
        {
            if (!ShouldUseMobileReloadUi())
            {
                SetCanvasActive(false);
                return;
            }

            Prepare();

            ResolveHero();
            bool show = _hero != null &&
                        _hero.ShouldShowReloadPrompt() &&
                        !_hero.IsReloadingWeapon();
            SetCanvasActive(show);
        }

        private bool ShouldUseMobileReloadUi()
        {
            return GamePlatform_V2.UseMobileGameplayRules ||
                   GamePlatform_V2.ShowMobileReloadUiOnDesktop ||
                   _showOnDesktopForTesting;
        }

        private void SetCanvasActive(bool active)
        {
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }

        private void ResolveHero()
        {
            if (_hero != null)
            {
                return;
            }

            _hero = FindAnyObjectByType<Hero_V2>(FindObjectsInactive.Exclude);
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

            EnsurePhoneReloadRootFullScreen();

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
                if (canvas.sortingOrder < 250)
                {
                    canvas.sortingOrder = 250;
                }
            }

            StretchFull(canvasRect);
        }

        private void LayoutReloadButton()
        {
            RectTransform buttonRect = FindButtonRect(transform, ReloadButtonName);
            if (buttonRect == null)
            {
                return;
            }

            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(_buttonSize, _buttonSize);
            buttonRect.localScale = Vector3.one;
            _reloadButtonRect = buttonRect;
        }

        private void WireReloadButtonIfNeeded()
        {
            if (_wired)
            {
                return;
            }

            RectTransform buttonRect = FindButtonRect(transform, ReloadButtonName);
            if (buttonRect == null)
            {
                return;
            }

            Button button = buttonRect.GetComponent<Button>();
            if (button == null)
            {
                button = buttonRect.gameObject.AddComponent<Button>();
            }

            button.onClick.RemoveListener(OnReloadButtonClicked);
            button.onClick.AddListener(OnReloadButtonClicked);
            _wired = true;
        }

        private void OnReloadButtonClicked()
        {
            ResolveHero();
            if (_hero != null)
            {
                _hero.RequestManualReloadFromUi();
                return;
            }

            MobileGameplayTouchInput_V2.EnsureInstance();
            MobileGameplayTouchInput_V2.Instance?.QueueReload();
        }

        private void EnsurePhoneReloadRootFullScreen()
        {
            Transform parent = transform.parent;
            if (parent == null ||
                !parent.name.Equals(PhoneReloadRootName, System.StringComparison.Ordinal))
            {
                return;
            }

            RectTransform rootRect = parent as RectTransform;
            if (rootRect == null)
            {
                return;
            }

            if (rootRect.localScale.sqrMagnitude < 0.001f)
            {
                rootRect.localScale = Vector3.one;
            }

            StretchFull(rootRect);
        }

        private void TryHandleDirectReloadPointer()
        {
            if (_reloadButtonRect == null)
            {
                _reloadButtonRect = FindButtonRect(transform, ReloadButtonName);
            }

            if (_reloadButtonRect == null)
            {
                return;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Ended)
                {
                    continue;
                }

                if (!RectTransformUtility.RectangleContainsScreenPoint(_reloadButtonRect, touch.position, null))
                {
                    continue;
                }

                OnReloadButtonClicked();
                return;
            }

            // Force Mobile In Editor uses mouse, not touch; handle clicks directly on the button rect.
            if (Input.touchCount == 0 && Input.GetMouseButtonUp(0) &&
                RectTransformUtility.RectangleContainsScreenPoint(_reloadButtonRect, Input.mousePosition, null))
            {
                OnReloadButtonClicked();
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

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        public static MobileReloadCanvas_V2 EnsureFromScene()
        {
            Transform canvas = FindCanvasTransform();
            if (canvas == null)
            {
                return null;
            }

            MobileReloadCanvas_V2 layout = canvas.GetComponent<MobileReloadCanvas_V2>();
            if (layout == null)
            {
                layout = canvas.gameObject.AddComponent<MobileReloadCanvas_V2>();
            }

            layout.Prepare();
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
