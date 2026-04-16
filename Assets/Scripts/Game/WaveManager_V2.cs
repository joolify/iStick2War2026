using System;
using System.Collections.Generic;
using UnityEngine;
using iStick2War;
using TMPro;

namespace iStick2War_V2
{
    public enum WaveLoopState_V2
    {
        Preparing,
        InWave,
        Shop,
        GameOver
    }

    public sealed class WaveManager_V2 : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Hero_V2 _hero;
        [SerializeField] private ShopPanel_V2 _shopPanel;
        [SerializeField] private EnemySpawner_V2 _enemySpawner;
        [SerializeField] private FollowCamera _followCamera;
        [Header("Top Bar UI (optional)")]
        [SerializeField] private TMP_Text _topBarHealthText;
        [SerializeField] private TMP_Text _topBarCurrentWeaponText;
        [SerializeField] private TMP_Text _topBarCurrentAmmoText;

        [Header("Waves")]
        [SerializeField] private List<WaveConfig_V2> _waves = new List<WaveConfig_V2>();
        [SerializeField] private float _prepareDurationSeconds = 2f;

        [Header("Economy")]
        [SerializeField] private int _startingCurrency = 100;
        [SerializeField] private int _healthPurchaseCost = 60;
        [SerializeField] private int _healthPurchaseAmount = 25;
        [SerializeField] private int _bunkerRepairCost = 80;
        [SerializeField] private int _bunkerRepairAmount = 25;
        [SerializeField] private int _bunkerMaxHealth = 250;
        [SerializeField] private int _startingBunkerHealth = 250;

        [Header("Debug")]
        [SerializeField] private bool _debugWaveLogs = false;
        [SerializeField] private bool _debugCameraFollowLogs = false;
        [SerializeField] private KeyCode _nextWaveDebugKey = KeyCode.Return;

        private WaveLoopState_V2 _state = WaveLoopState_V2.Preparing;
        private int _waveIndex;
        private float _stateEndTime;
        private int _currency;
        private int _bunkerHealth;
        private int _enemiesKilledThisWave;

        public event Action<WaveLoopState_V2> OnStateChanged;
        public event Action<int, int, int> OnMetaChanged;

        public WaveLoopState_V2 State => _state;
        public int CurrentWaveNumber => _waveIndex + 1;
        public int Currency => _currency;
        public int BunkerHealth => _bunkerHealth;
        public int BunkerMaxHealth => _bunkerMaxHealth;

        private void Awake()
        {
            _currency = Mathf.Max(0, _startingCurrency);
            _bunkerHealth = Mathf.Clamp(_startingBunkerHealth, 0, Mathf.Max(1, _bunkerMaxHealth));
        }

        private void Start()
        {
            ResolveCameraFollowReferenceIfNeeded();
            ResolveTopBarReferencesIfNeeded();
            if (_shopPanel != null)
            {
                _shopPanel.Initialize(this);
                _shopPanel.Hide();
            }

            EnterPreparingState();
            EmitMetaChanged();
            RefreshTopBar();
        }

        private void Update()
        {
            if (_state != WaveLoopState_V2.GameOver && _hero != null && _hero.IsDead())
            {
                EnterGameOverState();
                return;
            }

            switch (_state)
            {
                case WaveLoopState_V2.Preparing:
                    if (Time.time >= _stateEndTime)
                    {
                        EnterInWaveState();
                    }
                    break;
                case WaveLoopState_V2.InWave:
                    TickInWaveState();
                    break;
                case WaveLoopState_V2.Shop:
                    if (Input.GetKeyDown(_nextWaveDebugKey) || Input.GetKeyDown(KeyCode.N))
                    {
                        StartNextWaveFromShop();
                    }
                    break;
            }

            RefreshTopBar();
        }

        public void ReportEnemyKilled()
        {
            if (_state != WaveLoopState_V2.InWave)
            {
                return;
            }

            _enemiesKilledThisWave++;
            WaveConfig_V2 wave = GetCurrentWaveConfig();
            if (wave != null && _enemiesKilledThisWave >= wave.EnemyCount)
            {
                CompleteWave();
            }
        }

