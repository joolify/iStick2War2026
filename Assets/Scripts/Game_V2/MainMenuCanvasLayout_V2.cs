using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
 * MainMenuCanvasLayout_V2 (Main menu UI canvas — scaler + safe bottom-left column)
 *
 * PURPOSE:
 * Configures Scale With Screen Size, optional SafeAreaRoot, and lays out all MainMenu-canvas buttons
 * in a bottom-left column. Runtime layout is authoritative — only active buttons are stacked (hidden
 * Continue does not leave a gap).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Menu owner → MainMenu_V2.cs
 * Menu buttons → MainMenuNavUiButton_V2.cs
 */
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-250)]
    [ExecuteAlways]
    public sealed class MainMenuCanvasLayout_V2 : MonoBehaviour
    {
        // Top → bottom on screen (Settings is last = bottom of stack).
        private static readonly string[] ButtonNamesTopToBottom =
        {
            "MainMenu_Btn_Continue",
            "MainMenu_Btn_StartCampaign",
            "MainMenu_Btn_StartGame",
            "MainMenu_Btn_StartSurvivalMode",
            "MainMenu_Btn_HighScore",
            "MainMenu_Btn_Settings",
        };

        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private float _matchWidthOrHeight = 0.5f;
        [SerializeField] private float _leftPadding = 72f;
        [SerializeField] private float _buttonWidth = 496f;
        [SerializeField] private float _buttonHeight = 93f;
        [SerializeField] private float _verticalSpacing = 100f;
        [SerializeField] private float _bottomPadding = 48f;
        [SerializeField] private bool _useSafeArea = true;

        private bool _applied;

        private void Awake()
        {
            ApplyIfNeeded();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_applied)
            {
                ApplyButtonLayout();
            }
        }

        internal void ApplyIfNeeded()
        {
            if (_applied)
            {
                ApplyButtonLayout();
                return;
            }

            EnsureCanvasRectReady();
            DetachFromWorldOffsetParentIfNeeded();
            ConfigureCanvasScaler();
            EnsureSafeAreaRootIfNeeded();
            ApplyButtonLayout();
            _applied = true;
        }

        // Call when Continue visibility changes so the stack reflows without gaps.
        public void RefreshButtonLayout()
        {
            if (!_applied)
            {
                ApplyIfNeeded();
                return;
            }

            ApplyButtonLayout();
        }

        private void EnsureCanvasRectReady()
        {
            RectTransform rect = (RectTransform)transform;
            if (rect.localScale.sqrMagnitude < 0.001f)
            {
                rect.localScale = Vector3.one;
            }

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                StretchFull(rect);
            }
        }

        private void DetachFromWorldOffsetParentIfNeeded()
        {
            Transform parent = transform.parent;
            if (parent == null || parent == transform.root)
            {
                return;
            }

            // Keep hierarchy when parent is the menu composition root.
            if (parent.GetComponent<MainMenu_V2>() != null)
            {
                return;
            }

            if (parent.localPosition.sqrMagnitude < 0.0001f &&
                parent.localRotation == Quaternion.identity &&
                parent.localScale == Vector3.one)
            {
                return;
            }

            transform.SetParent(null, worldPositionStays: false);
            RectTransform rect = (RectTransform)transform;
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            StretchFull(rect);
        }

        private void ConfigureCanvasScaler()
        {
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = Mathf.Clamp01(_matchWidthOrHeight);
        }

        private void EnsureSafeAreaRootIfNeeded()
        {
            if (!_useSafeArea)
            {
                return;
            }

            RectTransform canvasRect = (RectTransform)transform;
            Transform existing = canvasRect.Find("SafeAreaRoot");
            RectTransform safeArea = existing as RectTransform;
            if (safeArea == null)
            {
                GameObject safeAreaGo = new GameObject("SafeAreaRoot", typeof(RectTransform), typeof(SafeAreaFitter));
                safeArea = safeAreaGo.GetComponent<RectTransform>();
                safeArea.SetParent(canvasRect, false);
            }

            StretchFull(safeArea);
            ReparentMenuButtonsUnder(safeArea);
        }

        private void ApplyButtonLayout()
        {
            RectTransform layoutRoot = ResolveLayoutRoot();
            if (layoutRoot == null)
            {
                return;
            }

            List<RectTransform> visibleButtons = CollectVisibleMenuButtons(layoutRoot);
            if (visibleButtons.Count == 0)
            {
                return;
            }

            float pivotOffsetX = _leftPadding + (_buttonWidth * 0.5f);
            float bottomY = _bottomPadding + (_buttonHeight * 0.5f);

            for (int i = 0; i < visibleButtons.Count; i++)
            {
                RectTransform buttonRect = visibleButtons[visibleButtons.Count - 1 - i];
                float y = bottomY + (i * _verticalSpacing);
                buttonRect.anchorMin = Vector2.zero;
                buttonRect.anchorMax = Vector2.zero;
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(_buttonWidth, _buttonHeight);
                buttonRect.anchoredPosition = new Vector2(pivotOffsetX, y);
                buttonRect.localScale = Vector3.one;
            }
        }

        private List<RectTransform> CollectVisibleMenuButtons(RectTransform layoutRoot)
        {
            var visibleButtons = new List<RectTransform>(ButtonNamesTopToBottom.Length);
            var added = new HashSet<RectTransform>();

            for (int i = 0; i < ButtonNamesTopToBottom.Length; i++)
            {
                RectTransform buttonRect = FindButtonRect(layoutRoot, ButtonNamesTopToBottom[i]);
                if (buttonRect == null || added.Contains(buttonRect))
                {
                    continue;
                }

                if (!buttonRect.gameObject.activeSelf)
                {
                    continue;
                }

                visibleButtons.Add(buttonRect);
                added.Add(buttonRect);
            }

            return visibleButtons;
        }

        private RectTransform ResolveLayoutRoot()
        {
            if (!_useSafeArea)
            {
                return (RectTransform)transform;
            }

            Transform safeArea = transform.Find("SafeAreaRoot");
            return safeArea as RectTransform ?? (RectTransform)transform;
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

        private void ReparentMenuButtonsUnder(RectTransform safeArea)
        {
            RectTransform canvasRect = (RectTransform)transform;
            var reparented = new HashSet<RectTransform>();

            for (int i = 0; i < ButtonNamesTopToBottom.Length; i++)
            {
                RectTransform buttonRect = FindButtonRect(canvasRect, ButtonNamesTopToBottom[i]);
                if (buttonRect == null || reparented.Contains(buttonRect))
                {
                    continue;
                }

                buttonRect.SetParent(safeArea, worldPositionStays: false);
                reparented.Add(buttonRect);
            }
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }
}
