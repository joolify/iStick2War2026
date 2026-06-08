using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
 * MainMenuNavUiButton_V2 (Canvas UI Button menu hit target)
 *
 * PURPOSE:
 * Routes Play / Continue / Settings / CloseSettings / ReturnToMainMenu to MainMenu_V2 via Unity UI Button.
 * Optional sibling *_Pressed root while held; otherwise rely on Button Sprite Swap.
 * Paired txt_mainMenu_* labels nudge on press.
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Menu owner → MainMenu_V2.cs
 * World-space menu hits → MainMenuNavButton_V2.cs
 */
    [AddComponentMenu("iStick2War/Main Menu Nav UI Button V2")]
    [RequireComponent(typeof(Button))]
    public sealed class MainMenuNavUiButton_V2 :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        [SerializeField] private MainMenu_V2 _mainMenu;
        [SerializeField] private MainMenuNavButton_V2.MenuAction _action = MainMenuNavButton_V2.MenuAction.Play;
        [Header("Pressed visual (optional sibling)")]
        [SerializeField] private GameObject _normalVisual;
        [SerializeField] private GameObject _pressedVisual;
        [Header("Label nudge when pressed")]
        [SerializeField] private TMP_Text _associatedLabel;
        [SerializeField] private Vector2 _labelPressedOffset = new Vector2(0f, -10f);
        [SerializeField] private bool _debugLogs;

        private Button _button;
        private bool _listenerRegistered;
        private bool _isPointerDown;
        private bool _latchPressedVisual;
        private int _lastHandledClickFrame = -1;
        private RectTransform _labelRect;
        private Vector2 _labelRestAnchoredPosition;
        private bool _labelRestCached;

        internal bool IsReturnToMainMenuAction() => _action == MainMenuNavButton_V2.MenuAction.ReturnToMainMenu;

        internal bool IsCloseSettingsAction() =>
            GetEffectiveAction() == MainMenuNavButton_V2.MenuAction.CloseSettings;

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
            ShowNormalVisual();
        }

        internal void Configure(MainMenu_V2 mainMenu, MainMenuNavButton_V2.MenuAction action)
        {
            _mainMenu = mainMenu;
            _action = TryResolveActionFromButtonName(ResolveButtonObjectName(), out MainMenuNavButton_V2.MenuAction mapped)
                ? mapped
                : action;
            _labelRestCached = false;
            ResolveVisualPairIfNeeded();
            UnregisterListener();
            RegisterListenerIfNeeded();
            ShowNormalVisual();
        }

        internal void ResetToNormalVisual()
        {
            _isPointerDown = false;
            _latchPressedVisual = false;
            ShowNormalVisual();
        }

        public void TriggerAutomationClick()
        {
            HandleClick();
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
            if (_lastHandledClickFrame == Time.frameCount)
            {
                return;
            }

            if (!TryBeginClick())
            {
                return;
            }

            _lastHandledClickFrame = Time.frameCount;

            MainMenuNavButton_V2.MenuAction action = GetEffectiveAction();

            if (action == MainMenuNavButton_V2.MenuAction.ReturnToMainMenu)
            {
                AudioManager_V2.PlayMenuClick();
                if (_debugLogs)
                {
                    Debug.Log($"[MainMenuNavUiButton_V2] '{name}' -> ReturnToMainMenu");
                }

                LoadMainMenuScene();
                return;
            }

            if (_mainMenu == null)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning($"[MainMenuNavUiButton_V2] '{name}': assign MainMenu_V2.");
                }

                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[MainMenuNavUiButton_V2] '{name}' click -> {action}");
            }

            if (action == MainMenuNavButton_V2.MenuAction.Play)
            {
                _mainMenu.HandlePlay();
            }
            else if (action == MainMenuNavButton_V2.MenuAction.Continue)
            {
                _mainMenu.HandleContinue();
            }
            else if (action == MainMenuNavButton_V2.MenuAction.Settings)
            {
                _latchPressedVisual = true;
                _mainMenu.HandleShowSettings();
            }
            else if (action == MainMenuNavButton_V2.MenuAction.CloseSettings)
            {
                _latchPressedVisual = true;
                _mainMenu.HandleHideSettings();
            }
        }

        private bool TryBeginClick()
        {
            WaveManager_V2 waveManager = UnityEngine.Object.FindAnyObjectByType<WaveManager_V2>();
            if (waveManager == null)
            {
                return true;
            }

            WaveLoopState_V2 state = waveManager.State;
            bool gameplayOwnsInput =
                state == WaveLoopState_V2.Preparing ||
                state == WaveLoopState_V2.InWave ||
                state == WaveLoopState_V2.Shop;
            if (!gameplayOwnsInput)
            {
                return true;
            }

            if (_debugLogs)
            {
                Debug.Log(
                    $"[MainMenuNavUiButton_V2] Ignored click on '{name}' while gameplay state is {state}.");
            }

            return false;
        }

        private MainMenuNavButton_V2.MenuAction GetEffectiveAction()
        {
            return TryResolveActionFromButtonName(ResolveButtonObjectName(), out MainMenuNavButton_V2.MenuAction mapped)
                ? mapped
                : _action;
        }

        private string ResolveButtonObjectName()
        {
            return _normalVisual != null ? _normalVisual.name : gameObject.name;
        }

        private static bool TryResolveActionFromButtonName(string buttonName, out MainMenuNavButton_V2.MenuAction action)
        {
            action = MainMenuNavButton_V2.MenuAction.Play;
            if (string.IsNullOrWhiteSpace(buttonName))
            {
                return false;
            }

            if (buttonName.Equals("TextBTN_MediumStartGame", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("btn_main_menu_play", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TestButton_Play", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("MainMenu_Btn_StartGame", StringComparison.OrdinalIgnoreCase))
            {
                action = MainMenuNavButton_V2.MenuAction.Play;
                return true;
            }

            if (buttonName.Equals("TextBTN_MediumContinue", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TestButton_Continue", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("MainMenu_Btn_Continue", StringComparison.OrdinalIgnoreCase))
            {
                action = MainMenuNavButton_V2.MenuAction.Continue;
                return true;
            }

            if (buttonName.Equals("TextBTN_MediumSettings", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("btn_main_menu_settings", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TestButton_Settings", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("MainMenu_Btn_Settings", StringComparison.OrdinalIgnoreCase))
            {
                action = MainMenuNavButton_V2.MenuAction.Settings;
                return true;
            }

            if (buttonName.Equals("TextBTN_MediumGoBack", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TestButton_GoBack", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("Settings_Btn_GoBack", StringComparison.OrdinalIgnoreCase))
            {
                action = MainMenuNavButton_V2.MenuAction.CloseSettings;
                return true;
            }

            return false;
        }

        private static void LoadMainMenuScene()
        {
            GameplayPauseButton_V2.ReturnToMainMenuScene();
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

            string buttonName = _normalVisual != null ? _normalVisual.name : gameObject.name;
            string[] labelNames = ResolveLabelNamesForButton(buttonName);
            if (labelNames == null || labelNames.Length == 0)
            {
                return;
            }

            _associatedLabel = FindTmpLabelInScene(labelNames);
            if (_associatedLabel == null)
            {
                _associatedLabel = FindTmpLabelInChildren();
            }

            if (_associatedLabel != null)
            {
                _labelRect = _associatedLabel.rectTransform;
            }
        }

        private TMP_Text FindTmpLabelInChildren()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null)
                {
                    return text;
                }
            }

            return null;
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

            if (buttonName.Equals("TextBTN_MediumStartGame", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("btn_main_menu_play", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TestButton_Play", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("MainMenu_Btn_StartGame", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "txt_mainMenu_startGame", "txt_mainmenu_play" };
            }

            if (buttonName.Equals("TextBTN_MediumSettings", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("btn_main_menu_settings", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TestButton_Settings", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("MainMenu_Btn_Settings", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "txt_mainMenu_settings", "txt_mainmenu_settings" };
            }

            if (buttonName.Equals("TextBTN_MediumContinue", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TestButton_Continue", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("MainMenu_Btn_Continue", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "txt_mainMenu_continue", "txt_mainmenu_continue" };
            }

            if (buttonName.Equals("TextBTN_MediumGoBack", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("TestButton_GoBack", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("Settings_Btn_GoBack", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "txt_settings_goBack" };
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