        public bool PurchaseHealth()
        {
            if (_state != WaveLoopState_V2.Shop || _hero == null)
            {
                return false;
            }

            if (!TrySpend(_healthPurchaseCost))
            {
                return false;
            }

            _hero.Heal(_healthPurchaseAmount);
            Log($"Health purchased (+{_healthPurchaseAmount}) for {_healthPurchaseCost}.");
            EmitMetaChanged();
            return true;
        }

        /// <summary>
        /// Unified purchase for the shop carousel (configured on ShopPanel_V2).
        /// </summary>
        public bool TryPurchaseOffer(ShopOfferConfig_V2 offer)
        {
            if (_state != WaveLoopState_V2.Shop || offer == null)
            {
                return false;
            }

            int cost = offer.Cost;
            switch (offer.Kind)
            {
                case ShopOfferKind_V2.HealthPack:
                    if (_hero == null || _hero.IsHealthFull())
                    {
                        return false;
                    }

                    if (!TrySpend(cost))
                    {
                        return false;
                    }

                    int heal = offer.HealthAmount > 0 ? offer.HealthAmount : _healthPurchaseAmount;
                    _hero.Heal(heal);
                    Log($"Health purchased (+{heal}) for {cost}.");
                    EmitMetaChanged();
                    return true;

                case ShopOfferKind_V2.BunkerRepair:
                    if (_bunkerHealth >= _bunkerMaxHealth)
                    {
                        return false;
                    }

                    if (!TrySpend(cost))
                    {
                        return false;
                    }

                    int repair = offer.BunkerRepairAmount > 0 ? offer.BunkerRepairAmount : _bunkerRepairAmount;
                    _bunkerHealth = Mathf.Min(_bunkerMaxHealth, _bunkerHealth + repair);
                    Log($"Bunker repaired (+{repair}) for {cost}. hp={_bunkerHealth}/{_bunkerMaxHealth}");
                    EmitMetaChanged();
                    return true;

                case ShopOfferKind_V2.WeaponUnlock:
                    if (_hero == null || offer.Weapon == null)
                    {
                        return false;
                    }

                    if (_hero.HasWeaponUnlocked(offer.Weapon))
                    {
                        return false;
                    }

                    if (!TrySpend(cost))
                    {
                        return false;
                    }

                    bool added = _hero.UnlockWeapon(offer.Weapon, true);
                    if (!added)
                    {
                        _currency += cost;
                        return false;
                    }

                    Log($"Weapon unlocked: {offer.Weapon.DisplayName} for {cost}.");
                    EmitMetaChanged();
                    return true;

                case ShopOfferKind_V2.AmmoRefill:
                    if (_hero == null || offer.Weapon == null)
                    {
                        return false;
                    }

                    if (!_hero.HasWeaponUnlocked(offer.Weapon))
                    {
                        return false;
                    }

                    if (_hero.IsWeaponMagazineFull(offer.Weapon))
                    {
                        return false;
                    }

                    if (!TrySpend(cost))
                    {
                        return false;
                    }

                    bool refilled = _hero.TryRefillWeaponMagazine(offer.Weapon);
                    if (!refilled)
                    {
                        _currency += cost;
                        return false;
                    }

                    Log($"Ammo refilled: {offer.Weapon.DisplayName} for {cost}.");
                    EmitMetaChanged();
                    return true;

                default:
                    return false;
            }
        }

        public bool IsWeaponOwned(HeroWeaponDefinition_V2 definition)
        {
            return definition != null && _hero != null && _hero.HasWeaponUnlocked(definition);
        }

        public bool IsWeaponAmmoFull(HeroWeaponDefinition_V2 definition)
        {
            return definition != null && _hero != null && _hero.IsWeaponMagazineFull(definition);
        }

        public bool IsBunkerFullHealth()
        {
            return _bunkerHealth >= _bunkerMaxHealth;
        }

