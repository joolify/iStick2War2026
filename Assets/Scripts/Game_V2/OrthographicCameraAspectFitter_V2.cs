using UnityEngine;

namespace iStick2War_V2
{
    /*
 * OrthographicCameraAspectFitter_V2 (Letterbox-safe orthographic width)
 *
 * PURPOSE:
 * Keeps a 2D orthographic view wide enough for art laid out at a reference aspect (default 16:9).
 * On narrower screens (4:3, 16:10 / Steam Deck) orthographic size increases so left/right content stays visible.
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Main menu boot → MainMenu_V2.cs (auto-attaches to Main Camera)
 */
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-300)]
    [ExecuteAlways]
    public sealed class OrthographicCameraAspectFitter_V2 : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [Tooltip("Orthographic size at the reference aspect below.")]
        [SerializeField] private float _referenceOrthographicSize = 5f;
        [SerializeField] private float _referenceAspectWidth = 16f;
        [SerializeField] private float _referenceAspectHeight = 9f;

        private float _appliedOrthographicSize = -1f;
        private float _lastAspect = -1f;
        private Vector2Int _lastScreenSize;

        private void Reset()
        {
            _camera = GetComponent<Camera>();
        }

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }
        }

        private void OnEnable()
        {
            ApplyIfNeeded(force: true);
        }

        private void Update()
        {
            ApplyIfNeeded(force: false);
        }

        internal void Configure(float referenceOrthographicSize, float referenceAspectWidth, float referenceAspectHeight)
        {
            _referenceOrthographicSize = Mathf.Max(0.01f, referenceOrthographicSize);
            _referenceAspectWidth = Mathf.Max(0.01f, referenceAspectWidth);
            _referenceAspectHeight = Mathf.Max(0.01f, referenceAspectHeight);
            ApplyIfNeeded(force: true);
        }

        private void ApplyIfNeeded(bool force)
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            if (_camera == null || !_camera.orthographic)
            {
                return;
            }

            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            float aspect = screenSize.y > 0 ? (float)screenSize.x / screenSize.y : _camera.aspect;
            if (!force &&
                _lastScreenSize == screenSize &&
                Mathf.Approximately(_lastAspect, aspect) &&
                Mathf.Approximately(_appliedOrthographicSize, _camera.orthographicSize))
            {
                return;
            }

            float referenceAspect = _referenceAspectWidth / _referenceAspectHeight;
            float targetSize = _referenceOrthographicSize;
            if (aspect < referenceAspect)
            {
                targetSize = _referenceOrthographicSize * (referenceAspect / aspect);
            }

            _camera.orthographicSize = targetSize;
            _appliedOrthographicSize = targetSize;
            _lastAspect = aspect;
            _lastScreenSize = screenSize;
        }
    }
}
