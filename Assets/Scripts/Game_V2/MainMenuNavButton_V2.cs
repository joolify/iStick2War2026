using UnityEngine;
using UnityEngine.SceneManagement;

namespace iStick2War_V2
{
    /*
 * MainMenuNavButton_V2 (World-space menu hit target)
 *
 * PURPOSE:
 * Collider2D + OnMouseDown handler for main menu actions (Play, Settings, ReturnToMainMenu) when UI Buttons cannot
 * raycast SpriteRenderer-based art. Delegates to MainMenu_V2 when assigned.
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
        private const string MainMenuSceneName = "MainMenuScene";

        public enum MenuAction
        {
            Play,
            Settings,
            // Load dedicated main menu scene.
            ReturnToMainMenu
        }

        [SerializeField] private MainMenu_V2 _mainMenu;
        [SerializeField] private MenuAction _action = MenuAction.Play;
        [SerializeField] private bool _debugLogs;

        internal bool IsReturnToMainMenuAction() => _action == MenuAction.ReturnToMainMenu;
        internal bool IsPlayAction() => _action == MenuAction.Play;

        // Automation helper for tests/agents.
        public void TriggerAutomationClick()
        {
            OnMouseDown();
        }

        private void OnMouseDown()
        {
            AudioManager_V2.PlayMenuClick();
            WaveManager_V2 waveManager = Object.FindAnyObjectByType<WaveManager_V2>();
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

            if (_action == MenuAction.ReturnToMainMenu)
            {
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

            if (_action == MenuAction.Play)
            {
                _mainMenu.HandlePlay();
            }
            else
            {
                _mainMenu.HandleSettingsToggle();
            }
        }

        // Freeze time before scene load so boot state always starts paused in menu.
        private static void LoadMainMenuScene()
        {
            Time.timeScale = 0f;
            SceneManager.sceneLoaded -= FinishReturnToMainMenuAfterSceneLoad;
            SceneManager.sceneLoaded += FinishReturnToMainMenuAfterSceneLoad;
            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        }

        // may be on an inactive GameObject (so Awake never runs on load). Restore menu visibility
        // and pause after the new scene instance exists.
        private static void FinishReturnToMainMenuAfterSceneLoad(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= FinishReturnToMainMenuAfterSceneLoad;
            MainMenu_V2[] menus = FindObjectsByType<MainMenu_V2>(FindObjectsInactive.Include);
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