        public bool IsHeroHealthFull()
        {
            return _hero != null && _hero.IsHealthFull();
        }

        public bool CanAfford(int cost)
        {
            return _currency >= Mathf.Max(0, cost);
        }

        public bool PurchaseBunkerRepair()
        {
            if (_state != WaveLoopState_V2.Shop)
            {
                return false;
            }

            if (_bunkerHealth >= _bunkerMaxHealth)
            {
                return false;
            }

            if (!TrySpend(_bunkerRepairCost))
            {
                return false;
            }

            _bunkerHealth = Mathf.Min(_bunkerMaxHealth, _bunkerHealth + _bunkerRepairAmount);
            Log($"Bunker repaired (+{_bunkerRepairAmount}) for {_bunkerRepairCost}. hp={_bunkerHealth}/{_bunkerMaxHealth}");
            EmitMetaChanged();
            return true;
        }

        public void StartNextWaveFromShop()
        {
            if (_state != WaveLoopState_V2.Shop)
            {
                return;
            }

            if (_shopPanel != null)
            {
                _shopPanel.Hide();
            }

            SetCameraFollowEnabled(true);
            _waveIndex++;
            EnterPreparingState();
            EmitMetaChanged();
        }

        public int GetHealthPurchaseCost() => Mathf.Max(0, _healthPurchaseCost);
        public int GetBunkerRepairCost() => Mathf.Max(0, _bunkerRepairCost);

        private void TickInWaveState()
        {
            WaveConfig_V2 wave = GetCurrentWaveConfig();
            if (wave == null)
            {
                EnterGameOverState();
                return;
            }

            if (_enemySpawner != null && _enemySpawner.IsWaveCleared())
            {
                CompleteWave();
                return;
            }

            if (Time.time >= _stateEndTime)
            {
                CompleteWave();
            }
        }

        private void CompleteWave()
        {
            WaveConfig_V2 wave = GetCurrentWaveConfig();
            if (wave == null)
            {
                EnterGameOverState();
                return;
            }

            if (_enemySpawner != null)
            {
                _enemySpawner.StopWave();
            }

            _currency += wave.WaveRewardCurrency;
            SetState(WaveLoopState_V2.Shop);
            SetCameraFollowEnabled(false);
            if (_shopPanel != null)
            {
                _shopPanel.Show();
                _shopPanel.Refresh();
            }
            Log($"Wave {CurrentWaveNumber} cleared. reward={wave.WaveRewardCurrency}, currency={_currency}");
            EmitMetaChanged();
        }

        private void EnterPreparingState()
        {
            SetState(WaveLoopState_V2.Preparing);
            _stateEndTime = Time.time + Mathf.Max(0.1f, _prepareDurationSeconds);
            _enemiesKilledThisWave = 0;
        }

        private void EnterInWaveState()
        {
            WaveConfig_V2 wave = GetCurrentWaveConfig();
            if (wave == null)
            {
                EnterGameOverState();
                return;
            }

            SetState(WaveLoopState_V2.InWave);
            SetCameraFollowEnabled(true);
            _stateEndTime = Time.time + wave.WaveDurationSeconds;
            _enemiesKilledThisWave = 0;
            if (_enemySpawner != null)
            {
                _enemySpawner.BeginWave(wave, ReportEnemyKilled);
            }
            Log($"Wave {CurrentWaveNumber} started. enemies={wave.EnemyCount}, duration={wave.WaveDurationSeconds:0.0}s");
        }

        private void EnterGameOverState()
        {
            SetState(WaveLoopState_V2.GameOver);
            if (_enemySpawner != null)
            {
                _enemySpawner.StopWave();
            }
            if (_shopPanel != null)
            {
                _shopPanel.Hide();
            }
            SetCameraFollowEnabled(false);
            Log("WaveManager entered GameOver (no more wave configs).");
        }

