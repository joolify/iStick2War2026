using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BulletImpactVfx_V2 (Ground / bunker bullet impact bursts)
 *
 * PURPOSE:
 * Scene-level VFX service for small sparks / dirt hits when hitscan bullets strike non-character
 * surfaces such as Ground or Bunker. Weapon systems pass the RaycastHit2D they already resolved;
 * this component owns prefab, rotation, sorting, and lifetime.
 *
 * ---------------------------------------------------------
 * INPUT SOURCES
 *
 * - HeroController_V2 after committed hitscan shots.
 * - ParatrooperWeaponSystem_V2 after MP40 raycast resolves bunker/ground hits.
 * - MechRobotBossWeaponSystem_V2 after hitscan raycast resolves bunker/ground hits.
 *
 * ---------------------------------------------------------
 * MUST NOT
 *
 * - Decide damage, hit priority, or weapon fire rules.
 */
    public sealed class BulletImpactVfx_V2 : MonoBehaviour
    {
        private static BulletImpactVfx_V2 s_instance;
        private static bool s_warnedNoInstance;
        private static bool s_warnedNoPrefab;

        [Header("Prefab")]
        [SerializeField] private GameObject _impactPrefab;
        [Tooltip("If true, impact rotation uses hit normal. If false, it faces opposite the shot direction.")]
        [SerializeField] private bool _alignToHitNormal = true;
        [Tooltip("Enable when prefab art points along local +Y/up at rotation 0. Disable when it points along local +X/right.")]
        [SerializeField] private bool _prefabPointsUpAtZero = true;
        [Tooltip("Added to resolved angle. Use this if your prefab points up/right at rotation 0.")]
        [SerializeField] private float _rotationOffsetDegrees;
        [SerializeField] private float _worldZ = 0f;
        [SerializeField] private float _lifetimeSeconds = 0.6f;
        [Tooltip("Moves the VFX slightly away from the hit surface so particles are not hidden inside Ground/Bunker geometry.")]
        [SerializeField] private float _surfaceOffsetWorld = 0.035f;
        [Tooltip("Particle prefabs can have longer child system durations than Lifetime Seconds. Keep them alive at least this long.")]
        [SerializeField] private float _minimumParticleLifetimeSeconds = 1.25f;
        [SerializeField] private Transform _optionalParent;
        [Tooltip("Surface layers for VFX-only raycasts. Empty = Ground + Bunker by name.")]
        [SerializeField] private LayerMask _surfaceRaycastMask;

        [Header("Sorting")]
        [SerializeField] private string _sortingLayerName = "";
        [SerializeField] private int _sortingOrder = 6500;

        [Header("Scale")]
        [SerializeField] private Vector2 _randomScaleRange = new Vector2(0.85f, 1.2f);

        [Header("Debug")]
        [SerializeField] private bool _logSpawn;

        private int _sortingLayerId = -1;

        public static void PlayIfSurfaceHit(RaycastHit2D hit, Vector2 shotDirection)
        {
            PlayIfSurfaceHit(hit, shotDirection, null);
        }

        public static void PlayIfSurfaceHit(RaycastHit2D hit, Vector2 shotDirection, bool? alignToHitNormalOverride)
        {
            if (hit.collider == null || !IsSurfaceCollider(hit.collider))
            {
                return;
            }

            if (s_instance == null)
            {
                if (!s_warnedNoInstance)
                {
                    s_warnedNoInstance = true;
                    Debug.LogWarning("[BulletImpactVfx_V2] No BulletImpactVfx_V2 instance in scene; impact VFX ignored.");
                }

                return;
            }

            s_instance.Spawn(hit, shotDirection, alignToHitNormalOverride);
        }

        public static bool PlayFirstSurfaceHitAlongRay(
            Vector2 origin,
            Vector2 direction,
            float range,
            bool includeBunker = true,
            bool? alignToHitNormalOverride = null)
        {
            if (s_instance == null)
            {
                if (!s_warnedNoInstance)
                {
                    s_warnedNoInstance = true;
                    Debug.LogWarning("[BulletImpactVfx_V2] No BulletImpactVfx_V2 instance in scene; impact VFX ignored.");
                }

                return false;
            }

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float dist = Mathf.Max(0.1f, range);
            int mask = s_instance.ResolveSurfaceRaycastMask(includeBunker);
            if (mask == 0)
            {
                return false;
            }

            bool prevHitTriggers = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = true;
            RaycastHit2D[] hits;
            try
            {
                hits = Physics2D.RaycastAll(origin, dir, dist, mask);
            }
            finally
            {
                Physics2D.queriesHitTriggers = prevHitTriggers;
            }

            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.collider == null || !IsSurfaceCollider(hit.collider, includeBunker))
                {
                    continue;
                }

                PlayIfSurfaceHit(hit, dir, alignToHitNormalOverride);
                return true;
            }

            return false;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Debug.LogWarning("[BulletImpactVfx_V2] Multiple instances found; latest instance will receive static Play calls.");
            }

            s_instance = this;
            s_warnedNoInstance = false;
            s_warnedNoPrefab = false;
            CacheSortingLayer();
        }

        private void OnDisable()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void Spawn(RaycastHit2D hit, Vector2 shotDirection, bool? alignToHitNormalOverride)
        {
            if (_impactPrefab == null)
            {
                if (!s_warnedNoPrefab)
                {
                    s_warnedNoPrefab = true;
                    Debug.LogWarning("[BulletImpactVfx_V2] Impact Prefab is not assigned.");
                }

                return;
            }

            Vector2 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : -shotDirection.normalized;
            bool alignToNormal = alignToHitNormalOverride ?? _alignToHitNormal;
            Vector2 facing = alignToNormal
                ? normal
                : (shotDirection.sqrMagnitude > 0.0001f ? -shotDirection.normalized : Vector2.up);
            float prefabAxisOffset = _prefabPointsUpAtZero ? -90f : 0f;
            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg + prefabAxisOffset + _rotationOffsetDegrees;
            Vector2 spawnPoint = hit.point + normal * Mathf.Max(0f, _surfaceOffsetWorld);
            Vector3 pos = new Vector3(spawnPoint.x, spawnPoint.y, _worldZ);
            GameObject go = Instantiate(
                _impactPrefab,
                pos,
                Quaternion.Euler(0f, 0f, angle),
                _optionalParent);

            float minScale = Mathf.Min(_randomScaleRange.x, _randomScaleRange.y);
            float maxScale = Mathf.Max(_randomScaleRange.x, _randomScaleRange.y);
            float scale = Random.Range(Mathf.Max(0.01f, minScale), Mathf.Max(0.01f, maxScale));
            go.transform.localScale = go.transform.localScale * scale;

            ApplyRendererSorting(go);
            float lifetime = Mathf.Max(0.01f, _lifetimeSeconds, PlayParticleSystemsAndResolveLifetime(go));
            Destroy(go, lifetime);

            if (_logSpawn)
            {
                Debug.Log($"[BulletImpactVfx_V2] Spawned impact at {pos}, collider='{hit.collider.name}', angle={angle:0.#}, lifetime={lifetime:0.##}");
            }
        }

        private float PlayParticleSystemsAndResolveLifetime(GameObject go)
        {
            if (go == null)
            {
                return Mathf.Max(0f, _minimumParticleLifetimeSeconds);
            }

            ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
            if (systems == null || systems.Length == 0)
            {
                return 0f;
            }

            float lifetime = Mathf.Max(0f, _minimumParticleLifetimeSeconds);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = ps.main;
                lifetime = Mathf.Max(
                    lifetime,
                    main.duration + main.startLifetime.constantMax);

                ps.gameObject.SetActive(true);
                ps.Clear(true);
                ps.Play(true);
            }

            return lifetime;
        }

        private static bool IsSurfaceCollider(Collider2D c, bool includeBunker = true)
        {
            if (c == null)
            {
                return false;
            }

            if (includeBunker && c.GetComponentInParent<BunkerHitbox_V2>() != null)
            {
                return true;
            }

            int layer = c.gameObject.layer;
            int ground = LayerMask.NameToLayer("Ground");
            int bunker = LayerMask.NameToLayer("Bunker");
            return (ground >= 0 && layer == ground) ||
                   (includeBunker && bunker >= 0 && layer == bunker);
        }

        private int ResolveSurfaceRaycastMask(bool includeBunker)
        {
            if (_surfaceRaycastMask.value != 0)
            {
                int configuredMask = _surfaceRaycastMask.value;
                if (!includeBunker)
                {
                    int bunkerLayer = LayerMask.NameToLayer("Bunker");
                    if (bunkerLayer >= 0)
                    {
                        configuredMask &= ~(1 << bunkerLayer);
                    }
                }

                return configuredMask;
            }

            int resolvedMask = 0;
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
            {
                resolvedMask |= 1 << groundLayer;
            }

            int bunkerDefaultLayer = LayerMask.NameToLayer("Bunker");
            if (includeBunker && bunkerDefaultLayer >= 0)
            {
                resolvedMask |= 1 << bunkerDefaultLayer;
            }

            return resolvedMask;
        }

        private void CacheSortingLayer()
        {
            _sortingLayerId = -1;
            if (string.IsNullOrWhiteSpace(_sortingLayerName))
            {
                return;
            }

            SortingLayer[] layers = SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == _sortingLayerName)
                {
                    _sortingLayerId = layers[i].id;
                    return;
                }
            }
        }

        private void ApplyRendererSorting(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                {
                    continue;
                }

                if (_sortingLayerId >= 0)
                {
                    r.sortingLayerID = _sortingLayerId;
                }

                r.sortingOrder = _sortingOrder;
            }
        }

        [ContextMenu("Test Impact Here")]
        private void TestImpactHere()
        {
            if (_impactPrefab == null)
            {
                Debug.LogWarning("[BulletImpactVfx_V2] Test skipped: Impact Prefab is not assigned.");
                return;
            }

            GameObject go = Instantiate(_impactPrefab, transform.position, Quaternion.identity, _optionalParent);
            ApplyRendererSorting(go);
            Destroy(go, Mathf.Max(0.01f, _lifetimeSeconds));
        }
    }
}
