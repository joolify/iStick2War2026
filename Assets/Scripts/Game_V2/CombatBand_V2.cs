using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * CombatBand_V2 (Designer combat X band — BoxCollider2D bounds)
     *
     * PURPOSE:
     * Defines a movable world-space horizontal band (from an attached BoxCollider2D) where combat-critical
     * events should happen: paratrooper drops and bomber bomb releases. Aircraft may still approach from
     * outside this band; only the X interval is gated/clamped.
     *
     * Place one CombatBand_V2 per safe-area / aspect profile (e.g. 16:9 and 4:3), size each box to match
     * the blue guide for that profile. At runtime the band whose profile aspect is closest to the current
     * screen aspect becomes ActiveInstance. Non-selected profile objects are deactivated so only one
     * gizmo / collider guide is visible at a time (switch Game View aspect to author another profile).
     *
     * ---------------------------------------------------------
     * NAVIGATION
     *
     * Aspect refresh host → CombatBandSelectionBootstrap_V2 on CombatBands parent
     * Paratrooper drop gate → EnemySpawner_V2 + HelicopterCarrier_V2
     * Bomber release clamp → BombPlaneController_V2 + BombDroneController_V2
     */
    [RequireComponent(typeof(BoxCollider2D))]
    [ExecuteAlways]
    public sealed class CombatBand_V2 : MonoBehaviour
    {
        private static readonly List<CombatBand_V2> s_registered = new List<CombatBand_V2>();

        private static CombatBand_V2 s_activeInstance;
        private static Vector2Int s_lastSelectionScreenSize;
        private static float s_lastSelectionAspect = -1f;
        private static bool s_applyingVisibility;

        public static CombatBand_V2 ActiveInstance => s_activeInstance;

        [Tooltip("When off, bounds are ignored at runtime (useful to disable without deleting the guide object).")]
        [SerializeField] private bool _enforceAtRuntime = true;

        [Tooltip("When off, this band is only used when wired explicitly (e.g. on EnemySpawner_V2).")]
        [SerializeField] private bool _includeInAspectSelection = true;

        [Header("Safe-area profile")]
        [Tooltip("Reference aspect this box was authored for (e.g. 16 x 9 or 4 x 3). Closest profile wins at runtime.")]
        [SerializeField] private float _profileAspectWidth = 16f;
        [SerializeField] private float _profileAspectHeight = 9f;
        [SerializeField] private string _profileLabel = "16:9";

        [Tooltip("Gate/clamp paratrooper helicopter drop world X to this band.")]
        [SerializeField] private bool _applyToParatrooperDrops = true;

        [Tooltip("Clamp bomber bomb-release world X to this band.")]
        [SerializeField] private bool _applyToBomberReleases = true;

        [Tooltip("Draw the box in the Scene view when this object is selected.")]
        [SerializeField] private bool _drawSelectedGizmo = true;

        [Tooltip("While playing, also draw the active band gizmo even when not selected.")]
        [SerializeField] private bool _drawActiveRuntimeGizmo = true;

        [SerializeField] private Color _gizmoFillColor = new Color(0.2f, 0.55f, 1f, 0.22f);
        [SerializeField] private Color _gizmoOutlineColor = new Color(0.15f, 0.45f, 1f, 0.95f);

        private BoxCollider2D _box;
        private bool _spawnerParatrooperDropsEnabled = true;
        private bool _spawnerBomberReleasesEnabled = true;

        public bool EnforceAtRuntime => _enforceAtRuntime && isActiveAndEnabled;
        public bool AppliesToParatrooperDrops =>
            EnforceAtRuntime && _applyToParatrooperDrops && _spawnerParatrooperDropsEnabled;
        public bool AppliesToBomberReleases =>
            EnforceAtRuntime && _applyToBomberReleases && _spawnerBomberReleasesEnabled;
        public float ProfileAspect =>
            _profileAspectWidth / Mathf.Max(0.01f, _profileAspectHeight);
        public string ProfileLabel => _profileLabel;

        public void SetSpawnerFeatureToggles(bool paratrooperDrops, bool bomberReleases)
        {
            _spawnerParatrooperDropsEnabled = paratrooperDrops;
            _spawnerBomberReleasesEnabled = bomberReleases;
        }

        private void Awake()
        {
            _box = GetComponent<BoxCollider2D>();
            if (_box != null)
            {
                _box.isTrigger = true;
            }

            Register(this);
        }

        private void OnDestroy()
        {
            Unregister(this);
        }

        private void OnEnable()
        {
            Register(this);
        }

        private void OnDisable()
        {
            if (s_applyingVisibility)
            {
                return;
            }

            if (s_activeInstance == this)
            {
                s_activeInstance = null;
            }
        }

        public static void RefreshActiveSelection(bool force)
        {
            EnsureAllBandsRegistered();

            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            float aspect = GetCurrentScreenAspect();
            if (!force &&
                screenSize == s_lastSelectionScreenSize &&
                Mathf.Approximately(aspect, s_lastSelectionAspect))
            {
                return;
            }

            s_lastSelectionScreenSize = screenSize;
            s_lastSelectionAspect = aspect;

            CombatBand_V2 best = SelectBestBandForAspect(aspect);
            if (best == null)
            {
                best = SelectFallbackBand();
            }

            s_activeInstance = best;
            ApplyActiveVisibility(best);
        }

        private static CombatBand_V2 SelectBestBandForAspect(float aspect)
        {
            CombatBand_V2 best = null;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < s_registered.Count; i++)
            {
                CombatBand_V2 band = s_registered[i];
                if (band == null || !band._enforceAtRuntime || !band._includeInAspectSelection)
                {
                    continue;
                }

                float delta = Mathf.Abs(aspect - band.ProfileAspect);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = band;
                }
            }

            return best;
        }

        // When no profile matches (e.g. empty registry on first editor frame), prefer 16:9 then any band.
        private static CombatBand_V2 SelectFallbackBand()
        {
            CombatBand_V2 fallback = null;
            float fallbackDelta = float.MaxValue;
            for (int i = 0; i < s_registered.Count; i++)
            {
                CombatBand_V2 band = s_registered[i];
                if (band == null || !band._enforceAtRuntime || !band._includeInAspectSelection)
                {
                    continue;
                }

                float delta = Mathf.Abs(band.ProfileAspect - (16f / 9f));
                if (delta < fallbackDelta)
                {
                    fallbackDelta = delta;
                    fallback = band;
                }
            }

            return fallback;
        }

        private static void ApplyActiveVisibility(CombatBand_V2 selected)
        {
            if (selected == null)
            {
                return;
            }
        {
            s_applyingVisibility = true;
            try
            {
                for (int i = 0; i < s_registered.Count; i++)
                {
                    CombatBand_V2 band = s_registered[i];
                    if (band == null || !band._includeInAspectSelection)
                    {
                        continue;
                    }

                    bool shouldBeActive = selected != null && band == selected;
                    if (band.gameObject.activeSelf == shouldBeActive)
                    {
                        continue;
                    }

                    band.gameObject.SetActive(shouldBeActive);
                }
            }
            finally
            {
                s_applyingVisibility = false;
            }
        }

        private static void EnsureAllBandsRegistered()
        {
            for (int i = s_registered.Count - 1; i >= 0; i--)
            {
                if (s_registered[i] == null)
                {
                    s_registered.RemoveAt(i);
                }
            }

            CombatBand_V2[] bands = Object.FindObjectsByType<CombatBand_V2>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < bands.Length; i++)
            {
                Register(bands[i]);
            }
        }

        public static bool TryGetActive(out CombatBand_V2 band)
        {
            RefreshActiveSelection(force: false);
            band = s_activeInstance;
            if (band != null && band.EnforceAtRuntime)
            {
                return true;
            }

            band = null;
            return false;
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = default;
            if (_box == null)
            {
                _box = GetComponent<BoxCollider2D>();
            }

            if (_box == null)
            {
                return false;
            }

            bounds = _box.bounds;
            return bounds.size.sqrMagnitude > 0.0001f;
        }

        public static bool IsWorldXInsideForGameplay(float worldX)
        {
            if (!TryGetActive(out CombatBand_V2 band) || !band.AppliesToParatrooperDrops || !band.TryGetWorldBounds(out Bounds b))
            {
                return true;
            }

            return worldX >= b.min.x && worldX <= b.max.x;
        }

        public static float ClampBomberReleaseWorldX(float worldX)
        {
            if (!TryGetActive(out CombatBand_V2 band) || !band.AppliesToBomberReleases || !band.TryGetWorldBounds(out Bounds b))
            {
                return worldX;
            }

            return Mathf.Clamp(worldX, b.min.x, b.max.x);
        }

        public static bool TryGetParatrooperDropXInterval(float cameraMinX, float cameraMaxX, out float minX, out float maxX)
        {
            minX = cameraMinX;
            maxX = cameraMaxX;

            if (!TryGetActive(out CombatBand_V2 band) || !band.AppliesToParatrooperDrops || !band.TryGetWorldBounds(out Bounds b))
            {
                return minX <= maxX;
            }

            minX = Mathf.Max(minX, b.min.x);
            maxX = Mathf.Min(maxX, b.max.x);
            return minX <= maxX;
        }

        private static float GetCurrentScreenAspect()
        {
            if (Screen.height <= 0)
            {
                Camera mainCamera = Camera.main;
                return mainCamera != null ? mainCamera.aspect : 16f / 9f;
            }

            return (float)Screen.width / Screen.height;
        }

        private static void Register(CombatBand_V2 band)
        {
            if (band == null || s_registered.Contains(band))
            {
                return;
            }

            s_registered.Add(band);
        }

        private static void Unregister(CombatBand_V2 band)
        {
            if (band == null)
            {
                return;
            }

            s_registered.Remove(band);
            if (s_activeInstance == band)
            {
                s_activeInstance = null;
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_drawActiveRuntimeGizmo || s_activeInstance != this)
            {
                return;
            }

            DrawBandGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawSelectedGizmo || !gameObject.activeInHierarchy)
            {
                return;
            }

            DrawBandGizmo();
        }

        private void DrawBandGizmo()
        {
            if (!TryGetWorldBounds(out Bounds b))
            {
                return;
            }

            Gizmos.color = _gizmoFillColor;
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = _gizmoOutlineColor;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
