using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace iStick2War_V2
{
    /// <summary>
    /// Shows at boot: pauses gameplay with Time.timeScale until Play.
    /// Use UI Button and/or world-space <see cref="MainMenuNavButton_V2"/> (Collider2D + OnMouseDown, same idea as ShopNavArrow_V2).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class MainMenu_V2 : MonoBehaviour
    {
        private const string TmpPlayName = "txt_mainmenu_play";
        private const string TmpSettingsName = "txt_mainmenu_settings";
        private static readonly string[] DefaultMenuObjectNames =
        {
            "bkg_main_menu",
            "MainMenu-canvas",
            "btn_main_menu_play",
            "btn_main_menu_settings",
            TmpPlayName,
            TmpSettingsName
        };

        [Header("Roots (optional)")]
        [Tooltip("Hidden when Play is pressed, e.g. bkg_main_menu and/or MainMenu-canvas.")]
        [SerializeField] private GameObject[] _hideOnPlay = Array.Empty<GameObject>();

        [Header("Buttons (optional if TMP names exist)")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _settingsButton;

        [Header("Settings")]
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private bool _pauseTimeWhileMenuOpen = true;

        [Header("Gameplay")]
        [Tooltip("Optional; if unset, resolved once when Play is pressed.")]
        [SerializeField] private WaveManager_V2 _waveManager;

        private bool _gameStarted;
        private bool _loggedMissingSettingsPanel;

        private void Awake()
        {
            if (_pauseTimeWhileMenuOpen)
            {
                Time.timeScale = 0f;
            }
        }

        private void Start()
        {
            ResolveButtonsIfNeeded();
            if (_playButton != null)
            {
                _playButton.onClick.AddListener(HandlePlay);
            }

            if (_settingsButton != null)
            {
                _settingsButton.onClick.AddListener(HandleSettingsToggle);
            }
        }

        private void OnDestroy()
        {
            if (_playButton != null)
            {
                _playButton.onClick.RemoveListener(HandlePlay);
            }

            if (_settingsButton != null)
            {
                _settingsButton.onClick.RemoveListener(HandleSettingsToggle);
            }
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

        /// <summary>
        /// When TMP labels (e.g. <c>txt_mainmenu_play</c>) are not wrapped in a <see cref="Button"/>, raycasts hit the
        /// text and block world-space <see cref="MainMenuNavButton_V2"/> colliders. Add a UI <see cref="Button"/> on the
        /// same GameObject so clicks invoke <see cref="HandlePlay"/> / settings.
        /// </summary>
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

        /// <summary>Called from UI Button or <see cref="MainMenuNavButton_V2"/> (world Collider2D).</summary>
        public void HandlePlay()
        {
            if (_gameStarted)
            {
                return;
            }

            _gameStarted = true;

            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }

            HideMainMenuRoots();

            // Unity does not reset timeScale when loading scenes; ReturnToMainMenu loads with timeScale 0.
            // Always resume simulation when leaving the menu so Play works for every boot path.
            Time.timeScale = 1f;

            NotifyWaveManagerGameStartedIfPossible();
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

        /// <summary>Called from UI Button or <see cref="MainMenuNavButton_V2"/> (world Collider2D).</summary>
        public void HandleSettingsToggle()
        {
            if (_settingsPanel == null)
            {
                if (!_loggedMissingSettingsPanel)
                {
                    _loggedMissingSettingsPanel = true;
                    Debug.Log(
                        "[MainMenu_V2] Settings: assign _settingsPanel in the inspector when you have a settings UI.");
                }

                return;
            }

            _settingsPanel.SetActive(!_settingsPanel.activeSelf);
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

        /// <summary>
        /// Call after <see cref="SceneManager.LoadScene"/> when returning from game over. If this component sits on an
        /// inactive GameObject, <see cref="Awake"/> never ran — re-show menu roots and pause time here.
        /// </summary>
        public void ApplyReturnToMainMenuAfterSceneReload()
        {
            _gameStarted = false;
            gameObject.SetActive(true);

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

            if (_pauseTimeWhileMenuOpen)
            {
                Time.timeScale = 0f;
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
