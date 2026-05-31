using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * Shop carousel prev/next for TextBTN_MediumPrev/Next-style controls.
     * Supports canvas UI (Button + raycast Graphic) and world-space sprites (Collider2D + OnMouseDown),
     * matching btn_shop_arrow_left/right when TextBTN uses SpriteRenderer instead of UI Image.
     * Swaps sibling *_Pressed visuals while held.
     */
    [AddComponentMenu("iStick2War/Shop Nav Arrow UI Button V2")]
    public sealed class ShopNavArrowUiButton_V2 :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        [SerializeField] private ShopPanel_V2 _shopPanel;
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

        private void Awake()
        {
            _button = GetComponent<Button>();
            _usesUiClickPath = HasRaycastableGraphic();
            EnsureWorldSpaceHitTargetIfNeeded();
            ResolveVisualPairIfNeeded();
            ShowNormalVisual();
        }

        private void OnEnable()
        {
            RegisterListenerIfNeeded();
            ResolveVisualPairIfNeeded();
            ShowNormalVisual();
        }

        private void OnDisable()
        {
            UnregisterListener();
            ShowNormalVisual();
        }

        internal void Configure(ShopPanel_V2 shopPanel, ShopNavArrow_V2.ArrowDirection direction)
        {
            _shopPanel = shopPanel;
            _direction = direction;
            _usesUiClickPath = HasRaycastableGraphic();
            EnsureWorldSpaceHitTargetIfNeeded();
            UnregisterListener();
            RegisterListenerIfNeeded();
            ResolveVisualPairIfNeeded();
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

        private void OnMouseDown()
        {
            if (_usesUiClickPath)
            {
                return;
            }

            ShowPressedVisual();
            HandleClick();
        }

        private void OnMouseUp()
        {
            if (_usesUiClickPath)
            {
                return;
            }

            ShowNormalVisual();
        }

        private void OnMouseExit()
        {
            if (_usesUiClickPath)
            {
                return;
            }

            ShowNormalVisual();
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
                Debug.Log($"[ShopNavArrowUiButton_V2] '{name}' click -> {_direction}");
            }

            if (_direction == ShopNavArrow_V2.ArrowDirection.Previous)
            {
                _shopPanel.OnShopArrowPreviousClicked();
            }
            else
            {
                _shopPanel.OnShopArrowNextClicked();
            }
        }

        private void EnsureWorldSpaceHitTargetIfNeeded()
        {
            if (_usesUiClickPath)
            {
                return;
            }

            if (GetComponent<Collider2D>() != null)
            {
                return;
            }

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

            BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
            FitBoxColliderToSprite(boxCollider, spriteRenderer);

            if (_debugLogs)
            {
                Debug.Log($"[ShopNavArrowUiButton_V2] '{name}' added BoxCollider2D for world-space clicks.");
            }
        }

        private void FitBoxColliderToSprite(BoxCollider2D boxCollider, SpriteRenderer spriteRenderer)
        {
            if (boxCollider == null || spriteRenderer == null)
            {
                return;
            }

            if (spriteRenderer.transform == transform && spriteRenderer.sprite != null)
            {
                boxCollider.size = spriteRenderer.sprite.bounds.size;
                boxCollider.offset = spriteRenderer.sprite.bounds.center;
                return;
            }

            Bounds bounds = spriteRenderer.bounds;
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = transform.InverseTransformVector(bounds.size);
            boxCollider.offset = localCenter;
            boxCollider.size = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
        }

        private bool HasRaycastableGraphic()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic != null && graphic.raycastTarget)
                {
                    return true;
                }
            }

            return false;
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

            if (!_visualPairResolved && _debugLogs)
            {
                Debug.LogWarning(
                    $"[ShopNavArrowUiButton_V2] '{name}' could not resolve normal/pressed visuals. " +
                    $"normal='{DescribeObject(_normalVisual)}', pressed='{DescribeObject(_pressedVisual)}'.");
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

        private void ShowPressedVisual()
        {
            ResolveVisualPairIfNeeded();
            SetVisualRootActive(_normalVisual, false);
            SetVisualRootActive(_pressedVisual, true);
        }

        private void ShowNormalVisual()
        {
            SetVisualRootActive(_pressedVisual, false);
            SetVisualRootActive(_normalVisual, true);
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

        private static string DescribeObject(GameObject target)
        {
            return target != null ? target.name : "none";
        }
    }
}
