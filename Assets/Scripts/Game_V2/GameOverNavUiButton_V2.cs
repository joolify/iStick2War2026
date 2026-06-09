using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * GameOverNavUiButton_V2 (Run ended — canvas UI Button)
     *
     * PURPOSE:
     * Routes GameOver V2 / GameWon UI buttons (restart run, return to main menu) while terminal end states are active.
     *
     * ---------------------------------------------------------
     * NAVIGATION (Game_V2)
     *
     * Restart → WaveManager_V2.ChooseRestartRun
     * Main menu → GameplayPauseButton_V2.ReturnToMainMenuScene
     */
    [AddComponentMenu("iStick2War/Game Over Nav UI Button V2")]
    [RequireComponent(typeof(Button))]
    public sealed class GameOverNavUiButton_V2 :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        public enum GameOverAction
        {
            RestartRun,
            ReturnToMainMenu,
        }

        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private GameOverAction _action = GameOverAction.RestartRun;
        [Header("Pressed visual (optional sibling)")]
        [SerializeField] private GameObject _normalVisual;
        [SerializeField] private GameObject _pressedVisual;
        [Header("Label nudge when pressed")]
        [SerializeField] private TMP_Text _associatedLabel;
        [SerializeField] private Vector2 _labelPressedOffset = new Vector2(0f, -10f);
        [SerializeField] private float _actionDelaySeconds = 0.2f;
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

        internal void Configure(WaveManager_V2 waveManager, GameOverAction action)
        {
            _waveManager = waveManager;
            _action = TryResolveActionFromButtonName(ResolveButtonObjectName(), out GameOverAction mapped)
                ? mapped
                : action;
            _labelRestCached = false;
            ResolveVisualPairIfNeeded();
            UnregisterListener();
            RegisterListenerIfNeeded();
            ShowNormalVisual();
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
            if (!IsNavAllowedForCurrentState())
            {
                if (_debugLogs)
                {
                    Debug.LogWarning(
                        $"[GameOverNavUiButton_V2] Ignored click on '{name}' (state={_waveManager?.State}).");
                }

                return;
            }

            AudioManager_V2.PlayMenuClick();
            _latchPressedVisual = true;
            ShowPressedVisual();
            _delayedActionRoutine = StartCoroutine(DelayedExecuteAction());
        }

        private IEnumerator DelayedExecuteAction()
        {
            float delay = Mathf.Max(0f, _actionDelaySeconds);
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

            if (!IsNavAllowedForCurrentState())
            {
                return;
            }

            GameOverAction action = GetEffectiveAction();
            if (_debugLogs)
            {
                Debug.Log($"[GameOverNavUiButton_V2] '{name}' -> {action}");
            }

            switch (action)
            {
                case GameOverAction.RestartRun:
                    _waveManager.ChooseRestartRun();
                    break;
                case GameOverAction.ReturnToMainMenu:
                    GameplayPauseButton_V2.ReturnToMainMenuScene();
                    break;
            }
        }

        private GameOverAction GetEffectiveAction()
        {
            return TryResolveActionFromButtonName(ResolveButtonObjectName(), out GameOverAction mapped)
                ? mapped
                : _action;
        }

        private bool IsNavAllowedForCurrentState()
        {
            if (_waveManager == null)
            {
                return false;
            }

            GameOverAction action = GetEffectiveAction();
            if (action == GameOverAction.ReturnToMainMenu)
            {
                return _waveManager.State == WaveLoopState_V2.GameOver ||
                       _waveManager.State == WaveLoopState_V2.GameWon ||
                       _waveManager.State == WaveLoopState_V2.GameError;
            }

            return _waveManager.State == WaveLoopState_V2.GameOver;
        }

        private string ResolveButtonObjectName()
        {
            return _normalVisual != null ? _normalVisual.name : gameObject.name;
        }

        private static bool TryResolveActionFromButtonName(string buttonName, out GameOverAction action)
        {
            action = GameOverAction.RestartRun;
            if (string.IsNullOrWhiteSpace(buttonName))
            {
                return false;
            }

            if (buttonName.Equals("LifeOver_Btn_StartGame", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("GameOver_Btn_StartGame", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TextBTN_GameOver_MediumStartNewGame", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TextBTN_MediumStartNewGame", StringComparison.OrdinalIgnoreCase))
            {
                action = GameOverAction.RestartRun;
                return true;
            }

            if (buttonName.Equals("LifeOver_Btn_MainMenu", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("GameOver_Btn_MainMenu", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("GameWon_Btn_MainMenu", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("GameError_Btn_MainMenu", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TextBTN_GameOver_MediumGoToMainMenu", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TextBTN_MediumGoToMainMenu", StringComparison.OrdinalIgnoreCase))
            {
                action = GameOverAction.ReturnToMainMenu;
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
