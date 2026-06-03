using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * HeartLifeBar_V2 (Run lives HUD — scene-authored heartLife prefab instances)
 *
 * PURPOSE:
 * Drives a pre-built HeartLifeBar with placed heartLife prefab instances (SpriteRenderer). Lost lives are
 * dimmed via alpha; WaveManager_V2 owns the life count via OnLivesChanged.
 *
 * NAVIGATION: WaveManager_V2.EnsureHeartLifeBar; prefab Assets/Prefabs/Hero/heartLife.prefab.
 */
    [AddComponentMenu("iStick2War/Heart Life Bar V2")]
    public sealed class HeartLifeBar_V2 : MonoBehaviour
    {
        private struct HeartSlot
        {
            public GameObject Instance;
            public SpriteRenderer Sprite;
            public Color BaseColor;
        }

        [SerializeField] private WaveManager_V2 _waveManager;
        [Tooltip("Optional root that holds the heartLife prefab instances. Defaults to this GameObject.")]
        [SerializeField] private Transform _heartsRoot;
        [Tooltip("Drag the 3 placed heartLife prefab instances here (left to right). Auto-resolved from direct children when empty.")]
        [SerializeField] private GameObject[] _heartInstances;
        [SerializeField] private float _lostHeartAlpha = 0.22f;
        [SerializeField] private bool _debugLogs;

        private readonly List<HeartSlot> _heartSlots = new List<HeartSlot>(3);
        private int _maxLives;
        private bool _resolved;

        public void Initialize(WaveManager_V2 waveManager, int maxLivesPerRun)
        {
            if (waveManager != null)
            {
                _waveManager = waveManager;
            }

            _maxLives = Mathf.Max(1, maxLivesPerRun);
            ResolveHeartSlotsIfNeeded(force: true);
            BindWaveManagerEvents();
            RefreshFromManager();
        }

        private void Awake()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Exclude);
            }

            ResolveHeartSlotsIfNeeded(force: false);
        }

        private void OnEnable()
        {
            BindWaveManagerEvents();
            RefreshFromManager();
        }

        private void OnDisable()
        {
            if (_waveManager != null)
            {
                _waveManager.OnLivesChanged -= HandleLivesChanged;
            }
        }

        private void BindWaveManagerEvents()
        {
            if (_waveManager == null)
            {
                return;
            }

            _waveManager.OnLivesChanged -= HandleLivesChanged;
            _waveManager.OnLivesChanged += HandleLivesChanged;
            _maxLives = Mathf.Max(_maxLives, _waveManager.MaxLivesPerRun);
        }

        private void HandleLivesChanged(int remaining, int max)
        {
            _maxLives = Mathf.Max(1, max);
            RefreshHearts(remaining, _maxLives);
        }

        private void RefreshFromManager()
        {
            if (_waveManager == null)
            {
                return;
            }

            RefreshHearts(_waveManager.LivesRemaining, _waveManager.MaxLivesPerRun);
        }

        private void ResolveHeartSlotsIfNeeded(bool force)
        {
            if (!force && _resolved && _heartSlots.Count > 0)
            {
                return;
            }

            _heartSlots.Clear();
            Transform root = _heartsRoot != null ? _heartsRoot : transform;

            if (_heartInstances != null && _heartInstances.Length > 0)
            {
                for (int i = 0; i < _heartInstances.Length; i++)
                {
                    TryAddHeartPrefabInstance(_heartInstances[i]);
                }
            }

            if (_heartSlots.Count == 0)
            {
                ResolveHeartPrefabsFromDirectChildren(root);
            }

            _resolved = _heartSlots.Count > 0;
            if (!_resolved && _debugLogs)
            {
                Debug.LogWarning(
                    $"[HeartLifeBar_V2] No heartLife prefab instances found under '{root.name}'. " +
                    "Place 3 heartLife prefabs as direct children or assign _heartInstances.");
            }
        }

        // Each direct child is expected to be one heartLife prefab instance (SpriteRenderer on root).
        private void ResolveHeartPrefabsFromDirectChildren(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                TryAddHeartPrefabInstance(child.gameObject);
            }
        }

        private void TryAddHeartPrefabInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            for (int i = 0; i < _heartSlots.Count; i++)
            {
                if (_heartSlots[i].Instance == instance)
                {
                    return;
                }
            }

            SpriteRenderer sprite = instance.GetComponent<SpriteRenderer>();
            if (sprite == null)
            {
                sprite = instance.GetComponentInChildren<SpriteRenderer>(true);
            }

            if (sprite == null)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning(
                        $"[HeartLifeBar_V2] '{instance.name}' has no SpriteRenderer (expected heartLife prefab).");
                }

                return;
            }

            Color baseColor = sprite.color;
            baseColor.a = 1f;
            _heartSlots.Add(new HeartSlot
            {
                Instance = instance,
                Sprite = sprite,
                BaseColor = baseColor
            });
        }

        private void RefreshHearts(int remaining, int max)
        {
            ResolveHeartSlotsIfNeeded(force: false);
            int clampedRemaining = Mathf.Clamp(remaining, 0, max);
            for (int i = 0; i < _heartSlots.Count; i++)
            {
                HeartSlot slot = _heartSlots[i];
                if (slot.Sprite == null)
                {
                    continue;
                }

                bool isRemaining = i < clampedRemaining;
                float alpha = isRemaining ? 1f : _lostHeartAlpha;
                Color c = slot.BaseColor;
                c.a = alpha;
                slot.Sprite.color = c;
                slot.Sprite.enabled = true;

                if (slot.Instance != null && !slot.Instance.activeSelf)
                {
                    slot.Instance.SetActive(true);
                }
            }
        }
    }
}
