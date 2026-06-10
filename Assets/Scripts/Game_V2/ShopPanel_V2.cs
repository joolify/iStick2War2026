using System.Collections.Generic;
using iStick2War;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace iStick2War_V2
{
    /*
     * ShopPanel_V2 (Shop V2 — canvas UI + purchases)
     *
     * PURPOSE:
     * Drives the resolution-adapted Shop V2 chrome (Shop-canvas): offer carousel, stat grid TMP rows,
     * ShopItemImage preview, and Shop_Btn_* navigation. Economy stays in WaveManager_V2.
     *
     * ---------------------------------------------------------
     * NAVIGATION (Game_V2)
     *
     * Purchases → WaveManager_V2.cs
     * Offer rows → ShopOfferConfig_V2.cs
     * Stat grid → ShopOfferStatsPresenter_V2.cs
     * Button hits → ShopNavArrowUiButton_V2.cs on Shop_Btn_Previous / Buy / StartGame / Next
     * Legacy world shop → ShopPanelLegacy_V2.cs
     */
    public sealed class ShopPanel_V2 : MonoBehaviour
    {
        private const string ShopChromeRootName = "Shop V2";
        private const string ShopCanvasName = "Shop-canvas";

        [Header("Shop carousel")]
        [Tooltip("Ordered carousel rows. Wire BUY to purchase the selected row.")]
        [SerializeField] private List<ShopOfferConfig_V2> _shopOffers = new List<ShopOfferConfig_V2>();

        [Header("Shop V2 UI (optional — auto-bound under chrome root)")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _currencyText;
        [SerializeField] private TMP_Text _selectedOfferItemText;
        [SerializeField] private Image _shopItemImage;
        [SerializeField] private TMP_Text _buyButtonLabelText;
        [SerializeField] private string _selectedOfferItemTextObjectName = "txt_shop_item";
        [SerializeField] private string _buyButtonDefaultLabel = "BUY";
        [Tooltip("ShopItemImage size multiplier for wide weapon previews that read too small at base size.")]
        [SerializeField] private float _largeWeaponPreviewImageScale = 2f;

        [Header("Visibility")]
        [Tooltip("When empty, uses this GameObject or the scene root named Shop V2.")]
        [SerializeField] private GameObject _shopChromeRoot;

        [Header("Shop stat warnings")]
        [SerializeField] private Color _shopWarningTextColor = new Color(0.9f, 0.2f, 0.2f, 1f);

        [Header("Shop grid typography")]
        [Tooltip("Extra line gap inside multi-line ShopGrid TMP (maps to TMP <line-height>). Also copied to TMP lineSpacing.")]
        [SerializeField] private float _shopGridLineSpacing = 14f;

        [SerializeField] private bool _debugShopNavigationLogs;

        private WaveManager_V2 _waveManager;
        private Transform _uiScope;
        private Transform _shopGridScope;
        private int _offerIndex;
        private bool _shopIsVisible;
        private bool _didResolveUi;
        private bool _didResolveOfferStats;
        private bool _didRebuildOfferStatBaselines;
        private bool _didCacheShopItemImageBaseSize;
        private Vector2 _shopItemImageBaseSize = new Vector2(200f, 200f);

        private readonly List<ShopOfferConfig_V2> _visibleShopOffers = new List<ShopOfferConfig_V2>();
        private readonly ShopOfferStatsPresenter_V2 _offerStatsPresenter = new ShopOfferStatsPresenter_V2();
        private readonly HashSet<TMP_Text> _statTmpBindingUsed = new HashSet<TMP_Text>();

        public bool IsShopVisible => _shopIsVisible;
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

        public int GetCarouselOfferIndex() => _offerIndex;

        public void SetCarouselOfferIndex(int offerIndex)
        {
            _offerIndex = Mathf.Max(0, offerIndex);
        }

        public HeroWeaponDefinition_V2 TryResolveWeaponDefinitionByType(WeaponType weaponType)
        {
            if (_shopOffers == null)
            {
                return null;
            }

            for (int i = 0; i < _shopOffers.Count; i++)
            {
                ShopOfferConfig_V2 row = _shopOffers[i];
                if (row == null)
                {
                    continue;
                }

                HeroWeaponDefinition_V2 weapon = row.Weapon;
                if (weapon != null && weapon.WeaponType == weaponType)
                {
                    return weapon;
                }
            }

            return null;
        }

        public void Initialize(WaveManager_V2 waveManager)
        {
            _waveManager = waveManager;
            if (_waveManager != null)
            {
                _waveManager.OnMetaChanged -= HandleMetaChanged;
                _waveManager.OnMetaChanged += HandleMetaChanged;
            }

            ResolveUiReferencesIfNeeded();
            RebuildVisibleShopOffersIfNeeded();
            BindCanvasShopButtons();
            ResolveOfferStatsIfNeeded();
            RebuildOfferStatBaselinesIfNeeded();
            Refresh();
            Hide();
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
            _offerIndex = 0;
            ResolveUiReferencesIfNeeded();
            RebuildVisibleShopOffersIfNeeded();
            SetChromeVisible(true);
            // Re-bind stat TMPs after chrome is active so ShopGrid rows resolve reliably.
            _statTmpBindingUsed.Clear();
            _didResolveOfferStats = false;
            BindCanvasShopButtons();
            RefitShopNavButtons();
            Refresh();
            _waveManager?.ApplyGameplayHudVisibility();
        }

        public void Hide()
        {
            _shopIsVisible = false;
            _offerStatsPresenter.HideAll();
            ResetShopNavPressedVisuals();
            SetChromeVisible(false);
            _waveManager?.ApplyGameplayHudVisibility();
        }

        public void SuppressLifeOverUiElements()
        {
            // LifeOver V2 owns its chrome; nothing under Shop V2 should mirror life-over labels.
        }

        public GameObject PrepareLifeOverCanvasForDisplay()
        {
            return null;
        }

        public GameObject DetachLifeOverCanvasForDisplay() => PrepareLifeOverCanvasForDisplay();

        public void RestoreLifeOverCanvasAfterDisplay()
        {
        }

        public void Refresh()
        {
            if (_waveManager == null)
            {
                return;
            }

            ResolveUiReferencesIfNeeded();
            SetText(_titleText, "Shop");
            SetText(_currencyText, $"Balance: {ShopMoneyFormat_V2.Format(_waveManager.Currency)}");
            SetText(_buyButtonLabelText, _buyButtonDefaultLabel);
            RefreshOfferSelection();
        }

        public void OnShopArrowPreviousClicked()
        {
            RebuildVisibleShopOffersIfNeeded();
            if (_visibleShopOffers.Count == 0)
            {
                return;
            }

            int before = _offerIndex;
            _offerIndex = (_offerIndex - 1 + _visibleShopOffers.Count) % _visibleShopOffers.Count;
            if (_debugShopNavigationLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] PREVIOUS: {before} -> {_offerIndex} / {_visibleShopOffers.Count}");
            }

            RefreshOfferSelection();
        }

        public void OnShopArrowNextClicked()
        {
            RebuildVisibleShopOffersIfNeeded();
            if (_visibleShopOffers.Count == 0)
            {
                return;
            }

            int before = _offerIndex;
            _offerIndex = (_offerIndex + 1) % _visibleShopOffers.Count;
            if (_debugShopNavigationLogs)
            {
                Debug.Log(
                    $"[ShopPanel_V2] NEXT: {before} -> {_offerIndex} / {_visibleShopOffers.Count}");
            }

            RefreshOfferSelection();
        }

        public void OnPurchaseSelectedOfferClicked()
        {
            RebuildVisibleShopOffersIfNeeded();
            if (_waveManager == null || _visibleShopOffers.Count == 0)
            {
                return;
            }

            ShopOfferConfig_V2 offer = GetSelectedOffer();
            bool ok = _waveManager.TryPurchaseOffer(offer);
            if (ok)
            {
                AudioManager_V2.PlayPurchaseSuccess();
            }
            else
            {
                AudioManager_V2.PlayFailure();
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
            SetText(_buyButtonLabelText, nextLabel);
        }

        private void HandleMetaChanged(int wave, int currency, int bunkerHp)
        {
            Refresh();
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

        private void RefreshOfferSelection()
        {
            RebuildVisibleShopOffersIfNeeded();
            if (_waveManager == null || _visibleShopOffers.Count == 0)
            {
                SetText(_selectedOfferItemText, string.Empty);
                ApplyOfferPreviewImage(null);
                _offerStatsPresenter.HideAll();
                return;
            }

            ShopOfferConfig_V2 offer = GetSelectedOffer();
            SetText(_selectedOfferItemText, offer.DisplayName);
            ApplyOfferPreviewImage(offer);
            SetBuyButtonLabel(ResolveBuyButtonLabel(offer));
            RefreshOfferStats(offer);
        }

        private void ApplyOfferPreviewImage(ShopOfferConfig_V2 offer)
        {
            if (_shopItemImage == null)
            {
                return;
            }

            CacheShopItemImageBaseSizeIfNeeded();
            float previewScale = ResolveShopPreviewImageScale(offer != null ? offer.PreviewObject : null);
            _shopItemImage.rectTransform.sizeDelta = _shopItemImageBaseSize * previewScale;

            if (offer == null || offer.PreviewObject == null)
            {
                _shopItemImage.enabled = false;
                return;
            }

            Sprite sprite = ResolvePreviewSprite(offer.PreviewObject);
            if (sprite == null)
            {
                _shopItemImage.enabled = false;
                return;
            }

            _shopItemImage.enabled = true;
            _shopItemImage.sprite = sprite;
            _shopItemImage.preserveAspect = true;
        }

        private void CacheShopItemImageBaseSizeIfNeeded()
        {
            if (_didCacheShopItemImageBaseSize || _shopItemImage == null)
            {
                return;
            }

            Vector2 size = _shopItemImage.rectTransform.sizeDelta;
            if (size.x > 0f && size.y > 0f)
            {
                _shopItemImageBaseSize = size;
            }

            _didCacheShopItemImageBaseSize = true;
        }

        private float ResolveShopPreviewImageScale(GameObject previewRoot)
        {
            if (previewRoot == null || _largeWeaponPreviewImageScale <= 1f)
            {
                return 1f;
            }

            string normalizedName = NormalizeShopUiName(previewRoot.name);
            if (normalizedName == "shop_bazooka" ||
                normalizedName == "shop_flamethrower" ||
                normalizedName == "shop_shotgun" ||
                normalizedName == "shop_teslagun" ||
                normalizedName == "shop_thompson")
            {
                return _largeWeaponPreviewImageScale;
            }

            return 1f;
        }

        private static Sprite ResolvePreviewSprite(GameObject previewRoot)
        {
            if (previewRoot == null)
            {
                return null;
            }

            SpriteRenderer spriteRenderer = previewRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                return spriteRenderer.sprite;
            }

            Image image = previewRoot.GetComponentInChildren<Image>(true);
            if (image != null && image.sprite != null)
            {
                return image.sprite;
            }

            return null;
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
            _offerStatsPresenter.ConfigureGridTypography(_shopGridLineSpacing);
            _offerStatsPresenter.Refresh(offer, _waveManager);
            ApplyShopGridTmpLineSpacing();
        }

        private void ResolveOfferStatsIfNeeded()
        {
            if (_didResolveOfferStats)
            {
                return;
            }

            _statTmpBindingUsed.Clear();
            _offerStatsPresenter.ConfigureGridTypography(_shopGridLineSpacing);
            _offerStatsPresenter.ResolveBindings(
                FindShopStatLabelTmpForBinding,
                FindShopStatValueTmpForBinding,
                FindShopObjectByName,
                FindShopStatLabelNearValue);
            TMP_Text warningText = FindShopTmpByObjectName("txt_shop_stat_warningText_label");
            if (warningText == null)
            {
                warningText = FindShopTmpByObjectName("txt_shop_stat_warning_text");
            }

            if (warningText != null)
            {
                _statTmpBindingUsed.Add(warningText);
                _offerStatsPresenter.SetWarningText(warningText, _shopWarningTextColor);
            }

            SuppressUnboundDuplicateShopStatTmps();
            _didResolveOfferStats = true;
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

        private string ResolveBuyButtonLabel(ShopOfferConfig_V2 offer)
        {
            if (_waveManager == null || offer == null)
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

        private void BindCanvasShopButtons()
        {
            EnsureShopNavUiButton("Shop_Btn_Previous", ShopTextButtonBehavior.CarouselPrevious);
            EnsureShopNavUiButton("Shop_Btn_Next", ShopTextButtonBehavior.CarouselNext);
            EnsureShopNavUiButton("Shop_Btn_Buy", ShopTextButtonBehavior.Buy);
            EnsureShopNavUiButton("Shop_Btn_StartGame", ShopTextButtonBehavior.StartNextWave);
        }

        private void RefitShopNavButtons()
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

        private void EnsureShopNavUiButton(string exactName, ShopTextButtonBehavior behavior)
        {
            GameObject target = FindShopObjectByName(exactName);
            if (target == null)
            {
                return;
            }

            ShopNavArrowUiButton_V2 nav = target.GetComponent<ShopNavArrowUiButton_V2>();
            if (nav == null)
            {
                nav = target.AddComponent<ShopNavArrowUiButton_V2>();
            }

            if (behavior == ShopTextButtonBehavior.CarouselPrevious)
            {
                nav.Configure(this, ShopNavArrow_V2.ArrowDirection.Previous);
            }
            else if (behavior == ShopTextButtonBehavior.CarouselNext)
            {
                nav.Configure(this, ShopNavArrow_V2.ArrowDirection.Next);
            }
            else
            {
                nav.Configure(this, behavior, _waveManager);
            }
        }

        private void ResetShopNavPressedVisuals()
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

        private void ResolveUiReferencesIfNeeded()
        {
            if (_didResolveUi)
            {
                return;
            }

            if (_shopChromeRoot == null)
            {
                if (gameObject.name.Equals(ShopChromeRootName, System.StringComparison.OrdinalIgnoreCase))
                {
                    _shopChromeRoot = gameObject;
                }
                else
                {
                    _shopChromeRoot = FindSceneObjectByName(ShopChromeRootName);
                }
            }

            Transform scopeRoot = _shopChromeRoot != null ? _shopChromeRoot.transform : transform;
            _uiScope = FindNamedChildRecursive(scopeRoot, ShopCanvasName);
            if (_uiScope == null)
            {
                _uiScope = scopeRoot;
            }

            _shopGridScope = FindNamedChildRecursive(_uiScope, "ShopGrid");
            ApplyShopGridTmpLineSpacing();

            if (_titleText == null)
            {
                _titleText = FindShopTmpByObjectName("txt_shop_title");
            }

            if (_currencyText == null)
            {
                _currencyText = FindShopTmpByObjectName("txt_shop_money");
            }

            if (_selectedOfferItemText == null && !string.IsNullOrEmpty(_selectedOfferItemTextObjectName))
            {
                _selectedOfferItemText = FindShopTmpByObjectName(_selectedOfferItemTextObjectName);
            }

            if (_shopItemImage == null)
            {
                GameObject imageGo = FindShopObjectByName("ShopItemImage");
                if (imageGo != null)
                {
                    _shopItemImage = imageGo.GetComponent<Image>();
                }
            }

            CacheShopItemImageBaseSizeIfNeeded();

            if (_buyButtonLabelText == null)
            {
                GameObject buyButton = FindShopObjectByName("Shop_Btn_Buy");
                if (buyButton != null)
                {
                    _buyButtonLabelText = buyButton.GetComponentInChildren<TMP_Text>(true);
                }
            }

            _didResolveUi = true;
        }

        private void ApplyShopGridTmpLineSpacing()
        {
            if (_shopGridScope == null)
            {
                return;
            }

            TMP_Text[] texts = _shopGridScope.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null)
                {
                    text.lineSpacing = _shopGridLineSpacing;
                }
            }
        }

        private void SetChromeVisible(bool visible)
        {
            GameObject chrome = _shopChromeRoot != null ? _shopChromeRoot : gameObject;
            chrome.SetActive(visible);
            if (!visible)
            {
                return;
            }

            Canvas[] canvases = chrome.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                GameplayHudLayoutUtility_V2.EnsureCanvasReceivesInput(canvases[i]);
            }
        }

        private TMP_Text FindShopTmpByObjectName(string exactName)
        {
            if (string.IsNullOrEmpty(exactName) || _uiScope == null)
            {
                return null;
            }

            TMP_Text[] texts = _uiScope.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && text.gameObject.name.Equals(exactName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return null;
        }

        private enum ShopStatTmpBindingSlot
        {
            Label,
            Value,
        }

        private TMP_Text FindShopStatLabelTmpForBinding(string exactName)
        {
            return RegisterShopStatTmpBinding(FindShopStatTmpForBinding(exactName, ShopStatTmpBindingSlot.Label));
        }

        private TMP_Text FindShopStatValueTmpForBinding(string exactName)
        {
            return RegisterShopStatTmpBinding(FindShopStatTmpForBinding(exactName, ShopStatTmpBindingSlot.Value));
        }

        private TMP_Text RegisterShopStatTmpBinding(TMP_Text match)
        {
            if (match != null)
            {
                _statTmpBindingUsed.Add(match);
            }

            return match;
        }

        private TMP_Text FindShopStatTmpForBinding(string exactName, ShopStatTmpBindingSlot slot)
        {
            if (string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            Transform scope = _shopGridScope != null ? _shopGridScope : _uiScope;
            if (scope == null)
            {
                return null;
            }

            string normalizedTarget = NormalizeShopUiName(exactName);
            TMP_Text[] texts = scope.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null ||
                    _statTmpBindingUsed.Contains(text) ||
                    !text.gameObject.name.Equals(exactName, System.StringComparison.OrdinalIgnoreCase) ||
                    !MatchesBindingSlot(text, slot))
                {
                    continue;
                }

                int score = ScoreShopStatTmpCandidate(text, normalizedTarget);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = text;
                }
            }

            return best;
        }

        private static int ScoreShopStatTmpCandidate(TMP_Text candidate, string normalizedTarget)
        {
            int score = 0;
            if (candidate.gameObject.activeInHierarchy)
            {
                score += 8;
            }

            string normalizedObjectName = NormalizeShopUiName(candidate.gameObject.name);
            if (normalizedObjectName.Equals(normalizedTarget, System.StringComparison.Ordinal))
            {
                score += 200;
            }

            string normalizedText = NormalizeShopUiName(candidate.text);
            if (normalizedText.Equals(normalizedTarget, System.StringComparison.Ordinal))
            {
                // Scene placeholders often copy the object name into m_text — prefer another match.
                score -= 80;
            }

            return score;
        }

        private static bool MatchesBindingSlot(TMP_Text text, ShopStatTmpBindingSlot slot)
        {
            if (text == null)
            {
                return false;
            }

            string objectName = NormalizeShopUiName(text.gameObject.name);
            bool looksLikeValue = objectName.Contains("value", System.StringComparison.Ordinal);
            bool looksLikeLabel =
                objectName.Contains("label", System.StringComparison.Ordinal) ||
                objectName.Contains("labael", System.StringComparison.Ordinal) ||
                objectName.Contains("labeal", System.StringComparison.Ordinal);

            if (slot == ShopStatTmpBindingSlot.Label)
            {
                return !looksLikeValue || looksLikeLabel;
            }

            return !looksLikeLabel || looksLikeValue;
        }

        private void SuppressUnboundDuplicateShopStatTmps()
        {
            Transform statsRoot = _shopGridScope != null ? _shopGridScope : _uiScope;
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

            return builder.ToString();
        }

        private TMP_Text FindShopStatLabelNearValue(TMP_Text valueText, string primaryLabelName, string[] alternateNames)
        {
            TMP_Text match = FindShopStatLabelTmpForBinding(primaryLabelName);
            if (match != null)
            {
                return match;
            }

            if (alternateNames == null)
            {
                return null;
            }

            for (int i = 0; i < alternateNames.Length; i++)
            {
                match = FindShopStatLabelTmpForBinding(alternateNames[i]);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private GameObject FindShopObjectByName(string exactName)
        {
            if (string.IsNullOrEmpty(exactName) || _uiScope == null)
            {
                return null;
            }

            Transform[] transforms = _uiScope.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    candidate.gameObject.name.Equals(exactName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate.gameObject;
                }
            }

            return null;
        }

        private static GameObject FindSceneObjectByName(string exactName)
        {
            GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate != null &&
                    candidate.name.Equals(exactName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform FindNamedChildRecursive(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            if (root.name.Equals(exactName, System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform found = FindNamedChildRecursive(child, exactName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void SetText(TMP_Text textField, string value)
        {
            if (textField != null)
            {
                textField.text = value;
            }
        }
    }
}
