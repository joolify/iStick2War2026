using UnityEngine;
using UnityEngine.EventSystems;

namespace iStick2War_V2
{
    /*
     * MobileGameplayBootstrap_V2 - enables mobile touch rules, touch input host, weapon arrows, and reload canvas.
     * Ensures EventSystem uses StandaloneInputModule when the project runs on legacy Input Manager (Android build).
     * Optional Force Mobile In Editor for desktop playtests with touch emulation (mouse hold = shoot/aim).
     */
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-450)]
    public sealed class MobileGameplayBootstrap_V2 : MonoBehaviour
    {
        [SerializeField] private bool _forceMobileInEditor;
        [SerializeField] private bool _showWeaponArrowsOnDesktop = true;
        [SerializeField] private bool _showReloadUiOnDesktop = true;
        [SerializeField] private bool _ensureEventSystem = true;

        private MobileReloadCanvas_V2 _reloadCanvas;

        private void Awake()
        {
            if (_forceMobileInEditor)
            {
                GamePlatform_V2.ForceMobileGameplayInEditor = true;
            }

            if (_showWeaponArrowsOnDesktop)
            {
                GamePlatform_V2.ShowPhoneWeaponButtonsOnDesktop = true;
            }

            if (_showReloadUiOnDesktop)
            {
                GamePlatform_V2.ShowMobileReloadUiOnDesktop = true;
            }

            if (_ensureEventSystem)
            {
                EnsureEventSystemExists();
            }

            bool needsTouchInputHost =
                GamePlatform_V2.UseMobileGameplayRules ||
                GamePlatform_V2.ShouldShowPhoneWeaponArrows ||
                GamePlatform_V2.ShouldUseMobileReloadUi;
            if (needsTouchInputHost)
            {
                MobileGameplayTouchInput_V2.EnsureInstance();
            }

            if (GamePlatform_V2.ShouldShowPhoneWeaponArrows)
            {
                PhoneWeaponArrowsCanvasLayout_V2.EnsureFromScene();
            }

            _reloadCanvas = MobileReloadCanvas_V2.EnsureFromScene();
        }

        private void LateUpdate()
        {
            _reloadCanvas?.RefreshVisibility();
        }

        private static void EnsureEventSystemExists()
        {
            EventSystem eventSystem = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                eventSystemGo.hideFlags = HideFlags.None;
                return;
            }

            LegacyUiEventSystemUtility_V2.EnsureLegacyUiInputModule(eventSystem.gameObject);
        }
    }
}
