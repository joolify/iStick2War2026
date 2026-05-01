using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /// <summary>
    /// Keeps a world-space health bar (or any UI root) glued to a target transform, e.g. above a paratrooper head or bunker.
    /// Put on the same GameObject as your <see cref="Canvas"/> (Render Mode: World Space). Tune scale on the canvas root.
    /// </summary>
    public sealed class WorldHealthBarFollower_V2 : MonoBehaviour
    {
        [Tooltip("World object to follow, e.g. paratrooper root, Spine head bone transform, or bunker.")]
        [SerializeField] private Transform _followTarget;

        [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 2.2f, 0f);

        [Tooltip("Billboard toward camera (typical for world-space UI).")]
        [SerializeField] private bool _faceCamera = true;

        [Tooltip(
            "When facing the camera: ON = use the camera's rotation (bar stays parallel to the view plane; best for 2D ortho " +
            "and avoids a slight skew from LookRotation + world up). OFF = legacy point-at-camera pivot.")]
        [SerializeField] private bool _matchCameraRotation = true;

        [SerializeField] private Camera _camera;

        [Tooltip("When true, disables this behaviour if Follow Target is null (avoids warnings every frame).")]
        [SerializeField] private bool _disableIfNoTarget;

        private bool _loggedMissingTarget;
        private bool _loggedCanvasMode;
        private bool _loggedZeroScale;
        private bool _loggedCullingMask;
        private bool _loggedCanvasScalerMode;

        private void Awake()
        {
            ValidateCanvasForWorldFollow();
        }

        private void ValidateCanvasForWorldFollow()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                Debug.LogWarning(
                    "[WorldHealthBarFollower_V2] No Canvas on this GameObject or parents. Follower only works with a UI Canvas.",
                    this);
                return;
            }

            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                if (!_loggedCanvasMode)
                {
                    _loggedCanvasMode = true;
                    Debug.LogError(
                        "[WorldHealthBarFollower_V2] Canvas Render Mode must be **World Space** for bars that follow units " +
                        "(paratrooper, bunker, hero over head). **Screen Space – Overlay / Camera** does not use world " +
                        "position, so the bar will not appear over the target. Set World Space, assign Event Camera " +
                        "(often Main Camera), and use a small Rect Transform scale (e.g. 0.01) on the canvas root.",
                        this);
                }

                return;
            }

            if (canvas.worldCamera == null && Camera.main != null)
            {
                canvas.worldCamera = Camera.main;
            }

            RectTransform rt = canvas.transform as RectTransform;
            if (rt != null && rt.localScale.sqrMagnitude < 1e-12f)
            {
                rt.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                if (!_loggedZeroScale)
                {
                    _loggedZeroScale = true;
                    Debug.LogWarning(
                        "[WorldHealthBarFollower_V2] RectTransform local scale was **zero**, so the health bar was invisible. " +
                        "Applied default scale (0.01, 0.01, 0.01). Adjust in the Inspector if the bar is too large or small.",
                        this);
                }
            }

            Camera cam = _camera != null ? _camera : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (cam != null)
            {
                int layer = canvas.gameObject.layer;
                if ((cam.cullingMask & (1 << layer)) == 0)
                {
                    if (!_loggedCullingMask)
                    {
                        _loggedCullingMask = true;
                        Debug.LogError(
                            "[WorldHealthBarFollower_V2] The event camera **does not render** layer " +
                            LayerMask.LayerToName(layer) + " (" + layer + "). World Space UI is drawn like geometry, " +
                            "so add that layer to the camera Culling Mask (or move the canvas to Default).",
                            this);
                    }
                }
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null &&
                scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                !_loggedCanvasScalerMode)
            {
                _loggedCanvasScalerMode = true;
                Debug.LogWarning(
                    "[WorldHealthBarFollower_V2] Canvas Scaler is **Scale With Screen Size**. For World Space bars this often " +
                    "produces the wrong world size after switching from Screen Space. Set UI Scale Mode to **Constant Pixel Size** " +
                    "(Scale Factor 1) on this canvas.",
                    this);
            }
        }

        private void LateUpdate()
        {
            if (_followTarget == null)
            {
                if (!_loggedMissingTarget)
                {
                    _loggedMissingTarget = true;
                    if (!_disableIfNoTarget)
                    {
                        Debug.LogWarning("[WorldHealthBarFollower_V2] Follow Target is not assigned.", this);
                    }
                }

                if (_disableIfNoTarget)
                {
                    enabled = false;
                }

                return;
            }

            _loggedMissingTarget = false;
            Vector3 p = _followTarget.position + _worldOffset;
            transform.position = p;

            if (!_faceCamera)
            {
                return;
            }

            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null)
            {
                return;
            }

            if (_matchCameraRotation)
            {
                transform.rotation = cam.transform.rotation;
                return;
            }

            Vector3 toCam = cam.transform.position - transform.position;
            if (toCam.sqrMagnitude < 1e-8f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
        }

        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
            enabled = true;
            _loggedMissingTarget = false;
        }
    }
}
