using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * Creates a sibling Screen Space Camera canvas for SpriteRenderer HUD props (pause, hearts).
     * TMP stays on the Overlay HUD-Canvas; sprites must live on a camera canvas to render correctly.
     */
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class HudSpritesCanvasBootstrap_V2 : MonoBehaviour
    {
        private const string SpritesCanvasName = "HUD-Sprites-Canvas";
        private const string TopbarSortingLayerName = "Topbar";

        [SerializeField] private Camera _camera;
        [SerializeField] private RectTransform _overlayTopLeft;
        [SerializeField] private RectTransform _overlayTopRight;
        [SerializeField] private RectTransform _pauseButtonHost;
        [SerializeField] private RectTransform _heartLifeBarHost;
        [SerializeField] private float _planeDistance = 1f;
        [SerializeField] private int _sortingOrder = 111;
        [SerializeField] private float _pauseSpriteFill = 0.85f;
        [SerializeField] private float _heartSizeCanvasUnits = 48f;
        [SerializeField] private float _heartSpacingCanvasUnits = 8f;

        private void Awake()
        {
            EnsureOverlayCanvasVisible();
            GameplayHudLayoutUtility_V2.EnsureGameplayHudLayoutReady(transform);

            if (_pauseButtonHost == null && _heartLifeBarHost == null)
            {
                return;
            }

            RectTransform spriteSafeArea = EnsureSpritesCanvas();
            if (spriteSafeArea == null)
            {
                return;
            }

            if (_overlayTopLeft != null && _pauseButtonHost != null)
            {
                RectTransform slot = CreateAnchorSlot(spriteSafeArea, "TopLeft", _overlayTopLeft);
                ReparentHostFillParent(_pauseButtonHost, slot);
            }

            if (_overlayTopRight != null && _heartLifeBarHost != null)
            {
                RectTransform slot = CreateAnchorSlot(spriteSafeArea, "TopRight", _overlayTopRight);
                ReparentHostFillParentTopRight(_heartLifeBarHost, slot);
            }
        }

        private void Start()
        {
            EnsureOverlayCanvasVisible();
            GameplayHudLayoutUtility_V2.EnsureGameplayHudLayoutReady(transform);
            ApplySpriteLayout();
            StartCoroutine(RebuildHudLayoutEndOfFrame());
        }

        private IEnumerator RebuildHudLayoutEndOfFrame()
        {
            yield return null;
            EnsureOverlayCanvasVisible();
            GameplayHudLayoutUtility_V2.EnsureGameplayHudLayoutReady(transform);
            ApplySpriteLayout();
        }

        private void ApplySpriteLayout()
        {
            Canvas.ForceUpdateCanvases();

            if (_pauseButtonHost != null)
            {
                FitSingleSpriteToRect(_pauseButtonHost, _pauseSpriteFill);
            }

            if (_heartLifeBarHost != null)
            {
                LayoutHeartSpritesRightAligned(_heartLifeBarHost, _heartSizeCanvasUnits, _heartSpacingCanvasUnits);
            }
        }

        private RectTransform EnsureSpritesCanvas()
        {
            // Sibling at scene root — nested Screen Space Camera canvas under Overlay HUD breaks sprite rendering.
            Transform existing = FindSpritesCanvasRoot();
            if (existing != null)
            {
                if (existing.parent != null)
                {
                    existing.SetParent(null, false);
                }

                if (existing is RectTransform existingRect)
                {
                    StretchFull(existingRect);
                }

                Canvas existingCanvas = existing.GetComponent<Canvas>();
                if (existingCanvas != null)
                {
                    existingCanvas.enabled = true;
                    if (existingCanvas.worldCamera == null)
                    {
                        existingCanvas.worldCamera = ResolveCamera();
                    }
                }

                SafeAreaFitter fitter = existing.GetComponentInChildren<SafeAreaFitter>();
                if (fitter != null)
                {
                    return fitter.transform as RectTransform;
                }

                return existing as RectTransform;
            }

            var canvasGo = new GameObject(
                SpritesCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.layer = gameObject.layer;
            canvasGo.transform.SetParent(null, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = ResolveCamera();
            canvas.planeDistance = _planeDistance;
            canvas.overrideSorting = true;
            canvas.sortingLayerName = TopbarSortingLayerName;
            canvas.sortingOrder = _sortingOrder;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            CanvasScaler overlayScaler = GetComponent<CanvasScaler>();
            if (overlayScaler != null)
            {
                scaler.uiScaleMode = overlayScaler.uiScaleMode;
                scaler.referenceResolution = overlayScaler.referenceResolution;
                scaler.screenMatchMode = overlayScaler.screenMatchMode;
                scaler.matchWidthOrHeight = overlayScaler.matchWidthOrHeight;
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            StretchFull(canvasRect);

            RectTransform root = CreateChildRect(canvasGo.transform, "HUDSpritesRoot");
            StretchFull(root);

            RectTransform safeArea = CreateChildRect(root, "SafeAreaRoot");
            StretchFull(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            return safeArea;
        }

        private Transform FindSpritesCanvasRoot()
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    candidate.name.Equals(SpritesCanvasName, System.StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private Camera ResolveCamera()
        {
            if (_camera != null)
            {
                return _camera;
            }

            return Camera.main;
        }

        private static RectTransform CreateChildRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform CreateAnchorSlot(RectTransform safeArea, string name, RectTransform template)
        {
            RectTransform slot = CreateChildRect(safeArea, name);
            CopyRectLayout(template, slot);
            return slot;
        }

        private static void ReparentHostFillParent(RectTransform host, RectTransform slot)
        {
            host.SetParent(slot, false);
            StretchFull(host);
        }

        private static void ReparentHostFillParentTopRight(RectTransform host, RectTransform slot)
        {
            host.SetParent(slot, false);
            host.anchorMin = Vector2.one;
            host.anchorMax = Vector2.one;
            host.pivot = Vector2.one;
            host.anchoredPosition = Vector2.zero;
            host.sizeDelta = Vector2.zero;
            host.localScale = Vector3.one;
        }

        private static void CopyRectLayout(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
            to.localRotation = Quaternion.identity;
            to.localScale = Vector3.one;
        }

        // Scene often stores HUD-Canvas at scale 0 to hide it in editor; restore before gameplay HUD binds.
        private void EnsureOverlayCanvasVisible()
        {
            if (transform is not RectTransform canvasRect)
            {
                return;
            }

            StretchFull(canvasRect);

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
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
        }

        private static void FitSingleSpriteToRect(RectTransform host, float fill)
        {
            SpriteRenderer spriteRenderer = host.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            Transform spriteTransform = spriteRenderer.transform;
            float hostWidth = host.rect.width;
            float hostHeight = host.rect.height;
            if (hostWidth <= 0f || hostHeight <= 0f)
            {
                return;
            }

            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            float scale = Mathf.Min(
                hostWidth * fill / spriteSize.x,
                hostHeight * fill / spriteSize.y);
            spriteTransform.localScale = new Vector3(scale, scale, 1f);

            RectTransform spriteRect = spriteTransform as RectTransform;
            if (spriteRect != null)
            {
                spriteRect.anchorMin = new Vector2(0.5f, 0.5f);
                spriteRect.anchorMax = new Vector2(0.5f, 0.5f);
                spriteRect.pivot = new Vector2(0.5f, 0.5f);
                spriteRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                Vector3 localPosition = spriteTransform.localPosition;
                spriteTransform.localPosition = new Vector3(0f, 0f, localPosition.z);
            }
        }

        // heartLife1..3 left-to-right; host pivot is top-right so we lay out from the right edge.
        private static void LayoutHeartSpritesRightAligned(RectTransform host, float heartSize, float spacing)
        {
            SpriteRenderer[] renderers = host.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            System.Array.Sort(renderers, (a, b) => string.CompareOrdinal(a.name, b.name));

            float cursorX = 0f;
            float y = -heartSize * 0.5f;
            for (int i = renderers.Length - 1; i >= 0; i--)
            {
                SpriteRenderer spriteRenderer = renderers[i];
                if (spriteRenderer.sprite == null)
                {
                    continue;
                }

                Transform spriteTransform = spriteRenderer.transform;
                Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
                float maxAxis = Mathf.Max(spriteSize.x, spriteSize.y);
                if (maxAxis <= 0f)
                {
                    continue;
                }

                float scale = heartSize / maxAxis;
                spriteTransform.localScale = new Vector3(scale, scale, 1f);

                float scaledWidth = spriteSize.x * scale;
                cursorX -= scaledWidth;
                spriteTransform.localPosition = new Vector3(
                    cursorX + scaledWidth * 0.5f,
                    y,
                    spriteTransform.localPosition.z);
                cursorX -= spacing;
            }
        }
    }
}