        private WaveConfig_V2 GetCurrentWaveConfig()
        {
            if (_waves == null || _waves.Count == 0)
            {
                return null;
            }

            int idx = Mathf.Clamp(_waveIndex, 0, _waves.Count - 1);
            return _waves[idx];
        }

        private bool TrySpend(int amount)
        {
            int cost = Mathf.Max(0, amount);
            if (_currency < cost)
            {
                return false;
            }

            _currency -= cost;
            return true;
        }

        private void SetState(WaveLoopState_V2 newState)
        {
            if (_state == newState)
            {
                return;
            }

            _state = newState;
            OnStateChanged?.Invoke(_state);
        }

        private void EmitMetaChanged()
        {
            OnMetaChanged?.Invoke(CurrentWaveNumber, _currency, _bunkerHealth);
            RefreshTopBar();
        }

        private void Log(string message)
        {
            if (_debugWaveLogs)
            {
                Debug.Log($"[WaveManager_V2] {message}");
            }
        }

        private void ResolveCameraFollowReferenceIfNeeded()
        {
            if (_followCamera != null)
            {
                return;
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _followCamera = mainCam.GetComponent<FollowCamera>();
                if (_debugCameraFollowLogs)
                {
                    Debug.Log(_followCamera != null
                        ? $"[WaveManager_V2] Found FollowCamera on '{mainCam.name}'. enabled={_followCamera.enabled}"
                        : $"[WaveManager_V2] No FollowCamera found on '{mainCam.name}'.");
                }
            }
            else if (_debugCameraFollowLogs)
            {
                Debug.LogWarning("[WaveManager_V2] Camera.main is null; cannot resolve FollowCamera.");
            }
        }

        private void SetCameraFollowEnabled(bool isEnabled)
        {
            ResolveCameraFollowReferenceIfNeeded();
            if (_followCamera != null)
            {
                bool previous = _followCamera.enabled;
                _followCamera.enabled = isEnabled;
                if (_debugCameraFollowLogs)
                {
                    Transform camT = _followCamera.transform;
                    Debug.Log(
                        $"[WaveManager_V2] FollowCamera enabled {previous} -> {_followCamera.enabled} " +
                        $"(requested={isEnabled}) at camPos={camT.position}, state={_state}");
                }
            }
            else if (_debugCameraFollowLogs)
            {
                Debug.LogWarning($"[WaveManager_V2] SetCameraFollowEnabled({isEnabled}) skipped; _followCamera is null.");
            }
        }

        private void ResolveTopBarReferencesIfNeeded()
        {
            if (_topBarHealthText == null)
            {
                _topBarHealthText = FindTextInSceneByName("txt_topbar_health");
            }

            if (_topBarCurrentWeaponText == null)
            {
                _topBarCurrentWeaponText = FindTextInSceneByName("txt_topbar_currentWeapon");
            }

            if (_topBarCurrentAmmoText == null)
            {
                _topBarCurrentAmmoText = FindTextInSceneByName("txt_topbar_currentAmmo");
            }
        }

        private void RefreshTopBar()
        {
            if (_hero == null)
            {
                return;
            }

            if (_topBarHealthText != null)
            {
                _topBarHealthText.text = $"HP: {_hero.GetCurrentHealth()}/{_hero.GetMaxHealth()}";
            }

            if (_topBarCurrentWeaponText != null)
            {
                _topBarCurrentWeaponText.text = $"Weapon: {_hero.GetCurrentWeaponDisplayName()}";
            }

            if (_topBarCurrentAmmoText != null)
            {
                _topBarCurrentAmmoText.text =
                    $"Ammo: {_hero.GetCurrentWeaponAmmo()}/{_hero.GetCurrentWeaponReserveAmmo()}";
            }
        }

        private static TMP_Text FindTextInSceneByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TMP_Text[] allTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            for (int i = 0; i < allTexts.Length; i++)
            {
                TMP_Text current = allTexts[i];
                if (current != null && current.gameObject.name.Equals(objectName, StringComparison.Ordinal))
                {
                    return current;
                }
            }

            return null;
        }
    }
}
