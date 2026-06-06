using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * SettingsGameplayControls_V2 (Settings panel gameplay UI)
     *
     * Wires MouseSensitivitySlider to GameSettings_V2.
     * Slider is resolved by object name under this root when inspector fields are empty.
     */
    public sealed class SettingsGameplayControls_V2 : MonoBehaviour
    {
        [SerializeField] private Slider _mouseSensitivitySlider;
        [SerializeField] private string _mouseSensitivitySliderObjectName = "MouseSensitivitySlider";

        private bool _suppressSliderCallbacks;

        private void OnEnable()
        {
            ResolveControlsIfNeeded();
            RegisterListeners();
            RefreshFromSavedSettings();
        }

        private void OnDisable()
        {
            UnregisterListeners();
        }

        public void RefreshFromSavedSettings()
        {
            GameSettings_V2.LoadFromPlayerPrefs();
            _suppressSliderCallbacks = true;
            SetMouseSensitivitySliderValue(GameSettings_V2.MouseSensitivity);
            _suppressSliderCallbacks = false;
        }

        private void ResolveControlsIfNeeded()
        {
            _mouseSensitivitySlider ??= FindSliderByName(_mouseSensitivitySliderObjectName);
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
            if (_mouseSensitivitySlider != null)
            {
                _mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            }
        }

        private void UnregisterListeners()
        {
            if (_mouseSensitivitySlider != null)
            {
                _mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivityChanged);
            }
        }

        private void OnMouseSensitivityChanged(float sliderValue)
        {
            if (_suppressSliderCallbacks)
            {
                return;
            }

            GameSettings_V2.SetMouseSensitivityFromSlider(sliderValue);
        }

        private void SetMouseSensitivitySliderValue(float sensitivity)
        {
            if (_mouseSensitivitySlider == null)
            {
                return;
            }

            _mouseSensitivitySlider.minValue = 0f;
            _mouseSensitivitySlider.maxValue = 1f;
            _mouseSensitivitySlider.SetValueWithoutNotify(GameSettings_V2.MouseSensitivityToSlider(sensitivity));
        }
    }
}
