using UnityEngine;
using UnityEngine.SceneManagement;

namespace iStick2War_V2
{
    /// <summary>
    /// World-space main menu hits: Collider2D + OnMouseDown (same pattern as ShopNavArrow_V2; UI Button does not raycast sprites).
    /// </summary>
    [AddComponentMenu("iStick2War/Main Menu Nav Button V2")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class MainMenuNavButton_V2 : MonoBehaviour
    {
        public enum MenuAction
        {
            Play,
            Settings,
            /// <summary>
            /// Reloads the active scene (single-scene build: full run reset). Time is frozen before load so the main menu
            /// appears paused; press Play to resume. Does not require <see cref="_mainMenu"/>.
            /// </summary>
            ReturnToMainMenu
        }

        [SerializeField] private MainMenu_V2 _mainMenu;
        [SerializeField] private MenuAction _action = MenuAction.Play;
        [SerializeField] private bool _debugLogs;

        internal bool IsReturnToMainMenuAction() => _action == MenuAction.ReturnToMainMenu;
        internal bool IsPlayAction() => _action == MenuAction.Play;

        /// <summary>Automation helper for tests/agents.</summary>
        public void TriggerAutomationClick()
        {
            OnMouseDown();
        }

        private void OnMouseDown()
        {
            if (_action == MenuAction.ReturnToMainMenu)
            {
                if (_debugLogs)
                {
                    Debug.Log($"[MainMenuNavButton_V2] '{name}' OnMouseDown -> {_action} (reload active scene, pause first)");
                }

                ReloadActiveSceneToMainMenu();
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

            if (_action == MenuAction.Play)
            {
                _mainMenu.HandlePlay();
            }
            else
            {
                _mainMenu.HandleSettingsToggle();
            }
        }

        /// <summary>
        /// Build only has one scene (game + main menu in SampleScene). Reload resets the run, but Unity keeps
        /// <see cref="Time.timeScale"/> across <see cref="SceneManager.LoadScene"/> — if it stays 1 after game over,
        /// <see cref="WaveManager_V2"/> leaves Preparing immediately and waves start without showing the menu.
        /// Freeze time before load so boot matches a fresh editor play: menu first, then Play resumes time.
        /// </summary>
        private static void ReloadActiveSceneToMainMenu()
        {
            Time.timeScale = 0f;
            SceneManager.sceneLoaded -= FinishReturnToMainMenuAfterSceneLoad;
            SceneManager.sceneLoaded += FinishReturnToMainMenuAfterSceneLoad;
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid())
            {
                SceneManager.LoadScene(active.buildIndex, LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadScene(0, LoadSceneMode.Single);
            }
        }

        /// <summary>
        /// <see cref="MainMenu_V2"/> may be on an inactive GameObject (so Awake never runs on load). Restore menu visibility
        /// and pause after the new scene instance exists.
        /// </summary>
        private static void FinishReturnToMainMenuAfterSceneLoad(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= FinishReturnToMainMenuAfterSceneLoad;
            MainMenu_V2[] menus = FindObjectsByType<MainMenu_V2>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < menus.Length; i++)
            {
                if (menus[i] != null)
                {
                    menus[i].ApplyReturnToMainMenuAfterSceneReload();
                    break;
                }
            }
        }
    }
}
