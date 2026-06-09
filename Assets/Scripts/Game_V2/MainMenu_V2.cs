using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace iStick2War_V2
{
    /*
 * MainMenu_V2 (Boot menu + time freeze)
 *
 * PURPOSE:
 * Pauses the simulation with Time.timeScale until Play, wires optional UI Buttons and/or world-space
 * MainMenuNavButton_V2 (world) / MainMenuNavUiButton_V2 (canvas UI), and hides configured
 * menu roots when the player starts the run.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Own wave progression or shop economy (WaveManager_V2 / ShopPanel_V2 after load).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * World-space menu hits → MainMenuNavButton_V2.cs
 * Canvas UI menu hits → MainMenuNavUiButton_V2.cs
 * Narrow-aspect menu camera → OrthographicCameraAspectFitter_V2.cs
 * Main menu UI canvas layout → MainMenuCanvasLayout_V2.cs
 * Settings panel layout → SettingsPanelLayout_V2.cs
 * After Play → WaveManager_V2.cs (gameplay scene), not this file
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Early execution order so gameplay systems awake after menu state is stable; keeps boot UX in one MonoBehaviour.
 */
    [DefaultExecutionOrder(-200)]
    public sealed class MainMenu_V2 : MonoBehaviour
    {
        private const string TmpPlayName = "txt_mainmenu_play";
        private const string TmpSettingsName = "txt_mainmenu_settings";
        private const string TmpStartGameAltName = "txt_mainMenu_startGame";
        private const string TmpSettingsAltName = "txt_mainMenu_settings";
        private const string DefaultWorldPlayButtonName = "TextBTN_MediumStartGame";
        private const string DefaultWorldContinueButtonName = "TextBTN_MediumContinue";
        private const string DefaultWorldSettingsButtonName = "TextBTN_MediumSettings";
        private const string TmpContinueName = "txt_mainmenu_continue";
        private const string TmpContinueAltName = "txt_mainMenu_continue";
        private const string DefaultSettingsPanelName = "Settings V2";
        private const string DefaultHighScorePanelName = "HighScore V2";
        private const string DefaultSettingsBackButtonName = "TextBTN_MediumGoBack";
        private const string MainMenuBackgroundObjectName = "bkg_main_menu";
        private const string MainMenuCanvasName = "MainMenu-canvas";
        private const string MainMenuSafeAreaRootName = "SafeAreaRoot";
        private const string MainMenuSafeAreaStateKey = "MainMenu-canvas/SafeAreaRoot";
        private static readonly string[] MenuButtonsHiddenWhileSettingsOpen =
        {
            DefaultWorldPlayButtonName,
            DefaultWorldSettingsButtonName,
            DefaultWorldContinueButtonName,
            "TextBTN_MediumStartGame_Pressed",
            "TextBTN_MediumSettings_Pressed",
            "TextBTN_MediumContinue_Pressed",
            TmpPlayName,
            TmpSettingsName,
            TmpStartGameAltName,
            TmpSettingsAltName,
            TmpContinueName,
            TmpContinueAltName,
            "TestButton_Settings",
            "TestButton_Settings_Pressed",
            "MainMenu_Btn_Continue",
            "MainMenu_Btn_StartGame",
            "MainMenu_Btn_StartCampaign",
            "MainMenu_Btn_Settings",
            "MainMenu_Btn_StartSurvivalMode",
            "MainMenu_Btn_HighScore"
        };
        private static readonly string[] DefaultMenuObjectNames =
        {
            "bkg_main_menu",
            "MainMenu-canvas",
            "btn_main_menu_play",
            "btn_main_menu_settings",
            DefaultWorldPlayButtonName,
            DefaultWorldSettingsButtonName,
            TmpPlayName,
            TmpSettingsName
        };

        [Header("Roots (optional)")]
        [Tooltip("Hidden when Play is pressed, e.g. bkg_main_menu and/or MainMenu-canvas.")]
        [SerializeField] private GameObject[] _hideOnPlay = Array.Empty<GameObject>();

        [Header("Buttons (optional if TMP names exist)")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _settingsButton;

        [Header("Settings")]
        [SerializeField] private GameObject _settingsPanel;
        [Tooltip("Resolved at runtime when _settingsPanel is unset (MainMenuScene default).")]
        [SerializeField] private string _settingsPanelObjectName = DefaultSettingsPanelName;
        [Header("High score")]
        [SerializeField] private GameObject _highScorePanel;
        [Tooltip("Resolved at runtime when _highScorePanel is unset (MainMenuScene default).")]
        [SerializeField] private string _highScorePanelObjectName = DefaultHighScorePanelName;
        [SerializeField] private bool _pauseTimeWhileMenuOpen = true;
        [Tooltip("Real-time pause before overlay panels open/close so pressed button states stay visible.")]
        [SerializeField] private float _settingsOpenDelaySeconds = 0.2f;

        [Header("World-space TextBTN (optional)")]
        [SerializeField] private string _worldPlayButtonObjectName = DefaultWorldPlayButtonName;
        [SerializeField] private string _worldContinueButtonObjectName = DefaultWorldContinueButtonName;
        [SerializeField] private string _worldSettingsButtonObjectName = DefaultWorldSettingsButtonName;

        [Header("Gameplay")]
        [Tooltip("When enabled, Play loads this scene instead of starting gameplay in the current scene.")]
        [SerializeField] private bool _loadGameplaySceneOnPlay;
        [SerializeField] private string _gameplaySceneName = "SampleScene";
        [Tooltip("Optional; if unset, resolved once when Play is pressed.")]
        [SerializeField] private WaveManager_V2 _waveManager;

        private bool _gameStarted;
        private bool _loggedMissingSettingsPanel;
        private Coroutine _loadGameplaySceneRoutine;
        private Coroutine _showSettingsRoutine;
        private Coroutine _hideSettingsRoutine;
        private Coroutine _showHighScoreRoutine;
        private Coroutine _hideHighScoreRoutine;
        private readonly Dictionary<string, bool> _menuButtonActiveBeforeSettings = new Dictionary<string, bool>();
        private bool _settingsMenuButtonsHideStateCaptured;
        private bool _loggedMissingHighScorePanel;

        // True when the menu is active and HandlePlay has not run this session
        // (automation / tests: use with WaveManager_V2 Preparing, not only Time.timeScale).
        public bool IsWaitingForPlay =>
            isActiveAndEnabled && gameObject.activeInHierarchy && !_gameStarted;

        public bool CanContinueSavedRun => WaveManager_V2.HasSavedRunAvailable();

        private void Awake()
        {
            GameSettings_V2.LoadAndApplyAll();
            AudioManager_V2.EnsureInstance();
            if (_pauseTimeWhileMenuOpen)
            {
                Time.timeScale = 0f;
            }

            AudioManager_V2.SetMenuMusic();
            EnsureMenuCameraAspectFitter();
            EnsureMainMenuCanvasLayout();
        }

        private static void EnsureMainMenuCanvasLayout()
        {
            GameObject canvasGo = GameObject.Find("MainMenu-canvas");
            if (canvasGo == null)
            {
                return;
            }

            MainMenuCanvasLayout_V2 layout = canvasGo.GetComponent<MainMenuCanvasLayout_V2>();
            if (layout == null)
            {
                layout = canvasGo.AddComponent<MainMenuCanvasLayout_V2>();
            }

            layout.ApplyIfNeeded();
        }

        private static void RefreshMainMenuButtonLayout()
        {
            GameObject canvasGo = GameObject.Find("MainMenu-canvas");
            if (canvasGo == null)
            {
                return;
            }

            MainMenuCanvasLayout_V2 layout = canvasGo.GetComponent<MainMenuCanvasLayout_V2>();
            if (layout != null)
            {
                layout.RefreshButtonLayout();
            }
        }

        private static void EnsureMenuCameraAspectFitter()
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic)
            {
                return;
            }

            OrthographicCameraAspectFitter_V2 fitter = camera.GetComponent<OrthographicCameraAspectFitter_V2>();
            if (fitter == null)
            {
                fitter = camera.gameObject.AddComponent<OrthographicCameraAspectFitter_V2>();
            }

            fitter.Configure(referenceOrthographicSize: 5f, referenceAspectWidth: 16f, referenceAspectHeight: 9f);
        }

        private void Start()
        {
            ResolveReferencesIfNeeded();
            PrepareSettingsPanelForMenuBoot();
            PrepareHighScorePanelForMenuBoot();
            if (_playButton != null)
            {
                _playButton.onClick.AddListener(HandlePlay);
            }

            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(HandleContinue);
            }

            if (_settingsButton != null)
            {
                _settingsButton.onClick.AddListener(HandleShowSettings);
            }

            RefreshContinueAvailability();
            RefreshSurvivalHighScoreLabel();
        }

        private void LateUpdate()
        {
            if (_gameStarted || !IsMainMenuOverlayOpen())
            {
                return;
            }

            // Keep menu buttons hidden while an overlay panel stays open.
            ApplyMainMenuButtonsHiddenForSettings(saveState: false);
        }

        private bool IsMainMenuOverlayOpen()
        {
            if (_settingsPanel != null && _settingsPanel.activeSelf)
            {
                return true;
            }

            return _highScorePanel != null && _highScorePanel.activeSelf;
        }

        private void OnDestroy()
        {
            if (_playButton != null)
            {
                _playButton.onClick.RemoveListener(HandlePlay);
            }

            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveListener(HandleContinue);
            }

            if (_settingsButton != null)
            {
                _settingsButton.onClick.RemoveListener(HandleShowSettings);
            }
        }

        private void ResolveReferencesIfNeeded()
        {
            ResolveButtonsIfNeeded();
            ResolveSettingsPanelIfNeeded();
            ResolveHighScorePanelIfNeeded();
            WireWorldNavButtonsIfNeeded();
        }

        private void ResolveHighScorePanelIfNeeded()
        {
            if (_highScorePanel != null || string.IsNullOrWhiteSpace(_highScorePanelObjectName))
            {
                return;
            }

            _highScorePanel = FindNamedObjectInScene(_highScorePanelObjectName);
        }

        private void ResolveSettingsPanelIfNeeded()
        {
            if (_settingsPanel != null || string.IsNullOrWhiteSpace(_settingsPanelObjectName))
            {
                return;
            }

            _settingsPanel = FindNamedObjectInScene(_settingsPanelObjectName);
        }

        private void PrepareSettingsPanelForMenuBoot()
        {
            if (_settingsPanel == null)
            {
                return;
            }

            if (_settingsPanel.activeSelf)
            {
                // Scene is often saved with Settings V2 active for layout work; boot into the main menu.
                _settingsPanel.SetActive(false);
            }

            _settingsMenuButtonsHideStateCaptured = false;
            RestoreMainMenuButtonsAfterSettings();
        }

        private void PrepareHighScorePanelForMenuBoot()
        {
            if (_highScorePanel == null)
            {
                return;
            }

            if (_highScorePanel.activeSelf)
            {
                _highScorePanel.SetActive(false);
            }
        }

        private void WireWorldNavButtonsIfNeeded()
        {
            EnsureWorldNavButton(_worldPlayButtonObjectName, MainMenuNavButton_V2.MenuAction.Play);
            EnsureWorldNavButton("TextBTN_MediumSurvival", MainMenuNavButton_V2.MenuAction.PlaySurvival);
            EnsureWorldNavButton(_worldContinueButtonObjectName, MainMenuNavButton_V2.MenuAction.Continue);
            EnsureWorldNavButton(_worldSettingsButtonObjectName, MainMenuNavButton_V2.MenuAction.Settings);
            EnsureWorldNavButton(DefaultSettingsBackButtonName, MainMenuNavButton_V2.MenuAction.CloseSettings);
            // Legacy sprite names from early main-menu prefabs.
            EnsureWorldNavButton("btn_main_menu_play", MainMenuNavButton_V2.MenuAction.Play);
            EnsureWorldNavButton("btn_main_menu_settings", MainMenuNavButton_V2.MenuAction.Settings);
            WireUiNavButtonsIfNeeded();
        }

        private void WireUiNavButtonsIfNeeded()
        {
            EnsureUiNavButton("MainMenu_Btn_StartGame", MainMenuNavButton_V2.MenuAction.Play);
            EnsureUiNavButton("MainMenu_Btn_StartCampaign", MainMenuNavButton_V2.MenuAction.Play);
            EnsureUiNavButton("MainMenu_Btn_Survival", MainMenuNavButton_V2.MenuAction.PlaySurvival);
            EnsureUiNavButton("MainMenu_Btn_StartSurvivalMode", MainMenuNavButton_V2.MenuAction.PlaySurvival);
            EnsureUiNavButton("TestButton_Survival", MainMenuNavButton_V2.MenuAction.PlaySurvival);
            EnsureUiNavButton("MainMenu_Btn_HighScore", MainMenuNavButton_V2.MenuAction.ShowHighScore);
            EnsureUiNavButton("MainMenu_Btn_Continue", MainMenuNavButton_V2.MenuAction.Continue);
            EnsureUiNavButton("MainMenu_Btn_Settings", MainMenuNavButton_V2.MenuAction.Settings);
            EnsureUiNavButton("TestButton_Settings", MainMenuNavButton_V2.MenuAction.Settings);
            EnsureUiNavButton("Settings_Btn_GoBack", MainMenuNavButton_V2.MenuAction.CloseSettings);
            EnsureUiNavButton("HighScore_Btn_GoBack", MainMenuNavButton_V2.MenuAction.CloseHighScore);
        }

        private void EnsureUiNavButton(string objectName, MainMenuNavButton_V2.MenuAction action)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            GameObject target = FindNamedObjectInScene(objectName);
            if (target == null)
            {
                return;
            }

            MainMenuNavUiButton_V2 nav = target.GetComponent<MainMenuNavUiButton_V2>();
            if (nav == null)
            {
                if (target.GetComponent<Button>() == null)
                {
                    return;
                }

                nav = target.AddComponent<MainMenuNavUiButton_V2>();
            }

            nav.Configure(this, action);
        }

        private void EnsureWorldNavButton(string objectName, MainMenuNavButton_V2.MenuAction action)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            GameObject target = FindNamedObjectInScene(objectName);
            if (target == null)
            {
                return;
            }

            MainMenuNavButton_V2 nav = target.GetComponent<MainMenuNavButton_V2>();
            if (nav == null)
            {
                Collider2D collider = target.GetComponent<Collider2D>();
                if (collider == null)
                {
                    BoxCollider2D box = target.AddComponent<BoxCollider2D>();
                    SpriteRenderer sprite = target.GetComponent<SpriteRenderer>();
                    if (sprite != null && sprite.sprite != null)
                    {
                        box.size = sprite.sprite.bounds.size;
                    }
                }

                nav = target.AddComponent<MainMenuNavButton_V2>();
            }

            nav.Configure(this, action);

            if (action == MainMenuNavButton_V2.MenuAction.CloseSettings)
            {
                // Settings V2 GoBack was copied from life-over UI; remove stray continue handler.
                LifeOverContinueButton_V2 strayContinue = target.GetComponent<LifeOverContinueButton_V2>();
                if (strayContinue != null)
                {
                    Destroy(strayContinue);
                }
            }

            EnsureSettingsGoBackLabelClean();
        }

        // txt_settings_goBack was copied from life-over UI; strip stray click handlers on the label.
        private void EnsureSettingsGoBackLabelClean()
        {
            TMP_Text label = FindTmpLabelInScene("txt_settings_goBack");
            if (label == null)
            {
                return;
            }

            label.raycastTarget = false;
            LifeOverLabelUiButton_V2 strayLabelHandler = label.GetComponent<LifeOverLabelUiButton_V2>();
            if (strayLabelHandler != null)
            {
                Destroy(strayLabelHandler);
            }
        }

        private static TMP_Text FindTmpLabelInScene(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && text.gameObject.name.Equals(objectName, StringComparison.Ordinal))
                {
                    return text;
                }
            }

            return null;
        }

        private GameObject FindNamedObjectInScene(string objectName)
        {
            Scene scene = gameObject.scene;
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    candidate.gameObject.scene == scene &&
                    candidate.name.Equals(objectName, StringComparison.Ordinal))
                {
                    return candidate.gameObject;
                }
            }

            return null;
        }

        private GameObject ResolveMenuButtonStateTarget(string objectKey)
        {
            if (objectKey.Equals(MainMenuSafeAreaStateKey, StringComparison.Ordinal))
            {
                GameObject canvas = FindNamedObjectInScene(MainMenuCanvasName);
                if (canvas == null)
                {
                    return null;
                }

                Transform safeArea = canvas.transform.Find(MainMenuSafeAreaRootName);
                return safeArea != null ? safeArea.gameObject : null;
            }

            return FindNamedObjectInScene(objectKey);
        }

        private void ResolveButtonsIfNeeded()
        {
            if (_playButton == null)
            {
                _playButton = FindButtonUnderTmpName(TmpPlayName);
                if (_playButton == null)
                {
                    _playButton = EnsureButtonOnTmpNamedObject(TmpPlayName);
                }
            }

            if (_settingsButton == null)
            {
                _settingsButton = FindButtonUnderTmpName(TmpSettingsName);
                if (_settingsButton == null)
                {
                    _settingsButton = EnsureButtonOnTmpNamedObject(TmpSettingsName);
                }
            }
        }

        private static Button FindButtonUnderTmpName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text t = texts[i];
                if (t == null || !t.gameObject.name.Equals(objectName, StringComparison.Ordinal))
                {
                    continue;
                }

                Button b = t.GetComponentInParent<Button>();
                return b;
            }

            return null;
        }

        // When TMP labels (e.g. txt_mainmenu_play) are not wrapped in a , raycasts hit the
        // text and block world-space MainMenuNavButton_V2 colliders. Add a UI Button on the
        // same GameObject so clicks invoke HandlePlay / settings.
        private static Button EnsureButtonOnTmpNamedObject(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text t = texts[i];
                if (t == null || !t.gameObject.name.Equals(objectName, StringComparison.Ordinal))
                {
                    continue;
                }

                Button existing = t.GetComponent<Button>();
                if (existing != null)
                {
                    return existing;
                }

                Graphic graphic = t as Graphic;
                if (graphic == null)
                {
                    return null;
                }

                Button button = t.gameObject.AddComponent<Button>();
                button.targetGraphic = graphic;
                button.transition = Selectable.Transition.None;
                return button;
            }

            return null;
        }

        // For automation: starts Campaign or Survival when the menu has not already started the run.
        public bool TryHandleStartRunFromAutomation(GameRunMode_V2 mode)
        {
            if (_gameStarted)
            {
                return false;
            }

            if (mode == GameRunMode_V2.Survival)
            {
                HandlePlaySurvival();
            }
            else
            {
                HandlePlayCampaign();
            }

            return true;
        }

        // Back-compat: automation callers that only start Campaign.
        public bool TryHandlePlayFromAutomation() =>
            TryHandleStartRunFromAutomation(GameRunMode_V2.Campaign);

        // Campaign (15 waves). Back-compat alias for automation/tests.
        public void HandlePlay() => HandlePlayCampaign();

        public void HandlePlayCampaign() => StartNewRun(GameRunMode_V2.Campaign);

        public void HandlePlaySurvival() => StartNewRun(GameRunMode_V2.Survival);

        private void StartNewRun(GameRunMode_V2 mode)
        {
            if (_gameStarted)
            {
                return;
            }

            AudioManager_V2.PlayMenuClick();
            _gameStarted = true;
            GameRunModeBootstrap_V2.SetPendingNewRunMode(mode);
            RunSaveService_V2.ClearSave();

            // Unity does not reset timeScale when loading scenes; ReturnToMainMenu loads with timeScale 0.
            // Always resume simulation when leaving the menu so Play works for every boot path.
            Time.timeScale = 1f;

            if (_loadGameplaySceneOnPlay && !string.IsNullOrWhiteSpace(_gameplaySceneName))
            {
                // Async load keeps MainMenuScene visible until SampleScene is ready — sync LoadScene
                // unloads menu art first and flashes the camera clear color (blue) during the load gap.
                WaveManager_V2.MarkGameplayEnteredFromMainMenu();
                if (_loadGameplaySceneRoutine != null)
                {
                    StopCoroutine(_loadGameplaySceneRoutine);
                }

                _loadGameplaySceneRoutine = StartCoroutine(LoadGameplaySceneWhenReady());
                return;
            }

            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }

            HideMainMenuRoots();
            NotifyWaveManagerGameStartedIfPossible();
        }

        // Resume the last autosaved run (shop, preparing, in-wave restart, or life-over).
        public void HandleContinue()
        {
            if (_gameStarted || !CanContinueSavedRun)
            {
                return;
            }

            AudioManager_V2.PlayMenuClick();
            _gameStarted = true;
            Time.timeScale = 1f;

            if (_loadGameplaySceneOnPlay && !string.IsNullOrWhiteSpace(_gameplaySceneName))
            {
                WaveManager_V2.MarkLoadSavedRunPending();
                if (_loadGameplaySceneRoutine != null)
                {
                    StopCoroutine(_loadGameplaySceneRoutine);
                }

                _loadGameplaySceneRoutine = StartCoroutine(LoadGameplaySceneWhenReady());
                return;
            }

            Debug.LogWarning(
                "[MainMenu_V2] Continue requires _loadGameplaySceneOnPlay with a gameplay scene name.");
        }

        private void RefreshContinueAvailability()
        {
            bool hasSave = CanContinueSavedRun;
            SetNamedSceneObjectActive(_worldContinueButtonObjectName, hasSave);
            SetNamedSceneObjectActive("TextBTN_MediumContinue_Pressed", false);
            SetNamedSceneObjectActive(TmpContinueName, hasSave);
            SetNamedSceneObjectActive(TmpContinueAltName, hasSave);
            if (_continueButton != null)
            {
                _continueButton.gameObject.SetActive(hasSave);
            }

            RefreshMainMenuButtonLayout();
        }

        private void SetNamedSceneObjectActive(string objectName, bool active)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            GameObject target = FindNamedObjectInScene(objectName);
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private void NotifyWaveManagerGameStartedIfPossible()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>();
            }

            if (_waveManager != null)
            {
                _waveManager.NotifyGameStartedFromMainMenu();
            }
        }

        private IEnumerator LoadGameplaySceneWhenReady()
        {
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(_gameplaySceneName, LoadSceneMode.Single);
            if (loadOp == null)
            {
                SceneManager.LoadScene(_gameplaySceneName, LoadSceneMode.Single);
                _loadGameplaySceneRoutine = null;
                yield break;
            }

            loadOp.allowSceneActivation = false;
            while (loadOp.progress < 0.9f)
            {
                yield return null;
            }

            loadOp.allowSceneActivation = true;
            while (!loadOp.isDone)
            {
                yield return null;
            }

            _loadGameplaySceneRoutine = null;
        }

        // Called from UI Button or MainMenuNavButton_V2 (world Collider2D).
        public void HandleShowSettings()
        {
            if (_showSettingsRoutine != null)
            {
                return;
            }

            ResolveSettingsPanelIfNeeded();
            if (_settingsPanel == null)
            {
                if (!_loggedMissingSettingsPanel)
                {
                    _loggedMissingSettingsPanel = true;
                    Debug.Log(
                        "[MainMenu_V2] Settings: assign _settingsPanel or add a '" +
                        DefaultSettingsPanelName + "' root in the scene.");
                }

                return;
            }

            if (_settingsPanel.activeSelf)
            {
                return;
            }

            CloseHighScorePanelImmediatelyIfOpen();
            AudioManager_V2.PlayMenuClick();
            ApplyMainMenuButtonsHiddenForSettings(saveState: true);
            _showSettingsRoutine = StartCoroutine(ShowSettingsAfterDelay());
        }

        private IEnumerator ShowSettingsAfterDelay()
        {
            float delay = Mathf.Max(0f, _settingsOpenDelaySeconds);
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            _showSettingsRoutine = null;
            OpenSettingsPanelNow();
        }

        private void OpenSettingsPanelNow()
        {
            if (_settingsPanel == null || _settingsPanel.activeSelf)
            {
                return;
            }

            _settingsPanel.SetActive(true);
            HideMainMenuButtonsForSettings();
            EnsureOverlayUiCanvasesVisible(_settingsPanel);
            EnsureSettingsPanelLayout();
            EnsureUiNavButton("Settings_Btn_GoBack", MainMenuNavButton_V2.MenuAction.CloseSettings);
            AudioManager_V2.PlaySettingsSuccess();
        }

        private void EnsureSettingsPanelLayout()
        {
            if (_settingsPanel == null)
            {
                return;
            }

            // Inspector layout is authoritative when SettingsPanelLayout_V2 is disabled on Settings V2.
            SettingsPanelLayout_V2 layout = _settingsPanel.GetComponent<SettingsPanelLayout_V2>();
            if (layout == null || !layout.isActiveAndEnabled)
            {
                return;
            }

            layout.ApplyIfNeeded();
        }

        private void HideMainMenuButtonsForSettings()
        {
            ApplyMainMenuButtonsHiddenForSettings(saveState: true);
        }

        private void ApplyMainMenuButtonsHiddenForSettings(bool saveState)
        {
            if (saveState && _settingsMenuButtonsHideStateCaptured)
            {
                // HandleShowSettings hides before the panel opens; OpenSettingsPanelNow must not re-record inactive state.
                saveState = false;
            }
            else if (saveState)
            {
                _menuButtonActiveBeforeSettings.Clear();
                _settingsMenuButtonsHideStateCaptured = true;
            }

            HideMainMenuButtonChildrenUnderBackground(saveState);
            HideMainMenuUiColumnForSettings(saveState);
            HideMainMenuButtonsByConfiguredNames(saveState);
            HideMainMenuUiNavButtonsForSettings(saveState);
        }

        private void HideMainMenuButtonChildrenUnderBackground(bool saveState)
        {
            GameObject background = FindNamedObjectInScene(MainMenuBackgroundObjectName);
            if (background == null)
            {
                return;
            }

            Transform root = background.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                GameObject childObject = child.gameObject;
                if (saveState)
                {
                    RememberMenuButtonStateBeforeHide(childObject.name, childObject.activeSelf);
                }

                childObject.SetActive(false);
            }
        }

        private void HideMainMenuUiColumnForSettings(bool saveState)
        {
            GameObject canvas = FindNamedObjectInScene(MainMenuCanvasName);
            if (canvas == null)
            {
                return;
            }

            Transform safeArea = canvas.transform.Find(MainMenuSafeAreaRootName);
            if (safeArea == null)
            {
                return;
            }

            GameObject safeAreaRoot = safeArea.gameObject;
            if (saveState)
            {
                RememberMenuButtonStateBeforeHide(MainMenuSafeAreaStateKey, safeAreaRoot.activeSelf);
            }

            safeAreaRoot.SetActive(false);
        }

        private void HideMainMenuUiNavButtonsForSettings(bool saveState)
        {
            Scene scene = gameObject.scene;
            MainMenuNavUiButton_V2[] navButtons =
                FindObjectsByType<MainMenuNavUiButton_V2>(FindObjectsInactive.Include);
            for (int i = 0; i < navButtons.Length; i++)
            {
                MainMenuNavUiButton_V2 nav = navButtons[i];
                if (nav == null ||
                    nav.IsReturnToMainMenuAction() ||
                    nav.IsCloseSettingsAction() ||
                    nav.IsCloseHighScoreAction())
                {
                    continue;
                }

                GameObject target = nav.gameObject;
                if (target == null || target.scene != scene)
                {
                    continue;
                }

                if (saveState)
                {
                    RememberMenuButtonStateBeforeHide(target.name, target.activeSelf);
                }

                target.SetActive(false);
            }
        }

        private void HideMainMenuButtonsByConfiguredNames(bool saveState)
        {
            Scene scene = gameObject.scene;
            for (int i = 0; i < MenuButtonsHiddenWhileSettingsOpen.Length; i++)
            {
                string objectName = MenuButtonsHiddenWhileSettingsOpen[i];
                GameObject target = FindNamedObjectInScene(objectName);
                if (target == null || target.scene != scene)
                {
                    continue;
                }

                if (saveState)
                {
                    RememberMenuButtonStateBeforeHide(objectName, target.activeSelf);
                }

                target.SetActive(false);
            }
        }

        private void RememberMenuButtonStateBeforeHide(string objectName, bool wasActive)
        {
            if (_menuButtonActiveBeforeSettings.ContainsKey(objectName))
            {
                return;
            }

            _menuButtonActiveBeforeSettings[objectName] = wasActive;
        }

        public void HandleShowHighScore()
        {
            if (_showHighScoreRoutine != null)
            {
                return;
            }

            ResolveHighScorePanelIfNeeded();
            if (_highScorePanel == null)
            {
                if (!_loggedMissingHighScorePanel)
                {
                    _loggedMissingHighScorePanel = true;
                    Debug.Log(
                        "[MainMenu_V2] High score: assign _highScorePanel or add a '" +
                        DefaultHighScorePanelName + "' root in the scene.");
                }

                return;
            }

            if (_highScorePanel.activeSelf)
            {
                return;
            }

            CloseSettingsPanelImmediatelyIfOpen();
            AudioManager_V2.PlayMenuClick();
            ApplyMainMenuButtonsHiddenForSettings(saveState: true);
            _showHighScoreRoutine = StartCoroutine(ShowHighScoreAfterDelay());
        }

        private IEnumerator ShowHighScoreAfterDelay()
        {
            float delay = Mathf.Max(0f, _settingsOpenDelaySeconds);
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            _showHighScoreRoutine = null;
            OpenHighScorePanelNow();
        }

        private void OpenHighScorePanelNow()
        {
            if (_highScorePanel == null || _highScorePanel.activeSelf)
            {
                return;
            }

            _highScorePanel.SetActive(true);
            HideMainMenuButtonsForSettings();
            EnsureOverlayUiCanvasesVisible(_highScorePanel);
            EnsureHighScorePanelPresenter();
            EnsureUiNavButton("HighScore_Btn_GoBack", MainMenuNavButton_V2.MenuAction.CloseHighScore);
            AudioManager_V2.PlaySettingsSuccess();
        }

        private void EnsureHighScorePanelPresenter()
        {
            if (_highScorePanel == null)
            {
                return;
            }

            HighScorePanel_V2 presenter = _highScorePanel.GetComponent<HighScorePanel_V2>();
            if (presenter == null)
            {
                presenter = _highScorePanel.AddComponent<HighScorePanel_V2>();
            }

            presenter.Refresh();
        }

        public void HandleHideHighScore()
        {
            if (_hideHighScoreRoutine != null)
            {
                return;
            }

            ResolveHighScorePanelIfNeeded();
            if (_highScorePanel == null || !_highScorePanel.activeSelf)
            {
                return;
            }

            if (_showHighScoreRoutine != null)
            {
                StopCoroutine(_showHighScoreRoutine);
                _showHighScoreRoutine = null;
            }

            AudioManager_V2.PlayMenuClick();
            _hideHighScoreRoutine = StartCoroutine(HideHighScoreAfterDelay());
        }

        private IEnumerator HideHighScoreAfterDelay()
        {
            float delay = Mathf.Max(0f, _settingsOpenDelaySeconds);
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            _hideHighScoreRoutine = null;
            CloseHighScorePanelNow();
        }

        private void CloseHighScorePanelNow()
        {
            if (_highScorePanel != null && _highScorePanel.activeSelf)
            {
                _highScorePanel.SetActive(false);
            }

            RestoreMainMenuButtonsAfterSettings();
            ResetMenuNavButtonVisuals();
        }

        private void CloseSettingsPanelImmediatelyIfOpen()
        {
            if (_settingsPanel == null || !_settingsPanel.activeSelf)
            {
                return;
            }

            if (_showSettingsRoutine != null)
            {
                StopCoroutine(_showSettingsRoutine);
                _showSettingsRoutine = null;
            }

            if (_hideSettingsRoutine != null)
            {
                StopCoroutine(_hideSettingsRoutine);
                _hideSettingsRoutine = null;
            }

            CloseSettingsPanelNow();
        }

        private void CloseHighScorePanelImmediatelyIfOpen()
        {
            if (_highScorePanel == null || !_highScorePanel.activeSelf)
            {
                return;
            }

            if (_showHighScoreRoutine != null)
            {
                StopCoroutine(_showHighScoreRoutine);
                _showHighScoreRoutine = null;
            }

            if (_hideHighScoreRoutine != null)
            {
                StopCoroutine(_hideHighScoreRoutine);
                _hideHighScoreRoutine = null;
            }

            CloseHighScorePanelNow();
        }

        // Called from UI Button or MainMenuNavButton_V2 (TextBTN_MediumGoBack).
        public void HandleHideSettings()
        {
            if (_hideSettingsRoutine != null)
            {
                return;
            }

            ResolveSettingsPanelIfNeeded();
            if (_settingsPanel == null || !_settingsPanel.activeSelf)
            {
                return;
            }

            if (_showSettingsRoutine != null)
            {
                StopCoroutine(_showSettingsRoutine);
                _showSettingsRoutine = null;
            }

            AudioManager_V2.PlayMenuClick();
            _hideSettingsRoutine = StartCoroutine(HideSettingsAfterDelay());
        }

        private IEnumerator HideSettingsAfterDelay()
        {
            float delay = Mathf.Max(0f, _settingsOpenDelaySeconds);
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            _hideSettingsRoutine = null;
            CloseSettingsPanelNow();
        }

        private void CloseSettingsPanelNow()
        {
            if (_settingsPanel != null && _settingsPanel.activeSelf)
            {
                _settingsPanel.SetActive(false);
            }

            RestoreMainMenuButtonsAfterSettings();
            ResetMenuNavButtonVisuals();
        }

        private static void ResetMenuNavButtonVisuals()
        {
            MainMenuNavButton_V2[] worldNavButtons =
                FindObjectsByType<MainMenuNavButton_V2>(FindObjectsInactive.Include);
            for (int i = 0; i < worldNavButtons.Length; i++)
            {
                MainMenuNavButton_V2 nav = worldNavButtons[i];
                if (nav != null)
                {
                    nav.ResetToNormalVisual();
                }
            }

            MainMenuNavUiButton_V2[] uiNavButtons =
                FindObjectsByType<MainMenuNavUiButton_V2>(FindObjectsInactive.Include);
            for (int i = 0; i < uiNavButtons.Length; i++)
            {
                MainMenuNavUiButton_V2 nav = uiNavButtons[i];
                if (nav != null)
                {
                    nav.ResetToNormalVisual();
                }
            }
        }

        // Overlay roots copied from LifeOver may have scale 0 and no camera; fix before Game view display.
        private static void EnsureOverlayUiCanvasesVisible(GameObject overlayRoot)
        {
            if (overlayRoot == null)
            {
                return;
            }

            Canvas[] canvases = overlayRoot.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                {
                    continue;
                }

                if (canvas.transform is RectTransform rect && rect.localScale.sqrMagnitude < 0.001f)
                {
                    rect.localScale = Vector3.one;
                }

                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                {
                    canvas.worldCamera = Camera.main;
                }

                canvas.enabled = true;
            }
        }

        private void HideMainMenuRoots()
        {
            bool anyConfigured = false;
            for (int i = 0; i < _hideOnPlay.Length; i++)
            {
                GameObject go = _hideOnPlay[i];
                if (go == null)
                {
                    continue;
                }

                anyConfigured = true;
                go.SetActive(false);
            }

            // Fallback so Play still works even when inspector wiring is missing.
            if (!anyConfigured)
            {
                HideByNameFallback();
                HideNavButtonsFallback();
            }
        }

        // Call after SceneManager.LoadScene when returning from game over. If this component sits on an
        // inactive GameObject, Awake never ran — re-show menu roots and pause time here.
        public void ApplyReturnToMainMenuAfterSceneReload()
        {
            _gameStarted = false;
            gameObject.SetActive(true);
            AudioManager_V2.SetMenuMusic();

            bool anyConfigured = false;
            for (int i = 0; i < _hideOnPlay.Length; i++)
            {
                GameObject go = _hideOnPlay[i];
                if (go == null)
                {
                    continue;
                }

                anyConfigured = true;
                go.SetActive(true);
            }

            if (!anyConfigured)
            {
                ShowByNameFallback();
                ShowNavButtonsFallback();
            }

            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }

            if (_highScorePanel != null)
            {
                _highScorePanel.SetActive(false);
            }

            RestoreMainMenuButtonsAfterSettings();

            if (_pauseTimeWhileMenuOpen)
            {
                Time.timeScale = 0f;
            }

            RefreshContinueAvailability();
            RefreshSurvivalHighScoreLabel();
        }

        private static void RefreshSurvivalHighScoreLabel()
        {
            MainMenuSurvivalHighScoreLabel_V2[] labels =
                FindObjectsByType<MainMenuSurvivalHighScoreLabel_V2>(FindObjectsInactive.Include);
            for (int i = 0; i < labels.Length; i++)
            {
                MainMenuSurvivalHighScoreLabel_V2 label = labels[i];
                if (label != null)
                {
                    label.Refresh();
                }
            }
        }

        private void ShowByNameFallback()
        {
            Scene s = gameObject.scene;
            GameObject[] gos = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < gos.Length; i++)
            {
                GameObject go = gos[i];
                if (go == null || go.scene != s)
                {
                    continue;
                }

                for (int n = 0; n < DefaultMenuObjectNames.Length; n++)
                {
                    if (go.name.Equals(DefaultMenuObjectNames[n], StringComparison.Ordinal))
                    {
                        go.SetActive(true);
                        break;
                    }
                }
            }
        }

        private static void ShowNavButtonsFallback()
        {
            MainMenuNavButton_V2[] navButtons =
                FindObjectsByType<MainMenuNavButton_V2>(FindObjectsInactive.Include);
            for (int i = 0; i < navButtons.Length; i++)
            {
                MainMenuNavButton_V2 nav = navButtons[i];
                if (nav != null && !nav.IsReturnToMainMenuAction())
                {
                    nav.gameObject.SetActive(true);
                }
            }
        }

        private static void HideByNameFallback()
        {
            for (int i = 0; i < DefaultMenuObjectNames.Length; i++)
            {
                string objectName = DefaultMenuObjectNames[i];
                GameObject target = GameObject.Find(objectName);
                if (target != null)
                {
                    target.SetActive(false);
                }
            }
        }

        private void RestoreMainMenuButtonsAfterSettings()
        {
            if (_menuButtonActiveBeforeSettings.Count == 0)
            {
                RestoreDefaultMainMenuButtonsVisible();
                return;
            }

            Scene scene = gameObject.scene;
            foreach (KeyValuePair<string, bool> entry in _menuButtonActiveBeforeSettings)
            {
                GameObject target = ResolveMenuButtonStateTarget(entry.Key);
                if (target == null || target.scene != scene)
                {
                    continue;
                }

                target.SetActive(entry.Value);
            }

            _menuButtonActiveBeforeSettings.Clear();
            _settingsMenuButtonsHideStateCaptured = false;
            RestoreMainMenuUiColumnVisibleByDefault();
            RestoreMainMenuUiNavButtonsVisibleByDefault();
            RefreshMainMenuButtonLayout();
        }

        // Fallback when no cached state exists (e.g. stale hide pass overwrote active flags).
        private void RestoreDefaultMainMenuButtonsVisible()
        {
            Scene scene = gameObject.scene;
            for (int i = 0; i < MenuButtonsHiddenWhileSettingsOpen.Length; i++)
            {
                string objectName = MenuButtonsHiddenWhileSettingsOpen[i];
                GameObject target = FindNamedObjectInScene(objectName);
                if (target == null || target.scene != scene)
                {
                    continue;
                }

                bool shouldBeActive = !objectName.EndsWith("_Pressed", StringComparison.Ordinal);
                target.SetActive(shouldBeActive);
            }

            RestoreMainMenuUiColumnVisibleByDefault();
            RestoreMainMenuUiNavButtonsVisibleByDefault();
        }

        private void RestoreMainMenuUiNavButtonsVisibleByDefault()
        {
            Scene scene = gameObject.scene;
            string[] uiButtonNames =
            {
                "MainMenu_Btn_Continue",
                "MainMenu_Btn_StartGame",
                "MainMenu_Btn_StartCampaign",
                "MainMenu_Btn_StartSurvivalMode",
                "MainMenu_Btn_HighScore",
                "MainMenu_Btn_Settings",
            };

            for (int i = 0; i < uiButtonNames.Length; i++)
            {
                GameObject target = FindNamedObjectInScene(uiButtonNames[i]);
                if (target != null && target.scene == scene)
                {
                    target.SetActive(true);
                }
            }
        }

        private void RestoreMainMenuUiColumnVisibleByDefault()
        {
            GameObject canvas = FindNamedObjectInScene(MainMenuCanvasName);
            if (canvas == null)
            {
                return;
            }

            Transform safeArea = canvas.transform.Find(MainMenuSafeAreaRootName);
            if (safeArea != null)
            {
                safeArea.gameObject.SetActive(true);
            }
        }

        private static void HideNavButtonsFallback()
        {
            MainMenuNavButton_V2[] navButtons =
                UnityEngine.Object.FindObjectsByType<MainMenuNavButton_V2>(FindObjectsInactive.Include);
            for (int i = 0; i < navButtons.Length; i++)
            {
                MainMenuNavButton_V2 nav = navButtons[i];
                if (nav != null)
                {
                    nav.gameObject.SetActive(false);
                }
            }
        }
    }
}
