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
     * Place one instance in the scene, size/position the box to match your blue safe-area guide (e.g. 4:3),
     * and wire EnemySpawner_V2 to it (or rely on auto-find).
     *
     * ---------------------------------------------------------
     * NAVIGATION
     *
     * Paratrooper drop gate → EnemySpawner_V2 + HelicopterCarrier_V2
     * Bomber release clamp → BombPlaneController_V2
     */
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [ExecuteAlways]
    public sealed class CombatBand_V2 : MonoBehaviour
    {
        public static CombatBand_V2 ActiveInstance { get; private set; }

        [Tooltip("When off, bounds are ignored at runtime (useful to disable without deleting the guide object).")]
        [SerializeField] private bool _enforceAtRuntime = true;

        [Tooltip("Gate/clamp paratrooper helicopter drop world X to this band.")]
        [SerializeField] private bool _applyToParatrooperDrops = true;

        [Tooltip("Clamp bomber bomb-release world X to this band.")]
        [SerializeField] private bool _applyToBomberReleases = true;

        [Tooltip("Draw the box in the Scene view when this object is selected.")]
        [SerializeField] private bool _drawSelectedGizmo = true;

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
        }

        private void OnEnable()
        {
            ActiveInstance = this;
        }

        private void OnDisable()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
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

        public static bool TryGetActive(out CombatBand_V2 band)
        {
            band = ActiveInstance;
            if (band != null && band.EnforceAtRuntime)
            {
                return true;
            }

            band = null;
            return false;
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

        private void OnDrawGizmosSelected()
        {
            if (!_drawSelectedGizmo || !TryGetWorldBounds(out Bounds b))
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
