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
 * MainMenuNavButton_V2 colliders (same Collider2D + OnMouseDown pattern as ShopNavArrow_V2), and hides configured
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
        private const string DefaultSettingsBackButtonName = "TextBTN_MediumGoBack";
        private const string MainMenuBackgroundObjectName = "bkg_main_menu";
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
            TmpContinueAltName
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
        [SerializeField] private bool _pauseTimeWhileMenuOpen = true;
        [Tooltip("Real-time pause before Settings V2 opens/closes so pressed TextBTN states stay visible.")]
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
        private readonly Dictionary<string, bool> _menuButtonActiveBeforeSettings = new Dictionary<string, bool>();

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
        }

        private void Start()
        {
            ResolveReferencesIfNeeded();
            PrepareSettingsPanelForMenuBoot();
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
        }

        private void LateUpdate()
        {
            if (_gameStarted || _settingsPanel == null || !_settingsPanel.activeSelf)
            {
                return;
            }

            // Keep menu TextBTN hidden while settings stay open (pressed siblings can re-enable same frame).
            ApplyMainMenuButtonsHiddenForSettings(saveState: false);
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
            WireWorldNavButtonsIfNeeded();
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

            RestoreMainMenuButtonsAfterSettings();
        }

        private void WireWorldNavButtonsIfNeeded()
        {
            EnsureWorldNavButton(_worldPlayButtonObjectName, MainMenuNavButton_V2.MenuAction.Play);
            EnsureWorldNavButton(_worldContinueButtonObjectName, MainMenuNavButton_V2.MenuAction.Continue);
            EnsureWorldNavButton(_worldSettingsButtonObjectName, MainMenuNavButton_V2.MenuAction.Settings);
            EnsureWorldNavButton(DefaultSettingsBackButtonName, MainMenuNavButton_V2.MenuAction.CloseSettings);
            // Legacy sprite names from early main-menu prefabs.
            EnsureWorldNavButton("btn_main_menu_play", MainMenuNavButton_V2.MenuAction.Play);
            EnsureWorldNavButton("btn_main_menu_settings", MainMenuNavButton_V2.MenuAction.Settings);
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
        }

        private GameObject FindNamedObjectInScene(string objectName)
        {
            Scene scene = gameObject.scene;
            GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate != null &&
                    candidate.scene == scene &&
                    candidate.name.Equals(objectName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
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

        // For automation (): runs the same path as Play if the menu has not already started the run.
        // False if Play was already committed for this menu session (no-op).
        public bool TryHandlePlayFromAutomation()
        {
            if (_gameStarted)
            {
                return false;
            }

            HandlePlay();
            return true;
        }

        // Called from UI Button or MainMenuNavButton_V2 (world Collider2D).
        public void HandlePlay()
        {
            if (_gameStarted)
            {
                return;
            }

            AudioManager_V2.PlayMenuClick();
            _gameStarted = true;
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

            AudioManager_V2.PlayMenuClick();
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
            EnsureSettingsUiCanvasesVisible();
            AudioManager_V2.PlaySettingsSuccess();
        }

        private void HideMainMenuButtonsForSettings()
        {
            ApplyMainMenuButtonsHiddenForSettings(saveState: true);
        }

        private void ApplyMainMenuButtonsHiddenForSettings(bool saveState)
        {
            if (saveState)
            {
                _menuButtonActiveBeforeSettings.Clear();
            }

            HideMainMenuButtonChildrenUnderBackground(saveState);
            HideMainMenuButtonsByConfiguredNames(saveState);
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
        }

        // LifeOver-canvas under Settings V2 was copied with scale 0 and no camera; UI shows in Scene view but not Game view.
        private void EnsureSettingsUiCanvasesVisible()
        {
            if (_settingsPanel == null)
            {
                return;
            }

            Canvas[] canvases = _settingsPanel.GetComponentsInChildren<Canvas>(true);
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

            RestoreMainMenuButtonsAfterSettings();

            if (_pauseTimeWhileMenuOpen)
            {
                Time.timeScale = 0f;
            }

            RefreshContinueAvailability();
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
                GameObject target = FindNamedObjectInScene(entry.Key);
                if (target == null || target.scene != scene)
                {
                    continue;
                }

                target.SetActive(entry.Value);
            }

            _menuButtonActiveBeforeSettings.Clear();
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
