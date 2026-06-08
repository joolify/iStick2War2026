using UnityEngine;

namespace iStick2War_V2
{
    /*
 * SettingsPanelLayout_V2 (Settings V2 — inset background + canvas layout)
 *
 * PURPOSE:
 * When Settings V2 opens, fits the parchment background inside the camera view with edge padding
 * (contain), normalizes the panel root transform, configures Settings-canvas scaling, and places
 * world Go Back sprites along the background bottom-left when no UI Go Back button is present.
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Menu owner → MainMenu_V2.cs
 * Settings UI canvas → SettingsCanvasLayout_V2.cs
 */
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-245)]
    public sealed class SettingsPanelLayout_V2 : MonoBehaviour
    {
        private static readonly string[] WorldGoBackButtonNames =
        {
            "TextBTN_MediumGoBack",
        };

        [SerializeField] private string _backgroundObjectName = "Settings V2 background";
        [SerializeField] private string _settingsCanvasObjectName = "Settings-canvas";
        [Tooltip("Fraction of half-view width/height kept as margin on each edge (e.g. 0.08 = 8%).")]
        [SerializeField] private float _backgroundEdgePaddingFraction = 0.08f;
        [SerializeField] private float _worldGoBackScreenPaddingX = 0.55f;
        [SerializeField] private float _worldGoBackScreenPaddingY = 0.45f;
        [SerializeField] private float _worldGoBackBelowBackgroundMargin = 0.2f;

        private Vector3 _backgroundReferenceLocalScale = Vector3.one;
        private bool _backgroundReferenceCached;
        private Vector2Int _lastScreenSize;
        private float _lastCameraOrthographicSize = -1f;
        private float _lastCameraAspect = -1f;

        private void OnEnable()
        {
            ApplyIfNeeded();
        }

        private void Update()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            Camera camera = Camera.main;
            float aspect = camera != null ? camera.aspect : 0f;
            float orthoSize = camera != null ? camera.orthographicSize : 0f;
            if (_lastScreenSize == screenSize &&
                Mathf.Approximately(_lastCameraAspect, aspect) &&
                Mathf.Approximately(_lastCameraOrthographicSize, orthoSize))
            {
                return;
            }

            ApplyIfNeeded();
        }

        internal void ApplyIfNeeded()
        {
            NormalizePanelRootTransform();
            ApplyBackgroundInsetFit();
            ApplySettingsCanvasLayout();
            HideWorldGoBackWhenUiPresent();
            ApplyWorldGoBackLayoutIfNeeded();

            Camera camera = Camera.main;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastCameraAspect = camera != null ? camera.aspect : 0f;
            _lastCameraOrthographicSize = camera != null ? camera.orthographicSize : 0f;
        }

        private void NormalizePanelRootTransform()
        {
            Transform root = transform;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        private void ApplyBackgroundInsetFit()
        {
            if (!TryResolveBackground(out Transform backgroundTransform, out SpriteRenderer spriteRenderer))
            {
                return;
            }

            CacheBackgroundReferenceIfNeeded(backgroundTransform, spriteRenderer);

            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic || spriteRenderer.sprite == null)
            {
                return;
            }

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            float edgePadding = Mathf.Clamp(_backgroundEdgePaddingFraction, 0f, 0.4f);
            float insetHalfWidth = halfWidth * (1f - edgePadding);
            float insetHalfHeight = halfHeight * (1f - edgePadding);
            Vector3 spriteSize = spriteRenderer.sprite.bounds.size;
            Vector3 referenceSize = new Vector3(
                spriteSize.x * _backgroundReferenceLocalScale.x,
                spriteSize.y * _backgroundReferenceLocalScale.y,
                1f);
            if (referenceSize.x <= 0.01f || referenceSize.y <= 0.01f)
            {
                return;
            }

            float scaleX = (insetHalfWidth * 2f) / referenceSize.x;
            float scaleY = (insetHalfHeight * 2f) / referenceSize.y;
            float containScale = Mathf.Min(scaleX, scaleY);
            backgroundTransform.localScale = _backgroundReferenceLocalScale * containScale;
            Vector3 cameraPosition = camera.transform.position;
            backgroundTransform.position = new Vector3(cameraPosition.x, cameraPosition.y, backgroundTransform.position.z);
        }

        private void ApplySettingsCanvasLayout()
        {
            Transform canvasTransform = transform.Find(_settingsCanvasObjectName);
            if (canvasTransform == null)
            {
                return;
            }

            SettingsCanvasLayout_V2 layout = canvasTransform.GetComponent<SettingsCanvasLayout_V2>();
            if (layout == null)
            {
                layout = canvasTransform.gameObject.AddComponent<SettingsCanvasLayout_V2>();
            }

            float? parchmentAspect = null;
            Bounds? worldParchmentBounds = null;
            if (TryResolveBackground(out _, out SpriteRenderer backgroundSprite) &&
                backgroundSprite.sprite != null)
            {
                Vector3 spriteSize = backgroundSprite.sprite.bounds.size;
                parchmentAspect = spriteSize.x / Mathf.Max(spriteSize.y, 0.001f);
                worldParchmentBounds = backgroundSprite.bounds;
            }

            layout.ApplyIfNeeded(_backgroundEdgePaddingFraction, parchmentAspect, worldParchmentBounds);
        }

        private void HideWorldGoBackWhenUiPresent()
        {
            Transform worldGoBack = FindChildByNames(transform, WorldGoBackButtonNames);
            if (worldGoBack == null)
            {
                return;
            }

            bool hideWorld = HasActiveUiGoBackButton();
            worldGoBack.gameObject.SetActive(!hideWorld);
        }

        private void ApplyWorldGoBackLayoutIfNeeded()
        {
            if (HasActiveUiGoBackButton())
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic)
            {
                return;
            }

            Transform goBackTransform = FindChildByNames(transform, WorldGoBackButtonNames);
            if (goBackTransform == null)
            {
                return;
            }

            goBackTransform.gameObject.SetActive(true);

            SpriteRenderer goBackSprite = goBackTransform.GetComponent<SpriteRenderer>();
            if (goBackSprite == null)
            {
                goBackSprite = goBackTransform.GetComponentInChildren<SpriteRenderer>(true);
            }

            float halfButtonWidth = 1.4f;
            float halfButtonHeight = 0.55f;
            if (goBackSprite != null && goBackSprite.sprite != null)
            {
                Vector3 bounds = goBackSprite.sprite.bounds.size;
                Vector3 lossyScale = goBackTransform.lossyScale;
                halfButtonWidth = Mathf.Abs(bounds.x * lossyScale.x) * 0.5f;
                halfButtonHeight = Mathf.Abs(bounds.y * lossyScale.y) * 0.5f;
            }

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 cameraPosition = camera.transform.position;
            float worldX = cameraPosition.x - halfWidth + _worldGoBackScreenPaddingX + halfButtonWidth;
            float worldY = cameraPosition.y - halfHeight + _worldGoBackScreenPaddingY + halfButtonHeight;

            if (TryResolveBackground(out Transform _, out SpriteRenderer backgroundSprite) && backgroundSprite != null)
            {
                Bounds backgroundBounds = backgroundSprite.bounds;
                float buttonTop = worldY + halfButtonHeight;
                float buttonLeft = worldX - halfButtonWidth;
                bool overlapsParchment = buttonTop > backgroundBounds.min.y && buttonLeft < backgroundBounds.max.x;
                if (overlapsParchment)
                {
                    worldY = backgroundBounds.min.y - _worldGoBackBelowBackgroundMargin - halfButtonHeight;
                }
            }

            goBackTransform.position = new Vector3(worldX, worldY, goBackTransform.position.z);
        }

        private bool HasActiveUiGoBackButton()
        {
            Transform canvasTransform = transform.Find(_settingsCanvasObjectName);
            if (canvasTransform == null)
            {
                return false;
            }

            Transform uiGoBack = FindChildByNames(canvasTransform, new[] { "Settings_Btn_GoBack" });
            if (uiGoBack == null || !uiGoBack.gameObject.activeInHierarchy)
            {
                return false;
            }

            RectTransform goBackRect = uiGoBack as RectTransform;
            if (goBackRect == null)
            {
                return true;
            }

            RectTransform layoutRoot = canvasTransform.Find("SafeAreaRoot") as RectTransform;
            if (layoutRoot == null)
            {
                layoutRoot = canvasTransform as RectTransform;
            }

            return layoutRoot == null || IsRectVisibleInLayoutRoot(goBackRect, layoutRoot);
        }

        private static bool IsRectVisibleInLayoutRoot(RectTransform element, RectTransform layoutRoot)
        {
            Vector3[] corners = new Vector3[4];
            element.GetWorldCorners(corners);
            Rect layoutRect = layoutRoot.rect;

            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 localPoint = layoutRoot.InverseTransformPoint(corners[i]);
                if (layoutRect.Contains(localPoint))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveBackground(out Transform backgroundTransform, out SpriteRenderer spriteRenderer)
        {
            backgroundTransform = FindChildByName(transform, _backgroundObjectName);
            if (backgroundTransform == null)
            {
                backgroundTransform = FindChildByName(transform, "Settings V2 background");
            }

            if (backgroundTransform == null)
            {
                spriteRenderer = null;
                return false;
            }

            spriteRenderer = backgroundTransform.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = backgroundTransform.GetComponentInChildren<SpriteRenderer>(true);
            }

            return spriteRenderer != null;
        }

        private void CacheBackgroundReferenceIfNeeded(Transform backgroundTransform, SpriteRenderer spriteRenderer)
        {
            if (_backgroundReferenceCached)
            {
                return;
            }

            _backgroundReferenceLocalScale = backgroundTransform.localScale;
            _backgroundReferenceCached = true;
        }

        private static Transform FindChildByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (root.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    candidate.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform FindChildByNames(Transform root, string[] objectNames)
        {
            if (root == null || objectNames == null)
            {
                return null;
            }

            for (int i = 0; i < objectNames.Length; i++)
            {
                Transform found = FindChildByName(root, objectNames[i]);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
