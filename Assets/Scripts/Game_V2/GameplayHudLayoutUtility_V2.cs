using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * Repairs scene-saved NaN RectTransform geometry under gameplay HUD and rebuilds layout groups.
     */
    public static class GameplayHudLayoutUtility_V2
    {
        public static void EnsureGameplayHudLayoutReady(Transform hudCanvasRoot)
        {
            if (hudCanvasRoot == null)
            {
                return;
            }

            RectTransform[] rects = hudCanvasRoot.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                SanitizeRectTransform(rects[i]);
            }

            Transform safeArea = FindDescendantByName(hudCanvasRoot, "SafeAreaRoot");
            if (safeArea is RectTransform safeRect)
            {
                SafeAreaFitter fitter = safeRect.GetComponent<SafeAreaFitter>();
                if (fitter != null)
                {
                    fitter.Refresh();
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(safeRect);

                Transform statsPanel = FindDescendantByName(safeArea, "StatsPanel");
                if (statsPanel is RectTransform statsRect)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(statsRect);

                    Transform top = statsPanel.Find("Top");
                    if (top is RectTransform topRect)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(topRect);
                    }
                }
            }

            Canvas.ForceUpdateCanvases();
        }

        public static void SanitizeRectTransform(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            Vector2 pos = rect.anchoredPosition;
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y))
            {
                rect.anchoredPosition = Vector2.zero;
            }

            Vector2 size = rect.sizeDelta;
            if (float.IsNaN(size.x) || float.IsNaN(size.y))
            {
                rect.sizeDelta = Vector2.zero;
            }

            Vector3 scale = rect.localScale;
            if (float.IsNaN(scale.x) || float.IsNaN(scale.y) || float.IsNaN(scale.z) ||
                scale.sqrMagnitude < 0.001f)
            {
                rect.localScale = Vector3.one;
            }
        }

        private static Transform FindDescendantByName(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            if (root.name.Equals(exactName, System.StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendantByName(root.GetChild(i), exactName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
