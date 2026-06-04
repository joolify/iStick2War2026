using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BunkerView_V2 (Bunker presentation from HP)
 *
 * PURPOSE:
 * Reads WaveManager_V2 bunker HP and shows the matching back/front prefab instances
 * under BunkerVariations (scene layout and transforms stay as authored). Ensures an active
 * bunker_front variation has BoxCollider2D + BunkerHitbox_V2 so enemy rays hit cover before the hero.
 * Four visual tiers (4K art): 75-100%, 50-74%, 25-49%, 0-24% max HP.
 * Optional legacy color tint on the active tier SpriteRenderers.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Modify bunker max HP or apply gameplay damage (WaveManager_V2 authoritative).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * HP source → WaveManager_V2.cs | Interior safe volume (optional) → BunkerInteriorZone_V2.cs
 */
    public sealed class BunkerView_V2 : MonoBehaviour
    {
        private const int HealthStageCount = 4;

        private const string BunkerVariationsObjectName = "BunkerVariations";

        [Header("References")]
        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private Transform _bunkerVariationsRoot;

        [Header("Optional Color Tint")]
        [SerializeField] private bool _applyColorTintByHealth;
        [SerializeField] private Color _healthyColor = Color.white;
        [SerializeField] private Color _damagedColor = new Color(1f, 0.72f, 0.72f, 1f);
        [SerializeField] private bool _updateEveryFrame = true;

        private readonly GameObject[] _backVariationRoots = new GameObject[HealthStageCount];
        private readonly GameObject[] _frontVariationRoots = new GameObject[HealthStageCount];
        private readonly SpriteRenderer[] _backVariationRenderers = new SpriteRenderer[HealthStageCount];
        private readonly SpriteRenderer[] _frontVariationRenderers = new SpriteRenderer[HealthStageCount];

        private bool _variationCacheBuilt;
        private int _lastAppliedStage = -1;
        private float _lastAppliedRatio = -1f;

        private void Awake()
        {
            ResolveReferencesIfNeeded();
            BuildVariationCache();
            ApplyVisual();
        }

        private void Update()
        {
            if (_updateEveryFrame)
            {
                ApplyVisual();
            }
        }

        public void RefreshNow()
        {
            ApplyVisual();
        }

        private void ResolveReferencesIfNeeded()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>();
            }

            if (_bunkerVariationsRoot == null)
            {
                _bunkerVariationsRoot = FindVariationsRootTransform();
            }
        }

        private Transform FindVariationsRootTransform()
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.name.Equals(BunkerVariationsObjectName, System.StringComparison.Ordinal))
                {
                    return t;
                }
            }

            return null;
        }

        private void BuildVariationCache()
        {
            _variationCacheBuilt = false;
            for (int i = 0; i < HealthStageCount; i++)
            {
                _backVariationRoots[i] = null;
                _frontVariationRoots[i] = null;
                _backVariationRenderers[i] = null;
                _frontVariationRenderers[i] = null;
            }

            if (_bunkerVariationsRoot == null)
            {
                return;
            }

            for (int i = 0; i < _bunkerVariationsRoot.childCount; i++)
            {
                Transform child = _bunkerVariationsRoot.GetChild(i);
                if (child == null || !TryParseVariationStage(child.name, out bool isBack, out int stageIndex))
                {
                    continue;
                }

                GameObject root = child.gameObject;
                SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    renderer = child.GetComponentInChildren<SpriteRenderer>(true);
                }

                if (isBack)
                {
                    _backVariationRoots[stageIndex] = root;
                    _backVariationRenderers[stageIndex] = renderer;
                }
                else
                {
                    _frontVariationRoots[stageIndex] = root;
                    _frontVariationRenderers[stageIndex] = renderer;
                }
            }

            for (int i = 0; i < HealthStageCount; i++)
            {
                EnsureFrontVariationShotBlockCollider(_frontVariationRoots[i]);
            }

            _variationCacheBuilt = true;
        }

        // Names like bunker_back_1@4K / bunker_front_4@4K (prefab instance roots in BunkerVariations).
        private static bool TryParseVariationStage(string objectName, out bool isBack, out int stageIndex)
        {
            isBack = false;
            stageIndex = -1;
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            if (objectName.StartsWith("bunker_back_", System.StringComparison.Ordinal))
            {
                isBack = true;
            }
            else if (objectName.StartsWith("bunker_front_", System.StringComparison.Ordinal))
            {
                isBack = false;
            }
            else
            {
                return false;
            }

            int atIndex = objectName.IndexOf('@');
            string core = atIndex > 0 ? objectName.Substring(0, atIndex) : objectName;
            int lastUnderscore = core.LastIndexOf('_');
            if (lastUnderscore < 0 || lastUnderscore >= core.Length - 1)
            {
                return false;
            }

            if (!int.TryParse(core.Substring(lastUnderscore + 1), out int stageOneBased))
            {
                return false;
            }

            stageIndex = stageOneBased - 1;
            return stageIndex >= 0 && stageIndex < HealthStageCount;
        }

        private void ApplyVisual()
        {
            if (_waveManager == null)
            {
                return;
            }

            if (!_variationCacheBuilt)
            {
                BuildVariationCache();
            }

            int maxHp = Mathf.Max(1, _waveManager.BunkerMaxHealth);
            float ratio = Mathf.Clamp01((float)_waveManager.BunkerHealth / maxHp);
            int stageIndex = GetHealthStageIndex(ratio);

            bool stageChanged = stageIndex != _lastAppliedStage;
            bool ratioChanged = !Mathf.Approximately(ratio, _lastAppliedRatio);
            if (!stageChanged && !ratioChanged)
            {
                return;
            }

            _lastAppliedStage = stageIndex;
            _lastAppliedRatio = ratio;

            ApplyVariationVisibility(stageIndex);

            if (_applyColorTintByHealth)
            {
                Color tint = Color.Lerp(_damagedColor, _healthyColor, ratio);
                ApplyActiveTierColor(tint);
            }
            else
            {
                ApplyActiveTierColor(Color.white);
            }
        }

        // 75-100% → tier 1 (index 0), 50-74% → tier 2, 25-49% → tier 3, 0-24% → tier 4.
        public static int GetHealthStageIndex(float healthRatio)
        {
            float percent = Mathf.Clamp01(healthRatio) * 100f;
            if (percent >= 75f)
            {
                return 0;
            }

            if (percent >= 50f)
            {
                return 1;
            }

            if (percent >= 25f)
            {
                return 2;
            }

            return 3;
        }

        private void ApplyVariationVisibility(int stageIndex)
        {
            stageIndex = Mathf.Clamp(stageIndex, 0, HealthStageCount - 1);

            for (int i = 0; i < HealthStageCount; i++)
            {
                bool active = i == stageIndex;
                SetVariationActive(_backVariationRoots[i], active);
                SetVariationActive(_frontVariationRoots[i], active);
            }

            EnsureFrontVariationShotBlockCollider(_frontVariationRoots[stageIndex]);
        }

        // MP40 / grenade rays need bunker_front geometry; 4K prefabs are sprite-only unless this runs.
        private static void EnsureFrontVariationShotBlockCollider(GameObject frontRoot)
        {
            if (frontRoot == null)
            {
                return;
            }

            if (frontRoot.GetComponent<BunkerHitbox_V2>() == null)
            {
                frontRoot.AddComponent<BunkerHitbox_V2>();
            }

            BoxCollider2D box = frontRoot.GetComponent<BoxCollider2D>();
            if (box == null)
            {
                box = frontRoot.AddComponent<BoxCollider2D>();
            }

            SpriteRenderer spriteRenderer = frontRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                // Extend toward +X (local) so rays from paratroopers on the right hit cover before hero hitboxes.
                Vector2 spriteSize = spriteRenderer.size;
                float extendX = spriteSize.x * 0.22f;
                box.size = new Vector2(spriteSize.x + extendX, spriteSize.y);
                box.offset = new Vector2(extendX * 0.5f, 0f);
            }

            box.isTrigger = true;
        }

        private static void SetVariationActive(GameObject variationRoot, bool active)
        {
            if (variationRoot == null || variationRoot.activeSelf == active)
            {
                return;
            }

            variationRoot.SetActive(active);
        }

        private void ApplyActiveTierColor(Color color)
        {
            int stage = Mathf.Clamp(_lastAppliedStage, 0, HealthStageCount - 1);
            ApplyRendererColor(_backVariationRenderers[stage], color);
            ApplyRendererColor(_frontVariationRenderers[stage], color);
        }

        private static void ApplyRendererColor(SpriteRenderer renderer, Color color)
        {
            if (renderer != null)
            {
                renderer.color = color;
            }
        }
    }
}
