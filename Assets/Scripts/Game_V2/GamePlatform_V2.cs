namespace iStick2War_V2
{
    /*
     * GamePlatform_V2 — runtime platform flags for mobile gameplay rules.
     * Set ForceMobileGameplayInEditor from MobileGameplayBootstrap_V2 to test touch in the Editor.
     */
    public static class GamePlatform_V2
    {
        public static bool ForceMobileGameplayInEditor { get; set; }
        public static bool ShowPhoneWeaponButtonsOnDesktop { get; set; }
        public static bool ShowMobileReloadUiOnDesktop { get; set; }

        public static bool UseMobileGameplayRules
        {
            get
            {
#if UNITY_EDITOR
                if (ForceMobileGameplayInEditor)
                {
                    return true;
                }
#endif
                return UnityEngine.Application.isMobilePlatform;
            }
        }

        public static bool ShouldShowPhoneWeaponArrows =>
            UseMobileGameplayRules || ShowPhoneWeaponButtonsOnDesktop;

        public static bool ShouldUseMobileReloadUi =>
            UseMobileGameplayRules || ShowMobileReloadUiOnDesktop;
    }
}
