using UnityEngine;

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
            Settings
        }

        [SerializeField] private MainMenu_V2 _mainMenu;
        [SerializeField] private MenuAction _action = MenuAction.Play;
        [SerializeField] private bool _debugLogs;

        private void OnMouseDown()
        {
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
    }
}
