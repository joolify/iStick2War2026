using UnityEngine;

namespace iStick2War_V2
{
    /*
     * MobileLandscapeOrientation_V2 — lock Android builds to landscape (sidescroller / bunker layout).
     * Runs before the first scene loads so MainMenu and gameplay both start horizontal.
     */
    public static class MobileLandscapeOrientation_V2
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyLandscapeOnAndroid()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;

            ScreenOrientation orientation = Screen.orientation;
            if (orientation == ScreenOrientation.Portrait ||
                orientation == ScreenOrientation.PortraitUpsideDown ||
                orientation == ScreenOrientation.AutoRotation)
            {
                Screen.orientation = ScreenOrientation.LandscapeLeft;
            }
#endif
        }
    }
}
