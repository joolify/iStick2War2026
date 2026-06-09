using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
 * MainMenuNavButton_V2 (World-space menu hit target)
 *
 * PURPOSE:
 * Collider2D + OnMouseDown handler for main menu actions (Play, Settings, ReturnToMainMenu) when UI Buttons cannot
 * raycast SpriteRenderer-based art. Delegates to MainMenu_V2 when assigned.
 * Sibling TextBTN_Medium*_Pressed shows while held (same pattern as shop text buttons).
 * Paired txt_mainMenu_* labels nudge down/left while pressed.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Pause Time.timeScale itself (MainMenu_V2 owns freeze policy).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Menu owner → MainMenu_V2.cs
 * Canvas UI menu hits → MainMenuNavUiButton_V2.cs
 * Shop world hits → ShopBuyButton_V2.cs, ShopNavArrow_V2.cs (same Collider2D pattern)
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Mirrors ShopNavArrow_V2 / ShopBuyButton_V2: thin input surface for world-space canvas stacks.
 */
    [AddComponentMenu("iStick2War/Main Menu Nav Button V2")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class MainMenuNavButton_V2 : MonoBehaviour
    {
        public enum MenuAction
        {
            Play,
            PlaySurvival,
            Continue,
            Settings,
            CloseSettings,
            ShowHighScore,
            CloseHighScore,
            // Load dedicated main menu scene.
            ReturnToMainMenu
        }

        [SerializeField] private MainMenu_V2 _mainMenu;
        [SerializeField] private MenuAction _action = MenuAction.Play;
        [Header("Pressed visual (TextBTN siblings)")]
        [SerializeField] private GameObject _normalVisual;
        [SerializeField] private GameObject _pressedVisual;
        [Header("Label nudge when pressed")]
        [SerializeField] private TMP_Text _associatedLabel;
        [SerializeField] private Vector2 _labelPressedOffset = new Vector2(-5f, -5f);
        [SerializeField] private bool _debugLogs;

        private bool _isPointerDown;
        private bool _latchPressedVisual;
        private RectTransform _labelRect;
        private Vector2 _labelRestAnchoredPosition;
        private bool _labelRestCached;

        internal bool IsReturnToMainMenuAction() => _action == MenuAction.ReturnToMainMenu;
        internal bool IsPlayAction() =>
            _action == MenuAction.Play || _action == MenuAction.PlaySurvival;

        private void Awake()
        {
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

        internal void Configure(MainMenu_V2 mainMenu, MenuAction action)
        {
            _mainMenu = mainMenu;
            _action = TryResolveActionFromButtonName(ResolveButtonObjectName(), out MenuAction mapped)
                ? mapped
                : action;
            ResolveVisualPairIfNeeded();
            ShowNormalVisual();
        }

        internal void ResetToNormalVisual()
        {
            _isPointerDown = false;
            _latchPressedVisual = false;
            ShowNormalVisual();
        }

        // Automation helper for tests/agents.
        public void TriggerAutomationClick()
        {
            OnMouseDown();
        }

        private void OnMouseDown()
        {
            WaveManager_V2 waveManager = UnityEngine.Object.FindAnyObjectByType<WaveManager_V2>();
            if (waveManager != null)
            {
                WaveLoopState_V2 state = waveManager.State;
                bool gameplayOwnsInput =
                    state == WaveLoopState_V2.Preparing ||
                    state == WaveLoopState_V2.InWave ||
                    state == WaveLoopState_V2.Shop;
                if (gameplayOwnsInput)
                {
                    if (_debugLogs)
                    {
                        Debug.Log(
                            $"[MainMenuNavButton_V2] Ignored click on '{name}' while gameplay state is {state}.");
                    }

                    return;
                }
            }

            _isPointerDown = true;
            ShowPressedVisual();

            MenuAction action = GetEffectiveAction();

            if (action == MenuAction.ReturnToMainMenu)
            {
                AudioManager_V2.PlayMenuClick();
                if (_debugLogs)
                {
                    Debug.Log($"[MainMenuNavButton_V2] '{name}' OnMouseDown -> {_action} (reload active scene, pause first)");
                }

                LoadMainMenuScene();
                return;
            }

            if (_mainMenu == null)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning($"[MainMenuNavButton_V2] '{name}': assign MainMenu_V2.");
                }

                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[MainMenuNavButton_V2] '{name}' OnMouseDown -> {_action}");
            }

            if (action == MenuAction.Play)
            {
                _mainMenu.HandlePlayCampaign();
            }
            else if (action == MenuAction.PlaySurvival)
            {
                _mainMenu.HandlePlaySurvival();
            }
            else if (action == MenuAction.Continue)
            {
                _mainMenu.HandleContinue();
            }
            else if (action == MenuAction.Settings)
            {
                _latchPressedVisual = true;
                _mainMenu.HandleShowSettings();
            }
            else if (action == MenuAction.CloseSettings)
            {
                _latchPressedVisual = true;
                _mainMenu.HandleHideSettings();
            }
            else if (action == MenuAction.ShowHighScore)
            {
                _latchPressedVisual = true;
                _mainMenu.HandleShowHighScore();
            }
            else if (action == MenuAction.CloseHighScore)
            {
                _latchPressedVisual = true;
                _mainMenu.HandleHideHighScore();
            }
        }

        internal bool IsCloseHighScoreAction() => GetEffectiveAction() == MenuAction.CloseHighScore;

        private MenuAction GetEffectiveAction()
        {
            return TryResolveActionFromButtonName(ResolveButtonObjectName(), out MenuAction mapped)
                ? mapped
                : _action;
        }

        private string ResolveButtonObjectName()
        {
            return _normalVisual != null ? _normalVisual.name : gameObject.name;
        }

        // Scene overrides often leave _action at Play (0); map known TextBTN names to the correct menu action.
        private static bool TryResolveActionFromButtonName(string buttonName, out MenuAction action)
        {
            action = MenuAction.Play;
            if (string.IsNullOrWhiteSpace(buttonName))
            {
                return false;
            }

            if (buttonName.Equals("TextBTN_MediumStartGame", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("btn_main_menu_play", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("MainMenu_Btn_StartCampaign", StringComparison.OrdinalIgnoreCase))
            {
                action = MenuAction.Play;
                return true;
            }

            if (buttonName.Equals("TextBTN_MediumSurvival", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("MainMenu_Btn_Survival", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("MainMenu_Btn_StartSurvivalMode", StringComparison.OrdinalIgnoreCase))
            {
                action = MenuAction.PlaySurvival;
                return true;
            }

            if (buttonName.Equals("MainMenu_Btn_HighScore", StringComparison.OrdinalIgnoreCase))
            {
                action = MenuAction.ShowHighScore;
                return true;
            }

            if (buttonName.Equals("HighScore_Btn_GoBack", StringComparison.OrdinalIgnoreCase))
            {
                action = MenuAction.CloseHighScore;
                return true;
            }

            if (buttonName.Equals("TextBTN_MediumContinue", StringComparison.OrdinalIgnoreCase))
            {
                action = MenuAction.Continue;
                return true;
            }

            if (buttonName.Equals("TextBTN_MediumSettings", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("btn_main_menu_settings", StringComparison.OrdinalIgnoreCase))
            {
                action = MenuAction.Settings;
                return true;
            }

            if (buttonName.Equals("TextBTN_MediumGoBack", StringComparison.OrdinalIgnoreCase))
            {
                action = MenuAction.CloseSettings;
                return true;
            }

            return false;
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

        // Freeze time before scene load so boot state always starts paused in menu.
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

            if (buttonName.Equals("TextBTN_MediumStartGame", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("btn_main_menu_play", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "txt_mainMenu_startGame", "txt_mainmenu_play" };
            }

            if (buttonName.Equals("TextBTN_MediumSettings", StringComparison.OrdinalIgnoreCase) ||
                buttonName.Equals("btn_main_menu_settings", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "txt_mainMenu_settings", "txt_mainmenu_settings" };
            }

            if (buttonName.Equals("TextBTN_MediumContinue", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "txt_mainMenu_continue", "txt_mainmenu_continue" };
            }

            if (buttonName.Equals("TextBTN_MediumGoBack", StringComparison.OrdinalIgnoreCase))
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
