using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace iStick2War_V2
{
    public sealed class ShopPanel_V2 : MonoBehaviour
    {
        [Header("UI (optional)")]
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _currencyText;
        [SerializeField] private TMP_Text _bunkerText;
        [SerializeField] private TMP_Text _healthCostText;
        [SerializeField] private TMP_Text _bazookaCostText;
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
            SetText(_bunkerText, $"Bunker HP: {_waveManager.BunkerHealth}");
            SetText(_buyButtonText, _buyButtonDefaultLabel);
            SetText(_healthCostText, $"Heal cost: {_waveManager.GetHealthPurchaseCost()}");
            SetText(
                _bazookaCostText,
                _waveManager.IsBazookaUnlocked()
                    ? "Bazooka: Unlocked"
                    : $"Bazooka cost: {_waveManager.GetBazookaUnlockCost()}");
            SetText(_bunkerCostText, $"Repair cost: {_waveManager.GetBunkerRepairCost()}");
            RefreshOfferSelection();
        }

        /// <summary>
        /// Wire left arrow (e.g. btn_shop_arrow_left OnClick).
        /// </summary>
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

        /// <summary>
        /// Wire right arrow (e.g. btn_shop_arrow_right OnClick).
        /// </summary>
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

        /// <summary>
        /// Wire main BUY button to purchase the currently selected carousel offer.
        /// </summary>
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

            bool ok = _waveManager.TryPurchaseOffer(offer);
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

        public void OnBuyBazookaClicked()
        {
            _waveManager?.PurchaseBazookaUnlock();
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
                        : $"Cost: {offer.Cost}";

                case ShopOfferKind_V2.BunkerRepair:
                    return _waveManager.IsBunkerFullHealth()
                        ? "Bunker full"
                        : $"Cost: {offer.Cost}";

                case ShopOfferKind_V2.WeaponUnlock:
                    return _waveManager.IsWeaponOwned(offer.Weapon)
                        ? "Owned"
                        : $"Cost: {offer.Cost}";

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

            bool canAfford = _waveManager.CanAfford(offer.Cost);
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
                        return "LOCKED";
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
            if (!_toggleVisualComponentsOnShowHide)
            {
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

            if (_debugShopPanelLogs)
            {
                Debug.Log($"[ShopPanel_V2] SetVisualComponentsVisible={visible}");
            }
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
