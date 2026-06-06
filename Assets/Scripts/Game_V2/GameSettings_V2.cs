using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * GameSettings_V2 (Persisted player preferences)
     *
     * Stores audio, display, and gameplay toggles in PlayerPrefs and applies them at boot / on change.
     */
    public static class GameSettings_V2
    {
        public const string MasterVolumeKey = "iStick2War_V2.MasterVolume";
        public const string MusicVolumeKey = "iStick2War_V2.MusicVolume";
        public const string SfxVolumeKey = "iStick2War_V2.SfxVolume";
        public const string ScreenShakeEnabledKey = "iStick2War_V2.ScreenShakeEnabled";
        public const string FullScreenEnabledKey = "iStick2War_V2.FullScreenEnabled";
        public const string VSyncEnabledKey = "iStick2War_V2.VSyncEnabled";
        public const string FpsLimitIndexKey = "iStick2War_V2.FpsLimitIndex";
        public const string ResolutionWidthKey = "iStick2War_V2.ResolutionWidth";
        public const string ResolutionHeightKey = "iStick2War_V2.ResolutionHeight";
        public const string MouseSensitivityKey = "iStick2War_V2.MouseSensitivity";

        public const float DefaultMasterVolume = 1f;
        public const float DefaultMusicVolume = 0.55f;
        public const float DefaultSfxVolume = 0.9f;
        public const bool DefaultScreenShakeEnabled = true;
        public const bool DefaultFullScreenEnabled = true;
        public const bool DefaultVSyncEnabled = true;
        public const int DefaultFpsLimitIndex = 2;
        public const float DefaultMouseSensitivity = 1f;
        public const float MinMouseSensitivity = 0.25f;
        public const float MaxMouseSensitivity = 2f;

        // 0 = unlimited. Index stored in PlayerPrefs (see DefaultFpsLimitIndex).
        public static readonly int[] FpsLimitOptions = { 0, 30, 60, 120 };

        public readonly struct ResolutionOption_V2
        {
            public readonly int Width;
            public readonly int Height;

            public ResolutionOption_V2(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public string Label => $"{Width} x {Height}";
        }

        public static float MasterVolume { get; private set; } = DefaultMasterVolume;
        public static float MusicVolume { get; private set; } = DefaultMusicVolume;
        public static float SfxVolume { get; private set; } = DefaultSfxVolume;
        public static bool ScreenShakeEnabled { get; private set; } = DefaultScreenShakeEnabled;
        public static bool FullScreenEnabled { get; private set; } = DefaultFullScreenEnabled;
        public static bool VSyncEnabled { get; private set; } = DefaultVSyncEnabled;
        public static int FpsLimitIndex { get; private set; } = DefaultFpsLimitIndex;
        public static int ResolutionWidth { get; private set; }
        public static int ResolutionHeight { get; private set; }
        public static float MouseSensitivity { get; private set; } = DefaultMouseSensitivity;

        private static List<ResolutionOption_V2> s_resolutionOptions;

        public static int CurrentFpsLimit => ResolveFpsLimit(FpsLimitIndex);

        public static void LoadFromPlayerPrefs()
        {
            MasterVolume = Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));
            MusicVolume = Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
            SfxVolume = Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
            ScreenShakeEnabled = PlayerPrefs.GetInt(ScreenShakeEnabledKey, BoolToInt(DefaultScreenShakeEnabled)) == 1;
            FullScreenEnabled = PlayerPrefs.GetInt(FullScreenEnabledKey, BoolToInt(DefaultFullScreenEnabled)) == 1;
            VSyncEnabled = PlayerPrefs.GetInt(VSyncEnabledKey, BoolToInt(DefaultVSyncEnabled)) == 1;
            FpsLimitIndex = ClampFpsLimitIndex(PlayerPrefs.GetInt(FpsLimitIndexKey, DefaultFpsLimitIndex));
            LoadResolutionFromPlayerPrefs();
            MouseSensitivity = ClampMouseSensitivity(
                PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity));
        }

        public static void LoadAndApplyAll()
        {
            LoadFromPlayerPrefs();
            ApplyAll();
        }

        public static void ApplyAll()
        {
            ApplyToAudioManager();
            ApplyDisplaySettings();
        }

        public static void SetMasterVolume(float value, bool persist = true)
        {
            MasterVolume = Clamp01(value);
            if (persist)
            {
                PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
                PlayerPrefs.Save();
            }

            ApplyToAudioManager();
        }

        public static void SetMusicVolume(float value, bool persist = true)
        {
            MusicVolume = Clamp01(value);
            if (persist)
            {
                PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
                PlayerPrefs.Save();
            }

            ApplyToAudioManager();
        }

        public static void SetSfxVolume(float value, bool persist = true)
        {
            SfxVolume = Clamp01(value);
            if (persist)
            {
                PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
                PlayerPrefs.Save();
            }

            ApplyToAudioManager();
        }

        public static void SetScreenShakeEnabled(bool enabled, bool persist = true)
        {
            ScreenShakeEnabled = enabled;
            if (persist)
            {
                PlayerPrefs.SetInt(ScreenShakeEnabledKey, BoolToInt(enabled));
                PlayerPrefs.Save();
            }
        }

        public static void SetFullScreenEnabled(bool enabled, bool persist = true)
        {
            FullScreenEnabled = enabled;
            if (persist)
            {
                PlayerPrefs.SetInt(FullScreenEnabledKey, BoolToInt(enabled));
                PlayerPrefs.Save();
            }

            ApplyDisplaySettings();
        }

        public static void SetVSyncEnabled(bool enabled, bool persist = true)
        {
            VSyncEnabled = enabled;
            if (persist)
            {
                PlayerPrefs.SetInt(VSyncEnabledKey, BoolToInt(enabled));
                PlayerPrefs.Save();
            }

            ApplyDisplaySettings();
        }

        public static void SetFpsLimitIndex(int index, bool persist = true)
        {
            FpsLimitIndex = ClampFpsLimitIndex(index);
            if (persist)
            {
                PlayerPrefs.SetInt(FpsLimitIndexKey, FpsLimitIndex);
                PlayerPrefs.Save();
            }

            ApplyDisplaySettings();
        }

        public static void SetResolutionIndex(int index, bool persist = true)
        {
            RefreshResolutionOptions();
            if (s_resolutionOptions == null || s_resolutionOptions.Count == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, s_resolutionOptions.Count - 1);
            ResolutionOption_V2 option = s_resolutionOptions[index];
            ResolutionWidth = option.Width;
            ResolutionHeight = option.Height;
            if (persist)
            {
                PlayerPrefs.SetInt(ResolutionWidthKey, ResolutionWidth);
                PlayerPrefs.SetInt(ResolutionHeightKey, ResolutionHeight);
                PlayerPrefs.Save();
            }

            ApplyDisplaySettings();
        }

        public static void SetMouseSensitivity(float value, bool persist = true)
        {
            MouseSensitivity = ClampMouseSensitivity(value);
            if (persist)
            {
                PlayerPrefs.SetFloat(MouseSensitivityKey, MouseSensitivity);
                PlayerPrefs.Save();
            }
        }

        public static void SetMouseSensitivityFromSlider(float slider01, bool persist = true)
        {
            SetMouseSensitivity(SliderToMouseSensitivity(slider01), persist);
        }

        public static float MouseSensitivityToSlider(float sensitivity)
        {
            sensitivity = ClampMouseSensitivity(sensitivity);
            return Mathf.InverseLerp(MinMouseSensitivity, MaxMouseSensitivity, sensitivity);
        }

        public static float SliderToMouseSensitivity(float slider01)
        {
            return Mathf.Lerp(MinMouseSensitivity, MaxMouseSensitivity, Clamp01(slider01));
        }

        public static IReadOnlyList<ResolutionOption_V2> GetResolutionOptions()
        {
            RefreshResolutionOptions();
            return s_resolutionOptions;
        }

        public static int FindResolutionIndex(int width, int height)
        {
            RefreshResolutionOptions();
            if (s_resolutionOptions == null)
            {
                return 0;
            }

            for (int i = 0; i < s_resolutionOptions.Count; i++)
            {
                ResolutionOption_V2 option = s_resolutionOptions[i];
                if (option.Width == width && option.Height == height)
                {
                    return i;
                }
            }

            return 0;
        }

        public static void RefreshResolutionOptions()
        {
            s_resolutionOptions ??= new List<ResolutionOption_V2>();
            s_resolutionOptions.Clear();

            HashSet<long> seen = new HashSet<long>();
            Resolution[] resolutions = Screen.resolutions;
            for (int i = 0; i < resolutions.Length; i++)
            {
                int width = resolutions[i].width;
                int height = resolutions[i].height;
                long key = ((long)width << 32) | (uint)height;
                if (!seen.Add(key))
                {
                    continue;
                }

                s_resolutionOptions.Add(new ResolutionOption_V2(width, height));
            }

            s_resolutionOptions.Sort(CompareResolutionOptionsDescending);

            if (ResolutionWidth > 0
                && ResolutionHeight > 0
                && !ResolutionMatchesOption(s_resolutionOptions, ResolutionWidth, ResolutionHeight))
            {
                s_resolutionOptions.Add(new ResolutionOption_V2(ResolutionWidth, ResolutionHeight));
                s_resolutionOptions.Sort(CompareResolutionOptionsDescending);
            }
        }

        public static void ApplyToAudioManager()
        {
            AudioManager_V2 audio = AudioManager_V2.EnsureInstance();
            audio.ApplyVolumeSettings(MasterVolume, MusicVolume, SfxVolume);
        }

        public static void ApplyDisplaySettings()
        {
            FullScreenMode mode = FullScreenEnabled
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;

            if (ResolutionWidth > 0 && ResolutionHeight > 0)
            {
                Screen.SetResolution(ResolutionWidth, ResolutionHeight, mode);
            }
            else
            {
                Screen.fullScreenMode = mode;
            }

            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
            Application.targetFrameRate = CurrentFpsLimit;
        }

        public static int FindFpsLimitIndex(int fpsLimit)
        {
            for (int i = 0; i < FpsLimitOptions.Length; i++)
            {
                if (FpsLimitOptions[i] == fpsLimit)
                {
                    return i;
                }
            }

            return DefaultFpsLimitIndex;
        }

        public static string FormatFpsLimitLabel(int fpsLimit)
        {
            return fpsLimit <= 0 ? "Unlimited" : fpsLimit.ToString();
        }

        private static int ResolveFpsLimit(int index)
        {
            index = ClampFpsLimitIndex(index);
            return FpsLimitOptions[index];
        }

        private static int ClampFpsLimitIndex(int index)
        {
            if (FpsLimitOptions.Length == 0)
            {
                return 0;
            }

            return Mathf.Clamp(index, 0, FpsLimitOptions.Length - 1);
        }

        private static float Clamp01(float value)
        {
            return Mathf.Clamp01(value);
        }

        private static int BoolToInt(bool value)
        {
            return value ? 1 : 0;
        }

        private static void LoadResolutionFromPlayerPrefs()
        {
            if (PlayerPrefs.HasKey(ResolutionWidthKey) && PlayerPrefs.HasKey(ResolutionHeightKey))
            {
                ResolutionWidth = PlayerPrefs.GetInt(ResolutionWidthKey);
                ResolutionHeight = PlayerPrefs.GetInt(ResolutionHeightKey);
                return;
            }

            Resolution current = Screen.currentResolution;
            ResolutionWidth = current.width;
            ResolutionHeight = current.height;
        }

        private static float ClampMouseSensitivity(float value)
        {
            return Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);
        }

        private static int CompareResolutionOptionsDescending(
            ResolutionOption_V2 left,
            ResolutionOption_V2 right)
        {
            int leftPixels = left.Width * left.Height;
            int rightPixels = right.Width * right.Height;
            int pixelCompare = rightPixels.CompareTo(leftPixels);
            if (pixelCompare != 0)
            {
                return pixelCompare;
            }

            int widthCompare = right.Width.CompareTo(left.Width);
            return widthCompare != 0 ? widthCompare : right.Height.CompareTo(left.Height);
        }

        private static bool ResolutionMatchesOption(
            List<ResolutionOption_V2> options,
            int width,
            int height)
        {
            if (options == null)
            {
                return false;
            }

            for (int i = 0; i < options.Count; i++)
            {
                ResolutionOption_V2 option = options[i];
                if (option.Width == width && option.Height == height)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
