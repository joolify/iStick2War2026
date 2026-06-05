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
        private const int LifeOverCanvasSortingOrderOffset = 10;
        private static readonly Vector2 DefaultCanvasSize = new Vector2(1920f, 1080f);

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
                new Vector2(0f, 55f),
                new Vector2(920f, 90f),
                30);
            changed |= EnsureLabel(
                canvasRoot,
                referenceLabel,
                StartLabelName,
                "Start Game",
                new Vector2(0f, -35f),
                new Vector2(520f, 50f),
                34);
            changed |= EnsureLabel(
                canvasRoot,
                referenceLabel,
                GoToShopLabelName,
                goToShopLabel,
                new Vector2(0f, -95f),
                new Vector2(520f, 50f),
                34);

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
                ApplyCanvasFromShopReference(existing.gameObject, shopCanvas);
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

        // Shop-canvas often uses localScale 0; LifeOver text must use scale 1 and a real rect size to render.
        public static void ApplyVisibleCanvasLayout(GameObject canvasGo)
        {
            if (canvasGo == null)
            {
                return;
            }

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                return;
            }

            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.anchoredPosition = Vector2.zero;
            if (canvasRect.sizeDelta.sqrMagnitude < 4f)
            {
                canvasRect.sizeDelta = DefaultCanvasSize;
            }

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            canvas.overrideSorting = true;
            Canvas shopCanvas = FindShopCanvasReference();
            if (shopCanvas != null)
            {
                canvas.renderMode = shopCanvas.renderMode;
                canvas.worldCamera = shopCanvas.worldCamera;
                canvas.planeDistance = shopCanvas.planeDistance;
                canvas.sortingLayerID = shopCanvas.sortingLayerID;
                canvas.sortingOrder = shopCanvas.sortingOrder + LifeOverCanvasSortingOrderOffset;
            }
            else if (canvas.sortingOrder < 210)
            {
                canvas.sortingOrder = 210;
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
