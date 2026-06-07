using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * Ensures LifeOver-canvas + txt_lifeOver_info / txt_lifeOver_startNewGame exist under LifeOver V2.
     * Used when the scene only has background + continue button (labels were never created).
     */
    public static class LifeOverUiFactory_V2
    {
        private const string ChromeRootName = "LifeOver V2";
        private const string CanvasName = "LifeOver-canvas";
        private const string InfoTextName = "txt_lifeOver_info";
        private const string StartLabelName = "txt_lifeOver_startNewGame";
        private const string GoToShopLabelName = "txt_lifeOver_goToShop";
        private const string GoToMainMenuLabelName = "txt_lifeOver_goToMainMenu";
        private const int LifeOverCanvasSortingOrderOffset = 10;
        private static readonly Vector2 InfoLabelAnchoredPosition = new Vector2(0f, 55f);
        private static readonly Vector2 InfoLabelSize = new Vector2(920f, 90f);
        private static readonly Vector2 StartLabelAnchoredPosition = new Vector2(0f, -35f);
        private static readonly Vector2 StartLabelSize = new Vector2(520f, 50f);
        private static readonly Vector2 GoToShopLabelAnchoredPosition = new Vector2(0f, -95f);
        private static readonly Vector2 GoToShopLabelSize = new Vector2(520f, 50f);
        // Mirrors hand-placed start label column (e.g. x=568) on the left for Main menu.
        private static readonly Vector2 GoToMainMenuLabelAnchoredPosition = new Vector2(-568f, -419f);
        private static readonly Vector2 GoToMainMenuLabelSize = new Vector2(520f, 50f);

        public static bool EnsureLabelsExist(string infoMessage, bool logWhenChanged)
        {
            return EnsureLabelsExist(infoMessage, "Go to shop", logWhenChanged);
        }

        public static bool EnsureLabelsExist(string infoMessage, string goToShopLabel, bool logWhenChanged)
        {
            Transform chromeRoot = FindChromeRoot();
            if (chromeRoot == null)
            {
                if (logWhenChanged)
                {
                    Debug.LogWarning($"[LifeOverUiFactory_V2] '{ChromeRootName}' not found in loaded scenes.");
                }

                return false;
            }

            Canvas shopCanvas = FindShopCanvasReference();
            Transform canvasRoot = EnsureLifeOverCanvas(chromeRoot, shopCanvas, logWhenChanged);
            if (canvasRoot == null)
            {
                return false;
            }

            TMP_Text referenceLabel = FindReferenceTmpLabel();
            bool changed = false;
            changed |= EnsureLabel(
                canvasRoot,
                referenceLabel,
                InfoTextName,
                infoMessage,
                InfoLabelAnchoredPosition,
                InfoLabelSize,
                30);
            changed |= EnsureLabel(
                canvasRoot,
                referenceLabel,
                StartLabelName,
                "Start Game",
                StartLabelAnchoredPosition,
                StartLabelSize,
                34);
            changed |= EnsureLabel(
                canvasRoot,
                referenceLabel,
                GoToShopLabelName,
                goToShopLabel,
                GoToShopLabelAnchoredPosition,
                GoToShopLabelSize,
                34);
            changed |= RepairOffScreenLifeOverLabels(canvasRoot);

            if (logWhenChanged && changed)
            {
                Debug.Log("[LifeOverUiFactory_V2] Created or repaired LifeOver text labels under LifeOver-canvas.");
            }

            return true;
        }

        private static Transform FindChromeRoot()
        {
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] transforms = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate != null &&
                        candidate.gameObject.name.Equals(ChromeRootName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static Transform EnsureLifeOverCanvas(Transform chromeRoot, Canvas shopCanvas, bool logWhenChanged)
        {
            Transform existing = chromeRoot.Find(CanvasName);
            if (existing != null)
            {
                ApplyVisibleCanvasLayout(existing.gameObject);
                return existing;
            }

            GameObject canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(chromeRoot, false);
            ApplyCanvasFromShopReference(canvasGo, shopCanvas);
            ApplyVisibleCanvasLayout(canvasGo);

            if (logWhenChanged)
            {
                Debug.Log($"[LifeOverUiFactory_V2] Created '{CanvasName}' under '{ChromeRootName}'.");
            }

            return canvasGo.transform;
        }

        private static void ApplyCanvasFromShopReference(GameObject canvasGo, Canvas shopCanvas)
        {
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            if (shopCanvas != null)
            {
                canvas.renderMode = shopCanvas.renderMode;
                canvas.worldCamera = shopCanvas.worldCamera;
                canvas.planeDistance = shopCanvas.planeDistance;
                canvas.sortingLayerID = shopCanvas.sortingLayerID;
                canvas.sortingOrder = shopCanvas.sortingOrder + LifeOverCanvasSortingOrderOffset;
                canvas.overrideSorting = true;

                CanvasScaler shopScaler = shopCanvas.GetComponent<CanvasScaler>();
                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                if (shopScaler != null && scaler != null)
                {
                    scaler.uiScaleMode = shopScaler.uiScaleMode;
                    scaler.referencePixelsPerUnit = shopScaler.referencePixelsPerUnit;
                    scaler.scaleFactor = shopScaler.scaleFactor;
                    scaler.referenceResolution = shopScaler.referenceResolution;
                    scaler.screenMatchMode = shopScaler.screenMatchMode;
                    scaler.matchWidthOrHeight = shopScaler.matchWidthOrHeight;
                }
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 210;
                canvas.overrideSorting = true;
            }

            canvasGo.layer = shopCanvas != null ? shopCanvas.gameObject.layer : canvasGo.layer;
        }

        // Shop-canvas often uses scale 0; LifeOver text must stay visible. Preserve scene/editor rect + label positions.
        public static void ApplyVisibleCanvasLayout(GameObject canvasGo)
        {
            if (canvasGo == null)
            {
                return;
            }

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one;
            }

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            canvas.enabled = true;
            canvas.overrideSorting = true;

            string canvasName = canvasGo.name;
            if (canvasName.Equals("Shop-canvas", System.StringComparison.OrdinalIgnoreCase) ||
                canvasName.Equals("ShopActionLabels-canvas", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Canvas shopCanvas = FindShopCanvasReference();
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null && shopCanvas != null)
            {
                canvas.worldCamera = shopCanvas.worldCamera;
                canvas.planeDistance = shopCanvas.planeDistance;
            }

            int sortOrder = shopCanvas != null
                ? shopCanvas.sortingOrder + LifeOverCanvasSortingOrderOffset
                : 300;
            if (canvas.sortingOrder < sortOrder)
            {
                canvas.sortingOrder = sortOrder;
            }
        }

        private static bool EnsureLabel(
            Transform canvasRoot,
            TMP_Text referenceLabel,
            string objectName,
            string defaultText,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float fontSize)
        {
            Transform existing = canvasRoot.Find(objectName);
            if (existing != null)
            {
                TMP_Text existingTmp = existing.GetComponent<TMP_Text>();
                if (existingTmp != null)
                {
                    if (!string.IsNullOrEmpty(defaultText))
                    {
                        if (string.IsNullOrWhiteSpace(existingTmp.text) ||
                            objectName.Equals(GoToShopLabelName, System.StringComparison.Ordinal))
                        {
                            existingTmp.text = defaultText;
                        }
                    }

                    // Keep scene/editor placement (e.g. labels over TextBTN_Medium* sprites).
                    return false;
                }
            }

            GameObject labelGo = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(canvasRoot, false);
            labelGo.layer = canvasRoot.gameObject.layer;

            RectTransform rect = labelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;

            TextMeshProUGUI tmp = labelGo.GetComponent<TextMeshProUGUI>();
            if (referenceLabel != null)
            {
                tmp.font = referenceLabel.font;
                tmp.color = referenceLabel.color;
            }
            else
            {
                tmp.color = Color.white;
            }

            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            tmp.text = defaultText ?? string.Empty;
            return true;
        }

        // Only repair legacy shop-duplicated labels parked far off-screen; never overwrite hand-placed scene layout.
        public static bool RepairOffScreenLifeOverLabels(Transform canvasRoot)
        {
            if (canvasRoot == null)
            {
                return false;
            }

            bool changed = false;
            changed |= RepairOffScreenLabelIfNeeded(canvasRoot, InfoTextName, InfoLabelAnchoredPosition, InfoLabelSize);
            changed |= RepairOffScreenLabelIfNeeded(canvasRoot, StartLabelName, StartLabelAnchoredPosition, StartLabelSize);
            changed |= RepairOffScreenLabelIfNeeded(canvasRoot, GoToShopLabelName, GoToShopLabelAnchoredPosition, GoToShopLabelSize);
            changed |= RepairOffScreenLabelIfNeeded(
                canvasRoot,
                GoToMainMenuLabelName,
                GoToMainMenuLabelAnchoredPosition,
                GoToMainMenuLabelSize);
            return changed;
        }

        private static bool RepairOffScreenLabelIfNeeded(
            Transform canvasRoot,
            string objectName,
            Vector2 fallbackAnchoredPosition,
            Vector2 fallbackSizeDelta)
        {
            Transform existing = canvasRoot.Find(objectName);
            if (existing == null)
            {
                return false;
            }

            RectTransform rect = existing as RectTransform;
            if (rect == null || !IsLikelyOffScreenLifeOverLabel(rect))
            {
                return false;
            }

            return ApplyCenteredLabelLayout(existing, fallbackAnchoredPosition, fallbackSizeDelta);
        }

        private static bool IsLikelyOffScreenLifeOverLabel(RectTransform rect)
        {
            if (rect.localScale.sqrMagnitude < 0.25f)
            {
                return true;
            }

            Vector2 pos = rect.anchoredPosition;
            // Legacy shop-panel duplicates (e.g. goToShop parked at x=-593).
            if (pos.x < -300f)
            {
                return true;
            }

            return false;
        }

        private static bool ApplyCenteredLabelLayout(Transform labelTransform, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (labelTransform == null)
            {
                return false;
            }

            RectTransform rect = labelTransform as RectTransform;
            if (rect == null)
            {
                return false;
            }

            bool changed =
                rect.anchorMin != new Vector2(0.5f, 0.5f) ||
                rect.anchorMax != new Vector2(0.5f, 0.5f) ||
                rect.pivot != new Vector2(0.5f, 0.5f) ||
                (rect.anchoredPosition - anchoredPosition).sqrMagnitude > 0.01f ||
                (rect.sizeDelta - sizeDelta).sqrMagnitude > 0.01f ||
                rect.localScale != Vector3.one;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
            return changed;
        }

        private static Canvas FindShopCanvasReference()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null &&
                    canvas.gameObject.name.Equals("Shop-canvas", System.StringComparison.OrdinalIgnoreCase))
                {
                    return canvas;
                }
            }

            return null;
        }

        private static TMP_Text FindReferenceTmpLabel()
        {
            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null &&
                    text.gameObject.name.Equals("txt_shop_money", System.StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    return texts[i];
                }
            }

            return null;
        }
    }
}
