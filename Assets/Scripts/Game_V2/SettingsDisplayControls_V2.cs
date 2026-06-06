using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * SettingsDisplayControls_V2 (Settings panel display / gameplay toggles)
     *
     * Wires ScreenShakeToggle, FullScreenToggle, VSyncToggle, FPSDropdown, and ResolutionDropdown to GameSettings_V2.
     * Controls are resolved by object name under this root when inspector fields are empty.
     */
    public sealed class SettingsDisplayControls_V2 : MonoBehaviour
    {
        [SerializeField] private Toggle _screenShakeToggle;
        [SerializeField] private Toggle _fullScreenToggle;
        [SerializeField] private Toggle _vSyncToggle;
        [SerializeField] private TMP_Dropdown _fpsDropdown;
        [SerializeField] private Dropdown _fpsDropdownLegacy;
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private Dropdown _resolutionDropdownLegacy;

        [SerializeField] private string _screenShakeToggleObjectName = "ScreenShakeToggle";
        [SerializeField] private string _fullScreenToggleObjectName = "FullScreenToggle";
        [SerializeField] private string _vSyncToggleObjectName = "VSyncToggle";
        [SerializeField] private string _fpsDropdownObjectName = "FPSDropdown";
        [SerializeField] private string _resolutionDropdownObjectName = "ResolutionDropdown";

        private bool _suppressCallbacks;

        private void OnEnable()
        {
            ResolveControlsIfNeeded();
            EnsureFpsDropdownOptions();
            EnsureResolutionDropdownOptions();
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
            _suppressCallbacks = true;

            SetToggleValue(_screenShakeToggle, GameSettings_V2.ScreenShakeEnabled);
            SetToggleValue(_fullScreenToggle, GameSettings_V2.FullScreenEnabled);
            SetToggleValue(_vSyncToggle, GameSettings_V2.VSyncEnabled);
            SetFpsDropdownIndex(GameSettings_V2.FpsLimitIndex);
            SetResolutionDropdownIndex(GameSettings_V2.FindResolutionIndex(
                GameSettings_V2.ResolutionWidth,
                GameSettings_V2.ResolutionHeight));

            _suppressCallbacks = false;
            GameSettings_V2.ApplyDisplaySettings();
        }

        private void ResolveControlsIfNeeded()
        {
            _screenShakeToggle ??= FindToggleByName(_screenShakeToggleObjectName);
            _fullScreenToggle ??= FindToggleByName(_fullScreenToggleObjectName);
            _vSyncToggle ??= FindToggleByName(_vSyncToggleObjectName);

            if (_fpsDropdown == null && _fpsDropdownLegacy == null)
            {
                _fpsDropdown = FindTmpDropdownByName(_fpsDropdownObjectName);
                if (_fpsDropdown == null)
                {
                    _fpsDropdownLegacy = FindLegacyDropdownByName(_fpsDropdownObjectName);
                }
            }

            if (_resolutionDropdown == null && _resolutionDropdownLegacy == null)
            {
                _resolutionDropdown = FindTmpDropdownByName(_resolutionDropdownObjectName);
                if (_resolutionDropdown == null)
                {
                    _resolutionDropdownLegacy = FindLegacyDropdownByName(_resolutionDropdownObjectName);
                }
            }
        }

        private Toggle FindToggleByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                Toggle toggle = toggles[i];
                if (toggle != null && toggle.gameObject.name == objectName)
                {
                    return toggle;
                }
            }

            return null;
        }

        private TMP_Dropdown FindTmpDropdownByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TMP_Dropdown[] dropdowns = GetComponentsInChildren<TMP_Dropdown>(true);
            for (int i = 0; i < dropdowns.Length; i++)
            {
                TMP_Dropdown dropdown = dropdowns[i];
                if (dropdown != null && dropdown.gameObject.name == objectName)
                {
                    return dropdown;
                }
            }

            return null;
        }

        private Dropdown FindLegacyDropdownByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Dropdown[] dropdowns = GetComponentsInChildren<Dropdown>(true);
            for (int i = 0; i < dropdowns.Length; i++)
            {
                Dropdown dropdown = dropdowns[i];
                if (dropdown != null && dropdown.gameObject.name == objectName)
                {
                    return dropdown;
                }
            }

            return null;
        }

        private void EnsureFpsDropdownOptions()
        {
            if (_fpsDropdown != null)
            {
                EnsureTmpDropdownCaption(_fpsDropdown);
                _fpsDropdown.options.Clear();
                PopulateFpsOptions(label => _fpsDropdown.options.Add(new TMP_Dropdown.OptionData(label)));
                _fpsDropdown.RefreshShownValue();
                return;
            }

            if (_fpsDropdownLegacy != null)
            {
                EnsureLegacyDropdownCaption(_fpsDropdownLegacy);
                _fpsDropdownLegacy.options.Clear();
                PopulateFpsOptions(label => _fpsDropdownLegacy.options.Add(new Dropdown.OptionData(label)));
                _fpsDropdownLegacy.RefreshShownValue();
            }
        }

        private void EnsureResolutionDropdownOptions()
        {
            if (_resolutionDropdown != null)
            {
                EnsureTmpDropdownCaption(_resolutionDropdown);
                _resolutionDropdown.options.Clear();
                PopulateResolutionOptions(label => _resolutionDropdown.options.Add(new TMP_Dropdown.OptionData(label)));
                _resolutionDropdown.RefreshShownValue();
                return;
            }

            if (_resolutionDropdownLegacy != null)
            {
                EnsureLegacyDropdownCaption(_resolutionDropdownLegacy);
                _resolutionDropdownLegacy.options.Clear();
                PopulateResolutionOptions(label => _resolutionDropdownLegacy.options.Add(new Dropdown.OptionData(label)));
                _resolutionDropdownLegacy.RefreshShownValue();
            }
        }

        private static void EnsureTmpDropdownCaption(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            if (dropdown.captionText == null)
            {
                Transform labelTransform = dropdown.transform.Find("Label");
                if (labelTransform != null)
                {
                    dropdown.captionText = labelTransform.GetComponent<TextMeshProUGUI>();
                }
            }

            if (dropdown.captionText != null)
            {
                dropdown.captionText.raycastTarget = false;
                return;
            }

            GameObject labelGo = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(dropdown.transform, false);

            RectTransform rect = labelGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 2f);
            rect.offsetMax = new Vector2(-25f, -2f);

            TextMeshProUGUI caption = labelGo.GetComponent<TextMeshProUGUI>();
            caption.fontSize = 14f;
            caption.color = Color.black;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            caption.raycastTarget = false;

            if (dropdown.itemText != null)
            {
                caption.font = dropdown.itemText.font;
                caption.fontSharedMaterial = dropdown.itemText.fontSharedMaterial;
                caption.fontSize = dropdown.itemText.fontSize;
                caption.color = dropdown.itemText.color;
            }

            dropdown.captionText = caption;
        }

        private static void EnsureLegacyDropdownCaption(Dropdown dropdown)
        {
            if (dropdown == null || dropdown.captionText != null)
            {
                return;
            }

            Transform labelTransform = dropdown.transform.Find("Label");
            if (labelTransform == null)
            {
                return;
            }

            Text caption = labelTransform.GetComponent<Text>();
            if (caption != null)
            {
                dropdown.captionText = caption;
                caption.raycastTarget = false;
            }
        }

        private static int FpsOptionCount()
        {
            return GameSettings_V2.FpsLimitOptions.Length;
        }

        private static void PopulateFpsOptions(System.Action<string> addOption)
        {
            int[] limits = GameSettings_V2.FpsLimitOptions;
            for (int i = 0; i < limits.Length; i++)
            {
                addOption(GameSettings_V2.FormatFpsLimitLabel(limits[i]));
            }
        }

        private static void PopulateResolutionOptions(System.Action<string> addOption)
        {
            IReadOnlyList<GameSettings_V2.ResolutionOption_V2> options = GameSettings_V2.GetResolutionOptions();
            for (int i = 0; i < options.Count; i++)
            {
                addOption(options[i].Label);
            }
        }

        private static int ResolutionOptionCount()
        {
            return GameSettings_V2.GetResolutionOptions().Count;
        }

        private void RegisterListeners()
        {
            if (_screenShakeToggle != null)
            {
                _screenShakeToggle.onValueChanged.AddListener(OnScreenShakeChanged);
            }

            if (_fullScreenToggle != null)
            {
                _fullScreenToggle.onValueChanged.AddListener(OnFullScreenChanged);
            }

            if (_vSyncToggle != null)
            {
                _vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
            }

            if (_fpsDropdown != null)
            {
                _fpsDropdown.onValueChanged.AddListener(OnFpsDropdownChanged);
            }

            if (_fpsDropdownLegacy != null)
            {
                _fpsDropdownLegacy.onValueChanged.AddListener(OnFpsDropdownChanged);
            }

            if (_resolutionDropdown != null)
            {
                _resolutionDropdown.onValueChanged.AddListener(OnResolutionDropdownChanged);
            }

            if (_resolutionDropdownLegacy != null)
            {
                _resolutionDropdownLegacy.onValueChanged.AddListener(OnResolutionDropdownChanged);
            }
        }

        private void UnregisterListeners()
        {
            if (_screenShakeToggle != null)
            {
                _screenShakeToggle.onValueChanged.RemoveListener(OnScreenShakeChanged);
            }

            if (_fullScreenToggle != null)
            {
                _fullScreenToggle.onValueChanged.RemoveListener(OnFullScreenChanged);
            }

            if (_vSyncToggle != null)
            {
                _vSyncToggle.onValueChanged.RemoveListener(OnVSyncChanged);
            }

            if (_fpsDropdown != null)
            {
                _fpsDropdown.onValueChanged.RemoveListener(OnFpsDropdownChanged);
            }

            if (_fpsDropdownLegacy != null)
            {
                _fpsDropdownLegacy.onValueChanged.RemoveListener(OnFpsDropdownChanged);
            }

            if (_resolutionDropdown != null)
            {
                _resolutionDropdown.onValueChanged.RemoveListener(OnResolutionDropdownChanged);
            }

            if (_resolutionDropdownLegacy != null)
            {
                _resolutionDropdownLegacy.onValueChanged.RemoveListener(OnResolutionDropdownChanged);
            }
        }

        private void OnScreenShakeChanged(bool enabled)
        {
            if (_suppressCallbacks)
            {
                return;
            }

            GameSettings_V2.SetScreenShakeEnabled(enabled);
        }

        private void OnFullScreenChanged(bool enabled)
        {
            if (_suppressCallbacks)
            {
                return;
            }

            GameSettings_V2.SetFullScreenEnabled(enabled);
        }

        private void OnVSyncChanged(bool enabled)
        {
            if (_suppressCallbacks)
            {
                return;
            }

            GameSettings_V2.SetVSyncEnabled(enabled);
        }

        private void OnFpsDropdownChanged(int index)
        {
            if (_suppressCallbacks)
            {
                return;
            }

            GameSettings_V2.SetFpsLimitIndex(index);
            ApplyCaptionText(_fpsDropdown, index);
            ApplyCaptionText(_fpsDropdownLegacy, index);
        }

        private void OnResolutionDropdownChanged(int index)
        {
            if (_suppressCallbacks)
            {
                return;
            }

            GameSettings_V2.SetResolutionIndex(index);
            ApplyCaptionText(_resolutionDropdown, index);
            ApplyCaptionText(_resolutionDropdownLegacy, index);
        }

        private static void SetToggleValue(Toggle toggle, bool value)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.SetIsOnWithoutNotify(value);
        }

        private void SetFpsDropdownIndex(int index)
        {
            index = Mathf.Clamp(index, 0, Mathf.Max(0, FpsOptionCount() - 1));
            if (_fpsDropdown != null)
            {
                EnsureTmpDropdownCaption(_fpsDropdown);
                _fpsDropdown.SetValueWithoutNotify(index);
                ApplyCaptionText(_fpsDropdown, index);
                _fpsDropdown.RefreshShownValue();
            }

            if (_fpsDropdownLegacy != null)
            {
                EnsureLegacyDropdownCaption(_fpsDropdownLegacy);
                _fpsDropdownLegacy.SetValueWithoutNotify(index);
                ApplyCaptionText(_fpsDropdownLegacy, index);
                _fpsDropdownLegacy.RefreshShownValue();
            }
        }

        private void SetResolutionDropdownIndex(int index)
        {
            index = Mathf.Clamp(index, 0, Mathf.Max(0, ResolutionOptionCount() - 1));
            if (_resolutionDropdown != null)
            {
                EnsureTmpDropdownCaption(_resolutionDropdown);
                _resolutionDropdown.SetValueWithoutNotify(index);
                ApplyCaptionText(_resolutionDropdown, index);
                _resolutionDropdown.RefreshShownValue();
            }

            if (_resolutionDropdownLegacy != null)
            {
                EnsureLegacyDropdownCaption(_resolutionDropdownLegacy);
                _resolutionDropdownLegacy.SetValueWithoutNotify(index);
                ApplyCaptionText(_resolutionDropdownLegacy, index);
                _resolutionDropdownLegacy.RefreshShownValue();
            }
        }

        private static void ApplyCaptionText(TMP_Dropdown dropdown, int index)
        {
            if (dropdown == null || dropdown.captionText == null || dropdown.options.Count == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, dropdown.options.Count - 1);
            dropdown.captionText.text = dropdown.options[index].text;
        }

        private static void ApplyCaptionText(Dropdown dropdown, int index)
        {
            if (dropdown == null || dropdown.captionText == null || dropdown.options.Count == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, dropdown.options.Count - 1);
            dropdown.captionText.text = dropdown.options[index].text;
        }
    }
}
