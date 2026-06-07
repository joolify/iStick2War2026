using UnityEngine;

[ExecuteAlways]
public sealed class SafeAreaFitter : MonoBehaviour
{
    [SerializeField] private RectTransform target;

    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;

    private void Reset()
    {
        target = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (_lastSafeArea != Screen.safeArea ||
            _lastScreenSize.x != Screen.width ||
            _lastScreenSize.y != Screen.height)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (target == null)
        {
            target = GetComponent<RectTransform>();
        }

        if (target == null)
            return;

        Rect safeArea = Screen.safeArea;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        target.anchorMin = anchorMin;
        target.anchorMax = anchorMax;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;

        _lastSafeArea = safeArea;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }
}