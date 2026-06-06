using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * SettingsVolumeSliders_V2 (Settings panel volume UI)
     *
     * Wires MasterVolumeSlider, MusicSlider, and SFXSlider to GameSettings_V2 + AudioManager_V2.
     * Sliders are resolved by object name under this root when inspector fields are empty.
     */
    public sealed class SettingsVolumeSliders_V2 : MonoBehaviour
    {
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;

        [SerializeField] private string _masterSliderObjectName = "MasterVolumeSlider";
        [SerializeField] private string _musicSliderObjectName = "MusicSlider";
        [SerializeField] private string _sfxSliderObjectName = "SFXSlider";

        private bool _suppressSliderCallbacks;

        private void OnEnable()
        {
            ResolveSlidersIfNeeded();
            RegisterListeners();
            RefreshSliderValuesFromSettings();
        }

        private void OnDisable()
        {
            UnregisterListeners();
        }

        public void RefreshSliderValuesFromSettings()
        {
            GameSettings_V2.LoadFromPlayerPrefs();
            _suppressSliderCallbacks = true;
            SetSliderValue(_masterVolumeSlider, GameSettings_V2.MasterVolume);
            SetSliderValue(_musicSlider, GameSettings_V2.MusicVolume);
            SetSliderValue(_sfxSlider, GameSettings_V2.SfxVolume);
            _suppressSliderCallbacks = false;
            GameSettings_V2.ApplyToAudioManager();
        }

        private void ResolveSlidersIfNeeded()
        {
            _masterVolumeSlider ??= FindSliderByName(_masterSliderObjectName);
            _musicSlider ??= FindSliderByName(_musicSliderObjectName);
            _sfxSlider ??= FindSliderByName(_sfxSliderObjectName);
        }

        private Slider FindSliderByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Slider[] sliders = GetComponentsInChildren<Slider>(true);
            for (int i = 0; i < sliders.Length; i++)
            {
                Slider slider = sliders[i];
                if (slider != null && slider.gameObject.name == objectName)
                {
                    return slider;
                }
            }

            return null;
        }

        private void RegisterListeners()
        {
            if (_masterVolumeSlider != null)
            {
                _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }

            if (_musicSlider != null)
            {
                _musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }
        }

        private void UnregisterListeners()
        {
            if (_masterVolumeSlider != null)
            {
                _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            }

            if (_musicSlider != null)
            {
                _musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            }
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (_suppressSliderCallbacks)
            {
                return;
            }

            GameSettings_V2.SetMasterVolume(value);
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (_suppressSliderCallbacks)
            {
                return;
            }

            GameSettings_V2.SetMusicVolume(value);
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (_suppressSliderCallbacks)
            {
                return;
            }

            GameSettings_V2.SetSfxVolume(value);
        }

        private static void SetSliderValue(Slider slider, float value)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
        }
    }
}
