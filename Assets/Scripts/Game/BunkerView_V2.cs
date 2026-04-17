using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Simple bunker visual feedback based on WaveManager bunker HP ratio.
    /// Place on BunkerRoot; assign back/front renderers or rely on name lookup.
    /// </summary>
    public sealed class BunkerView_V2 : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private SpriteRenderer _bunkerBackRenderer;
        [SerializeField] private SpriteRenderer _bunkerFrontRenderer;

        [Header("Color by Health")]
        [SerializeField] private Color _healthyColor = Color.white;
        [SerializeField] private Color _damagedColor = new Color(1f, 0.72f, 0.72f, 1f);
        [SerializeField] private bool _updateEveryFrame = true;

        private float _lastAppliedRatio = -1f;

        private void Awake()
        {
            ResolveReferencesIfNeeded();
            ApplyVisual();
        }

        private void Update()
        {
            if (_updateEveryFrame)
            {
                ApplyVisual();
            }
        }

        public void RefreshNow()
        {
            ApplyVisual();
        }

        private void ResolveReferencesIfNeeded()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>();
            }

            if (_bunkerBackRenderer == null)
            {
                _bunkerBackRenderer = FindRendererByName("bunkerBack");
            }

            if (_bunkerFrontRenderer == null)
            {
                _bunkerFrontRenderer = FindRendererByName("bunkerFront");
            }
        }

        private SpriteRenderer FindRendererByName(string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || !t.name.Equals(targetName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                return t.GetComponent<SpriteRenderer>();
            }

            return null;
        }

        private void ApplyVisual()
        {
            if (_waveManager == null)
            {
                return;
            }

            int maxHp = Mathf.Max(1, _waveManager.BunkerMaxHealth);
            float ratio = Mathf.Clamp01((float)_waveManager.BunkerHealth / maxHp);

            if (Mathf.Approximately(ratio, _lastAppliedRatio))
            {
                return;
            }

            _lastAppliedRatio = ratio;
            Color c = Color.Lerp(_damagedColor, _healthyColor, ratio);

            if (_bunkerBackRenderer != null)
            {
                _bunkerBackRenderer.color = c;
            }

            if (_bunkerFrontRenderer != null)
            {
                _bunkerFrontRenderer.color = c;
            }
        }
    }
}
