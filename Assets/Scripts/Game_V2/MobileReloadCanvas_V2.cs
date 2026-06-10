using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * MobileReloadCanvas_V2 — center-screen reload button when the magazine is empty on mobile.
     * Expects ReloadCanvas with ReloadButton. Visibility is driven from MobileGameplayBootstrap_V2.
     *
     * NAVIGATION: MobileGameplayTouchInput_V2.cs, HeroInput_V2.cs, Hero_V2.cs
     */
    [DisallowMultipleComponent]
    public sealed class MobileReloadCanvas_V2 : MonoBehaviour
    {
        private const string CanvasObjectName = "ReloadCanvas";
        private const string ReloadButtonName = "ReloadButton";

        [SerializeField] private float _buttonSize = 160f;
        // Visible on desktop Play Mode so ReloadButton can be clicked without a phone build.
        [SerializeField] private bool _showOnDesktopForTesting = true;

        private Hero_V2 _hero;
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

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
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
            MobileGameplayTouchInput_V2.EnsureInstance();
            MobileGameplayTouchInput_V2.Instance?.QueueReload();
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
