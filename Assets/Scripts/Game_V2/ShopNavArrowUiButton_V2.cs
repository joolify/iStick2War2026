using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * TextBTN_Medium* shop controls: carousel prev/next, BUY, and start-game.
     * Supports canvas UI (Button + raycast Graphic) and world-space sprites (Collider2D + mouse overlap).
     * Sibling *_Pressed visuals show while the button is held (same as TextBTN_MediumNext).
     */
    public enum ShopTextButtonBehavior
    {
        CarouselPrevious = 0,
        CarouselNext = 1,
        Buy = 2,
        StartNextWave = 3,
    }

    [AddComponentMenu("iStick2War/Shop Nav Arrow UI Button V2")]
    public sealed class ShopNavArrowUiButton_V2 :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        [SerializeField] private ShopPanel_V2 _shopPanel;
        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private ShopTextButtonBehavior _behavior = ShopTextButtonBehavior.CarouselPrevious;
        [SerializeField] private ShopNavArrow_V2.ArrowDirection _direction = ShopNavArrow_V2.ArrowDirection.Previous;

        [Header("Pressed visual (TextBTN siblings)")]
        [Tooltip("Normal-state root (e.g. TextBTN_MediumPrev). Auto-resolved from this GameObject when empty.")]
        [SerializeField] private GameObject _normalVisual;
        [Tooltip("Pressed-state root (e.g. TextBTN_MediumPrev_Pressed). Auto-resolved as '<normal name>_Pressed' when empty.")]
        [SerializeField] private GameObject _pressedVisual;

        [Header("Debug")]
        [SerializeField] private bool _debugLogs;

        private Button _button;
        private bool _listenerRegistered;
        private bool _visualPairResolved;
        private bool _usesUiClickPath;
        private bool _isWorldPointerDown;

        private void Awake()
        {
            _button = GetComponent<Button>();
            ResolveVisualPairIfNeeded();
            ConfigureDecorativeLabelPassthrough();
            _usesUiClickPath = HasRaycastableGraphic();
            EnsureWorldSpaceHitTargetIfNeeded();
            ShowNormalVisual();
        }

        private void OnEnable()
        {
            ConfigureDecorativeLabelPassthrough();
            RegisterListenerIfNeeded();
            ResolveVisualPairIfNeeded();
            ShowNormalVisual();
        }

        private void OnDisable()
        {
            UnregisterListener();
            _isWorldPointerDown = false;
            ShowNormalVisual();
        }

        internal void Configure(ShopPanel_V2 shopPanel, ShopNavArrow_V2.ArrowDirection direction)
        {
            _shopPanel = shopPanel;
            _direction = direction;
            _behavior = direction == ShopNavArrow_V2.ArrowDirection.Previous
                ? ShopTextButtonBehavior.CarouselPrevious
                : ShopTextButtonBehavior.CarouselNext;
            SyncShopInputLayer();
            ResolveVisualPairIfNeeded();
            ConfigureDecorativeLabelPassthrough();
            _usesUiClickPath = HasRaycastableGraphic();
            EnsureWorldSpaceHitTargetIfNeeded();
            UnregisterListener();
            RegisterListenerIfNeeded();
            ShowNormalVisual();
        }

        internal void Configure(ShopPanel_V2 shopPanel, ShopTextButtonBehavior behavior, WaveManager_V2 waveManager = null)
        {
            _shopPanel = shopPanel;
            _waveManager = waveManager;
            _behavior = behavior;
            if (behavior == ShopTextButtonBehavior.CarouselPrevious)
            {
                _direction = ShopNavArrow_V2.ArrowDirection.Previous;
            }
            else if (behavior == ShopTextButtonBehavior.CarouselNext)
            {
                _direction = ShopNavArrow_V2.ArrowDirection.Next;
            }

            SyncShopInputLayer();
            ResolveVisualPairIfNeeded();
            ConfigureDecorativeLabelPassthrough();
            _usesUiClickPath = HasRaycastableGraphic();
            EnsureWorldSpaceHitTargetIfNeeded();
            UnregisterListener();
            RegisterListenerIfNeeded();
            ShowNormalVisual();
        }

        // Re-fit collider after ShopPanel reparents to the camera (Show()).
        internal void RefitHitTarget()
        {
            _usesUiClickPath = HasRaycastableGraphic();
            EnsureWorldSpaceHitTargetIfNeeded();
        }

        internal void ResetToNormalVisual()
        {
            _isWorldPointerDown = false;
            ShowNormalVisual();
        }

        internal void ForwardLabelPointerDown()
        {
            ShowPressedVisual();
            HandleClick();
            ShowPressedVisual();
        }

        internal void ForwardLabelPointerUp()
        {
            ShowNormalVisual();
        }

        internal void ForwardLabelPointerExit()
        {
            ShowNormalVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_usesUiClickPath)
            {
                return;
            }

            ShowPressedVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_usesUiClickPath)
            {
                return;
            }

            ShowNormalVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_usesUiClickPath)
            {
                return;
            }

            ShowNormalVisual();
        }

        private void Update()
        {
            if (_usesUiClickPath || !isActiveAndEnabled)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0) && TryHandleWorldPointerDown())
            {
                _isWorldPointerDown = true;
                ShowPressedVisual();
                HandleClick();
                if (_isWorldPointerDown)
                {
                    ShowPressedVisual();
                }

                return;
            }

            if (_isWorldPointerDown && !Input.GetMouseButton(0))
            {
                _isWorldPointerDown = false;
                ShowNormalVisual();
            }
        }

        private void RegisterListenerIfNeeded()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (!_usesUiClickPath || _button == null || _listenerRegistered)
            {
                return;
            }

            _button.onClick.AddListener(HandleClick);
            _listenerRegistered = true;
        }

        private void UnregisterListener()
        {
            if (_button == null || !_listenerRegistered)
            {
                return;
            }

            _button.onClick.RemoveListener(HandleClick);
            _listenerRegistered = false;
        }

        private void HandleClick()
        {
            if (_shopPanel == null)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning($"[ShopNavArrowUiButton_V2] '{name}': assign ShopPanel_V2.");
                }

                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[ShopNavArrowUiButton_V2] '{name}' click -> {_behavior}");
            }

            switch (_behavior)
            {
                case ShopTextButtonBehavior.CarouselPrevious:
                    _shopPanel.OnShopArrowPreviousClicked();
                    break;
                case ShopTextButtonBehavior.CarouselNext:
                    _shopPanel.OnShopArrowNextClicked();
                    break;
                case ShopTextButtonBehavior.Buy:
                    _shopPanel.OnPurchaseSelectedOfferClicked();
                    break;
                case ShopTextButtonBehavior.StartNextWave:
                    if (_waveManager == null)
                    {
                        _waveManager = FindAnyObjectByType<WaveManager_V2>();
                    }

                    if (_waveManager != null)
                    {
                        _waveManager.StartNextWaveFromShop();
                    }
                    else
                    {
                        _shopPanel.OnStartNextWaveClicked();
                    }

                    break;
            }
        }

        private void EnsureWorldSpaceHitTargetIfNeeded()
        {
            if (_usesUiClickPath)
            {
                return;
            }

            BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider == null)
            {
                SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
                }

                if (spriteRenderer == null)
                {
                    if (_debugLogs)
                    {
                        Debug.LogWarning(
                            $"[ShopNavArrowUiButton_V2] '{name}' has no UI Graphic raycast target and no Collider2D. " +
                            "Add a Collider2D (world sprite) or an Image with Raycast Target enabled (canvas UI).");
                    }

                    return;
                }

                boxCollider = gameObject.AddComponent<BoxCollider2D>();
                if (_debugLogs)
                {
                    Debug.Log($"[ShopNavArrowUiButton_V2] '{name}' added BoxCollider2D for world-space clicks.");
                }
            }

            FitBoxColliderToVisuals(boxCollider);
        }

        private void SyncShopInputLayer()
        {
            if (_shopPanel == null)
            {
                return;
            }

            int shopLayer = _shopPanel.gameObject.layer;
            SetLayerRecursively(gameObject, shopLayer);
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

        private bool TryHandleWorldPointerDown()
        {
            if (_shopPanel == null || !_shopPanel.isActiveAndEnabled)
            {
                return false;
            }

            Collider2D hitCollider = GetComponent<Collider2D>();
            if (hitCollider == null || !hitCollider.enabled)
            {
                return false;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            Vector3 screenPoint = Input.mousePosition;
            screenPoint.z = 0f;
            Vector2 worldPoint = camera.ScreenToWorldPoint(screenPoint);
            return hitCollider.OverlapPoint(worldPoint);
        }

        private void ConfigureDecorativeLabelPassthrough()
        {
            TMP_Text label = FindAssociatedShopLabel();
            if (label == null)
            {
                return;
            }

            // Label must receive UI raycasts so clicks on text forward to the nav button.
            label.raycastTarget = true;

            Graphic[] graphics = label.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic != null)
                {
                    graphic.raycastTarget = true;
                }
            }

            Collider2D[] colliders = label.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }

            ShopNavArrowLabelForwarder_V2 forwarder = label.GetComponent<ShopNavArrowLabelForwarder_V2>();
            if (forwarder == null)
            {
                forwarder = label.gameObject.AddComponent<ShopNavArrowLabelForwarder_V2>();
            }

            forwarder.Configure(this);

            if (_debugLogs)
            {
                Debug.Log(
                    $"[ShopNavArrowUiButton_V2] '{name}' wired label forwarder on '{label.name}'.");
            }
        }

        private TMP_Text FindAssociatedShopLabel()
        {
            string labelName = ResolveAssociatedShopLabelName();
            if (string.IsNullOrEmpty(labelName))
            {
                return null;
            }

            Transform anchor = _normalVisual != null ? _normalVisual.transform : transform;
            Transform parent = anchor != null ? anchor.parent : null;
            if (parent != null)
            {
                TMP_Text[] localTexts = parent.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < localTexts.Length; i++)
                {
                    TMP_Text localText = localTexts[i];
                    if (localText != null &&
                        localText.gameObject.name.Equals(labelName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return localText;
                    }
                }
            }

            TMP_Text[] allTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < allTexts.Length; i++)
            {
                TMP_Text text = allTexts[i];
                if (text != null &&
                    text.gameObject.name.Equals(labelName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return null;
        }

        private string ResolveAssociatedShopLabelName()
        {
            switch (_behavior)
            {
                case ShopTextButtonBehavior.CarouselPrevious:
                    return "txt_shop_previous";
                case ShopTextButtonBehavior.CarouselNext:
                    return "txt_shop_next";
                case ShopTextButtonBehavior.Buy:
                    return "txt_shop_buy";
                case ShopTextButtonBehavior.StartNextWave:
                    return "txt_shop_startGame";
                default:
                    return null;
            }
        }

        private void FitBoxColliderToVisuals(BoxCollider2D boxCollider)
        {
            if (boxCollider == null)
            {
                return;
            }

            bool hasBounds = false;
            Bounds combined = default;

            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (spriteRenderer != null)
            {
                EncapsulateBounds(ref combined, ref hasBounds, spriteRenderer.bounds);
            }

            TMP_Text label = FindAssociatedShopLabel();
            if (label != null)
            {
                EncapsulateRectTransformBounds(label.rectTransform, ref combined, ref hasBounds);
            }

            if (!hasBounds)
            {
                return;
            }

            Vector3 localCenter = transform.InverseTransformPoint(combined.center);
            Vector3 localSize = transform.InverseTransformVector(combined.size);
            boxCollider.offset = localCenter;
            boxCollider.size = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
        }

        private static void EncapsulateBounds(ref Bounds combined, ref bool hasBounds, Bounds next)
        {
            if (!hasBounds)
            {
                combined = next;
                hasBounds = true;
                return;
            }

            combined.Encapsulate(next.min);
            combined.Encapsulate(next.max);
        }

        private static void EncapsulateRectTransformBounds(
            RectTransform rectTransform,
            ref Bounds combined,
            ref bool hasBounds)
        {
            if (rectTransform == null)
            {
                return;
            }

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            for (int i = 0; i < corners.Length; i++)
            {
                if (!hasBounds)
                {
                    combined = new Bounds(corners[i], Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(corners[i]);
                }
            }
        }

        private bool HasRaycastableGraphic()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic != null &&
                    graphic.raycastTarget &&
                    !IsDecorativeShopButtonLabel(graphic.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDecorativeShopButtonLabel(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            string name = target.name;
            return name.Equals("txt_shop_previous", System.StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("txt_shop_next", System.StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("txt_shop_buy", System.StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("txt_shop_startGame", System.StringComparison.OrdinalIgnoreCase);
        }

        private void ResolveVisualPairIfNeeded()
        {
            if (_visualPairResolved && _normalVisual != null && _pressedVisual != null)
            {
                return;
            }

            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_normalVisual == null)
            {
                _normalVisual = ResolveNormalVisualRoot()?.gameObject;
            }

            if (_pressedVisual == null && _normalVisual != null)
            {
                _pressedVisual = FindPressedVisualForNormal(_normalVisual.transform)?.gameObject;
            }

            _visualPairResolved = _normalVisual != null && _pressedVisual != null;

            if (_pressedVisual == null && _debugLogs && _normalVisual != null)
            {
                Debug.LogWarning(
                    $"[ShopNavArrowUiButton_V2] '{name}' could not find pressed visual sibling for '{_normalVisual.name}'.");
            }
        }

        private Transform ResolveNormalVisualRoot()
        {
            Transform candidate = _button != null ? _button.transform : transform;
            if (IsShopTextButtonRoot(candidate))
            {
                return candidate;
            }

            Transform parent = candidate.parent;
            if (parent != null && IsShopTextButtonRoot(parent))
            {
                return parent;
            }

            return candidate;
        }

        private void ShowPressedVisual()
        {
            ResolveVisualPairIfNeeded();
            if (_pressedVisual == null)
            {
                return;
            }

            SyncPressedTransformToNormal();
            SyncPressedRenderingFromNormal();
            SetVisualRootActive(_normalVisual, false);
            SetVisualRootActive(_pressedVisual, true);
        }

        private void SyncPressedRenderingFromNormal()
        {
            if (_normalVisual == null || _pressedVisual == null)
            {
                return;
            }

            SpriteRenderer normalSprite = _normalVisual.GetComponent<SpriteRenderer>();
            if (normalSprite == null)
            {
                normalSprite = _normalVisual.GetComponentInChildren<SpriteRenderer>(true);
            }

            SpriteRenderer pressedSprite = _pressedVisual.GetComponent<SpriteRenderer>();
            if (pressedSprite == null)
            {
                pressedSprite = _pressedVisual.GetComponentInChildren<SpriteRenderer>(true);
            }

            if (normalSprite == null || pressedSprite == null)
            {
                return;
            }

            pressedSprite.sortingLayerID = normalSprite.sortingLayerID;
            pressedSprite.sortingOrder = normalSprite.sortingOrder;
        }

        private void ShowNormalVisual()
        {
            SetVisualRootActive(_pressedVisual, false);
            SetVisualRootActive(_normalVisual, true);
        }

        private void SyncPressedTransformToNormal()
        {
            if (_normalVisual == null || _pressedVisual == null)
            {
                return;
            }

            Transform normalTransform = _normalVisual.transform;
            Transform pressedTransform = _pressedVisual.transform;
            pressedTransform.localPosition = normalTransform.localPosition;
            pressedTransform.localRotation = normalTransform.localRotation;
            pressedTransform.localScale = normalTransform.localScale;
        }

        private void SetVisualRootActive(GameObject visualRoot, bool visible)
        {
            if (visualRoot == null)
            {
                return;
            }

            // Keep this host alive so mouse up / pointer exit still fire.
            if (ReferenceEquals(visualRoot, gameObject))
            {
                SetVisualArtifactsVisible(visualRoot.transform, visible);
                return;
            }

            visualRoot.SetActive(visible);
        }

        private static void SetVisualArtifactsVisible(Transform root, bool visible)
        {
            if (root == null)
            {
                return;
            }

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic != null)
                {
                    graphic.enabled = visible;
                }
            }

            SpriteRenderer[] spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = visible;
                }
            }
        }

        private static bool IsShopTextButtonRoot(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            string name = transform.name;
            return name.StartsWith("TextBTN_Medium", System.StringComparison.OrdinalIgnoreCase) &&
                   !name.EndsWith("_Pressed", System.StringComparison.OrdinalIgnoreCase);
        }

        private static Transform FindPressedVisualForNormal(Transform normalRoot)
        {
            if (normalRoot == null)
            {
                return null;
            }

            string pressedName = normalRoot.name + "_Pressed";
            Transform pressedChild = normalRoot.Find(pressedName);
            if (pressedChild != null)
            {
                return pressedChild;
            }

            Transform parent = normalRoot.parent;
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform sibling = parent.GetChild(i);
                if (sibling != null &&
                    string.Equals(sibling.name, pressedName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return sibling;
                }
            }

            return null;
        }
    }
}
