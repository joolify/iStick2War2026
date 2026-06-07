using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
 * WorldHealthBarFollower_V2 (World-space UI follow + billboard)
 *
 * PURPOSE:
 * Each LateUpdate positions the hosting transform at a follow target plus offset, optionally matching camera rotation
 * for orthographic readability. Intended for world-space Canvas roots paired with HealthBarCanvas_V2.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Sample HP (HealthBarCanvas_V2 reads models separately).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * HP fill driver → HealthBarCanvas_V2.cs | Paratrooper world bar spawn → Paratrooper_V2.cs (runtime health bar hook)
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Presentation-only motion layer so bar prefabs stay reusable across paratroopers, bunkers, and bosses.
 */
    [DefaultExecutionOrder(650)]
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

        [Header("Render above follow target (paratrooper)")]
        [Tooltip("When true, force this canvas onto a sorting layer above Paratrooper mesh and refresh each LateUpdate.")]
        [SerializeField] private bool _renderAboveFollowTargetMesh;
        [SerializeField] private string _renderAboveSortingLayerName = "Topbar";
        [SerializeField] private int _renderAboveSortingOrderOffset = 10;
        [Tooltip("Extra world Z added to the follow offset so the bar draws in front of co-planar Spine meshes.")]
        [SerializeField] private float _renderAboveDepthBiasZ = -0.5f;

        private bool _loggedMissingTarget;
        private bool _loggedCanvasMode;
        private bool _loggedZeroScale;
        private bool _loggedCullingMask;
        private bool _loggedCanvasScalerMode;
        private Func<Vector3> _worldAnchorOverride;
        private MeshRenderer _followTargetMeshRenderer;
        private Canvas _followCanvas;
        private SortingGroup _followSortingGroup;

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
            if (_followTarget == null && _worldAnchorOverride == null)
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
            Vector3 anchor = ResolveAnchorWorldPosition();
            transform.position = anchor + ResolveFollowOffset();

            if (!_faceCamera)
            {
                if (_renderAboveFollowTargetMesh)
                {
                    ApplyRenderAboveFollowTargetMeshSorting();
                }

                return;
            }

            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null)
            {
                if (_renderAboveFollowTargetMesh)
                {
                    ApplyRenderAboveFollowTargetMeshSorting();
                }

                return;
            }

            if (_matchCameraRotation)
            {
                transform.rotation = cam.transform.rotation;
                if (_renderAboveFollowTargetMesh)
                {
                    ApplyRenderAboveFollowTargetMeshSorting();
                }

                return;
            }

            Vector3 toCam = cam.transform.position - transform.position;
            if (toCam.sqrMagnitude < 1e-8f)
            {
                if (_renderAboveFollowTargetMesh)
                {
                    ApplyRenderAboveFollowTargetMeshSorting();
                }

                return;
            }

            transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
            if (_renderAboveFollowTargetMesh)
            {
                ApplyRenderAboveFollowTargetMeshSorting();
            }
        }

        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
            _followTargetMeshRenderer = null;
            enabled = true;
            _loggedMissingTarget = false;

            if (target != null || _worldAnchorOverride != null)
            {
                transform.position = ResolveAnchorWorldPosition() + ResolveFollowOffset();
            }
        }

        // Optional spine / custom anchor (runs every LateUpdate before offset).
        public void SetWorldAnchorOverride(Func<Vector3> provider)
        {
            _worldAnchorOverride = provider;
            if (isActiveAndEnabled && (_followTarget != null || _worldAnchorOverride != null))
            {
                transform.position = ResolveAnchorWorldPosition() + ResolveFollowOffset();
            }
        }

        public void SetWorldOffset(Vector3 offset)
        {
            _worldOffset = offset;
            if (_followTarget != null || _worldAnchorOverride != null)
            {
                transform.position = ResolveAnchorWorldPosition() + ResolveFollowOffset();
            }
        }

        // Paratrooper bars must sit above per-spawn Spine mesh sorting; refresh after each follow tick.
        public void ConfigureRenderAboveFollowTargetMesh(
            bool enabled,
            string sortingLayerName = "Topbar",
            int sortingOrderOffset = 10,
            float depthBiasZ = -0.5f)
        {
            _renderAboveFollowTargetMesh = enabled;
            _renderAboveSortingLayerName = sortingLayerName;
            _renderAboveSortingOrderOffset = sortingOrderOffset;
            _renderAboveDepthBiasZ = depthBiasZ;
            _followTargetMeshRenderer = null;
            if (enabled)
            {
                ApplyRenderAboveFollowTargetMeshSorting();
            }
        }

        private void ApplyRenderAboveFollowTargetMeshSorting()
        {
            if (_followCanvas == null)
            {
                _followCanvas = GetComponent<Canvas>();
                if (_followCanvas == null)
                {
                    _followCanvas = GetComponentInChildren<Canvas>(true);
                }
            }

            if (_followCanvas == null)
            {
                return;
            }

            if (_followTargetMeshRenderer == null && _followTarget != null)
            {
                _followTargetMeshRenderer = _followTarget.GetComponentInChildren<MeshRenderer>(true);
            }

            int layerId = SortingLayer.NameToID(_renderAboveSortingLayerName);
            if (layerId == 0 && _renderAboveSortingLayerName != "Default")
            {
                layerId = SortingLayer.NameToID("Topbar");
            }

            int order = _followTargetMeshRenderer != null
                ? _followTargetMeshRenderer.sortingOrder + _renderAboveSortingOrderOffset
                : _renderAboveSortingOrderOffset;

            _followCanvas.overrideSorting = true;
            _followCanvas.sortingLayerID = layerId;
            _followCanvas.sortingOrder = order;

            if (_followSortingGroup == null)
            {
                _followSortingGroup = GetComponent<SortingGroup>();
                if (_followSortingGroup == null)
                {
                    _followSortingGroup = gameObject.AddComponent<SortingGroup>();
                }
            }

            _followSortingGroup.sortingLayerID = layerId;
            _followSortingGroup.sortingOrder = order;
        }

        private Vector3 ResolveFollowOffset()
        {
            Vector3 offset = _worldOffset;
            if (_renderAboveFollowTargetMesh)
            {
                offset.z += _renderAboveDepthBiasZ;
            }

            return offset;
        }

        private Vector3 ResolveAnchorWorldPosition()
        {
            if (_worldAnchorOverride != null)
            {
                return _worldAnchorOverride();
            }

            return _followTarget != null ? _followTarget.position : transform.position;
        }
    }
}
