using System.Collections.Generic;
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
        private readonly Dictionary<int, GameObject> _resolvedPreviewByOfferIndex = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _runtimeInstantiatedPreviewsBySourceId = new Dictionary<int, GameObject>();
        private bool _didEnsureShopNavButtons;
        private bool _didBuildPreviewCatalog;
        private bool _shopIsVisible;

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
            _resolvedCarouselPreviewRoot = null;
            _didBuildPreviewCatalog = false;
            EnsureLooseShopPreviewReparentedOnce();
            EnsureShopNavButtonsReady();
            BindUiCarouselNavigationButtons();
            BindShopActionTextButtons();
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
            _shopIsVisible = true;
            gameObject.SetActive(true);
            _offerIndex = 0;
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
                _shopOffers != null && _shopOffers.Count > 0 ? _shopOffers[_offerIndex] : null);
        }

        public void Hide()
        {
            _shopIsVisible = false;
            SetShopTextButtonsVisible(false);
            ResetShopNavPressedVisuals();
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
            return nav;
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
            ResolveSelectedOfferItemTextIfNeeded();

            if (_waveManager == null || _shopOffers == null || _shopOffers.Count == 0)
            {
                SetText(_selectedOfferItemText, string.Empty);
                return;
            }

            _offerIndex = Mathf.Clamp(_offerIndex, 0, _shopOffers.Count - 1);
            ShopOfferConfig_V2 offer = _shopOffers[_offerIndex];

            SetText(_offerTitleText, offer.DisplayName);
            SetText(_offerSubtitleText, BuildOfferSubtitle(offer));
            SetText(_selectedOfferItemText, offer.DisplayName);

            Transform carouselRoot = ResolveCarouselPreviewObjectsRoot();
            if (!_didBuildPreviewCatalog)
            {
                EnsureCarouselPreviewCatalogReady(carouselRoot);
                _didBuildPreviewCatalog = true;
            }

            ApplySelectedOfferPreviewVisibility(carouselRoot, offer);

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

        private void EnsureCarouselPreviewCatalogReady(Transform carouselRoot)
        {
            if (carouselRoot == null || _shopOffers == null)
            {
                return;
            }

            ReparentLooseShopPreviewsUnderCarousel(carouselRoot);
            _resolvedPreviewByOfferIndex.Clear();

            for (int i = 0; i < _shopOffers.Count; i++)
            {
                ShopOfferConfig_V2 offer = _shopOffers[i];
                GameObject resolved = ResolveOrCreatePreviewInstance(offer.PreviewObject, carouselRoot);
                if (resolved == null && offer.PreviewObject != null)
                {
                    resolved = FindPreviewUnderCarousel(carouselRoot, ResolvePreviewObjectName(offer.PreviewObject));
                }

                if (resolved != null)
                {
                    _resolvedPreviewByOfferIndex[i] = resolved;
                }
                else if (_debugShopPanelLogs)
                {
                    Debug.LogWarning(
                        $"[ShopPanel_V2] No preview resolved for offer[{i}] '{offer.DisplayName}' " +
                        $"(previewRef='{DescribeGameObject(offer.PreviewObject)}').");
                }
            }
        }

        private void ApplySelectedOfferPreviewVisibility(Transform carouselRoot, ShopOfferConfig_V2 selectedOffer)
        {
            if (carouselRoot == null || selectedOffer == null)
            {
                return;
            }

            EnsureActiveHierarchy(carouselRoot.gameObject);

            string selectedPreviewName = ResolvePreviewObjectName(selectedOffer.PreviewObject);
            GameObject selectedPreview = FindPreviewUnderCarousel(carouselRoot, selectedPreviewName);
            if (selectedPreview == null && selectedOffer.PreviewObject != null)
            {
                selectedPreview = ResolveOrCreatePreviewInstance(selectedOffer.PreviewObject, carouselRoot);
            }

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
            return !string.IsNullOrWhiteSpace(objectName) &&
                   objectName.StartsWith("shop_", System.StringComparison.OrdinalIgnoreCase);
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

        private TMP_Text FindShopTmpByObjectName(string objectName)
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

                TMP_Text match = FindTmpUnderRoot(searchRoot, objectName);
                if (match != null)
                {
                    return match;
                }
            }

            TMP_Text[] allTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < allTexts.Length; i++)
            {
                TMP_Text text = allTexts[i];
                if (text != null &&
                    text.gameObject.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return null;
        }

        private static TMP_Text FindTmpUnderRoot(Transform searchRoot, string objectName)
        {
            TMP_Text[] texts = searchRoot.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null &&
                    text.gameObject.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return null;
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
                if (spriteRenderer == null ||
                    IsShopPreviewSprite(spriteRenderer) ||
                    IsShopTextButtonSprite(spriteRenderer))
                {
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
