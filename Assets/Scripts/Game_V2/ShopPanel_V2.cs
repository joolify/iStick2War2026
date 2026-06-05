using System.Collections.Generic;
using iStick2War;
using UnityEngine;
using UnityEngine.Rendering;
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
        [Tooltip("Shows the selected carousel offer name (e.g. canvas txt_shop_item). Auto-bound by name when empty.")]
        [SerializeField] private TMP_Text _selectedOfferItemText;
        [SerializeField] private string _selectedOfferItemTextObjectName = "txt_shop_item";
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
        [Header("Shop carousel navigation visuals (optional)")]
        [Tooltip("Spawned under Visual Root when TextBTN_MediumPrev/Next are missing from the scene.")]
        [SerializeField] private GameObject _textBtnMediumNormalPrefab;
        [SerializeField] private GameObject _textBtnMediumPressedPrefab;
        [SerializeField] private Vector3 _textBtnMediumPrevLocalPosition = new Vector3(-10.71f, -0.52f, 0f);
        [SerializeField] private Vector3 _textBtnMediumNextLocalPosition = new Vector3(10.78f, -0.24f, 0f);
        [SerializeField] private string _uiBuyButtonObjectName = "TextBTN_MediumBuy";
        [SerializeField] private string[] _uiBuyButtonAlternateNames =
        {
            "btn_shop_buy",
            "bkg_shop_buy",
        };
        [SerializeField] private string _uiStartGameButtonObjectName = "TextBTN_MediumStartGame";
        [SerializeField] private string[] _uiStartGameButtonAlternateNames =
        {
            "btn_shop_startGame",
        };
        [SerializeField] private Vector3 _textBtnMediumBuyLocalPosition = new Vector3(5.5f, -3f, 0f);
        [SerializeField] private Vector3 _textBtnMediumStartGameLocalPosition = new Vector3(0f, -3f, 0f);
        [Header("Offer previews (carousel)")]
        [Tooltip(
            "Parent transform for shop_* weapon preview sprites. Only resolved PreviewObject entries are toggled; " +
            "TextBTN nav controls under this root are left alone.")]
        [SerializeField] private Transform _carouselPreviewObjectsRoot;
        [Tooltip(
            "If a scene object with this exact name sits at the root of a loaded scene (not parented under the carousel root), " +
            "it is reparented under the carousel root once (typical: shop_bazookaRocket left as a loose instance). Leave empty to disable.")]
        [SerializeField] private string _reparentLooseRootPreviewByExactName = "shop_bazookaRocket";
        [Tooltip("Must match shop UI sprites (layer Shop). Previews on other layers render behind ShopPanel background.")]
        [SerializeField] private string _shopPreviewSortingLayerName = "Shop";
        [Tooltip("Above ShopPanel background (~200) and TextBTN (~205). Values at or below 200 are clamped at runtime.")]
        [SerializeField] private int _shopPreviewSortingOrder = 220;
        [Tooltip("Local position under Items where weapon previews are shown (matches shop_teslaGun slot).")]
        [SerializeField] private Vector3 _previewDisplayLocalPosition = new Vector3(0.79f, 2.71f, 0f);
        [Header("Shop stat warnings")]
        [Tooltip("Color for txt_shop_stat_warningText_label at runtime (Face on that TMP material is forced to white).")]
        [SerializeField] private Color _shopWarningTextColor = new Color(0.9f, 0.2f, 0.2f, 1f);

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
        private readonly List<ShopOfferConfig_V2> _visibleShopOffers = new List<ShopOfferConfig_V2>();
        private readonly List<Canvas> _resolvedShopUiCanvases = new List<Canvas>();
        private bool _didResolveShopUiCanvases;
        private bool _didReparentLooseShopPreview;
        private readonly Dictionary<int, GameObject> _resolvedPreviewByOfferIndex = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _runtimeInstantiatedPreviewsBySourceId = new Dictionary<int, GameObject>();
        private bool _didEnsureShopNavButtons;
        private bool _didBuildPreviewCatalog;
        private int _shopOfferCountWhenCatalogBuilt = -1;
        private bool _shopIsVisible;
        public bool IsShopVisible => _shopIsVisible;
        private bool _lifeOverCanvasIsDetached;
        private Transform _lifeOverCanvasParentBeforeDetach;
        private Vector3 _lifeOverCanvasLocalPositionBeforeDetach;
        private Quaternion _lifeOverCanvasLocalRotationBeforeDetach;
        private Vector3 _lifeOverCanvasLocalScaleBeforeDetach;
        private readonly ShopOfferStatsPresenter_V2 _offerStatsPresenter = new ShopOfferStatsPresenter_V2();
        private bool _didResolveOfferStats;
        private bool _didRebuildOfferStatBaselines;
        private readonly HashSet<TMP_Text> _statTmpBindingUsed = new HashSet<TMP_Text>();

        // Carousel rows shown in shop (excludes legacy AmmoRefill-only rows; ammo is bought on the weapon row).
        public IReadOnlyList<ShopOfferConfig_V2> ConfiguredShopOffers => _visibleShopOffers;

        public bool TryGetWeaponRowOffer(HeroWeaponDefinition_V2 weapon, out ShopOfferConfig_V2 weaponRow)
        {
            weaponRow = null;
            if (weapon == null)
            {
                return false;
            }

            RebuildVisibleShopOffersIfNeeded();
            for (int i = 0; i < _visibleShopOffers.Count; i++)
            {
                ShopOfferConfig_V2 row = _visibleShopOffers[i];
                if (row != null &&
                    row.Kind == ShopOfferKind_V2.WeaponUnlock &&
                    row.Weapon == weapon)
                {
                    weaponRow = row;
                    return true;
                }
            }

            return false;
        }

        public int ResolveAmmoRefillCostForWeaponRow(ShopOfferConfig_V2 weaponRowOffer)
        {
            if (weaponRowOffer == null)
            {
                return 0;
            }

            if (weaponRowOffer.AmmoRefillCost > 0)
            {
                return weaponRowOffer.AmmoRefillCost;
            }

            if (weaponRowOffer.Weapon != null &&
                TryGetLegacyAmmoRowCost(weaponRowOffer.Weapon, out int legacyCost))
            {
                return legacyCost;
            }

            return 29;
        }

        private bool TryGetLegacyAmmoRowCost(HeroWeaponDefinition_V2 weapon, out int cost)
        {
            cost = 0;
            if (weapon == null || _shopOffers == null)
            {
                return false;
            }

            for (int i = 0; i < _shopOffers.Count; i++)
            {
                ShopOfferConfig_V2 row = _shopOffers[i];
                if (row != null &&
                    row.Kind == ShopOfferKind_V2.AmmoRefill &&
                    row.Weapon == weapon)
                {
                    cost = row.Cost;
                    return true;
                }
            }

            return false;
        }

        private void RebuildVisibleShopOffersIfNeeded()
        {
            _visibleShopOffers.Clear();
            if (_shopOffers == null)
            {
                return;
            }

            for (int i = 0; i < _shopOffers.Count; i++)
            {
                ShopOfferConfig_V2 row = _shopOffers[i];
                if (row != null &&
                    row.Kind != ShopOfferKind_V2.AmmoRefill &&
                    !IsShopExcludedWeaponRow(row))
                {
                    _visibleShopOffers.Add(row);
                }
            }
        }

        private static bool IsShopExcludedWeaponRow(ShopOfferConfig_V2 row)
        {
            return row.Kind == ShopOfferKind_V2.WeaponUnlock &&
                   row.Weapon != null &&
                   row.Weapon.WeaponType == WeaponType.Colt45;
        }

        private ShopOfferConfig_V2 GetSelectedOffer()
        {
            if (_visibleShopOffers.Count == 0)
            {
                return null;
            }

            _offerIndex = Mathf.Clamp(_offerIndex, 0, _visibleShopOffers.Count - 1);
            return _visibleShopOffers[_offerIndex];
        }

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
            RebuildVisibleShopOffersIfNeeded();
            _resolvedCarouselPreviewRoot = null;
            InvalidatePreviewCatalog();
            EnsureLooseShopPreviewReparentedOnce();
            EnsureShopNavButtonsReady();
            ResolveOfferStatsIfNeeded();
            RebuildOfferStatBaselinesIfNeeded();
            BindUiCarouselNavigationButtons();
            BindShopActionTextButtons();
            Refresh();
            HideShopCarouselPreviews();
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
            if (_waveManager != null)
            {
                _waveManager.EnsureLifeOverUiHidden();
            }

            _shopIsVisible = true;
            gameObject.SetActive(true);
            _offerIndex = 0;
            RebuildVisibleShopOffersIfNeeded();
            RestoreVisualRootTransformIfNeeded();
            AttachToCameraIfNeeded();
            SetVisualComponentsVisible(true);
            SetShopTextButtonsVisible(true);
            EnsureShopNavButtonsReady();
            BindUiCarouselNavigationButtons();
            BindShopActionTextButtons();
            Refresh();
            ApplySelectedOfferPreviewVisibility(
                ResolveCarouselPreviewObjectsRoot(),
                GetSelectedOffer());
        }

        public void Hide()
        {
            _shopIsVisible = false;
            SetShopTextButtonsVisible(false);
            ResetShopNavPressedVisuals();
            _offerStatsPresenter.HideAll();
            HideShopCarouselPreviews();
            DetachFromCameraIfNeeded();
            SetVisualComponentsVisible(false);
            gameObject.SetActive(false);
        }

        // Hides LifeOver-canvas and life-over labels even when that canvas is excluded from shop show/hide.
        public void SuppressLifeOverUiElements()
        {
            Transform lifeOver = FindLifeOverCanvasTransform();
            if (lifeOver != null)
            {
                lifeOver.gameObject.SetActive(false);
            }

            string[] objectNames =
            {
                "txt_lifeOver_info",
                "txt_lifeOver_startNewGame",
                "txt_lifeOver_goToShop",
                "txt_shop_info",
                "txt_shop_startNewGame",
                "TextBTN_MediumStartNewGame",
                "TextBTN_MediumStartGame",
                "TextBTN_MediumGoToShop",
                "TextBTN_MediumGoToShop_Pressed",
            };

            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null)
                {
                    continue;
                }

                for (int n = 0; n < objectNames.Length; n++)
                {
                    if (candidate.gameObject.name.Equals(objectNames[n], System.StringComparison.Ordinal))
                    {
                        candidate.gameObject.SetActive(false);
                        break;
                    }
                }
            }
        }

        // Activates LifeOver-canvas for display. Detach only when the canvas lives under ShopPanel (not under LifeOver V2).
        public GameObject PrepareLifeOverCanvasForDisplay()
        {
            Transform lifeOver = FindLifeOverCanvasTransform();
            if (lifeOver == null)
            {
                return null;
            }

            bool underLifeOverChrome =
                lifeOver.parent != null &&
                lifeOver.parent.gameObject.name.IndexOf("LifeOver V2", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (!underLifeOverChrome && !_lifeOverCanvasIsDetached)
            {
                _lifeOverCanvasParentBeforeDetach = lifeOver.parent;
                _lifeOverCanvasLocalPositionBeforeDetach = lifeOver.localPosition;
                _lifeOverCanvasLocalRotationBeforeDetach = lifeOver.localRotation;
                _lifeOverCanvasLocalScaleBeforeDetach = lifeOver.localScale;

                Transform anchor = transform.parent != null ? transform.parent : transform;
                lifeOver.SetParent(anchor, false);
                _lifeOverCanvasIsDetached = true;
            }

            lifeOver.gameObject.SetActive(true);
            return lifeOver.gameObject;
        }

        // Back-compat alias for older call sites.
        public GameObject DetachLifeOverCanvasForDisplay() => PrepareLifeOverCanvasForDisplay();

        public void RestoreLifeOverCanvasAfterDisplay()
        {
            if (!_lifeOverCanvasIsDetached)
            {
                return;
            }

            Transform lifeOver = FindLifeOverCanvasTransform();
            if (lifeOver != null && _lifeOverCanvasParentBeforeDetach != null)
            {
                lifeOver.SetParent(_lifeOverCanvasParentBeforeDetach, false);
                lifeOver.localPosition = _lifeOverCanvasLocalPositionBeforeDetach;
                lifeOver.localRotation = _lifeOverCanvasLocalRotationBeforeDetach;
                lifeOver.localScale = _lifeOverCanvasLocalScaleBeforeDetach;
            }

            _lifeOverCanvasIsDetached = false;
            _lifeOverCanvasParentBeforeDetach = null;
        }

        public void Refresh()
        {
            if (_waveManager == null)
            {
                return;
            }

            SetText(_waveText, $"Wave: {_waveManager.CurrentWaveNumber}");
            SetText(_currencyText, $"Balance: {ShopMoneyFormat_V2.Format(_waveManager.Currency)}");
            SetText(
                _bunkerText,
                $"Bunker HP: {_waveManager.BunkerHealth}/{_waveManager.BunkerMaxHealth}");
            SetText(_buyButtonText, _buyButtonDefaultLabel);
            SetText(
                _healthCostText,
                $"Heal: {ShopMoneyFormat_V2.Format(_waveManager.GetHealthPurchaseCost())}");
            SetText(
                _bunkerCostText,
                $"Repair: {ShopMoneyFormat_V2.Format(_waveManager.GetScaledBunkerRepairCost())}");
            EnsureLooseShopPreviewReparentedOnce();
            Transform carouselRoot = ResolveCarouselPreviewObjectsRoot();
            ReparentLooseShopPreviewsUnderCarousel(carouselRoot);
            EnsureShopNavButtonsReady();
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

            if (_uiPreviousOfferButton != null)
            {
                EnsureUiNavComponent(_uiPreviousOfferButton, ShopNavArrow_V2.ArrowDirection.Previous);
            }

            if (_uiNextOfferButton != null)
            {
                EnsureUiNavComponent(_uiNextOfferButton, ShopNavArrow_V2.ArrowDirection.Next);
            }

            ShopNavArrowUiButton_V2 previousNav = EnsureNamedShopNavButton(
                _uiPreviousOfferButtonObjectName,
                _uiPreviousOfferButtonAlternateNames,
                ShopNavArrow_V2.ArrowDirection.Previous);
            ShopNavArrowUiButton_V2 nextNav = EnsureNamedShopNavButton(
                _uiNextOfferButtonObjectName,
                _uiNextOfferButtonAlternateNames,
                ShopNavArrow_V2.ArrowDirection.Next);

            RefitShopNavButtonHitTargets();
            DisableLegacyWorldCarouselArrows();

            if (_debugShopNavigationLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] UI carousel nav bound: previous='{DescribeShopNavButton(previousNav)}', " +
                    $"next='{DescribeShopNavButton(nextNav)}', uiButtonPrev='{DescribeUiButton(_uiPreviousOfferButton)}', " +
                    $"uiButtonNext='{DescribeUiButton(_uiNextOfferButton)}'.");
            }

            if (previousNav == null)
            {
                Debug.LogWarning(
                    $"[ShopPanel_V2] TextBTN previous offer control not found. Expected '{_uiPreviousOfferButtonObjectName}' " +
                    "under ShopPanel or Visual Root.");
            }

            if (nextNav == null)
            {
                Debug.LogWarning(
                    $"[ShopPanel_V2] TextBTN next offer control not found. Expected '{_uiNextOfferButtonObjectName}' " +
                    "under ShopPanel or Visual Root.");
            }
        }

        private void BindShopActionTextButtons()
        {
            ShopNavArrowUiButton_V2 buyButton = EnsureNamedShopTextButton(
                _uiBuyButtonObjectName,
                _uiBuyButtonAlternateNames,
                ShopTextButtonBehavior.Buy);
            ShopNavArrowUiButton_V2 startButton = EnsureNamedShopTextButton(
                _uiStartGameButtonObjectName,
                _uiStartGameButtonAlternateNames,
                ShopTextButtonBehavior.StartNextWave);

            RefitShopNavButtonHitTargets();

            if (_debugShopNavigationLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] Shop action TextBTN bound: buy='{DescribeShopNavButton(buyButton)}', " +
                    $"start='{DescribeShopNavButton(startButton)}'.");
            }

            if (buyButton == null)
            {
                Debug.LogWarning(
                    $"[ShopPanel_V2] TextBTN buy control not found. Expected '{_uiBuyButtonObjectName}' " +
                    "under ShopPanel or Visual Root.");
            }

            if (startButton == null)
            {
                Debug.LogWarning(
                    $"[ShopPanel_V2] TextBTN start-game control not found. Expected '{_uiStartGameButtonObjectName}' " +
                    "under ShopPanel or Visual Root.");
            }
        }

        private ShopNavArrowUiButton_V2 EnsureNamedShopTextButton(
            string primaryObjectName,
            string[] alternateObjectNames,
            ShopTextButtonBehavior behavior)
        {
            GameObject namedRoot = FindShopObjectByNames(primaryObjectName, alternateObjectNames);
            if (namedRoot == null)
            {
                return null;
            }

            DisableLegacyShopClickHandlersOn(namedRoot, behavior);

            ShopNavArrowUiButton_V2 textButton = namedRoot.GetComponent<ShopNavArrowUiButton_V2>();
            if (textButton == null)
            {
                textButton = namedRoot.AddComponent<ShopNavArrowUiButton_V2>();
            }

            textButton.Configure(this, behavior, _waveManager);
            return textButton;
        }

        private static void DisableLegacyShopClickHandlersOn(
            GameObject root,
            ShopTextButtonBehavior behavior)
        {
            if (root == null)
            {
                return;
            }

            if (behavior == ShopTextButtonBehavior.Buy)
            {
                ShopBuyButton_V2 legacyBuy = root.GetComponent<ShopBuyButton_V2>();
                if (legacyBuy != null)
                {
                    legacyBuy.enabled = false;
                }
            }
            else if (behavior == ShopTextButtonBehavior.StartNextWave)
            {
                ShopStartWaveButton_V2 legacyStart = root.GetComponent<ShopStartWaveButton_V2>();
                if (legacyStart != null)
                {
                    legacyStart.enabled = false;
                }
            }
        }

        private ShopNavArrowUiButton_V2 EnsureNamedShopNavButton(
            string primaryObjectName,
            string[] alternateObjectNames,
            ShopNavArrow_V2.ArrowDirection direction)
        {
            GameObject namedRoot = FindShopObjectByNames(primaryObjectName, alternateObjectNames);
            if (namedRoot == null)
            {
                return null;
            }

            ShopNavArrowUiButton_V2 nav = namedRoot.GetComponent<ShopNavArrowUiButton_V2>();
            if (nav == null)
            {
                nav = namedRoot.AddComponent<ShopNavArrowUiButton_V2>();
            }

            nav.Configure(this, direction);
            DisableLegacyCarouselArrowOn(namedRoot);
            return nav;
        }

        private static void DisableLegacyCarouselArrowOn(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            ShopNavArrow_V2 legacyArrow = root.GetComponent<ShopNavArrow_V2>();
            if (legacyArrow != null)
            {
                legacyArrow.enabled = false;
            }
        }

        private void DisableLegacyWorldCarouselArrows()
        {
            DisableLegacyCarouselArrowOn(FindShopObjectByName("btn_shop_arrow_left"));
            DisableLegacyCarouselArrowOn(FindShopObjectByName("btn_shop_arrow_right"));
        }

        private void RefitShopNavButtonHitTargets()
        {
            ShopNavArrowUiButton_V2[] navButtons = GetComponentsInChildren<ShopNavArrowUiButton_V2>(true);
            for (int i = 0; i < navButtons.Length; i++)
            {
                ShopNavArrowUiButton_V2 navButton = navButtons[i];
                if (navButton != null)
                {
                    navButton.RefitHitTarget();
                }
            }
        }

        private GameObject FindShopObjectByNames(string primaryObjectName, string[] alternateObjectNames)
        {
            if (!string.IsNullOrWhiteSpace(primaryObjectName))
            {
                GameObject primary = FindShopObjectByName(primaryObjectName);
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

                GameObject alternateMatch = FindShopObjectByName(alternate);
                if (alternateMatch != null)
                {
                    return alternateMatch;
                }
            }

            return null;
        }

        private static string DescribeShopNavButton(ShopNavArrowUiButton_V2 navButton)
        {
            return navButton != null ? navButton.name : "none";
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

        private static string DescribeUiButton(Button button)
        {
            return button != null ? button.name : "none";
        }

        // Wire left arrow (e.g. btn_shop_arrow_left OnClick).
        public void OnShopArrowPreviousClicked()
        {
            RebuildVisibleShopOffersIfNeeded();
            if (_visibleShopOffers.Count == 0)
            {
                if (_debugShopNavigationLogs)
                {
                    Debug.Log("[ShopPanel_V2] OnShopArrowPrevious: no shop offers configured.");
                }

                return;
            }

            int before = _offerIndex;
            _offerIndex = (_offerIndex - 1 + _visibleShopOffers.Count) % _visibleShopOffers.Count;
            if (_debugShopNavigationLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] Shop arrow PREVIOUS: index {before} -> {_offerIndex} / {_visibleShopOffers.Count} " +
                    $"(current='{_visibleShopOffers[_offerIndex].DisplayName}')");
            }

            RefreshOfferSelection();
        }

        // Wire right arrow (e.g. btn_shop_arrow_right OnClick).
        public void OnShopArrowNextClicked()
        {
            RebuildVisibleShopOffersIfNeeded();
            if (_visibleShopOffers.Count == 0)
            {
                if (_debugShopNavigationLogs)
                {
                    Debug.Log("[ShopPanel_V2] OnShopArrowNext: no shop offers configured.");
                }

                return;
            }

            int before = _offerIndex;
            _offerIndex = (_offerIndex + 1) % _visibleShopOffers.Count;
            if (_debugShopNavigationLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] Shop arrow NEXT: index {before} -> {_offerIndex} / {_visibleShopOffers.Count} " +
                    $"(current='{_visibleShopOffers[_offerIndex].DisplayName}')");
            }

            RefreshOfferSelection();
        }

        // Wire main BUY button to purchase the currently selected carousel offer.
        public void OnPurchaseSelectedOfferClicked()
        {
            RebuildVisibleShopOffersIfNeeded();
            if (_waveManager == null || _visibleShopOffers.Count == 0)
            {
                if (_debugShopNavigationLogs)
                {
                    Debug.Log("[ShopPanel_V2] OnPurchaseSelectedOffer: missing manager or offers.");
                }

                return;
            }

            ShopOfferConfig_V2 offer = GetSelectedOffer();
            if (_debugShopNavigationLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] BUY clicked: offer='{offer.DisplayName}', kind={offer.Kind}, cost={offer.Cost}");
            }

            bool ok = _waveManager.TryPurchaseOffer(offer);
            if (ok)
            {
                AudioManager_V2.PlayPurchaseSuccess();
            }
            else
            {
                AudioManager_V2.PlayFailure();
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
            AudioManager_V2.PlayMenuClick();
            _waveManager?.StartNextWaveFromShop();
        }

        public void SetBuyButtonLabel(string label)
        {
            string nextLabel = string.IsNullOrWhiteSpace(label) ? _buyButtonDefaultLabel : label;
            SetText(_buyButtonText, nextLabel);
        }

        private void RefreshOfferSelection()
        {
            ResolveSelectedOfferItemTextIfNeeded();

            RebuildVisibleShopOffersIfNeeded();
            if (_waveManager == null || _visibleShopOffers.Count == 0)
            {
                SetText(_selectedOfferItemText, string.Empty);
                return;
            }

            ShopOfferConfig_V2 offer = GetSelectedOffer();

            SetText(_offerTitleText, offer.DisplayName);
            SetText(_offerSubtitleText, BuildOfferSubtitle(offer));
            SetText(_selectedOfferItemText, offer.DisplayName);

            Transform carouselRoot = ResolveCarouselPreviewObjectsRoot();
            EnsurePreviewCatalogReady(carouselRoot);

            if (!_shopIsVisible)
            {
                HideShopCarouselPreviews();
            }
            else
            {
                ApplySelectedOfferPreviewVisibility(carouselRoot, offer);
            }

            if (_debugShopPanelLogs)
            {
                string selectedPreviewName = ResolvePreviewObjectName(offer.PreviewObject);
                GameObject visiblePreview = FindPreviewUnderCarousel(carouselRoot, selectedPreviewName);
                Debug.Log(
                    $"[ShopPanel_V2] RefreshOfferSelection index={_offerIndex}, offer='{offer.DisplayName}', " +
                    $"previewName='{selectedPreviewName}', visiblePreview='{DescribeGameObject(visiblePreview)}', " +
                    $"carouselRoot='{DescribeTransform(carouselRoot)}', catalogCount={_resolvedPreviewByOfferIndex.Count}.");
            }

            EnsureShopNavButtonsActive();
            SetBuyButtonLabel(ResolveBuyButtonLabel(offer));
            RefreshOfferStats(offer);
        }

        private void ResolveOfferStatsIfNeeded()
        {
            if (_didResolveOfferStats)
            {
                return;
            }

            _statTmpBindingUsed.Clear();
            _offerStatsPresenter.ResolveBindings(
                FindShopStatLabelTmpForBinding,
                FindShopStatValueTmpForBinding,
                FindShopObjectByName,
                FindShopStatLabelNearValue);
            TMP_Text warningText = FindShopTmpByObjectName(
                "txt_shop_stat_warningText_label",
                allowTextContentFallback: true,
                slot: ShopStatTmpBindingSlot.Any);
            if (warningText == null)
            {
                warningText = FindShopTmpByObjectName(
                    "txt_shop_stat_warning_text",
                    allowTextContentFallback: true,
                    slot: ShopStatTmpBindingSlot.Any);
            }

            if (warningText != null)
            {
                _statTmpBindingUsed.Add(warningText);
                _offerStatsPresenter.SetWarningText(warningText, _shopWarningTextColor);
            }

            SuppressUnboundDuplicateShopStatTmps();
            _didResolveOfferStats = true;

            if (_debugShopPanelLogs)
            {
                Debug.Log("[ShopPanel_V2] Resolved shop offer stat TMP bindings.");
            }
        }

        private void RebuildOfferStatBaselinesIfNeeded()
        {
            if (_didRebuildOfferStatBaselines)
            {
                return;
            }

            _offerStatsPresenter.RebuildBaselines(_visibleShopOffers, _waveManager);
            _didRebuildOfferStatBaselines = true;
        }

        private void RefreshOfferStats(ShopOfferConfig_V2 offer)
        {
            if (!_shopIsVisible)
            {
                _offerStatsPresenter.HideAll();
                return;
            }

            ResolveOfferStatsIfNeeded();
            RebuildOfferStatBaselinesIfNeeded();
            _offerStatsPresenter.Refresh(offer, _waveManager);
        }

        private void EnsureShopNavButtonsReady()
        {
            if (_didEnsureShopNavButtons)
            {
                EnsureShopNavButtonsActive();
                return;
            }

            Transform parent = _visualRoot != null ? _visualRoot : transform;
            EnsureShopNavButtonPair(
                parent,
                _uiPreviousOfferButtonObjectName,
                _uiPreviousOfferButtonObjectName + "_Pressed",
                _textBtnMediumPrevLocalPosition);
            EnsureShopNavButtonPair(
                parent,
                _uiNextOfferButtonObjectName,
                _uiNextOfferButtonObjectName + "_Pressed",
                _textBtnMediumNextLocalPosition);
            EnsureShopNavButtonPair(
                parent,
                _uiBuyButtonObjectName,
                _uiBuyButtonObjectName + "_Pressed",
                _textBtnMediumBuyLocalPosition);
            EnsureShopNavButtonPair(
                parent,
                _uiStartGameButtonObjectName,
                _uiStartGameButtonObjectName + "_Pressed",
                _textBtnMediumStartGameLocalPosition);

            _didEnsureShopNavButtons = true;
            if (_shopIsVisible)
            {
                EnsureShopNavButtonsActive();
            }

            ResetShopNavPressedVisuals();
        }

        private void SetShopTextButtonsVisible(bool visible)
        {
            SetShopNavButtonVisible(_uiPreviousOfferButtonObjectName, visible);
            SetShopNavButtonVisible(_uiNextOfferButtonObjectName, visible);
            SetShopNavButtonVisible(_uiBuyButtonObjectName, visible);
            SetShopNavButtonVisible(_uiStartGameButtonObjectName, visible);
            SetShopNavButtonVisible(_uiPreviousOfferButtonObjectName + "_Pressed", false);
            SetShopNavButtonVisible(_uiNextOfferButtonObjectName + "_Pressed", false);
            SetShopNavButtonVisible(_uiBuyButtonObjectName + "_Pressed", false);
            SetShopNavButtonVisible(_uiStartGameButtonObjectName + "_Pressed", false);

            if (!visible)
            {
                ShopNavArrowUiButton_V2[] navButtons = GetComponentsInChildren<ShopNavArrowUiButton_V2>(true);
                for (int i = 0; i < navButtons.Length; i++)
                {
                    ShopNavArrowUiButton_V2 navButton = navButtons[i];
                    if (navButton != null)
                    {
                        navButton.ResetToNormalVisual();
                    }
                }
            }
        }

        private void EnsureShopNavButtonPair(
            Transform parent,
            string normalName,
            string pressedName,
            Vector3 localPosition)
        {
            if (parent == null)
            {
                return;
            }

            if (FindShopObjectByName(normalName) == null && _textBtnMediumNormalPrefab != null)
            {
                GameObject normal = Instantiate(_textBtnMediumNormalPrefab, parent);
                normal.name = normalName;
                normal.transform.localPosition = localPosition;
                normal.transform.localRotation = Quaternion.identity;
                normal.transform.localScale = Vector3.one;
                SetLayerRecursively(normal, parent.gameObject.layer);
            }

            if (FindShopObjectByName(pressedName) == null && _textBtnMediumPressedPrefab != null)
            {
                GameObject pressed = Instantiate(_textBtnMediumPressedPrefab, parent);
                pressed.name = pressedName;
                pressed.transform.localPosition = localPosition;
                pressed.transform.localRotation = Quaternion.identity;
                pressed.transform.localScale = Vector3.one;
                SetLayerRecursively(pressed, parent.gameObject.layer);
            }
        }

        // Keeps normal TextBTN roots alive during carousel refresh without clearing an in-progress pressed visual.
        private void EnsureShopNavButtonsActive()
        {
            if (!_shopIsVisible)
            {
                return;
            }

            EnsureNormalShopNavButtonActive(_uiPreviousOfferButtonObjectName);
            EnsureNormalShopNavButtonActive(_uiNextOfferButtonObjectName);
            EnsureNormalShopNavButtonActive(_uiBuyButtonObjectName);
            EnsureNormalShopNavButtonActive(_uiStartGameButtonObjectName);
            SyncShopNavPressedSiblingTransform(_uiPreviousOfferButtonObjectName);
            SyncShopNavPressedSiblingTransform(_uiNextOfferButtonObjectName);
            SyncShopNavPressedSiblingTransform(_uiBuyButtonObjectName);
            SyncShopNavPressedSiblingTransform(_uiStartGameButtonObjectName);
        }

        private void EnsureNormalShopNavButtonActive(string objectName)
        {
            GameObject target = FindShopObjectByName(objectName);
            if (target == null || target.activeSelf)
            {
                return;
            }

            target.SetActive(true);
            EnsurePreviewRenderersVisible(target);
        }

        private void ResetShopNavPressedVisuals()
        {
            SetShopNavButtonVisible(_uiPreviousOfferButtonObjectName + "_Pressed", false);
            SetShopNavButtonVisible(_uiNextOfferButtonObjectName + "_Pressed", false);
            SetShopNavButtonVisible(_uiBuyButtonObjectName + "_Pressed", false);
            SetShopNavButtonVisible(_uiStartGameButtonObjectName + "_Pressed", false);

            ShopNavArrowUiButton_V2[] navButtons = GetComponentsInChildren<ShopNavArrowUiButton_V2>(true);
            for (int i = 0; i < navButtons.Length; i++)
            {
                ShopNavArrowUiButton_V2 navButton = navButtons[i];
                if (navButton != null)
                {
                    navButton.ResetToNormalVisual();
                }
            }
        }

        private void SyncShopNavPressedSiblingTransform(string normalObjectName)
        {
            if (string.IsNullOrWhiteSpace(normalObjectName))
            {
                return;
            }

            GameObject normal = FindShopObjectByName(normalObjectName);
            GameObject pressed = FindShopObjectByName(normalObjectName + "_Pressed");
            if (normal == null || pressed == null)
            {
                return;
            }

            Transform normalTransform = normal.transform;
            Transform pressedTransform = pressed.transform;
            pressedTransform.localPosition = normalTransform.localPosition;
            pressedTransform.localRotation = normalTransform.localRotation;
            pressedTransform.localScale = normalTransform.localScale;
        }

        private void SetShopNavButtonVisible(string objectName, bool visible)
        {
            GameObject target = FindShopObjectByName(objectName);
            if (target == null)
            {
                return;
            }

            target.SetActive(visible);
            if (visible)
            {
                EnsurePreviewRenderersVisible(target);
            }
        }

        private GameObject FindShopObjectByName(string objectName)
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

                Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate != null &&
                        candidate.gameObject.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate.gameObject;
                    }
                }
            }

            return null;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
            {
                return;
            }

            root.layer = layer;
            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null)
                {
                    SetLayerRecursively(child.gameObject, layer);
                }
            }
        }

        private static bool IsShopNavButtonObjectName(string objectName)
        {
            return !string.IsNullOrWhiteSpace(objectName) &&
                   objectName.StartsWith("TextBTN_Medium", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsShopTextButtonSprite(SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer == null)
            {
                return false;
            }

            Transform walk = spriteRenderer.transform;
            while (walk != null)
            {
                if (IsShopNavButtonObjectName(walk.name) ||
                    (walk.name.EndsWith("_Pressed", System.StringComparison.OrdinalIgnoreCase) &&
                     IsShopNavButtonObjectName(
                         walk.name.Substring(0, walk.name.Length - "_Pressed".Length))))
                {
                    return true;
                }

                walk = walk.parent;
            }

            return false;
        }

        private void InvalidatePreviewCatalog()
        {
            _didBuildPreviewCatalog = false;
            _shopOfferCountWhenCatalogBuilt = -1;
            _resolvedPreviewByOfferIndex.Clear();
        }

        private void EnsurePreviewCatalogReady(Transform carouselRoot)
        {
            RebuildVisibleShopOffersIfNeeded();
            int offerCount = _visibleShopOffers.Count;
            if (_didBuildPreviewCatalog && _shopOfferCountWhenCatalogBuilt == offerCount)
            {
                return;
            }

            EnsureCarouselPreviewCatalogReady(carouselRoot);
            _didBuildPreviewCatalog = true;
            _shopOfferCountWhenCatalogBuilt = offerCount;
        }

        private void EnsureCarouselPreviewCatalogReady(Transform carouselRoot)
        {
            if (carouselRoot == null || _visibleShopOffers.Count == 0)
            {
                return;
            }

            ReparentLooseShopPreviewsUnderCarousel(carouselRoot);
            _resolvedPreviewByOfferIndex.Clear();

            for (int i = 0; i < _visibleShopOffers.Count; i++)
            {
                ShopOfferConfig_V2 offer = _visibleShopOffers[i];
                GameObject resolved = ResolvePreviewForOffer(i, offer, carouselRoot);

                if (resolved != null)
                {
                    _resolvedPreviewByOfferIndex[i] = resolved;
                }
                else if (_debugShopPanelLogs && offer.PreviewObject != null)
                {
                    Debug.LogWarning(
                        $"[ShopPanel_V2] No preview resolved for offer[{i}] '{offer.DisplayName}' " +
                        $"(previewRef='{DescribeGameObject(offer.PreviewObject)}').");
                }
            }
        }

        private GameObject ResolvePreviewForOffer(int offerIndex, ShopOfferConfig_V2 offer, Transform carouselRoot)
        {
            if (offer == null || offer.PreviewObject == null || carouselRoot == null)
            {
                return null;
            }

            if (offerIndex >= 0 &&
                _resolvedPreviewByOfferIndex.TryGetValue(offerIndex, out GameObject cached) &&
                cached != null)
            {
                return cached;
            }

            string previewName = ResolvePreviewObjectName(offer.PreviewObject);
            GameObject sceneMatch = FindPreviewUnderCarousel(carouselRoot, previewName);
            if (sceneMatch == null)
            {
                sceneMatch = FindLoadedScenePreviewByName(previewName);
                if (sceneMatch != null)
                {
                    EnsurePreviewUnderCarouselRoot(sceneMatch.transform, carouselRoot);
                }
            }

            if (sceneMatch != null)
            {
                return sceneMatch;
            }

            return ResolveOrCreatePreviewInstance(offer.PreviewObject, carouselRoot);
        }

        private void ApplySelectedOfferPreviewVisibility(Transform carouselRoot, ShopOfferConfig_V2 selectedOffer)
        {
            if (carouselRoot == null || selectedOffer == null)
            {
                return;
            }

            EnsureActiveHierarchy(carouselRoot.gameObject);

            GameObject selectedPreview = ResolvePreviewForOffer(_offerIndex, selectedOffer, carouselRoot);
            string selectedPreviewName = ResolvePreviewObjectName(selectedOffer.PreviewObject);

            HideAllShopPreviewsUnder(carouselRoot);

            if (selectedPreview != null)
            {
                ShowShopPreview(selectedPreview);
            }
            else if (_debugShopPanelLogs)
            {
                Debug.LogWarning(
                    $"[ShopPanel_V2] Selected preview not found for '{selectedOffer.DisplayName}' " +
                    $"(expected name '{selectedPreviewName}').");
            }
        }

        private void HideShopCarouselPreviews()
        {
            HideAllShopPreviewsUnder(ResolveCarouselPreviewObjectsRoot());
        }

        private void HideAllShopPreviewsUnder(Transform carouselRoot)
        {
            if (carouselRoot == null)
            {
                return;
            }

            Transform[] transforms = carouselRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate == carouselRoot)
                {
                    continue;
                }

                if (!IsShopPreviewObjectName(candidate.name))
                {
                    continue;
                }

                candidate.gameObject.SetActive(false);
            }

            DisableUnnamedCarouselPreviewSprites(carouselRoot);
        }

        // Covers editor names like Thompson-preview-bild / thompson_temp that do not use shop_* yet.
        private void DisableUnnamedCarouselPreviewSprites(Transform carouselRoot)
        {
            if (carouselRoot == null)
            {
                return;
            }

            SpriteRenderer[] spriteRenderers = carouselRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null ||
                    IsShopTextButtonSprite(spriteRenderer) ||
                    IsShopPreviewSprite(spriteRenderer))
                {
                    continue;
                }

                spriteRenderer.enabled = false;
            }
        }

        private void ShowShopPreview(GameObject preview)
        {
            if (preview == null)
            {
                return;
            }

            Transform carouselRoot = ResolveCarouselPreviewObjectsRoot();
            if (carouselRoot != null)
            {
                preview.transform.SetParent(carouselRoot, false);
                preview.transform.localPosition = _previewDisplayLocalPosition;
                preview.transform.SetAsLastSibling();
            }

            EnsureActiveHierarchy(preview);
            preview.SetActive(true);
            EnsurePreviewRenderersVisible(preview);
            ApplyPreviewSorting(preview);

            if (_debugShopPanelLogs)
            {
                SpriteRenderer spriteRenderer = preview.GetComponentInChildren<SpriteRenderer>(true);
                string spriteName = spriteRenderer != null && spriteRenderer.sprite != null
                    ? spriteRenderer.sprite.name
                    : "none";
                int appliedOrder = ResolveShopPreviewSortingOrder();
                Debug.Log(
                    $"[ShopPanel_V2] ShowShopPreview '{preview.name}' layer={spriteRenderer?.sortingLayerName} " +
                    $"order={spriteRenderer?.sortingOrder} (target={appliedOrder}) sprite={spriteName} " +
                    $"active={preview.activeSelf} worldPos={preview.transform.position}.");
            }
        }

        private void ApplyPreviewSorting(GameObject previewRoot)
        {
            if (previewRoot == null)
            {
                return;
            }

            int shopLayerId = ResolveShopPreviewSortingLayerId();
            int sortingOrder = ResolveShopPreviewSortingOrder();

            SortingGroup staleSortingGroup = previewRoot.GetComponent<SortingGroup>();
            if (staleSortingGroup != null)
            {
                Destroy(staleSortingGroup);
            }

            SpriteRenderer[] spriteRenderers = previewRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer != null)
                {
                    spriteRenderer.sortingLayerID = shopLayerId;
                    spriteRenderer.sortingOrder = sortingOrder;
                }
            }
        }

        private int ResolveShopPreviewSortingOrder()
        {
            // ShopPanel background uses ~200 on layer Shop; TextBTN ~205. Previews must sit above both.
            const int shopBackgroundOrder = 200;
            const int minimumPreviewOrder = 220;
            if (_shopPreviewSortingOrder <= shopBackgroundOrder + 5)
            {
                return minimumPreviewOrder;
            }

            return _shopPreviewSortingOrder;
        }

        private int ResolveShopPreviewSortingLayerId()
        {
            if (!string.IsNullOrWhiteSpace(_shopPreviewSortingLayerName))
            {
                int namedLayerId = SortingLayer.NameToID(_shopPreviewSortingLayerName);
                if (namedLayerId != 0)
                {
                    return namedLayerId;
                }
            }

            return SortingLayer.NameToID("Shop");
        }

        private static void EnsureActiveHierarchy(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            Transform walk = target.transform;
            while (walk != null)
            {
                if (!walk.gameObject.activeSelf)
                {
                    walk.gameObject.SetActive(true);
                }

                walk = walk.parent;
            }
        }

        private GameObject FindPreviewUnderCarousel(Transform carouselRoot, string previewName)
        {
            if (carouselRoot == null || string.IsNullOrWhiteSpace(previewName))
            {
                return null;
            }

            return FindPreviewChildByName(carouselRoot, previewName);
        }

        private static string ResolvePreviewObjectName(GameObject previewRef)
        {
            if (previewRef == null)
            {
                return string.Empty;
            }

            string name = previewRef.name;
            const string cloneSuffix = "(Clone)";
            if (name.EndsWith(cloneSuffix, System.StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - cloneSuffix.Length).Trim();
            }

            return name;
        }

        private static string DescribeGameObject(GameObject target)
        {
            return target != null ? target.name : "none";
        }

        private void ReparentLooseShopPreviewsUnderCarousel(Transform carouselRoot)
        {
            if (carouselRoot == null)
            {
                return;
            }

            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    candidate.parent != null ||
                    !IsShopPreviewObjectName(candidate.name))
                {
                    continue;
                }

                if (IsDescendantOf(candidate, carouselRoot))
                {
                    continue;
                }

                candidate.SetParent(carouselRoot, true);
                candidate.gameObject.SetActive(false);

                if (_debugShopPanelLogs)
                {
                    Debug.Log(
                        $"[ShopPanel_V2] Reparented loose shop preview '{candidate.name}' under '{carouselRoot.name}'.");
                }
            }
        }

        private GameObject ResolveOrCreatePreviewInstance(GameObject previewRef, Transform carouselRoot)
        {
            if (previewRef == null || carouselRoot == null)
            {
                return null;
            }

            if (IsLoadedSceneObject(previewRef))
            {
                if (IsShopNavButtonObjectName(previewRef.name))
                {
                    return null;
                }

                EnsurePreviewUnderCarouselRoot(previewRef.transform, carouselRoot);
                return previewRef;
            }

            string previewName = ResolvePreviewObjectName(previewRef);
            GameObject sceneMatch = FindPreviewChildByName(carouselRoot, previewName);
            if (sceneMatch == null)
            {
                sceneMatch = FindLoadedScenePreviewByName(previewName);
                if (sceneMatch != null)
                {
                    EnsurePreviewUnderCarouselRoot(sceneMatch.transform, carouselRoot);
                }
            }

            if (sceneMatch != null)
            {
                return sceneMatch;
            }

            int sourceId = previewRef.GetInstanceID();
            if (_runtimeInstantiatedPreviewsBySourceId.TryGetValue(sourceId, out GameObject cachedInstance) &&
                cachedInstance != null)
            {
                return cachedInstance;
            }

            GameObject instance = Instantiate(previewRef, carouselRoot);
            instance.name = previewName;
            instance.SetActive(false);
            _runtimeInstantiatedPreviewsBySourceId[sourceId] = instance;

            if (_debugShopPanelLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] Instantiated shop preview '{previewName}' under '{carouselRoot.name}'.");
            }

            return instance;
        }

        private static bool IsLoadedSceneObject(GameObject target)
        {
            return target != null && target.scene.IsValid() && target.scene.isLoaded;
        }

        private static bool IsShopPreviewObjectName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            if (objectName.StartsWith("shop_", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (objectName.Contains("preview", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return objectName.Equals("thompson_temp", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsurePreviewUnderCarouselRoot(Transform previewTransform, Transform carouselRoot)
        {
            if (previewTransform == null || carouselRoot == null)
            {
                return;
            }

            if (previewTransform.parent == carouselRoot || previewTransform.IsChildOf(carouselRoot))
            {
                return;
            }

            previewTransform.SetParent(carouselRoot, true);
        }

        private static GameObject FindPreviewChildByName(Transform carouselRoot, string objectName)
        {
            if (carouselRoot == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform[] transforms = carouselRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    !IsShopNavButtonObjectName(candidate.gameObject.name) &&
                    candidate.gameObject.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate.gameObject;
                }
            }

            return null;
        }

        private static GameObject FindLoadedScenePreviewByName(string previewName)
        {
            if (string.IsNullOrWhiteSpace(previewName))
            {
                return null;
            }

            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    !IsShopPreviewObjectName(candidate.name) ||
                    IsShopNavButtonObjectName(candidate.name) ||
                    !candidate.name.Equals(previewName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject sceneObject = candidate.gameObject;
                if (IsLoadedSceneObject(sceneObject))
                {
                    return sceneObject;
                }
            }

            return null;
        }

        private static void EnsurePreviewRenderersVisible(GameObject previewRoot)
        {
            if (previewRoot == null)
            {
                return;
            }

            SpriteRenderer[] spriteRenderers = previewRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                spriteRenderer.enabled = true;
                Color color = spriteRenderer.color;
                color.a = 1f;
                spriteRenderer.color = color;
            }
        }

        private static string DescribeTransform(Transform target)
        {
            return target != null ? target.name : "none";
        }

        private Transform _resolvedCarouselPreviewRoot;

        private Transform ResolveCarouselPreviewObjectsRoot()
        {
            if (_carouselPreviewObjectsRoot != null)
            {
                return _carouselPreviewObjectsRoot;
            }

            if (_resolvedCarouselPreviewRoot != null)
            {
                return _resolvedCarouselPreviewRoot;
            }

            Transform searchRoot = _visualRoot != null ? _visualRoot : transform;
            if (searchRoot == null)
            {
                return null;
            }

            Transform weapons = FindChildTransformByName(searchRoot, "Weapons");
            if (weapons != null)
            {
                _resolvedCarouselPreviewRoot = weapons;
                return weapons;
            }

            Transform items = FindChildTransformByName(searchRoot, "Items");
            if (items != null)
            {
                _resolvedCarouselPreviewRoot = items;
                return items;
            }

            Transform previewContainer = FindShopPreviewContainerUnder(searchRoot);
            _resolvedCarouselPreviewRoot = previewContainer != null ? previewContainer : searchRoot;
            return _resolvedCarouselPreviewRoot;
        }

        private static Transform FindChildTransformByName(Transform searchRoot, string objectName)
        {
            if (searchRoot == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (searchRoot.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return searchRoot;
            }

            Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    candidate.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform FindShopPreviewContainerUnder(Transform searchRoot)
        {
            if (searchRoot == null)
            {
                return null;
            }

            for (int i = 0; i < searchRoot.childCount; i++)
            {
                Transform child = searchRoot.GetChild(i);
                if (child != null && HasDirectShopPreviewChild(child))
                {
                    return child;
                }
            }

            return null;
        }

        private static bool HasDirectShopPreviewChild(Transform parent)
        {
            if (parent == null)
            {
                return false;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && IsShopPreviewObjectName(child.name))
                {
                    return true;
                }
            }

            return false;
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

        private bool TryBuildOwnedWeaponAmmoSubtitle(HeroWeaponDefinition_V2 weapon, out string subtitle)
        {
            subtitle = string.Empty;
            if (_waveManager == null || weapon == null)
            {
                return false;
            }

            Hero_V2 hero = _waveManager.Hero;
            if (hero == null || !hero.TryGetOwnedWeaponAmmo(
                    weapon,
                    out int mag,
                    out int maxMag,
                    out int reserve,
                    out int maxReserve))
            {
                return false;
            }

            subtitle = ShopOfferStatsPresenter_V2.FormatOwnedWeaponAmmoDisplay(
                weapon.WeaponType,
                mag,
                maxMag,
                reserve,
                maxReserve);
            return true;
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
                        : ShopMoneyFormat_V2.FormatCost(_waveManager.GetOfferEffectiveCost(offer));

                case ShopOfferKind_V2.BunkerRepair:
                    return _waveManager.IsBunkerFullHealth()
                        ? "Bunker full"
                        : ShopMoneyFormat_V2.FormatCost(_waveManager.GetOfferEffectiveCost(offer));

                case ShopOfferKind_V2.BunkerMaxUpgrade:
                    if (_waveManager.IsBunkerMaxAtCap())
                    {
                        return "Bunker max cap";
                    }

                    return $"{ShopMoneyFormat_V2.FormatCost(_waveManager.GetOfferEffectiveCost(offer))} (+max HP)";

                case ShopOfferKind_V2.WeaponUnlock:
                    if (offer.Weapon == null)
                    {
                        return string.Empty;
                    }

                    if (_waveManager.IsWeaponOwned(offer.Weapon))
                    {
                        if (TryBuildOwnedWeaponAmmoSubtitle(offer.Weapon, out string ammoSubtitle))
                        {
                            if (_waveManager.IsWeaponAmmoFull(offer.Weapon))
                            {
                                return $"{ammoSubtitle} · Full";
                            }

                            return $"{ammoSubtitle} · Refill {ShopMoneyFormat_V2.FormatCost(_waveManager.GetOfferEffectiveCost(offer))}";
                        }

                        if (_waveManager.IsWeaponAmmoFull(offer.Weapon))
                        {
                            return "Ammo full";
                        }

                        return $"Ammo {ShopMoneyFormat_V2.FormatCost(_waveManager.GetOfferEffectiveCost(offer))}";
                    }

                    string role = offer.Weapon != null && offer.Weapon.WeaponType == iStick2War.WeaponType.Minigun
                        ? "Role: DPS"
                        : offer.Weapon != null && offer.Weapon.WeaponType == iStick2War.WeaponType.Tesla
                            ? "Role: Control"
                            : "";
                    return string.IsNullOrEmpty(role)
                        ? ShopMoneyFormat_V2.FormatCost(offer.Cost)
                        : $"{ShopMoneyFormat_V2.FormatCost(offer.Cost)} ({role})";

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
                        : ShopMoneyFormat_V2.FormatCost(offer.Cost);

                default:
                    return ShopMoneyFormat_V2.FormatCost(offer.Cost);
            }
        }

        private string ResolveBuyButtonLabel(ShopOfferConfig_V2 offer)
        {
            if (_waveManager == null)
            {
                return _buyButtonDefaultLabel;
            }

            int effectiveCost = _waveManager.GetOfferEffectiveCost(offer);
            bool canAfford = _waveManager.CanAfford(effectiveCost);
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
                        if (_waveManager.IsWeaponAmmoFull(offer.Weapon))
                        {
                            return "FULL";
                        }

                        return canAfford ? _buyButtonDefaultLabel : "NO CASH";
                    }

                    return canAfford ? _buyButtonDefaultLabel : "NO CASH";

                default:
                    return _buyButtonDefaultLabel;
            }
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

        private void ResolveSelectedOfferItemTextIfNeeded()
        {
            if (_selectedOfferItemText != null || string.IsNullOrWhiteSpace(_selectedOfferItemTextObjectName))
            {
                return;
            }

            _selectedOfferItemText = FindShopTmpByObjectName(_selectedOfferItemTextObjectName);

            if (_selectedOfferItemText != null && _debugShopPanelLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] Bound selected offer item text '{_selectedOfferItemTextObjectName}' " +
                    $"-> '{_selectedOfferItemText.name}'.");
            }
        }

        private enum ShopStatTmpBindingSlot
        {
            Any,
            Label,
            Value,
        }

        private TMP_Text FindShopStatLabelTmpForBinding(string objectName)
        {
            return RegisterShopStatTmpBinding(
                FindShopTmpByObjectName(objectName, allowTextContentFallback: true, slot: ShopStatTmpBindingSlot.Label));
        }

        private TMP_Text FindShopStatValueTmpForBinding(string objectName)
        {
            return RegisterShopStatTmpBinding(
                FindShopTmpByObjectName(objectName, allowTextContentFallback: true, slot: ShopStatTmpBindingSlot.Value));
        }

        private TMP_Text RegisterShopStatTmpBinding(TMP_Text match)
        {
            if (match != null)
            {
                _statTmpBindingUsed.Add(match);
            }

            return match;
        }

        private TMP_Text FindShopStatLabelNearValue(TMP_Text valueText, string labelObjectName, string[] labelAlternateNames)
        {
            if (valueText == null)
            {
                return null;
            }

            TMP_Text match = FindShopStatLabelNearValueOnParent(valueText.transform.parent, labelObjectName);
            if (match != null)
            {
                return match;
            }

            if (labelAlternateNames == null)
            {
                return null;
            }

            for (int i = 0; i < labelAlternateNames.Length; i++)
            {
                match = FindShopStatLabelNearValueOnParent(valueText.transform.parent, labelAlternateNames[i]);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private TMP_Text FindShopStatLabelNearValueOnParent(Transform parent, string labelObjectName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(labelObjectName))
            {
                return null;
            }

            string normalizedTarget = NormalizeShopUiName(labelObjectName);
            Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null)
                {
                    continue;
                }

                string normalizedCandidateName = NormalizeShopUiName(candidate.name);
                bool nameMatches = normalizedCandidateName.Equals(normalizedTarget, System.StringComparison.Ordinal) ||
                                   MatchesArmorPenLabelFuzzy(normalizedTarget, normalizedCandidateName);
                if (!nameMatches)
                {
                    continue;
                }

                TMP_Text labelText = GetTmpOnTransform(candidate);
                if (labelText == null ||
                    _statTmpBindingUsed.Contains(labelText) ||
                    !MatchesBindingSlot(labelText, ShopStatTmpBindingSlot.Label))
                {
                    continue;
                }

                _statTmpBindingUsed.Add(labelText);
                return labelText;
            }

            return null;
        }

        private TMP_Text FindShopTmpByObjectName(
            string objectName,
            bool allowTextContentFallback = false,
            ShopStatTmpBindingSlot slot = ShopStatTmpBindingSlot.Any)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            string normalizedTarget = NormalizeShopUiName(objectName);
            if (string.IsNullOrEmpty(normalizedTarget))
            {
                return null;
            }

            List<TMP_Text> matches = new List<TMP_Text>();
            CollectShopStatTmpMatches(normalizedTarget, matches, matchTextContent: false, slot);
            TMP_Text best = PickBestShopStatTmp(matches, normalizedTarget);
            if (best != null)
            {
                return best;
            }

            if (!allowTextContentFallback || !normalizedTarget.StartsWith("txt_shop_stat", System.StringComparison.Ordinal))
            {
                return null;
            }

            matches.Clear();
            CollectShopStatTmpMatches(normalizedTarget, matches, matchTextContent: true, slot);
            return PickBestShopStatTmp(matches, normalizedTarget);
        }

        private void SuppressUnboundDuplicateShopStatTmps()
        {
            Transform statsRoot = ResolveShopStatsContainerRoot();
            if (statsRoot == null)
            {
                return;
            }

            TMP_Text[] texts = statsRoot.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null ||
                    _statTmpBindingUsed.Contains(text) ||
                    !IsShopStatObjectName(text.gameObject.name))
                {
                    continue;
                }

                text.gameObject.SetActive(false);
            }
        }

        private Transform ResolveShopStatsContainerRoot()
        {
            GameObject panel = FindShopObjectByName("panel_shop_stats");
            if (panel != null)
            {
                return panel.transform;
            }

            panel = FindShopObjectByName("ShopStatsContainer");
            return panel != null ? panel.transform : null;
        }

        private static bool IsShopStatObjectName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            string normalized = NormalizeShopUiName(objectName);
            if (normalized.Contains("warning", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return normalized.StartsWith("txt_shop_stat", System.StringComparison.Ordinal);
        }

        private void CollectShopStatTmpMatches(
            string normalizedTarget,
            List<TMP_Text> matches,
            bool matchTextContent,
            ShopStatTmpBindingSlot slot)
        {
            if (matches == null || string.IsNullOrEmpty(normalizedTarget))
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

                CollectShopStatTmpMatchesUnderRoot(searchRoot, normalizedTarget, matches, matchTextContent, slot);
            }

            TMP_Text[] allTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < allTexts.Length; i++)
            {
                TryAddShopStatTmpMatchFromText(allTexts[i], normalizedTarget, matches, matchTextContent, slot);
            }
        }

        private void CollectShopStatTmpMatchesUnderRoot(
            Transform searchRoot,
            string normalizedTarget,
            List<TMP_Text> matches,
            bool matchTextContent,
            ShopStatTmpBindingSlot slot)
        {
            Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                TryAddShopStatTmpMatch(transforms[i], normalizedTarget, matches, matchTextContent, slot);
            }
        }

        private void TryAddShopStatTmpMatch(
            Transform candidate,
            string normalizedTarget,
            List<TMP_Text> matches,
            bool matchTextContent,
            ShopStatTmpBindingSlot slot)
        {
            if (candidate == null)
            {
                return;
            }

            string normalizedCandidateName = NormalizeShopUiName(candidate.name);
            bool transformNameMatches = normalizedCandidateName.Equals(normalizedTarget, System.StringComparison.Ordinal);
            bool fuzzyArmorPenLabelMatch =
                slot == ShopStatTmpBindingSlot.Label &&
                MatchesArmorPenLabelFuzzy(normalizedTarget, normalizedCandidateName);
            TMP_Text text = GetTmpOnTransform(candidate);
            if (text == null || _statTmpBindingUsed.Contains(text) || matches.Contains(text))
            {
                return;
            }

            if (!MatchesBindingSlot(text, slot))
            {
                return;
            }

            if (transformNameMatches || fuzzyArmorPenLabelMatch)
            {
                matches.Add(text);
                return;
            }

            if (matchTextContent &&
                NormalizeShopUiName(text.text).Equals(normalizedTarget, System.StringComparison.Ordinal) &&
                MatchesStatObjectIdentity(text, normalizedTarget))
            {
                matches.Add(text);
            }
        }

        private void TryAddShopStatTmpMatchFromText(
            TMP_Text text,
            string normalizedTarget,
            List<TMP_Text> matches,
            bool matchTextContent,
            ShopStatTmpBindingSlot slot)
        {
            if (text == null || _statTmpBindingUsed.Contains(text) || matches.Contains(text))
            {
                return;
            }

            if (!MatchesBindingSlot(text, slot))
            {
                return;
            }

            if (NormalizeShopUiName(text.gameObject.name).Equals(normalizedTarget, System.StringComparison.Ordinal) ||
                HasAncestorWithNormalizedName(text.transform, normalizedTarget) ||
                (slot == ShopStatTmpBindingSlot.Label &&
                 MatchesArmorPenLabelFuzzy(normalizedTarget, NormalizeShopUiName(text.gameObject.name))))
            {
                matches.Add(text);
                return;
            }

            if (matchTextContent &&
                NormalizeShopUiName(text.text).Equals(normalizedTarget, System.StringComparison.Ordinal) &&
                MatchesStatObjectIdentity(text, normalizedTarget))
            {
                matches.Add(text);
            }
        }

        private static bool MatchesStatObjectIdentity(TMP_Text text, string normalizedTarget)
        {
            if (text == null || string.IsNullOrEmpty(normalizedTarget))
            {
                return false;
            }

            string normalizedObjectName = NormalizeShopUiName(text.gameObject.name);
            if (normalizedObjectName.Equals(normalizedTarget, System.StringComparison.Ordinal))
            {
                return true;
            }

            if (HasAncestorWithNormalizedName(text.transform, normalizedTarget))
            {
                return true;
            }

            return MatchesArmorPenLabelFuzzy(normalizedTarget, normalizedObjectName);
        }

        private static bool MatchesArmorPenLabelFuzzy(string normalizedTarget, string normalizedCandidateName)
        {
            if (!normalizedTarget.Contains("armor_pen_label", System.StringComparison.Ordinal))
            {
                return false;
            }

            if (!normalizedCandidateName.Contains("armor", System.StringComparison.Ordinal))
            {
                return false;
            }

            if (!normalizedCandidateName.Contains("pen", System.StringComparison.Ordinal) &&
                !normalizedCandidateName.Contains("pe_n", System.StringComparison.Ordinal))
            {
                return false;
            }

            if (normalizedCandidateName.Contains("value", System.StringComparison.Ordinal))
            {
                return false;
            }

            return normalizedCandidateName.Contains("label", System.StringComparison.Ordinal) ||
                   normalizedCandidateName.Contains("labael", System.StringComparison.Ordinal) ||
                   normalizedCandidateName.Contains("labeal", System.StringComparison.Ordinal);
        }

        private static bool MatchesBindingSlot(TMP_Text text, ShopStatTmpBindingSlot slot)
        {
            if (text == null || slot == ShopStatTmpBindingSlot.Any)
            {
                return true;
            }

            string objectName = NormalizeShopUiName(text.gameObject.name);
            string hierarchyNames = GetNormalizedHierarchyNames(text.transform);
            bool looksLikeValue =
                objectName.Contains("value", System.StringComparison.Ordinal) ||
                hierarchyNames.Contains("value", System.StringComparison.Ordinal);
            bool looksLikeLabel =
                objectName.Contains("label", System.StringComparison.Ordinal) ||
                objectName.Contains("labael", System.StringComparison.Ordinal) ||
                objectName.Contains("labeal", System.StringComparison.Ordinal) ||
                hierarchyNames.Contains("label", System.StringComparison.Ordinal) ||
                hierarchyNames.Contains("labael", System.StringComparison.Ordinal) ||
                hierarchyNames.Contains("labeal", System.StringComparison.Ordinal);

            if (slot == ShopStatTmpBindingSlot.Label)
            {
                return !looksLikeValue || looksLikeLabel;
            }

            return !looksLikeLabel || looksLikeValue;
        }

        private static string GetNormalizedHierarchyNames(Transform node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            Transform walk = node;
            while (walk != null)
            {
                builder.Append(NormalizeShopUiName(walk.name));
                walk = walk.parent;
            }

            return builder.ToString();
        }

        private static bool HasAncestorWithNormalizedName(Transform node, string normalizedTarget)
        {
            Transform walk = node != null ? node.parent : null;
            while (walk != null)
            {
                if (NormalizeShopUiName(walk.name).Equals(normalizedTarget, System.StringComparison.Ordinal))
                {
                    return true;
                }

                walk = walk.parent;
            }

            return false;
        }

        private TMP_Text PickBestShopStatTmp(List<TMP_Text> matches, string normalizedTarget)
        {
            if (matches == null || matches.Count == 0)
            {
                return null;
            }

            Transform preferredRoot = _visualRoot != null ? _visualRoot : transform;
            TMP_Text best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < matches.Count; i++)
            {
                TMP_Text candidate = matches[i];
                if (candidate == null)
                {
                    continue;
                }

                int score = ScoreShopStatTmpCandidate(candidate, normalizedTarget, preferredRoot);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private static int ScoreShopStatTmpCandidate(TMP_Text candidate, string normalizedTarget, Transform preferredRoot)
        {
            int score = 0;
            if (candidate.gameObject.activeInHierarchy)
            {
                score += 8;
            }

            if (preferredRoot != null && candidate.transform.IsChildOf(preferredRoot))
            {
                score += 16;
            }

            string normalizedObjectName = NormalizeShopUiName(candidate.gameObject.name);
            if (!string.IsNullOrEmpty(normalizedTarget) &&
                normalizedObjectName.Equals(normalizedTarget, System.StringComparison.Ordinal))
            {
                score += 200;
            }
            else if (!string.IsNullOrEmpty(normalizedTarget) &&
                     MatchesArmorPenLabelFuzzy(normalizedTarget, normalizedObjectName))
            {
                score += 180;
            }
            else if (HasAncestorWithNormalizedName(candidate.transform, normalizedTarget))
            {
                score += 150;
            }

            if (candidate.rectTransform != null)
            {
                Vector2 size = candidate.rectTransform.sizeDelta;
                if (size.x > 4f || size.y > 4f)
                {
                    score += 24;
                }

                if (Mathf.Abs(candidate.rectTransform.anchoredPosition.x) > 0.5f ||
                    Mathf.Abs(candidate.rectTransform.anchoredPosition.y) > 0.5f)
                {
                    score += 12;
                }
            }

            string normalizedText = NormalizeShopUiName(candidate.text);
            if (!string.IsNullOrEmpty(normalizedTarget) &&
                normalizedText.Equals(normalizedTarget, System.StringComparison.Ordinal))
            {
                score += 6;
            }
            else if (!string.IsNullOrEmpty(normalizedTarget) &&
                     !string.IsNullOrEmpty(normalizedText) &&
                     !normalizedText.Equals(normalizedTarget, System.StringComparison.Ordinal) &&
                     normalizedText.Contains("txt_shop_stat", System.StringComparison.Ordinal))
            {
                score -= 80;
            }

            return score;
        }

        private static TMP_Text GetTmpOnTransform(Transform transformNode)
        {
            if (transformNode == null)
            {
                return null;
            }

            TMP_Text text = transformNode.GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }

            return transformNode.GetComponentInChildren<TMP_Text>(true);
        }

        private static string NormalizeShopUiName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            string trimmed = name.Trim();
            System.Text.StringBuilder builder = new System.Text.StringBuilder(trimmed.Length);
            bool pendingSeparator = false;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char character = trimmed[i];
                if (char.IsWhiteSpace(character) || character == '_' || character == '-')
                {
                    pendingSeparator = builder.Length > 0;
                    continue;
                }

                if (pendingSeparator)
                {
                    builder.Append('_');
                    pendingSeparator = false;
                }

                builder.Append(char.ToLowerInvariant(character));
            }

            while (builder.Length > 0 && builder[builder.Length - 1] == '_')
            {
                builder.Length--;
            }

            return builder.ToString();
        }

        private static bool IsShopPreviewSprite(SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer == null)
            {
                return false;
            }

            Transform walk = spriteRenderer.transform;
            while (walk != null)
            {
                if (IsShopPreviewObjectName(walk.name))
                {
                    return true;
                }

                walk = walk.parent;
            }

            return false;
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
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null || IsShopTextButtonSprite(spriteRenderer))
                {
                    continue;
                }

                if (IsShopPreviewSprite(spriteRenderer))
                {
                    if (visible)
                    {
                        spriteRenderer.gameObject.SetActive(true);
                    }

                    spriteRenderer.enabled = visible;
                    if (!visible)
                    {
                        spriteRenderer.gameObject.SetActive(false);
                    }

                    continue;
                }

                spriteRenderer.enabled = visible;
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
                "txt_shop_startGame",
                "txt_shop_previous",
                "txt_shop_prev",
                "txt_shop_next",
                "txt_shop_item",
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
                    if (canvas != null && !IsLifeOverShopCanvas(canvas))
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

        private Transform FindLifeOverCanvasTransform()
        {
            const string canvasName = "LifeOver-canvas";
            Transform[] underShop = transform.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < underShop.Length; i++)
            {
                Transform candidate = underShop[i];
                if (candidate != null &&
                    candidate.gameObject.name.Equals(canvasName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] inRoot = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < inRoot.Length; i++)
                {
                    Transform candidate = inRoot[i];
                    if (candidate != null &&
                        candidate.gameObject.name.Equals(canvasName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static bool IsLifeOverShopCanvas(Canvas canvas)
        {
            if (canvas == null)
            {
                return false;
            }

            string name = canvas.gameObject.name;
            return name.Equals("LifeOver-canvas", System.StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("LifeOver", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void SetShopCanvasHierarchyVisible(Canvas canvas, bool visible)
        {
            if (canvas == null || IsLifeOverShopCanvas(canvas))
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
