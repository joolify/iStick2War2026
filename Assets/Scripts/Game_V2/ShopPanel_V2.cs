using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace iStick2War_V2
{
    /*
 * ShopPanel_V2 (Between-wave shop UI + purchases)
 *
 * PURPOSE:
 * Shows currency, bunker/health offer copy, carousel of ShopOfferConfig_V2 entries, and wires BUY / arrow navigation
 * from world-space buttons. Calls into WaveManager_V2 for pricing, purchases, and visibility when the shop phase is active.
 *
 * ---------------------------------------------------------
 * INPUT SOURCES
 *
 * - WaveManager_V2 (meta text, purchase execution, show/hide hooks).
 * - Serialized TMP labels, optional camera parenting for scaled canvases.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Spawn enemies or advance InWave timers (WaveManager_V2 / EnemySpawner_V2).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Purchases + currency rules executed here but priced by → WaveManager_V2.cs
 * Offer rows data → ShopOfferConfig_V2.cs (+ ShopOfferKind_V2 in same file)
 * World BUY / arrows / continue → ShopBuyButton_V2.cs, ShopNavArrow_V2.cs, ShopNavArrowUiButton_V2.cs, ShopStartWaveButton_V2.cs
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Presentation-focused MonoBehaviour with explicit methods WaveManager invokes; keeps economy rules centralized.
 */
    public sealed class ShopPanel_V2 : MonoBehaviour
    {
        [Header("UI (optional)")]
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _currencyText;
        [SerializeField] private TMP_Text _bunkerText;
        [SerializeField] private TMP_Text _healthCostText;
        [SerializeField] private TMP_Text _bunkerCostText;
        [Header("Shop carousel")]
        [Tooltip("Ordered list: use arrow buttons to cycle. Wire BUY to OnPurchaseSelectedOfferClicked.")]
        [SerializeField] private List<ShopOfferConfig_V2> _shopOffers = new List<ShopOfferConfig_V2>();
        [SerializeField] private TMP_Text _offerTitleText;
        [SerializeField] private TMP_Text _offerSubtitleText;
        [Header("Button Labels")]
        [SerializeField] private TMP_Text _buyButtonText;
        [SerializeField] private string _buyButtonDefaultLabel = "BUY";
        [Header("Visibility")]
        [SerializeField] private bool _toggleVisualComponentsOnShowHide = true;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private bool _toggleCanvases = false;
        [SerializeField] private bool _toggleGraphics = false;
        [SerializeField] private bool _lockVisualRootTransformOnShow = true;
        [SerializeField] private bool _detachFromScaledParentOnInitialize = true;
        [SerializeField] private Camera _lockCamera;
        [SerializeField] private bool _parentToCameraWhileVisible = true;
        [SerializeField] private bool _useFixedCameraLocalPlacement = true;
        [SerializeField] private Vector3 _fixedCameraLocalPosition = new Vector3(0f, 0f, 10f);
        [SerializeField] private Vector3 _fixedCameraLocalScale = Vector3.one;
        [SerializeField] private bool _useCachedVisualScaleWhenParentedToCamera = true;
        [SerializeField] private bool _debugShopPanelLogs = false;
        [SerializeField] private bool _debugShopNavigationLogs = true;
        [Header("UI carousel navigation (optional)")]
        [Tooltip("Canvas/TextBTN previous offer control. Auto-bound by name when empty.")]
        [SerializeField] private Button _uiPreviousOfferButton;
        [Tooltip("Canvas/TextBTN next offer control. Auto-bound by name when empty.")]
        [SerializeField] private Button _uiNextOfferButton;
        [SerializeField] private string _uiPreviousOfferButtonObjectName = "TextBTN_MediumPrev";
        [SerializeField] private string[] _uiPreviousOfferButtonAlternateNames =
        {
            "TextBTN_MediumPrevious",
            "TextBTN_Medium_Prev",
            "TextBTN_MediumBack",
        };
        [SerializeField] private string _uiNextOfferButtonObjectName = "TextBTN_MediumNext";
        [SerializeField] private string[] _uiNextOfferButtonAlternateNames =
        {
            "TextBTN_Medium_Next",
            "TextBTN_MediumForward",
        };
        [Header("Offer previews (carousel)")]
        [Tooltip(
            "Parent transform for 3D/2D weapon preview objects. When set, every direct child is deactivated first, " +
            "then the selected offer's PreviewObject is activated — so stray previews under this root never stay on-screen. " +
            "If empty, tries Transform.Find(\"Weapons\") under Visual Root.")]
        [SerializeField] private Transform _carouselPreviewObjectsRoot;
        [Tooltip(
            "If a scene object with this exact name sits at the root of a loaded scene (not parented under the carousel root), " +
            "it is reparented under the carousel root once (typical: shop_bazookaRocket left as a loose instance). Leave empty to disable.")]
        [SerializeField] private string _reparentLooseRootPreviewByExactName = "shop_bazookaRocket";

        private WaveManager_V2 _waveManager;
        private bool _hasCachedVisualRootTransform;
        private Vector3 _cachedVisualRootLocalPosition;
        private Quaternion _cachedVisualRootLocalRotation;
        private Vector3 _cachedVisualRootLocalScale;
        private Vector3 _cachedVisualRootWorldPosition;
        private Quaternion _cachedVisualRootWorldRotation;
        private Transform _originalParent;
        private int _originalSiblingIndex;
        private bool _isParentedToCamera;
        private int _offerIndex;
        private readonly List<Canvas> _resolvedShopUiCanvases = new List<Canvas>();
        private bool _didResolveShopUiCanvases;
        private bool _didReparentLooseShopPreview;

        // Carousel rows configured in the Inspector (read-only for bots / tools).
        public IReadOnlyList<ShopOfferConfig_V2> ConfiguredShopOffers =>
            _shopOffers != null ? _shopOffers : global::System.Array.Empty<ShopOfferConfig_V2>();

        public void Initialize(WaveManager_V2 waveManager)
        {
            _waveManager = waveManager;
            if (_waveManager != null)
            {
                _waveManager.OnMetaChanged -= HandleMetaChanged;
                _waveManager.OnMetaChanged += HandleMetaChanged;
            }

            MaybeDetachFromScaledParent();
            CacheVisualRootTransform();
            EnsureLooseShopPreviewReparentedOnce();
            BindUiCarouselNavigationButtons();
            Refresh();
        }

        private void OnDestroy()
        {
            if (_waveManager != null)
            {
                _waveManager.OnMetaChanged -= HandleMetaChanged;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _offerIndex = 0;
            RestoreVisualRootTransformIfNeeded();
            AttachToCameraIfNeeded();
            SetVisualComponentsVisible(true);
            BindUiCarouselNavigationButtons();
            Refresh();
        }

        public void Hide()
        {
            DetachFromCameraIfNeeded();
            SetVisualComponentsVisible(false);
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (_waveManager == null)
            {
                return;
            }

            SetText(_waveText, $"Wave: {_waveManager.CurrentWaveNumber}");
            SetText(_currencyText, $"Currency: {_waveManager.Currency}");
            SetText(
                _bunkerText,
                $"Bunker HP: {_waveManager.BunkerHealth}/{_waveManager.BunkerMaxHealth}");
            SetText(_buyButtonText, _buyButtonDefaultLabel);
            SetText(_healthCostText, $"Heal cost: {_waveManager.GetHealthPurchaseCost()}");
            SetText(_bunkerCostText, $"Repair cost: {_waveManager.GetScaledBunkerRepairCost()}");
            EnsureLooseShopPreviewReparentedOnce();
            RefreshOfferSelection();
        }

        private void BindUiCarouselNavigationButtons()
        {
            _uiPreviousOfferButton = ResolveUiCarouselButton(
                _uiPreviousOfferButtonObjectName,
                _uiPreviousOfferButtonAlternateNames,
                _uiPreviousOfferButton);
            _uiNextOfferButton = ResolveUiCarouselButton(
                _uiNextOfferButtonObjectName,
                _uiNextOfferButtonAlternateNames,
                _uiNextOfferButton);

            EnsureUiNavComponent(_uiPreviousOfferButton, ShopNavArrow_V2.ArrowDirection.Previous);
            EnsureUiNavComponent(_uiNextOfferButton, ShopNavArrow_V2.ArrowDirection.Next);

            if (_uiPreviousOfferButton == null)
            {
                EnsureUiNavComponentOnNamedObject(
                    _uiPreviousOfferButtonObjectName,
                    ShopNavArrow_V2.ArrowDirection.Previous);
            }

            if (_uiNextOfferButton == null)
            {
                EnsureUiNavComponentOnNamedObject(
                    _uiNextOfferButtonObjectName,
                    ShopNavArrow_V2.ArrowDirection.Next);
            }

            if (_debugShopNavigationLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] UI carousel nav bound: previous='{DescribeUiButton(_uiPreviousOfferButton)}', " +
                    $"next='{DescribeUiButton(_uiNextOfferButton)}'.");
            }

            if (_uiPreviousOfferButton == null)
            {
                Debug.LogWarning(
                    $"[ShopPanel_V2] UI previous offer button not found. Expected '{_uiPreviousOfferButtonObjectName}' " +
                    "under ShopPanel or Visual Root with a UnityEngine.UI.Button.");
            }
        }

        private Button ResolveUiCarouselButton(
            string primaryObjectName,
            string[] alternateObjectNames,
            Button serializedFallback)
        {
            Button resolved = FindUiButtonByNames(primaryObjectName, alternateObjectNames);
            return resolved != null ? resolved : serializedFallback;
        }

        private Button FindUiButtonByNames(string primaryObjectName, string[] alternateObjectNames)
        {
            if (!string.IsNullOrWhiteSpace(primaryObjectName))
            {
                Button primary = FindUiButtonUnderShopHierarchy(primaryObjectName);
                if (primary != null)
                {
                    return primary;
                }
            }

            if (alternateObjectNames == null)
            {
                return null;
            }

            for (int i = 0; i < alternateObjectNames.Length; i++)
            {
                string alternate = alternateObjectNames[i];
                if (string.IsNullOrWhiteSpace(alternate))
                {
                    continue;
                }

                Button alternateMatch = FindUiButtonUnderShopHierarchy(alternate);
                if (alternateMatch != null)
                {
                    return alternateMatch;
                }
            }

            return null;
        }

        private Button FindUiButtonUnderShopHierarchy(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform[] searchRoots =
            {
                _visualRoot,
                transform,
            };

            for (int rootIndex = 0; rootIndex < searchRoots.Length; rootIndex++)
            {
                Transform searchRoot = searchRoots[rootIndex];
                if (searchRoot == null)
                {
                    continue;
                }

                Button match = FindUiButtonUnderRoot(searchRoot, objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Button FindUiButtonUnderRoot(Transform searchRoot, string objectName)
        {
            Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    !string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Button button = candidate.GetComponent<Button>();
                if (button == null)
                {
                    button = candidate.GetComponentInChildren<Button>(true);
                }

                if (button == null)
                {
                    button = candidate.GetComponentInParent<Button>();
                }

                if (button != null)
                {
                    return button;
                }
            }

            return null;
        }

        private void EnsureUiNavComponent(Button button, ShopNavArrow_V2.ArrowDirection direction)
        {
            if (button == null)
            {
                return;
            }

            ShopNavArrowUiButton_V2 nav = button.GetComponent<ShopNavArrowUiButton_V2>();
            if (nav == null)
            {
                nav = button.gameObject.AddComponent<ShopNavArrowUiButton_V2>();
            }

            nav.Configure(this, direction);
        }

        // Binds TextBTN roots that may only have SpriteRenderer + Collider2D (no Unity UI Button).
        private void EnsureUiNavComponentOnNamedObject(string objectName, ShopNavArrow_V2.ArrowDirection direction)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            Transform[] searchRoots =
            {
                _visualRoot,
                transform,
            };

            for (int rootIndex = 0; rootIndex < searchRoots.Length; rootIndex++)
            {
                Transform searchRoot = searchRoots[rootIndex];
                if (searchRoot == null)
                {
                    continue;
                }

                Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate == null ||
                        !string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    ShopNavArrowUiButton_V2 nav = candidate.GetComponent<ShopNavArrowUiButton_V2>();
                    if (nav == null)
                    {
                        nav = candidate.gameObject.AddComponent<ShopNavArrowUiButton_V2>();
                    }

                    nav.Configure(this, direction);
                    return;
                }
            }
        }

        private static string DescribeUiButton(Button button)
        {
            return button != null ? button.name : "none";
        }

        // Wire left arrow (e.g. btn_shop_arrow_left OnClick).
        public void OnShopArrowPreviousClicked()
        {
            if (_shopOffers == null || _shopOffers.Count == 0)
            {
                if (_debugShopNavigationLogs)
                {
                    Debug.Log("[ShopPanel_V2] OnShopArrowPrevious: no shop offers configured.");
                }

                return;
            }

            int before = _offerIndex;
            _offerIndex = (_offerIndex - 1 + _shopOffers.Count) % _shopOffers.Count;
            if (_debugShopNavigationLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] Shop arrow PREVIOUS: index {before} -> {_offerIndex} / {_shopOffers.Count} " +
                    $"(current='{_shopOffers[_offerIndex].DisplayName}')");
            }

            RefreshOfferSelection();
        }

        // Wire right arrow (e.g. btn_shop_arrow_right OnClick).
        public void OnShopArrowNextClicked()
        {
            if (_shopOffers == null || _shopOffers.Count == 0)
            {
                if (_debugShopNavigationLogs)
                {
                    Debug.Log("[ShopPanel_V2] OnShopArrowNext: no shop offers configured.");
                }

                return;
            }

            int before = _offerIndex;
            _offerIndex = (_offerIndex + 1) % _shopOffers.Count;
            if (_debugShopNavigationLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] Shop arrow NEXT: index {before} -> {_offerIndex} / {_shopOffers.Count} " +
                    $"(current='{_shopOffers[_offerIndex].DisplayName}')");
            }

            RefreshOfferSelection();
        }

        // Wire main BUY button to purchase the currently selected carousel offer.
        public void OnPurchaseSelectedOfferClicked()
        {
            if (_waveManager == null || _shopOffers == null || _shopOffers.Count == 0)
            {
                if (_debugShopNavigationLogs)
                {
                    Debug.Log("[ShopPanel_V2] OnPurchaseSelectedOffer: missing manager or offers.");
                }

                return;
            }

            _offerIndex = Mathf.Clamp(_offerIndex, 0, _shopOffers.Count - 1);
            ShopOfferConfig_V2 offer = _shopOffers[_offerIndex];
            if (_debugShopNavigationLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] BUY clicked: offer='{offer.DisplayName}', kind={offer.Kind}, cost={offer.Cost}");
            }

            bool ok;
            // UX guard: if player is on an AmmoRefill row for a locked weapon, BUY should still progress
            // by purchasing the matching WeaponUnlock row first (if configured).
            if (offer.Kind == ShopOfferKind_V2.AmmoRefill &&
                offer.Weapon != null &&
                !_waveManager.IsWeaponOwned(offer.Weapon))
            {
                ShopOfferConfig_V2 unlockOffer = FindWeaponUnlockOfferFor(offer.Weapon);
                ok = unlockOffer != null && _waveManager.TryPurchaseOffer(unlockOffer);
            }
            else
            {
                ok = _waveManager.TryPurchaseOffer(offer);
            }

            if (_debugShopNavigationLogs)
            {
                Debug.Log($"[ShopPanel_V2] TryPurchaseOffer -> {ok}");
            }

            Refresh();
        }

        public void OnBuyHealthClicked()
        {
            _waveManager?.PurchaseHealth();
            Refresh();
        }

        public void OnRepairBunkerClicked()
        {
            _waveManager?.PurchaseBunkerRepair();
            Refresh();
        }

        public void OnStartNextWaveClicked()
        {
            _waveManager?.StartNextWaveFromShop();
        }

        public void SetBuyButtonLabel(string label)
        {
            string nextLabel = string.IsNullOrWhiteSpace(label) ? _buyButtonDefaultLabel : label;
            SetText(_buyButtonText, nextLabel);
        }

        private void RefreshOfferSelection()
        {
            if (_waveManager == null || _shopOffers == null || _shopOffers.Count == 0)
            {
                return;
            }

            _offerIndex = Mathf.Clamp(_offerIndex, 0, _shopOffers.Count - 1);
            ShopOfferConfig_V2 offer = _shopOffers[_offerIndex];

            SetText(_offerTitleText, offer.DisplayName);
            SetText(_offerSubtitleText, BuildOfferSubtitle(offer));

            Transform carouselRoot = ResolveCarouselPreviewObjectsRoot();
            if (carouselRoot != null)
            {
                for (int c = 0; c < carouselRoot.childCount; c++)
                {
                    Transform child = carouselRoot.GetChild(c);
                    if (child != null)
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            for (int i = 0; i < _shopOffers.Count; i++)
            {
                GameObject preview = _shopOffers[i].PreviewObject;
                if (preview != null)
                {
                    preview.SetActive(i == _offerIndex);
                }
            }

            SetBuyButtonLabel(ResolveBuyButtonLabel(offer));
        }

        private Transform ResolveCarouselPreviewObjectsRoot()
        {
            if (_carouselPreviewObjectsRoot != null)
            {
                return _carouselPreviewObjectsRoot;
            }

            if (_visualRoot == null)
            {
                return null;
            }

            return _visualRoot.Find("Weapons");
        }

        private void EnsureLooseShopPreviewReparentedOnce()
        {
            if (_didReparentLooseShopPreview)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_reparentLooseRootPreviewByExactName))
            {
                _didReparentLooseShopPreview = true;
                return;
            }

            Transform carouselRoot = ResolveCarouselPreviewObjectsRoot();
            if (carouselRoot == null)
            {
                _didReparentLooseShopPreview = true;
                return;
            }

            Transform loose = FindLoadedSceneRootTransformByExactName(_reparentLooseRootPreviewByExactName.Trim());
            if (loose == null || loose == carouselRoot || IsDescendantOf(loose, carouselRoot))
            {
                _didReparentLooseShopPreview = true;
                return;
            }

            loose.SetParent(carouselRoot, true);
            loose.gameObject.SetActive(false);
            _didReparentLooseShopPreview = true;

            if (_debugShopPanelLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] Reparented loose preview '{loose.name}' under '{carouselRoot.name}' for carousel exclusivity.");
            }
        }

        private static Transform FindLoadedSceneRootTransformByExactName(string exactName)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null || t.name != exactName || t.parent != null)
                {
                    continue;
                }

                if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded)
                {
                    continue;
                }

                return t;
            }

            return null;
        }

        private static bool IsDescendantOf(Transform node, Transform ancestor)
        {
            for (Transform walk = node; walk != null; walk = walk.parent)
            {
                if (walk == ancestor)
                {
                    return true;
                }
            }

            return false;
        }

        private string BuildOfferSubtitle(ShopOfferConfig_V2 offer)
        {
            if (_waveManager == null)
            {
                return string.Empty;
            }

            switch (offer.Kind)
            {
                case ShopOfferKind_V2.HealthPack:
                    return _waveManager.IsHeroHealthFull()
                        ? "HP full"
                        : $"Cost: {_waveManager.GetOfferEffectiveCost(offer)}";

                case ShopOfferKind_V2.BunkerRepair:
                    return _waveManager.IsBunkerFullHealth()
                        ? "Bunker full"
                        : $"Cost: {offer.Cost}";

                case ShopOfferKind_V2.BunkerMaxUpgrade:
                    if (_waveManager.IsBunkerMaxAtCap())
                    {
                        return "Bunker max cap";
                    }

                    return $"Cost: {_waveManager.GetOfferEffectiveCost(offer)} (+max HP)";

                case ShopOfferKind_V2.WeaponUnlock:
                    if (_waveManager.IsWeaponOwned(offer.Weapon))
                    {
                        return "Owned";
                    }

                    string role = offer.Weapon != null && offer.Weapon.WeaponType == iStick2War.WeaponType.Minigun
                        ? "Role: DPS"
                        : offer.Weapon != null && offer.Weapon.WeaponType == iStick2War.WeaponType.Tesla
                            ? "Role: Control"
                            : "";
                    return string.IsNullOrEmpty(role)
                        ? $"Cost: {offer.Cost}"
                        : $"Cost: {offer.Cost} ({role})";

                case ShopOfferKind_V2.AmmoRefill:
                    if (offer.Weapon == null)
                    {
                        return "No weapon set";
                    }

                    if (!_waveManager.IsWeaponOwned(offer.Weapon))
                    {
                        return "Unlock weapon first";
                    }

                    return _waveManager.IsWeaponAmmoFull(offer.Weapon)
                        ? "Ammo full"
                        : $"Cost: {offer.Cost}";

                default:
                    return $"Cost: {offer.Cost}";
            }
        }

        private string ResolveBuyButtonLabel(ShopOfferConfig_V2 offer)
        {
            if (_waveManager == null)
            {
                return _buyButtonDefaultLabel;
            }

            int effectiveCost = _waveManager.GetOfferEffectiveCost(offer);
            bool canAfford = offer.Kind == ShopOfferKind_V2.BunkerRepair ||
                             offer.Kind == ShopOfferKind_V2.WeaponUnlock ||
                             offer.Kind == ShopOfferKind_V2.AmmoRefill
                ? _waveManager.CanAfford(offer.Cost)
                : _waveManager.CanAfford(effectiveCost);
            switch (offer.Kind)
            {
                case ShopOfferKind_V2.HealthPack:
                    if (_waveManager.IsHeroHealthFull())
                    {
                        return "FULL";
                    }

                    return canAfford ? _buyButtonDefaultLabel : "NO CASH";

                case ShopOfferKind_V2.BunkerRepair:
                    if (_waveManager.IsBunkerFullHealth())
                    {
                        return "FULL";
                    }

                    return canAfford ? _buyButtonDefaultLabel : "NO CASH";

                case ShopOfferKind_V2.BunkerMaxUpgrade:
                    if (_waveManager.IsBunkerMaxAtCap())
                    {
                        return "MAX";
                    }

                    return canAfford ? _buyButtonDefaultLabel : "NO CASH";

                case ShopOfferKind_V2.WeaponUnlock:
                    if (offer.Weapon == null)
                    {
                        return "-";
                    }

                    if (_waveManager.IsWeaponOwned(offer.Weapon))
                    {
                        return "OWNED";
                    }

                    return canAfford ? _buyButtonDefaultLabel : "NO CASH";

                case ShopOfferKind_V2.AmmoRefill:
                    if (offer.Weapon == null)
                    {
                        return "-";
                    }

                    if (!_waveManager.IsWeaponOwned(offer.Weapon))
                    {
                        return FindWeaponUnlockOfferFor(offer.Weapon) != null
                            ? "BUY WEAPON"
                            : "LOCKED";
                    }

                    if (_waveManager.IsWeaponAmmoFull(offer.Weapon))
                    {
                        return "FULL";
                    }

                    return canAfford ? _buyButtonDefaultLabel : "NO CASH";

                default:
                    return _buyButtonDefaultLabel;
            }
        }

        private ShopOfferConfig_V2 FindWeaponUnlockOfferFor(HeroWeaponDefinition_V2 weapon)
        {
            if (_shopOffers == null || weapon == null)
            {
                return null;
            }

            for (int i = 0; i < _shopOffers.Count; i++)
            {
                ShopOfferConfig_V2 candidate = _shopOffers[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.Kind == ShopOfferKind_V2.WeaponUnlock &&
                    candidate.Weapon == weapon)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void HandleMetaChanged(int wave, int currency, int bunkerHp)
        {
            Refresh();
        }

        private static void SetText(TMP_Text textField, string value)
        {
            if (textField != null)
            {
                textField.text = value;
            }
        }

        private void SetVisualComponentsVisible(bool visible)
        {
            ResolveShopUiCanvasesIfNeeded();

            if (!_toggleVisualComponentsOnShowHide)
            {
                for (int i = 0; i < _resolvedShopUiCanvases.Count; i++)
                {
                    Canvas canvas = _resolvedShopUiCanvases[i];
                    if (canvas != null)
                    {
                        SetShopCanvasHierarchyVisible(canvas, visible);
                    }
                }

                return;
            }

            Transform root = _visualRoot != null ? _visualRoot : transform;
            SpriteRenderer[] spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].enabled = visible;
                }
            }

            if (_toggleCanvases)
            {
                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i] != null)
                    {
                        canvases[i].enabled = visible;
                    }
                }
            }

            if (_toggleGraphics)
            {
                Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length; i++)
                {
                    if (graphics[i] != null)
                    {
                        graphics[i].enabled = visible;
                    }
                }
            }

            for (int i = 0; i < _resolvedShopUiCanvases.Count; i++)
            {
                Canvas canvas = _resolvedShopUiCanvases[i];
                if (canvas != null)
                {
                    SetShopCanvasHierarchyVisible(canvas, visible);
                }
            }

            if (_debugShopPanelLogs)
            {
                Debug.Log($"[ShopPanel_V2] SetVisualComponentsVisible={visible}");
            }
        }

        private void ResolveShopUiCanvasesIfNeeded()
        {
            if (_didResolveShopUiCanvases)
            {
                return;
            }

            _resolvedShopUiCanvases.Clear();

            string[] names =
            {
                "txt_shop_money",
                "txt_shop_buy",
                "txt_shop_startGame"
            };

            TMP_Text[] allTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            HashSet<Canvas> unique = new HashSet<Canvas>();

            for (int n = 0; n < names.Length; n++)
            {
                string targetName = names[n];
                for (int i = 0; i < allTexts.Length; i++)
                {
                    TMP_Text text = allTexts[i];
                    if (text == null)
                    {
                        continue;
                    }

                    if (!text.gameObject.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Canvas canvas = text.GetComponentInParent<Canvas>(true);
                    if (canvas != null)
                    {
                        unique.Add(canvas);
                    }

                    break;
                }
            }

            foreach (Canvas canvas in unique)
            {
                _resolvedShopUiCanvases.Add(canvas);
            }

            _didResolveShopUiCanvases = true;

            if (_debugShopPanelLogs)
            {
                Debug.Log($"[ShopPanel_V2] Resolved shop UI canvases: {_resolvedShopUiCanvases.Count}");
            }
        }

        private static void SetShopCanvasHierarchyVisible(Canvas canvas, bool visible)
        {
            if (canvas == null)
            {
                return;
            }

            GameObject root = canvas.gameObject;
            if (root.activeSelf != visible)
            {
                root.SetActive(visible);
            }

            canvas.enabled = visible;
        }

        private void CacheVisualRootTransform()
        {
            Transform root = _visualRoot != null ? _visualRoot : transform;
            _cachedVisualRootLocalPosition = root.localPosition;
            _cachedVisualRootLocalRotation = root.localRotation;
            _cachedVisualRootLocalScale = root.localScale;
            _cachedVisualRootWorldPosition = root.position;
            _cachedVisualRootWorldRotation = root.rotation;
            _hasCachedVisualRootTransform = true;
        }

        private void RestoreVisualRootTransformIfNeeded()
        {
            if (!_lockVisualRootTransformOnShow)
            {
                return;
            }

            Transform root = _visualRoot != null ? _visualRoot : transform;
            if (!_hasCachedVisualRootTransform)
            {
                CacheVisualRootTransform();
            }

            root.SetPositionAndRotation(_cachedVisualRootWorldPosition, _cachedVisualRootWorldRotation);
            root.localPosition = _cachedVisualRootLocalPosition;
            root.localRotation = _cachedVisualRootLocalRotation;
            root.localScale = _cachedVisualRootLocalScale;

            if (_debugShopPanelLogs)
            {
                Debug.Log($"[ShopPanel_V2] Restored visual root transform. localPos={root.localPosition}");
            }
        }

        private void MaybeDetachFromScaledParent()
        {
            if (!_detachFromScaledParentOnInitialize)
            {
                return;
            }

            Transform root = _visualRoot != null ? _visualRoot : transform;
            Transform parent = root.parent;
            if (parent == null)
            {
                return;
            }

            Vector3 s = parent.lossyScale;
            bool parentScaleIsNormal =
                Mathf.Approximately(s.x, 1f) &&
                Mathf.Approximately(s.y, 1f) &&
                Mathf.Approximately(s.z, 1f);
            if (parentScaleIsNormal)
            {
                return;
            }

            root.SetParent(null, true);
            if (_debugShopPanelLogs)
            {
                Debug.Log($"[ShopPanel_V2] Detached visual root from scaled parent '{parent.name}' (lossyScale={s}).");
            }
        }

        private Camera ResolveCamera()
        {
            if (_lockCamera != null)
            {
                return _lockCamera;
            }

            return Camera.main;
        }

        private void AttachToCameraIfNeeded()
        {
            if (!_parentToCameraWhileVisible || _isParentedToCamera)
            {
                return;
            }

            Transform root = _visualRoot != null ? _visualRoot : transform;
            Camera cam = ResolveCamera();
            if (root == null || cam == null)
            {
                return;
            }

            _originalParent = root.parent;
            _originalSiblingIndex = root.GetSiblingIndex();
            root.SetParent(cam.transform, true);

            if (_useFixedCameraLocalPlacement)
            {
                // Force deterministic on-screen placement instead of inheriting stale scene transforms.
                root.localPosition = _fixedCameraLocalPosition;
                root.localRotation = Quaternion.identity;
                bool canUseCachedScale = _useCachedVisualScaleWhenParentedToCamera && _hasCachedVisualRootTransform;
                root.localScale = canUseCachedScale ? _cachedVisualRootLocalScale : _fixedCameraLocalScale;
            }

            _isParentedToCamera = true;

            if (_debugShopPanelLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] Parented visual root to camera '{cam.name}'. " +
                    $"localPos={root.localPosition}, localScale={root.localScale}, fixedPlacement={_useFixedCameraLocalPlacement}");
            }
        }

        private void DetachFromCameraIfNeeded()
        {
            if (!_isParentedToCamera)
            {
                return;
            }

            Transform root = _visualRoot != null ? _visualRoot : transform;
            if (root == null)
            {
                _isParentedToCamera = false;
                return;
            }

            root.SetParent(_originalParent, true);
            if (_originalParent != null)
            {
                int clampedIndex = Mathf.Clamp(_originalSiblingIndex, 0, _originalParent.childCount - 1);
                root.SetSiblingIndex(clampedIndex);
            }

            _isParentedToCamera = false;
            if (_debugShopPanelLogs)
            {
                Debug.Log("[ShopPanel_V2] Restored visual root parent after hide.");
            }
        }
    }
}
