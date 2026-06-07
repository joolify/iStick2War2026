using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * LifeOverNavButton_V2 (Life lost — TextBTN hit targets)
     *
     * PURPOSE:
     * Collider2D + OnMouseDown for LifeOver V2 actions (continue, shop, main menu).
     * Sibling TextBTN_Medium*_Pressed shows while held; paired txt_lifeOver_* labels nudge (-5, -5).
     * Real-time delay before the action so the pressed state is visible (menu uses the same pattern).
     *
     * NAVIGATION: WaveManager_V2 TryContinueAfterLifeLost / TryGoToShopAfterLifeLost / TryGoToMainMenuAfterLifeLost.
     */
    [AddComponentMenu("iStick2War/Life Over Nav Button V2")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class LifeOverNavButton_V2 : MonoBehaviour
    {
        public enum LifeOverAction
        {
            Continue,
            GoToShop,
            GoToMainMenu
        }

        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private LifeOverAction _action = LifeOverAction.Continue;
        [Header("Pressed visual (TextBTN siblings)")]
        [SerializeField] private GameObject _normalVisual;
        [SerializeField] private GameObject _pressedVisual;
        [Header("Label nudge when pressed")]
        [SerializeField] private TMP_Text _associatedLabel;
        [SerializeField] private Vector2 _labelPressedOffset = new Vector2(-5f, -5f);
        [Tooltip("Real-time pause before LifeOver action runs (Time.timeScale may be 0).")]
        [SerializeField] private float _actionDelaySeconds = 0.2f;
        [Tooltip("Longer delay for Go to shop — shop transition hides LifeOver chrome immediately after.")]
        [SerializeField] private float _goToShopDelaySeconds = 0.4f;
        [SerializeField] private bool _debugLogs;

        private bool _isPointerDown;
        private bool _latchPressedVisual;
        private Coroutine _delayedActionRoutine;
        private RectTransform _labelRect;
        private Vector2 _labelRestAnchoredPosition;
        private bool _labelRestCached;

        internal bool IsContinueAction() => _action == LifeOverAction.Continue;

        internal bool IsDedicatedNavAction() => _action != LifeOverAction.Continue;

        private void Awake()
        {
            ResolveWaveManagerIfNeeded();
            ResolveVisualPairIfNeeded();
            ShowNormalVisual();
        }

        private void OnEnable()
        {
            ResolveVisualPairIfNeeded();
            ShowNormalVisual();
        }

        private void OnDisable()
        {
            _isPointerDown = false;
            _latchPressedVisual = false;
            if (_delayedActionRoutine != null)
            {
                StopCoroutine(_delayedActionRoutine);
                _delayedActionRoutine = null;
            }

            ShowNormalVisual();
        }

        private void Update()
        {
            if (!_isPointerDown || Input.GetMouseButton(0))
            {
                return;
            }

            _isPointerDown = false;
            if (!_latchPressedVisual)
            {
                ShowNormalVisual();
            }
        }

        internal void Configure(WaveManager_V2 waveManager, LifeOverAction action)
        {
            _waveManager = waveManager;
            _action = action;
            ResolveVisualPairIfNeeded();
            ShowNormalVisual();
        }

        public void TriggerAutomationClick()
        {
            ExecuteAction();
        }

        private void OnMouseDown()
        {
            if (_delayedActionRoutine != null)
            {
                return;
            }

            if (_waveManager != null && _waveManager.State != WaveLoopState_V2.LifeOver)
            {
                return;
            }

            _isPointerDown = true;
            _latchPressedVisual = true;
            ShowPressedVisual();
            AudioManager_V2.PlayMenuClick();
            _delayedActionRoutine = StartCoroutine(DelayedExecuteAction());
        }

        private void OnMouseUp()
        {
            _isPointerDown = false;
            if (!_latchPressedVisual)
            {
                ShowNormalVisual();
            }
        }

        private void OnMouseExit()
        {
            if (!_isPointerDown)
            {
                return;
            }

            _isPointerDown = false;
            if (!_latchPressedVisual)
            {
                ShowNormalVisual();
            }
        }

        private IEnumerator DelayedExecuteAction()
        {
            float delay = ResolveActionDelaySeconds();
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            _delayedActionRoutine = null;
            ExecuteAction();
        }

        private float ResolveActionDelaySeconds()
        {
            if (_action == LifeOverAction.GoToShop)
            {
                return Mathf.Max(0f, _goToShopDelaySeconds);
            }

            return Mathf.Max(0f, _actionDelaySeconds);
        }

        private void ExecuteAction()
        {
            ResolveWaveManagerIfNeeded();
            if (_waveManager == null || _waveManager.State != WaveLoopState_V2.LifeOver)
            {
                _latchPressedVisual = false;
                ShowNormalVisual();
                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[LifeOverNavButton_V2] '{name}' -> {_action}");
            }

            switch (_action)
            {
                case LifeOverAction.Continue:
                    _waveManager.TryContinueAfterLifeLost();
                    break;
                case LifeOverAction.GoToShop:
                    _waveManager.TryGoToShopAfterLifeLost();
                    break;
                case LifeOverAction.GoToMainMenu:
                    _waveManager.TryGoToMainMenuAfterLifeLost();
                    break;
            }
        }

        private void ResolveWaveManagerIfNeeded()
        {
            if (_waveManager == null)
            {
                _waveManager = UnityEngine.Object.FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Exclude);
            }
        }

        private void ResolveVisualPairIfNeeded()
        {
            if (_normalVisual == null)
            {
                _normalVisual = gameObject;
            }

            if (_pressedVisual != null)
            {
                return;
            }

            Transform pressed = FindPressedVisualForNormal(_normalVisual.transform);
            if (pressed != null)
            {
                _pressedVisual = pressed.gameObject;
            }
        }

        private void ShowNormalVisual()
        {
            ResolveVisualPairIfNeeded();
            SetVisualRootActive(_pressedVisual, false);
            SetVisualRootActive(_normalVisual, true);
            RestoreLabelPosition();
        }

        private void ShowPressedVisual()
        {
            ResolveVisualPairIfNeeded();
            ApplyLabelPressedOffset();
            if (_pressedVisual == null)
            {
                return;
            }

            SyncPressedTransformToNormal();
            SyncPressedRenderingFromNormal();
            SetVisualRootActive(_normalVisual, false);
            SetVisualRootActive(_pressedVisual, true);
        }

        private void ResolveAssociatedLabelIfNeeded()
        {
            if (_associatedLabel != null)
            {
                _labelRect = _associatedLabel.rectTransform;
                return;
            }

            string buttonName = _normalVisual != null ? _normalVisual.name : gameObject.name;
            string[] labelNames = ResolveLabelNamesForButton(buttonName);
            if (labelNames == null || labelNames.Length == 0)
            {
                return;
            }

            _associatedLabel = FindTmpLabelInScene(labelNames);
            if (_associatedLabel != null)
            {
                _labelRect = _associatedLabel.rectTransform;
            }
        }

        private void CacheLabelRestPositionIfNeeded()
        {
            ResolveAssociatedLabelIfNeeded();
            if (_labelRect == null || _labelRestCached)
            {
                return;
            }

            _labelRestAnchoredPosition = _labelRect.anchoredPosition;
            _labelRestCached = true;
        }

        private void ApplyLabelPressedOffset()
        {
            CacheLabelRestPositionIfNeeded();
            if (_labelRect == null)
            {
                return;
            }

            _labelRect.anchoredPosition = _labelRestAnchoredPosition + _labelPressedOffset;
        }

        private void RestoreLabelPosition()
        {
            if (_labelRect == null || !_labelRestCached)
            {
                return;
            }

            _labelRect.anchoredPosition = _labelRestAnchoredPosition;
        }

        private TMP_Text FindTmpLabelInScene(string[] labelNames)
        {
            Scene scene = gameObject.scene;
            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || text.gameObject.scene != scene)
                {
                    continue;
                }

                string objectName = text.gameObject.name;
                for (int nameIndex = 0; nameIndex < labelNames.Length; nameIndex++)
                {
                    if (objectName.Equals(labelNames[nameIndex], StringComparison.OrdinalIgnoreCase))
                    {
                        return text;
                    }
                }
            }

            return null;
        }

        private static string[] ResolveLabelNamesForButton(string buttonName)
        {
            if (string.IsNullOrWhiteSpace(buttonName))
            {
                return null;
            }

            if (buttonName.Equals("TextBTN_MediumStartNewGame", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "txt_lifeOver_startNewGame" };
            }

            if (buttonName.Equals("TextBTN_MediumGoToShop", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "txt_lifeOver_goToShop" };
            }

            if (buttonName.Equals("TextBTN_MediumGoToMainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "txt_lifeOver_goToMainMenu" };
            }

            return null;
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

        private void SetVisualRootActive(GameObject visualRoot, bool visible)
        {
            if (visualRoot == null)
            {
                return;
            }

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
                    string.Equals(sibling.name, pressedName, StringComparison.OrdinalIgnoreCase))
                {
                    return sibling;
                }
            }

            return null;
        }
    }
}
