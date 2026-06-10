using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * LifeOverNavUiButton_V2 (Life lost — canvas UI Button)
     *
     * PURPOSE:
     * Routes LifeOver_Btn_Continue / Shop / MainMenu to WaveManager_V2 while
     * WaveLoopState_V2.LifeOver is active. Optional sibling *_Pressed and label nudge on press.
     *
     * ---------------------------------------------------------
     * NAVIGATION (Game_V2)
     *
     * Wave actions → WaveManager_V2.TryContinueAfterLifeLost / TryGoToShopAfterLifeLost / TryGoToMainMenuAfterLifeLost
     * World-space hits → LifeOverNavButton_V2.cs
     */
    [AddComponentMenu("iStick2War/Life Over Nav UI Button V2")]
    [RequireComponent(typeof(Button))]
    [DefaultExecutionOrder(-50)]
    public sealed class LifeOverNavUiButton_V2 :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private LifeOverNavButton_V2.LifeOverAction _action = LifeOverNavButton_V2.LifeOverAction.Continue;
        [Header("Pressed visual (optional sibling)")]
        [SerializeField] private GameObject _normalVisual;
        [SerializeField] private GameObject _pressedVisual;
        [Header("Label nudge when pressed")]
        [SerializeField] private TMP_Text _associatedLabel;
        [SerializeField] private Vector2 _labelPressedOffset = new Vector2(0f, -10f);
        [Tooltip("Real-time pause before LifeOver action runs (Time.timeScale may be 0).")]
        [SerializeField] private float _actionDelaySeconds = 0.2f;
        [Tooltip("Longer delay for Go to shop — shop transition hides LifeOver chrome immediately after.")]
        [SerializeField] private float _goToShopDelaySeconds = 0.4f;
        [SerializeField] private bool _debugLogs;

        private Button _button;
        private bool _listenerRegistered;
        private bool _isPointerDown;
        private bool _latchPressedVisual;
        private int _lastHandledClickFrame = -1;
        private Coroutine _delayedActionRoutine;
        private RectTransform _labelRect;
        private Vector2 _labelRestAnchoredPosition;
        private bool _labelRestCached;

        private void Awake()
        {
            _button = GetComponent<Button>();
            ResolveVisualPairIfNeeded();
            ShowNormalVisual();
        }

        private void OnEnable()
        {
            if (GetComponent<GameOverNavUiButton_V2>() != null)
            {
                enabled = false;
                return;
            }

            _labelRestCached = false;
            _button = GetComponent<Button>();
            ResolveVisualPairIfNeeded();
            RegisterListenerIfNeeded();
            ShowNormalVisual();
        }

        private void OnDisable()
        {
            UnregisterListener();
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
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            TryHandleDirectPointerClick();
        }

        internal void Configure(WaveManager_V2 waveManager, LifeOverNavButton_V2.LifeOverAction action)
        {
            _waveManager = waveManager;
            _action = TryResolveActionFromButtonName(ResolveButtonObjectName(), out LifeOverNavButton_V2.LifeOverAction mapped)
                ? mapped
                : action;
            _labelRestCached = false;
            ResolveVisualPairIfNeeded();
            UnregisterListener();
            RegisterListenerIfNeeded();
            ShowNormalVisual();
        }

        public void TriggerAutomationClick()
        {
            ExecuteAction();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPointerDown = true;
            ShowPressedVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPointerDown = false;
            if (!_latchPressedVisual)
            {
                ShowNormalVisual();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
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

        private void RegisterListenerIfNeeded()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            if (_button == null || _listenerRegistered)
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
            if (_lastHandledClickFrame == Time.frameCount || _delayedActionRoutine != null)
            {
                return;
            }

            _lastHandledClickFrame = Time.frameCount;
            ResolveWaveManagerIfNeeded();
            if (_waveManager == null || _waveManager.State != WaveLoopState_V2.LifeOver)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning(
                        $"[LifeOverNavUiButton_V2] Ignored click on '{name}' (state={_waveManager?.State}).");
                }

                return;
            }

            AudioManager_V2.PlayMenuClick();
            _latchPressedVisual = true;
            ShowPressedVisual();
            _delayedActionRoutine = StartCoroutine(DelayedExecuteAction());
        }

        private void TryHandleDirectPointerClick()
        {
            if (GetComponent<GameOverNavUiButton_V2>() != null)
            {
                return;
            }

            if (_button == null || !_button.isActiveAndEnabled || !_button.interactable)
            {
                return;
            }

            RectTransform buttonRect = transform as RectTransform;
            if (buttonRect == null)
            {
                return;
            }

            Camera eventCamera = ResolveEventCamera();

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Ended)
                {
                    continue;
                }

                if (!RectTransformUtility.RectangleContainsScreenPoint(buttonRect, touch.position, eventCamera))
                {
                    continue;
                }

                HandleClick();
                return;
            }

            if (Input.touchCount == 0 &&
                Input.GetMouseButtonUp(0) &&
                RectTransformUtility.RectangleContainsScreenPoint(buttonRect, Input.mousePosition, eventCamera))
            {
                HandleClick();
            }
        }

        private Camera ResolveEventCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        private IEnumerator DelayedExecuteAction()
        {
            float delay = _action == LifeOverNavButton_V2.LifeOverAction.GoToShop
                ? Mathf.Max(0f, _goToShopDelaySeconds)
                : Mathf.Max(0f, _actionDelaySeconds);
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            _delayedActionRoutine = null;
            ExecuteAction();
        }

        private void ExecuteAction()
        {
            ResolveWaveManagerIfNeeded();
            _latchPressedVisual = false;
            ShowNormalVisual();

            if (_waveManager == null || _waveManager.State != WaveLoopState_V2.LifeOver)
            {
                return;
            }

            LifeOverNavButton_V2.LifeOverAction action = GetEffectiveAction();
            if (_debugLogs)
            {
                Debug.Log($"[LifeOverNavUiButton_V2] '{name}' -> {action}");
            }

            switch (action)
            {
                case LifeOverNavButton_V2.LifeOverAction.Continue:
                    _waveManager.TryContinueAfterLifeLost();
                    break;
                case LifeOverNavButton_V2.LifeOverAction.GoToShop:
                    _waveManager.TryGoToShopAfterLifeLost();
                    break;
                case LifeOverNavButton_V2.LifeOverAction.GoToMainMenu:
                    _waveManager.TryGoToMainMenuAfterLifeLost();
                    break;
            }
        }

        private LifeOverNavButton_V2.LifeOverAction GetEffectiveAction()
        {
            return TryResolveActionFromButtonName(ResolveButtonObjectName(), out LifeOverNavButton_V2.LifeOverAction mapped)
                ? mapped
                : _action;
        }

        private string ResolveButtonObjectName()
        {
            return _normalVisual != null ? _normalVisual.name : gameObject.name;
        }

        private static bool TryResolveActionFromButtonName(
            string buttonName,
            out LifeOverNavButton_V2.LifeOverAction action)
        {
            action = LifeOverNavButton_V2.LifeOverAction.Continue;
            if (string.IsNullOrWhiteSpace(buttonName))
            {
                return false;
            }

            if (buttonName.Equals("LifeOver_Btn_Continue", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TextBTN_MediumStartNewGame", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TextBTN_MediumStartGame", StringComparison.OrdinalIgnoreCase))
            {
                action = LifeOverNavButton_V2.LifeOverAction.Continue;
                return true;
            }

            if (buttonName.Equals("LifeOver_Btn_Shop", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TextBTN_MediumGoToShop", StringComparison.OrdinalIgnoreCase))
            {
                action = LifeOverNavButton_V2.LifeOverAction.GoToShop;
                return true;
            }

            if (buttonName.Equals("LifeOver_Btn_MainMenu", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TextBTN_MediumGoToMainMenu", StringComparison.OrdinalIgnoreCase))
            {
                action = LifeOverNavButton_V2.LifeOverAction.GoToMainMenu;
                return true;
            }

            return false;
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
            if (_pressedVisual != null)
            {
                SetVisualRootActive(_pressedVisual, false);
                SetVisualRootActive(_normalVisual, true);
            }

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

            TMP_Text childLabel = GetComponentInChildren<TMP_Text>(true);
            if (childLabel != null)
            {
                _associatedLabel = childLabel;
                _labelRect = childLabel.rectTransform;
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

        private void SyncPressedTransformToNormal()
        {
            if (_normalVisual == null || _pressedVisual == null)
            {
                return;
            }

            Transform normalTransform = _normalVisual.transform;
            Transform pressedTransform = _pressedVisual.transform;

            RectTransform normalRect = normalTransform as RectTransform;
            RectTransform pressedRect = pressedTransform as RectTransform;
            if (normalRect != null && pressedRect != null)
            {
                pressedRect.anchorMin = normalRect.anchorMin;
                pressedRect.anchorMax = normalRect.anchorMax;
                pressedRect.pivot = normalRect.pivot;
                pressedRect.anchoredPosition = normalRect.anchoredPosition;
                pressedRect.sizeDelta = normalRect.sizeDelta;
                pressedRect.localRotation = normalRect.localRotation;
                pressedRect.localScale = normalRect.localScale;
                return;
            }

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
