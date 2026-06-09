using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * SurvivalPowerUpRewardHud_V2 — centered pickup celebration (PowerUpTitle, PowerUpImage, PowerUpText).
     * Hidden during supply drop; shown in the middle of the game view after the hero picks up the crate.
     */
    public sealed class SurvivalPowerUpRewardHud_V2 : MonoBehaviour
    {
        private static SurvivalPowerUpRewardHud_V2 s_instance;

        [SerializeField] private TMP_Text _powerUpTitle;
        [SerializeField] private Image _powerUpImage;
        [SerializeField] private TMP_Text _powerUpText;
        [SerializeField] private RectTransform _hudRect;
        [SerializeField] private GameObject _previewRoot;
        [SerializeField] private string _powerUpTitleObjectName = "PowerUpTitle";
        [SerializeField] private string _powerUpImageObjectName = "PowerUpImage";
        [SerializeField] private string _powerUpTextObjectName = "PowerUpText";
        [SerializeField] private bool _preserveImageAspect = true;
        [SerializeField] private float _largeWeaponPreviewImageScale = 1.35f;
        [SerializeField] private float _autoHideSecondsAfterPickup = 3.5f;

        private bool _didResolveUi;
        private bool _didCacheImageBaseSize;
        private Vector2 _imageBaseSize = new Vector2(64f, 64f);
        private Coroutine _autoHideRoutine;

        public static bool HasInstance => s_instance != null;

        public static void EnsureInitializedFromScene()
        {
            if (s_instance != null)
            {
                s_instance.ResolveUiIfNeeded();
                s_instance.HideInternal();
                return;
            }

            s_instance = FindAnyObjectByType<SurvivalPowerUpRewardHud_V2>(FindObjectsInactive.Include);
            if (s_instance != null)
            {
                s_instance.ResolveUiIfNeeded();
                s_instance.HideInternal();
                return;
            }

            Image sceneImage = FindScenePowerUpImage();
            if (sceneImage == null)
            {
                return;
            }

            Canvas canvas = sceneImage.GetComponentInParent<Canvas>();
            GameObject host = canvas != null ? canvas.gameObject : sceneImage.gameObject;
            s_instance = host.GetComponent<SurvivalPowerUpRewardHud_V2>();
            if (s_instance == null)
            {
                s_instance = host.AddComponent<SurvivalPowerUpRewardHud_V2>();
            }

            s_instance.ResolveUiIfNeeded();
            s_instance.HideInternal();
        }

        public static void ShowPickupReward(SurvivalPowerUpOffer_V2 offer)
        {
            EnsureInitializedFromScene();
            s_instance?.ShowPickupInternal(offer);
        }

        public static void Hide()
        {
            if (s_instance == null)
            {
                return;
            }

            s_instance.HideInternal();
        }

        private static Image FindScenePowerUpImage()
        {
            Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && image.name == "PowerUpImage")
                {
                    return image;
                }
            }

            return null;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                HideInternal();
                return;
            }

            s_instance = this;
            ResolveUiIfNeeded();
            HideInternal();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void ShowPickupInternal(SurvivalPowerUpOffer_V2 offer)
        {
            ResolveUiIfNeeded();
            if (offer == null)
            {
                HideInternal();
                return;
            }

            if (_autoHideRoutine != null)
            {
                StopCoroutine(_autoHideRoutine);
                _autoHideRoutine = null;
            }

            SetTitle(SurvivalPowerUpPreviewResolver_V2.ResolvePickupTitle(offer));
            SetText(SurvivalPowerUpPreviewResolver_V2.ResolveDisplayName(offer));
            ApplyPreviewSprite(SurvivalPowerUpPreviewResolver_V2.ResolvePreviewSprite(offer));
            CenterHudInGameView();
            SetHudVisible(true);

            if (_autoHideSecondsAfterPickup > 0f)
            {
                _autoHideRoutine = StartCoroutine(AutoHideAfterDelay(_autoHideSecondsAfterPickup));
            }
        }

        private IEnumerator AutoHideAfterDelay(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, seconds));
            HideInternal();
            _autoHideRoutine = null;
        }

        private void HideInternal()
        {
            if (_autoHideRoutine != null)
            {
                StopCoroutine(_autoHideRoutine);
                _autoHideRoutine = null;
            }

            SetTitle(string.Empty);
            SetText(string.Empty);
            ApplyPreviewSprite(null);
            SetHudVisible(false);
        }

        private void CenterHudInGameView()
        {
            RectTransform rect = null;
            if (_previewRoot != null)
            {
                rect = _previewRoot.GetComponent<RectTransform>();
            }

            if (rect == null)
            {
                rect = _hudRect;
            }

            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private void ResolveUiIfNeeded()
        {
            if (_didResolveUi)
            {
                return;
            }

            if (_powerUpTitle == null && !string.IsNullOrWhiteSpace(_powerUpTitleObjectName))
            {
                _powerUpTitle = FindUiComponentInChildren<TMP_Text>(_powerUpTitleObjectName);
            }

            if (_powerUpImage == null && !string.IsNullOrWhiteSpace(_powerUpImageObjectName))
            {
                _powerUpImage = FindUiComponentInChildren<Image>(_powerUpImageObjectName);
            }

            if (_powerUpText == null && !string.IsNullOrWhiteSpace(_powerUpTextObjectName))
            {
                _powerUpText = FindUiComponentInChildren<TMP_Text>(_powerUpTextObjectName);
            }

            if (_hudRect == null)
            {
                if (_previewRoot != null)
                {
                    _hudRect = _previewRoot.GetComponent<RectTransform>();
                }

                if (_hudRect == null && _powerUpImage != null)
                {
                    _hudRect = _powerUpImage.rectTransform;
                }
                else if (_hudRect == null && _powerUpText != null)
                {
                    _hudRect = _powerUpText.rectTransform;
                }
                else if (_hudRect == null && _powerUpTitle != null)
                {
                    _hudRect = _powerUpTitle.rectTransform;
                }
            }

            if (_previewRoot == null)
            {
                _previewRoot = TryResolvePreviewRoot();
            }

            _didResolveUi = true;
        }

        private GameObject TryResolvePreviewRoot()
        {
            if (_powerUpTitle != null)
            {
                Transform parent = _powerUpTitle.transform.parent;
                if (parent != null && parent.GetComponent<Canvas>() == null)
                {
                    return parent.gameObject;
                }
            }

            if (_powerUpImage != null &&
                _powerUpText != null &&
                _powerUpImage.transform.parent == _powerUpText.transform.parent &&
                _powerUpImage.transform.parent != null &&
                _powerUpImage.transform.parent.GetComponent<Canvas>() == null)
            {
                return _powerUpImage.transform.parent.gameObject;
            }

            if (_powerUpImage != null)
            {
                Transform parent = _powerUpImage.transform.parent;
                return parent != null && parent.GetComponent<Canvas>() == null
                    ? parent.gameObject
                    : _powerUpImage.gameObject;
            }

            if (_powerUpText != null)
            {
                Transform parent = _powerUpText.transform.parent;
                return parent != null && parent.GetComponent<Canvas>() == null
                    ? parent.gameObject
                    : _powerUpText.gameObject;
            }

            return null;
        }

        private T FindUiComponentInChildren<T>(string objectName) where T : Component
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.name != objectName)
                {
                    continue;
                }

                T component = candidate.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private void ApplyPreviewSprite(Sprite sprite)
        {
            if (_powerUpImage == null)
            {
                return;
            }

            CacheImageBaseSizeIfNeeded();
            if (sprite == null)
            {
                _powerUpImage.enabled = false;
                _powerUpImage.sprite = null;
                return;
            }

            float scale = ResolvePreviewImageScale(sprite);
            _powerUpImage.rectTransform.sizeDelta = _imageBaseSize * scale;
            _powerUpImage.sprite = sprite;
            _powerUpImage.preserveAspect = _preserveImageAspect;
            _powerUpImage.enabled = true;
        }

        private float ResolvePreviewImageScale(Sprite sprite)
        {
            if (sprite == null || _largeWeaponPreviewImageScale <= 1f)
            {
                return 1f;
            }

            if (sprite.rect.width <= sprite.rect.height * 1.15f)
            {
                return 1f;
            }

            return _largeWeaponPreviewImageScale;
        }

        private void CacheImageBaseSizeIfNeeded()
        {
            if (_didCacheImageBaseSize || _powerUpImage == null)
            {
                return;
            }

            Vector2 size = _powerUpImage.rectTransform.sizeDelta;
            if (size.x > 0f && size.y > 0f)
            {
                _imageBaseSize = size;
            }

            _didCacheImageBaseSize = true;
        }

        private void SetTitle(string value)
        {
            if (_powerUpTitle != null)
            {
                _powerUpTitle.text = value ?? string.Empty;
            }
        }

        private void SetText(string value)
        {
            if (_powerUpText != null)
            {
                _powerUpText.text = value ?? string.Empty;
            }
        }

        private void SetHudVisible(bool visible)
        {
            if (_previewRoot != null && _previewRoot.GetComponent<Canvas>() == null)
            {
                _previewRoot.SetActive(visible);
            }

            if (_powerUpTitle != null)
            {
                _powerUpTitle.gameObject.SetActive(visible);
            }

            if (_powerUpImage != null)
            {
                _powerUpImage.gameObject.SetActive(visible);
            }

            if (_powerUpText != null)
            {
                _powerUpText.gameObject.SetActive(visible);
            }
        }
    }
}
