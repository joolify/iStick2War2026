using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * SwedishPlanePowerUpRewardPreview_V2 — optional world-space reward preview on the powerup prefab.
     * Scene HUD uses SurvivalPowerUpRewardHud_V2 (PowerUpImage + PowerUpText on gameplay canvas).
     */
    public sealed class SwedishPlanePowerUpRewardPreview_V2 : MonoBehaviour
    {
        [SerializeField] private Image _powerUpImage;
        [SerializeField] private TMP_Text _powerUpText;
        [SerializeField] private GameObject _previewRoot;
        [SerializeField] private string _powerUpImageObjectName = "PowerUpImage";
        [SerializeField] private string _powerUpTextObjectName = "PowerUpText";
        [SerializeField] private bool _preserveImageAspect = true;
        [SerializeField] private float _largeWeaponPreviewImageScale = 1.35f;

        private bool _didResolveUi;
        private bool _didCacheImageBaseSize;
        private Vector2 _imageBaseSize = new Vector2(64f, 64f);

        private void Awake()
        {
            ClearForSpawn();
        }

        public void ClearForSpawn()
        {
            ResolveUiIfNeeded();
            SetText(string.Empty);
            ApplyPreviewSprite(null);
            SetPreviewVisible(false);
        }

        public void BindOffer(SurvivalPowerUpOffer_V2 offer)
        {
            if (SurvivalPowerUpRewardHud_V2.HasInstance)
            {
                ClearForSpawn();
                return;
            }

            ResolveUiIfNeeded();
            if (offer == null)
            {
                ClearForSpawn();
                return;
            }

            SetText(SurvivalPowerUpPreviewResolver_V2.ResolveDisplayName(offer));
            ApplyPreviewSprite(SurvivalPowerUpPreviewResolver_V2.ResolvePreviewSprite(offer));
            SetPreviewVisible(true);
        }

        private void ResolveUiIfNeeded()
        {
            if (_didResolveUi)
            {
                return;
            }

            if (_powerUpImage == null && !string.IsNullOrWhiteSpace(_powerUpImageObjectName))
            {
                _powerUpImage = FindUiComponentInChildren<Image>(_powerUpImageObjectName);
            }

            if (_powerUpText == null && !string.IsNullOrWhiteSpace(_powerUpTextObjectName))
            {
                _powerUpText = FindUiComponentInChildren<TMP_Text>(_powerUpTextObjectName);
            }

            if (_previewRoot == null)
            {
                if (_powerUpImage != null)
                {
                    _previewRoot = _powerUpImage.transform.parent != null
                        ? _powerUpImage.transform.parent.gameObject
                        : _powerUpImage.gameObject;
                }
                else if (_powerUpText != null)
                {
                    _previewRoot = _powerUpText.transform.parent != null
                        ? _powerUpText.transform.parent.gameObject
                        : _powerUpText.gameObject;
                }
            }

            _didResolveUi = true;
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

        private void SetText(string value)
        {
            if (_powerUpText != null)
            {
                _powerUpText.text = value ?? string.Empty;
            }
        }

        private void SetPreviewVisible(bool visible)
        {
            if (_previewRoot != null && _previewRoot.GetComponent<Canvas>() == null)
            {
                _previewRoot.SetActive(visible);
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
