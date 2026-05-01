using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using iStick2War;
using TMPro;

namespace iStick2War_V2
{
    public enum WaveLoopState_V2
    {
        Preparing,
        InWave,
        Shop,
        GameOver,
        GameWon,
        GameError
    }

    public enum DeathContinueTier_V2
    {
        RestartRun,
        CheckpointContinue,
        ClutchSave
    }

    public sealed class WaveManager_V2 : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Hero_V2 _hero;
        [SerializeField] private ShopPanel_V2 _shopPanel;
        [SerializeField] private EnemySpawner_V2 _enemySpawner;
        [SerializeField] private FollowCamera _followCamera;
        [Header("Bunker hero protection")]
        [Tooltip("Optional trigger: hero inside takes no HP damage (enemy shots may still damage the bunker).")]
        [SerializeField] private Collider2D _bunkerHeroSafeZoneCollider;
        [Header("Top Bar UI (optional)")]
        [SerializeField] private TMP_Text _topBarHealthText;
        [SerializeField] private TMP_Text _topBarCurrentWeaponText;
        [SerializeField] private TMP_Text _topBarCurrentAmmoText;
        [SerializeField] private TMP_Text _topBarReloadText;
        [SerializeField] private TMP_Text _topBarBunkerHealthText;
        [Tooltip("Optional UI Image (Type: Filled) for hero HP ratio.")]
        [SerializeField] private Image _topBarHeroHealthFill;
        [Tooltip("Optional UI Image (Type: Filled) for bunker HP ratio.")]
        [SerializeField] private Image _topBarBunkerHealthFill;
        [SerializeField] private TMP_Text _topBarWaveText;
        [Header("Game Over UI")]
        [Tooltip("Optional; if unset, resolved once when entering Game Over. Shown only when the hero is dead.")]
        [SerializeField] private GameOverUI_V2 _gameOverUi;
        [Header("Game Over UI — hero death only")]
        [Tooltip("e.g. world-space GameOver root. Hidden until Hero_V2 dies.")]
        [SerializeField] private GameObject _heroDeathGameOverRoot;
        [Tooltip("Child button on GameOver root, e.g. btn_gameOver_continue / bkg_gameOver_continue.")]
        [SerializeField] private GameObject _heroDeathContinueButton;
        [Tooltip("Top bar label, e.g. txt_topbar_gameOver. Hidden until Hero_V2 dies.")]
        [SerializeField] private TMP_Text _heroDeathTopBarTitle;
        [Tooltip("Top bar label, e.g. txt_topbar_gameOver_continue. Hidden until Hero_V2 dies.")]
        [SerializeField] private TMP_Text _heroDeathTopBarContinue;
        [Header("Game Won UI — wave 10 clear")]
        [Tooltip("e.g. world-space GameWon root. Hidden until last wave is cleared.")]
        [SerializeField] private GameObject _gameWonRoot;
        [Tooltip("Continue button on win panel, e.g. btn_gameWon_continue.")]
        [SerializeField] private GameObject _gameWonContinueButton;
        [Tooltip("Top bar label, e.g. txt_topbar_gameWon.")]
        [SerializeField] private TMP_Text _gameWonTopBarTitle;
        [Tooltip("Top bar label, e.g. txt_topbar_gameWon_continue.")]
        [SerializeField] private TMP_Text _gameWonTopBarContinue;
        [Header("Game Error UI — runtime watchdog")]
        [Tooltip("Show GameError UI if runtime gets stuck (e.g. no aim/shoot or no enemy spawns for too long).")]
        [SerializeField] private bool _enableGameErrorWatchdog = true;
        [SerializeField] private float _autoHeroNoAimOrShootErrorSeconds = 60f;
        [SerializeField] private float _enemyNoSpawnErrorSeconds = 60f;
        [SerializeField] private GameObject _gameErrorRoot;
        [SerializeField] private GameObject _gameErrorContinueButton;
        [SerializeField] private TMP_Text _gameErrorTopBarTitle;
        [SerializeField] private TMP_Text _gameErrorTopBarContinue;
        [Tooltip("Optional detail text shown when GameError triggers (e.g. watchdog reason).")]
        [SerializeField] private TMP_Text _gameErrorReasonText;
        [Header("Top bar wave label (intro)")]
        [Tooltip("Fully visible duration after main menu Play or shop Continue before fade-out.")]
        [SerializeField] private float _topBarWaveTextVisibleSeconds = 4f;
        [Tooltip("Alpha fade duration after the hold.")]
        [SerializeField] private float _topBarWaveTextFadeOutSeconds = 0.75f;
        [Tooltip("When reload prompt is visible, pulse color between the label's base color and accent.")]
        [SerializeField] private bool _reloadPromptPulse = true;
        [SerializeField] private float _reloadPromptPulsePeriodSeconds = 0.85f;
        [SerializeField] private Color _reloadPromptPulseAccent = new Color(1f, 0.5f, 0.12f, 1f);

        [Header("Waves")]
        [SerializeField] private List<WaveConfig_V2> _waves = new List<WaveConfig_V2>();
        [Tooltip("Optional global multipliers per wave number (multiplied onto each WaveConfig_V2). Leave unassigned for identity scaling.")]
        [SerializeField]
        private WaveBalanceConfig_V2 _waveBalanceConfig;
        [SerializeField] private float _prepareDurationSeconds = 2f;
        [Header("Between-wave pressure reset")]
        [Tooltip("Apply a partial bunker heal after each cleared wave (before shop) to reduce carry-over pressure debt.")]
        [SerializeField] private bool _enableBetweenWavePressureReset = true;
        [Tooltip("Fraction of bunker max HP restored after each clear (0.15 = +15% max HP).")]
        [SerializeField] [Range(0f, 1f)] private float _betweenWaveBunkerHealFraction = 0.15f;
        [Tooltip("Extra Prepare delay before the next wave starts (breathing room).")]
        [SerializeField] private float _betweenWaveExtraPrepareSeconds = 1.5f;
        [Tooltip(
            "When EnemySpawner is used, waves end only when the spawner reports cleared (all drops + kills). " +
            "WaveConfig duration no longer cuts the wave short. This is a last-resort timeout if the spawner never clears.")]
        [SerializeField] private float _waveSpawnerStuckFailSafeSeconds = 300f;

        [Header("Economy")]
        [SerializeField] private int _startingCurrency = 100;
        [SerializeField] private int _healthPurchaseCost = 60;
        [SerializeField] private int _healthPurchaseAmount = 25;
        [SerializeField] private int _bunkerRepairCost = 80;
        [SerializeField] private int _bunkerRepairAmount = 25;
        [Tooltip("Starting bunker max HP for the run (can be raised via shop BunkerMaxUpgrade).")]
        [SerializeField] private int _bunkerMaxHealth = 250;
        [SerializeField] private int _startingBunkerHealth = 250;
        [Tooltip("Base cost for bunker max upgrade; scales with each purchase.")]
        [SerializeField] private int _bunkerMaxUpgradeBaseCost = 90;
        [Tooltip("HP added to bunker max (and current, up to new max) per upgrade.")]
        [SerializeField] private int _bunkerMaxUpgradeAmount = 25;
        [Tooltip("0 = no cap on bunker max from upgrades.")]
        [SerializeField] private int _bunkerMaxHealthCap;
        [Tooltip("Multiply cost after each completed purchase of that category (e.g. 1.08 ≈ +8%).")]
        [SerializeField] private float _shopCostScalePerPurchase = 1.08f;
        [Header("Death continue (3-layer)")]
        [SerializeField] private int _checkpointContinueCost = 120;
        [SerializeField] private int _clutchSaveCost = 200;
        [Tooltip("Checkpoint continue applies extra pressure to keep stakes high.")]
        [SerializeField] [Range(1f, 2f)] private float _checkpointEnemyPressureMultiplier = 1.2f;
        [Tooltip("Clutch save revives hero with this HP fraction and restarts current wave quickly.")]
        [SerializeField] [Range(0.1f, 1f)] private float _clutchReviveHealthFraction = 0.6f;
        [SerializeField] [Range(0f, 0.5f)] private float _restartRunPermanentDamageBonusStep = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool _debugWaveLogs = false;
        [SerializeField] private bool _debugCameraFollowLogs = false;
        [SerializeField] private KeyCode _nextWaveDebugKey = KeyCode.Return;

        private WaveLoopState_V2 _state = WaveLoopState_V2.Preparing;
        private int _waveIndex;
        private float _stateEndTime;
        private float _waveSpawnerFailSafeEndTime;
        private int _currency;
        private int _bunkerHealth;
        private int _bunkerMaxHealthRuntime;
        private int _healthPurchasesThisRun;
        private int _bunkerRepairsThisRun;
        private int _bunkerMaxUpgradesThisRun;
        private int _enemiesKilledThisWave;
        private Color _reloadPromptBaseColor = Color.white;
        private bool _reloadPromptBaseColorCached;
        private Transform _cachedBunkerRootTransform;
        private bool _bunkerRootResolveAttempted;
        private Coroutine _topBarWaveTextRoutine;
        private Coroutine _deferredTopBarWaveIntroRoutine;
        private Color _topBarWaveTextBaseColor = Color.white;
        private bool _topBarWaveTextBaseColorCached;
        private WaveRunScalingSnapshot _scalingForActiveWave;
        private bool _hasScalingForActiveWave;
        private AutoHero_V2 _autoHero;
        private float _inWaveEnteredUnscaledTime;
        private string _lastGameErrorReason = "";
        private float _extraPrepareDelaySecondsForNextWave;
        private float _continueEnemyPressureMultiplierRuntime = 1f;
        private static float s_restartRunPermanentDamageBonus01;

        public event Action<WaveLoopState_V2> OnStateChanged;
        public event Action<int, int, int> OnMetaChanged;

        /// <summary>Raised when <see cref="ApplyBunkerDamage"/> applies a positive amount (enemy fire, etc.).</summary>
        public event Action<int> OnBunkerDamaged;

        public WaveLoopState_V2 State => _state;
        public EnemySpawner_V2 EnemySpawner => _enemySpawner;
        public int CurrentWaveNumber => _waveIndex + 1;

        /// <summary>Unscaled seconds since this run entered <see cref="WaveLoopState_V2.InWave"/>; -1 if not InWave.</summary>
        public float InWaveElapsedUnscaledSec =>
            _state == WaveLoopState_V2.InWave ? Time.unscaledTime - _inWaveEnteredUnscaledTime : -1f;
        public int Currency => _currency;
        public int BunkerHealth => _bunkerHealth;
        public int BunkerMaxHealth => _bunkerMaxHealthRuntime;
        public ShopPanel_V2 ShopPanel => _shopPanel;
        public Hero_V2 Hero => _hero;
        /// <summary>Kill counter for the active wave (reset when a new wave starts).</summary>
        public int EnemiesKilledThisWave => _enemiesKilledThisWave;
        public int CheckpointContinueCost => Mathf.Max(0, _checkpointContinueCost);
        public int ClutchSaveCost => Mathf.Max(0, _clutchSaveCost);
        public float RestartRunPermanentDamageBonus01 => s_restartRunPermanentDamageBonus01;

        /// <summary>
        /// Last scaling snapshot for the wave that entered <see cref="WaveLoopState_V2.InWave"/> (still valid in Shop until the next wave starts).
        /// </summary>
        public bool TryGetScalingSnapshotForTelemetry(out WaveRunScalingSnapshot snapshot)
        {
            if (!_hasScalingForActiveWave)
            {
                snapshot = default;
                return false;
            }

            snapshot = _scalingForActiveWave;
            return true;
        }

        public bool TryGetLastGameErrorReason(out string reason)
        {
            reason = _lastGameErrorReason;
            return !string.IsNullOrWhiteSpace(reason);
        }

        /// <summary>
        /// Call from <see cref="MainMenu_V2"/> after Play: shows <c>Wave N</c> for <see cref="_topBarWaveTextVisibleSeconds"/>, then fades out.
        /// </summary>
        public void NotifyGameStartedFromMainMenu()
        {
            BeginTopBarWaveTextIntro();
        }

        /// <summary>
        /// Shows the top-bar wave label (hold + fade) using <see cref="CurrentWaveNumber"/>.
        /// </summary>
        private void BeginTopBarWaveTextIntro()
        {
            if (_topBarWaveTextRoutine != null)
            {
                StopCoroutine(_topBarWaveTextRoutine);
                _topBarWaveTextRoutine = null;
            }

            _topBarWaveTextRoutine = StartCoroutine(TopBarWaveTextIntroRoutine());
        }

        /// <summary>Enemy fire etc. — reduces current bunker HP and refreshes UI.</summary>
        public void ApplyBunkerDamage(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _bunkerHealth = Mathf.Max(0, _bunkerHealth - amount);
            Log($"Bunker took {amount} damage. hp={_bunkerHealth}/{_bunkerMaxHealthRuntime}");
            WaveRunTelemetry_V2.NotifyBunkerDamageTaken(amount);
            OnBunkerDamaged?.Invoke(amount);
            EmitMetaChanged();
        }

        /// <summary>
        /// When true, hero HP damage from enemies is blocked while in the bunker zone.
        /// If <see cref="BunkerHealth"/> is 0, cover is breached — always false (hero can be hit).
        /// </summary>
        public bool IsHeroInsideBunker()
        {
            return IsHeroInsideBunker(_hero);
        }

        public bool IsHeroInsideBunker(Hero_V2 hero)
        {
            if (hero == null || hero.IsDead())
            {
                return false;
            }

            if (_bunkerHealth <= 0)
            {
                return false;
            }

            Vector2 p = GetHeroWorldPointForBunkerCheck(hero);

            if (_bunkerHeroSafeZoneCollider != null)
            {
                return _bunkerHeroSafeZoneCollider.OverlapPoint(p);
            }

            BunkerInteriorZone_V2 zone =
                FindAnyObjectByType<BunkerInteriorZone_V2>(FindObjectsInactive.Include);
            if (zone != null && zone.ContainsWorldPoint(p))
            {
                return true;
            }

            return FallbackHeroInsideBunkerRootColliderBounds(p);
        }

        private static Vector2 GetHeroWorldPointForBunkerCheck(Hero_V2 hero)
        {
            Collider2D c = hero.GetComponentInChildren<Collider2D>();
            if (c != null)
            {
                return c.bounds.center;
            }

            return hero.transform.position;
        }

        private bool FallbackHeroInsideBunkerRootColliderBounds(Vector2 p)
        {
            Transform root = ResolveBunkerRootTransformCached();
            if (root == null)
            {
                return false;
            }

            Collider2D[] cols = root.GetComponentsInChildren<Collider2D>(true);
            if (cols == null || cols.Length == 0)
            {
                return false;
            }

            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++)
            {
                if (cols[i] != null)
                {
                    b.Encapsulate(cols[i].bounds);
                }
            }

            b.Expand(0.08f);
            Vector3 p3 = new Vector3(p.x, p.y, b.center.z);
            return b.Contains(p3);
        }

        private Transform ResolveBunkerRootTransformCached()
        {
            if (_cachedBunkerRootTransform != null)
            {
                return _cachedBunkerRootTransform;
            }

            if (_bunkerRootResolveAttempted)
            {
                return null;
            }

            _bunkerRootResolveAttempted = true;
            _cachedBunkerRootTransform = FindTransformByExactName("BunkerRoot");
            return _cachedBunkerRootTransform;
        }

        private static Transform FindTransformByExactName(string exactName)
        {
            Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t != null && t.gameObject.name.Equals(exactName, StringComparison.Ordinal))
                {
                    return t;
                }
            }

            return null;
        }

        private void Awake()
        {
            _currency = Mathf.Max(0, _startingCurrency);
            _bunkerMaxHealthRuntime = Mathf.Max(1, _bunkerMaxHealth);
            _bunkerHealth = Mathf.Clamp(_startingBunkerHealth, 0, _bunkerMaxHealthRuntime);
        }

        private void Start()
        {
            ResolveCameraFollowReferenceIfNeeded();
            ResolveTopBarReferencesIfNeeded();
            CacheTopBarWaveTextBaseColorIfNeeded();
            HideTopBarWaveTextImmediate();
            ResolveHeroDeathGameOverUiIfNeeded();
            SetHeroDeathGameOverUiVisible(false);
            ResolveGameWonUiIfNeeded();
            SetGameWonUiVisible(false);
            ResolveGameErrorUiIfNeeded();
            SetGameErrorUiVisible(false);
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
            if (_state != WaveLoopState_V2.GameOver &&
                _state != WaveLoopState_V2.GameWon &&
                _state != WaveLoopState_V2.GameError &&
                _hero != null &&
                _hero.IsDead())
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
            WaveRunTelemetry_V2.NotifyEnemyKilledForFeelKpis();

            // Do not complete wave from kill-count while spawner-driven delayed spawns
            // (e.g. aircraft -> drop-when-visible) may still be pending.
            if (_enemySpawner == null)
            {
                WaveConfig_V2 wave = GetCurrentWaveConfig();
                if (wave != null && _enemiesKilledThisWave >= wave.EnemyCount)
                {
                    CompleteWave();
                }
            }
        }

        public bool PurchaseHealth()
        {
            if (_state != WaveLoopState_V2.Shop || _hero == null || _hero.IsHealthFull())
            {
                return false;
            }

            int cost = GetHealthPurchaseCost();
            if (!TrySpend(cost))
            {
                return false;
            }

            _hero.Heal(_healthPurchaseAmount);
            _healthPurchasesThisRun++;
            Log($"Health purchased (+{_healthPurchaseAmount}) for {cost}.");
            WaveRunTelemetry_V2.NotifyShopPurchase("health_pack_top_bar", cost);
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

            if (GameplaySceneRules_V2.IsShopOfferBlocked(offer))
            {
                return false;
            }

            switch (offer.Kind)
            {
                case ShopOfferKind_V2.HealthPack:
                    if (_hero == null || _hero.IsHealthFull())
                    {
                        return false;
                    }

                    int healthCost = GetOfferEffectiveCost(offer);
                    if (!TrySpend(healthCost))
                    {
                        return false;
                    }

                    int heal = offer.HealthAmount > 0 ? offer.HealthAmount : _healthPurchaseAmount;
                    _hero.Heal(heal);
                    _healthPurchasesThisRun++;
                    Log($"Health purchased (+{heal}) for {healthCost}.");
                    WaveRunTelemetry_V2.NotifyShopPurchase("HealthPack", healthCost);
                    EmitMetaChanged();
                    return true;

                case ShopOfferKind_V2.BunkerRepair:
                    if (_bunkerHealth >= _bunkerMaxHealthRuntime)
                    {
                        return false;
                    }

                    int repairCost = GetOfferEffectiveCost(offer);
                    if (!TrySpend(repairCost))
                    {
                        return false;
                    }

                    int repair = offer.BunkerRepairAmount > 0 ? offer.BunkerRepairAmount : _bunkerRepairAmount;
                    _bunkerHealth = Mathf.Min(_bunkerMaxHealthRuntime, _bunkerHealth + repair);
                    _bunkerRepairsThisRun++;
                    Log($"Bunker repaired (+{repair}) for {repairCost}. hp={_bunkerHealth}/{_bunkerMaxHealthRuntime}");
                    WaveRunTelemetry_V2.NotifyShopPurchase("BunkerRepair", repairCost);
                    EmitMetaChanged();
                    return true;

                case ShopOfferKind_V2.BunkerMaxUpgrade:
                    if (IsBunkerMaxAtCap())
                    {
                        return false;
                    }

                    int delta = offer.BunkerMaxIncrease > 0 ? offer.BunkerMaxIncrease : _bunkerMaxUpgradeAmount;
                    if (delta <= 0)
                    {
                        return false;
                    }

                    if (_bunkerMaxHealthCap > 0)
                    {
                        int room = _bunkerMaxHealthCap - _bunkerMaxHealthRuntime;
                        if (room <= 0)
                        {
                            return false;
                        }

                        delta = Mathf.Min(delta, room);
                    }

                    int maxCost = GetOfferEffectiveCost(offer);
                    if (!TrySpend(maxCost))
                    {
                        return false;
                    }

                    _bunkerMaxHealthRuntime += delta;
                    _bunkerHealth = Mathf.Min(_bunkerMaxHealthRuntime, _bunkerHealth + delta);
                    _bunkerMaxUpgradesThisRun++;
                    Log(
                        $"Bunker max upgraded (+{delta} max) for {maxCost}. hp={_bunkerHealth}/{_bunkerMaxHealthRuntime}");
                    WaveRunTelemetry_V2.NotifyShopPurchase("BunkerMaxUpgrade", maxCost);
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

                    int weaponCost = Mathf.Max(0, offer.Cost);
                    if (!TrySpend(weaponCost))
                    {
                        return false;
                    }

                    bool added = _hero.UnlockWeapon(offer.Weapon, true);
                    if (!added)
                    {
                        _currency += weaponCost;
                        return false;
                    }

                    Log($"Weapon unlocked: {offer.Weapon.DisplayName} for {weaponCost}.");
                    WaveRunTelemetry_V2.NotifyShopPurchase("WeaponUnlock", weaponCost);
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

                    int ammoCost = Mathf.Max(0, offer.Cost);
                    if (!TrySpend(ammoCost))
                    {
                        return false;
                    }

                    bool refilled = _hero.TryRefillWeaponMagazine(offer.Weapon);
                    if (!refilled)
                    {
                        _currency += ammoCost;
                        return false;
                    }

                    Log($"Ammo refilled: {offer.Weapon.DisplayName} for {ammoCost}.");
                    WaveRunTelemetry_V2.NotifyShopPurchase("AmmoRefill", ammoCost);
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
            return _bunkerHealth >= _bunkerMaxHealthRuntime;
        }

        public bool IsBunkerMaxAtCap()
        {
            return _bunkerMaxHealthCap > 0 && _bunkerMaxHealthRuntime >= _bunkerMaxHealthCap;
        }

        public bool IsHeroHealthFull()
        {
            return _hero != null && _hero.IsHealthFull();
        }

        public bool CanAfford(int cost)
        {
            return _currency >= Mathf.Max(0, cost);
        }

        public bool TryChooseDeathContinue(DeathContinueTier_V2 tier)
        {
            switch (tier)
            {
                case DeathContinueTier_V2.RestartRun:
                    ChooseRestartRun();
                    return true;
                case DeathContinueTier_V2.CheckpointContinue:
                    return TryCheckpointContinue();
                case DeathContinueTier_V2.ClutchSave:
                    return TryClutchSave();
                default:
                    return false;
            }
        }

        public void ChooseRestartRun()
        {
            if (_state != WaveLoopState_V2.GameOver && _state != WaveLoopState_V2.GameError)
            {
                return;
            }

            s_restartRunPermanentDamageBonus01 =
                Mathf.Clamp01(s_restartRunPermanentDamageBonus01 + Mathf.Max(0f, _restartRunPermanentDamageBonusStep));
            Time.timeScale = 1f;
            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.path.Length > 0 ? active.path : active.name);
        }

        public bool TryCheckpointContinue()
        {
            if (!TryStartContinueFromGameOver(CheckpointContinueCost, 0.7f, true))
            {
                return false;
            }

            WaveRunTelemetry_V2.NotifyShopPurchase("DeathCheckpointContinue", CheckpointContinueCost);
            return true;
        }

        public bool TryClutchSave()
        {
            if (!TryStartContinueFromGameOver(ClutchSaveCost, _clutchReviveHealthFraction, false))
            {
                return false;
            }

            WaveRunTelemetry_V2.NotifyShopPurchase("DeathClutchSave", ClutchSaveCost);
            return true;
        }

        public bool PurchaseBunkerRepair()
        {
            if (_state != WaveLoopState_V2.Shop)
            {
                return false;
            }

            if (_bunkerHealth >= _bunkerMaxHealthRuntime)
            {
                return false;
            }

            int repairCost = GetScaledBunkerRepairCost();
            if (!TrySpend(repairCost))
            {
                return false;
            }

            _bunkerHealth = Mathf.Min(_bunkerMaxHealthRuntime, _bunkerHealth + _bunkerRepairAmount);
            _bunkerRepairsThisRun++;
            Log(
                $"Bunker repaired (+{_bunkerRepairAmount}) for {repairCost}. hp={_bunkerHealth}/{_bunkerMaxHealthRuntime}");
            WaveRunTelemetry_V2.NotifyShopPurchase("bunker_repair_top_bar", repairCost);
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
            if (_deferredTopBarWaveIntroRoutine != null)
            {
                StopCoroutine(_deferredTopBarWaveIntroRoutine);
                _deferredTopBarWaveIntroRoutine = null;
            }

            _deferredTopBarWaveIntroRoutine = StartCoroutine(DeferredTopBarWaveTextIntroNextFrame());
        }

        public int GetHealthPurchaseCost()
        {
            return GetScaledPurchaseCost(Mathf.Max(0, _healthPurchaseCost), _healthPurchasesThisRun);
        }

        public int GetBunkerRepairCost() => Mathf.Max(0, _bunkerRepairCost);
        
        public int GetScaledBunkerRepairCost()
        {
            return GetScaledPurchaseCost(Mathf.Max(0, _bunkerRepairCost), _bunkerRepairsThisRun);
        }

        public int GetBunkerMaxUpgradeCost()
        {
            return GetScaledPurchaseCost(Mathf.Max(0, _bunkerMaxUpgradeBaseCost), _bunkerMaxUpgradesThisRun);
        }

        /// <summary>
        /// Effective price for carousel UI and purchases (health / bunker max scale per buy; other kinds use offer.Cost).
        /// </summary>
        public int GetOfferEffectiveCost(ShopOfferConfig_V2 offer)
        {
            if (offer == null)
            {
                return 0;
            }

            switch (offer.Kind)
            {
                case ShopOfferKind_V2.HealthPack:
                {
                    int basis = offer.Cost > 0 ? offer.Cost : _healthPurchaseCost;
                    return GetScaledPurchaseCost(Mathf.Max(0, basis), _healthPurchasesThisRun);
                }
                case ShopOfferKind_V2.BunkerMaxUpgrade:
                {
                    int basis = offer.Cost > 0 ? offer.Cost : _bunkerMaxUpgradeBaseCost;
                    return GetScaledPurchaseCost(Mathf.Max(0, basis), _bunkerMaxUpgradesThisRun);
                }
                case ShopOfferKind_V2.BunkerRepair:
                {
                    int basis = offer.Cost > 0 ? offer.Cost : _bunkerRepairCost;
                    return GetScaledPurchaseCost(Mathf.Max(0, basis), _bunkerRepairsThisRun);
                }
                default:
                    return Mathf.Max(0, offer.Cost);
            }
        }

        private int GetScaledPurchaseCost(int baseCost, int completedPurchases)
        {
            float mult = Mathf.Max(1f, _shopCostScalePerPurchase);
            return Mathf.Max(1, Mathf.RoundToInt(baseCost * Mathf.Pow(mult, completedPurchases)));
        }

        private void TickInWaveState()
        {
            if (TryTriggerGameErrorFromWatchdog())
            {
                return;
            }

            WaveConfig_V2 wave = GetCurrentWaveConfig();
            if (wave == null)
            {
                EnterGameOverState();
                return;
            }

            if (_enemySpawner != null)
            {
                if (_enemySpawner.IsWaveCleared())
                {
                    CompleteWave();
                    return;
                }

                if (Time.time >= _waveSpawnerFailSafeEndTime)
                {
                    Log(
                        $"Wave {CurrentWaveNumber} force-completed (fail-safe): EnemySpawner did not report cleared before stuck timeout. " +
                        "Increase _waveSpawnerStuckFailSafeSeconds or fix spawner if this triggers in normal play.");
                    CompleteWave();
                }

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

            bool clearedLastWave = _waves != null && _waves.Count > 0 && _waveIndex >= _waves.Count - 1;
            if (clearedLastWave)
            {
                EnterGameWonState();
                return;
            }

            ApplyBetweenWavePressureReset();

            int reward = _hasScalingForActiveWave
                ? _scalingForActiveWave.EffectiveWaveRewardCurrency
                : wave.WaveRewardCurrency;
            _currency += reward;
            SetState(WaveLoopState_V2.Shop);
            SetCameraFollowEnabled(false);
            if (_shopPanel != null)
            {
                _shopPanel.Show();
                _shopPanel.Refresh();
            }
            Log($"Wave {CurrentWaveNumber} cleared. reward={reward}, currency={_currency}");
            EmitMetaChanged();
        }

        private void EnterPreparingState()
        {
            SetState(WaveLoopState_V2.Preparing);
            float prepare = Mathf.Max(0.1f, _prepareDurationSeconds) + Mathf.Max(0f, _extraPrepareDelaySecondsForNextWave);
            _extraPrepareDelaySecondsForNextWave = 0f;
            _stateEndTime = Time.time + prepare;
            _enemiesKilledThisWave = 0;
        }

        private void ApplyBetweenWavePressureReset()
        {
            if (!_enableBetweenWavePressureReset)
            {
                _extraPrepareDelaySecondsForNextWave = 0f;
                return;
            }

            int before = _bunkerHealth;
            int healAmount = Mathf.RoundToInt(Mathf.Max(0f, _betweenWaveBunkerHealFraction) * _bunkerMaxHealthRuntime);
            if (healAmount > 0)
            {
                _bunkerHealth = Mathf.Min(_bunkerMaxHealthRuntime, _bunkerHealth + healAmount);
            }

            _extraPrepareDelaySecondsForNextWave = Mathf.Max(0f, _betweenWaveExtraPrepareSeconds);
            if (_debugWaveLogs)
            {
                Log(
                    $"Between-wave pressure reset: bunker {before}->{_bunkerHealth}/{_bunkerMaxHealthRuntime}, " +
                    $"extraPrepare={_extraPrepareDelaySecondsForNextWave:0.##}s.");
            }
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
            float failSafeBasis = Mathf.Max(
                _waveSpawnerStuckFailSafeSeconds,
                wave.WaveDurationSeconds * 2f + 60f);
            _waveSpawnerFailSafeEndTime = Time.time + failSafeBasis;
            _enemiesKilledThisWave = 0;
            _inWaveEnteredUnscaledTime = Time.unscaledTime;
            if (_hero != null)
            {
                _autoHero = _hero.GetComponent<AutoHero_V2>();
            }
            _scalingForActiveWave = BuildScalingSnapshot(wave, CurrentWaveNumber);
            if (_continueEnemyPressureMultiplierRuntime > 1f)
            {
                _scalingForActiveWave = BuildContinuePressureSnapshot(_scalingForActiveWave, _continueEnemyPressureMultiplierRuntime);
            }
            _hasScalingForActiveWave = true;
            if (_enemySpawner != null)
            {
                _enemySpawner.BeginWave(
                    wave,
                    ReportEnemyKilled,
                    CurrentWaveNumber,
                    _scalingForActiveWave.EffectiveEnemyHpMultiplier,
                    _scalingForActiveWave.EffectiveEnemyDamageMultiplier,
                    _scalingForActiveWave.EffectiveSpawnIntervalSeconds);
            }
            Log(
                $"Wave {CurrentWaveNumber} started. enemies={wave.EnemyCount}, " +
                $"configDuration={wave.WaveDurationSeconds:0.0}s (not used as hard cap when spawner active), " +
                $"spawnerFailSafe={failSafeBasis:0.0}s");
            _continueEnemyPressureMultiplierRuntime = 1f;
        }

        private static WaveRunScalingSnapshot BuildContinuePressureSnapshot(
            WaveRunScalingSnapshot source,
            float pressureMultiplier)
        {
            float p = Mathf.Max(1f, pressureMultiplier);
            return new WaveRunScalingSnapshot(
                scalingVersion: source.ScalingVersion + "+continue",
                balanceEnemyHpMultiplier: source.BalanceEnemyHpMultiplier,
                balanceEnemyDamageMultiplier: source.BalanceEnemyDamageMultiplier,
                balanceSpawnRateMultiplier: source.BalanceSpawnRateMultiplier,
                balanceWaveRewardMultiplier: source.BalanceWaveRewardMultiplier,
                configEnemyHpMultiplier: source.ConfigEnemyHpMultiplier,
                configEnemyDamageMultiplier: source.ConfigEnemyDamageMultiplier,
                configSpawnIntervalSeconds: source.ConfigSpawnIntervalSeconds,
                configWaveRewardCurrency: source.ConfigWaveRewardCurrency,
                effectiveEnemyHpMultiplier: source.EffectiveEnemyHpMultiplier * p,
                effectiveEnemyDamageMultiplier: source.EffectiveEnemyDamageMultiplier * p,
                effectiveSpawnIntervalSeconds: source.EffectiveSpawnIntervalSeconds / p,
                effectiveWaveRewardCurrency: source.EffectiveWaveRewardCurrency);
        }

        private bool TryStartContinueFromGameOver(int cost, float reviveHealthFraction, bool applyCheckpointPressure)
        {
            if ((_state != WaveLoopState_V2.GameOver && _state != WaveLoopState_V2.GameError) ||
                _hero == null ||
                !TrySpend(cost))
            {
                return false;
            }

            if (!_hero.TryReviveWithHealthFraction(reviveHealthFraction))
            {
                _currency += cost;
                return false;
            }

            if (_enemySpawner != null)
            {
                _enemySpawner.StopWave();
            }

            SetHeroDeathGameOverUiVisible(false);
            SetGameErrorUiVisible(false);
            SetGameWonUiVisible(false);
            SetCameraFollowEnabled(true);
            _continueEnemyPressureMultiplierRuntime =
                applyCheckpointPressure ? Mathf.Max(1f, _checkpointEnemyPressureMultiplier) : 1f;
            _extraPrepareDelaySecondsForNextWave = 0f;
            EnterPreparingState();
            EmitMetaChanged();
            return true;
        }

        private WaveRunScalingSnapshot BuildScalingSnapshot(WaveConfig_V2 wave, int waveNumberOneBased)
        {
            WaveBalanceWaveRow balance = _waveBalanceConfig != null
                ? _waveBalanceConfig.ResolveRowForWave(waveNumberOneBased)
                : WaveBalanceWaveRow.Identity;
            string version = _waveBalanceConfig != null ? _waveBalanceConfig.ScalingVersion : "none";

            float cfgHp = wave.EnemyHealthMultiplier;
            float cfgDmg = wave.EnemyDamageMultiplier;
            float cfgInterval = wave.SpawnIntervalSeconds;
            int cfgReward = wave.WaveRewardCurrency;

            float effHp = cfgHp * balance.enemyHpMultiplier;
            float effDmg = cfgDmg * balance.enemyDamageMultiplier;
            float effInterval = cfgInterval / balance.spawnRateMultiplier;
            int effReward = Mathf.Max(0, Mathf.RoundToInt(cfgReward * balance.waveRewardMultiplier));

            return new WaveRunScalingSnapshot(
                scalingVersion: version,
                balanceEnemyHpMultiplier: balance.enemyHpMultiplier,
                balanceEnemyDamageMultiplier: balance.enemyDamageMultiplier,
                balanceSpawnRateMultiplier: balance.spawnRateMultiplier,
                balanceWaveRewardMultiplier: balance.waveRewardMultiplier,
                configEnemyHpMultiplier: cfgHp,
                configEnemyDamageMultiplier: cfgDmg,
                configSpawnIntervalSeconds: cfgInterval,
                configWaveRewardCurrency: cfgReward,
                effectiveEnemyHpMultiplier: effHp,
                effectiveEnemyDamageMultiplier: effDmg,
                effectiveSpawnIntervalSeconds: effInterval,
                effectiveWaveRewardCurrency: effReward);
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

            bool heroDeath = _hero != null && _hero.IsDead();
            ResolveHeroDeathGameOverUiIfNeeded();
            if (heroDeath)
            {
                SetHeroDeathGameOverUiVisible(true);
                if (_gameOverUi == null)
                {
                    _gameOverUi = FindAnyObjectByType<GameOverUI_V2>(FindObjectsInactive.Include);
                }

                _gameOverUi?.Show();
            }
            else
            {
                SetHeroDeathGameOverUiVisible(false);
            }

            Log($"WaveManager entered GameOver (heroDeath={heroDeath}).");
        }

        private void EnterGameWonState()
        {
            SetState(WaveLoopState_V2.GameWon);
            if (_enemySpawner != null)
            {
                _enemySpawner.StopWave();
            }
            if (_shopPanel != null)
            {
                _shopPanel.Hide();
            }
            SetCameraFollowEnabled(false);
            SetHeroDeathGameOverUiVisible(false);
            SetGameErrorUiVisible(false);
            ResolveGameWonUiIfNeeded();
            SetGameWonUiVisible(true);
            Log($"WaveManager entered GameWon at wave {CurrentWaveNumber}.");
        }

        private string BuildGameErrorDiagnosticsSnapshot()
        {
            var sb = new StringBuilder(2048);
            sb.Append("time: realtimeSinceStartup=");
            sb.Append(Time.realtimeSinceStartup.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(" unscaledTime=");
            sb.Append(Time.unscaledTime.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(" timeSinceLevelLoad=");
            sb.Append(Time.timeSinceLevelLoad.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(" timeScale=");
            sb.Append(Time.timeScale.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(" frame=");
            sb.Append(Time.frameCount);
            sb.Append(" activeScene=");
            Scene activeScene = SceneManager.GetActiveScene();
            sb.Append(activeScene.path.Length > 0 ? activeScene.path : activeScene.name);
            sb.Append(" loadedScenes=");
            sb.Append(SceneManager.sceneCount);
            sb.Append(" isEditor=");
            sb.Append(Application.isEditor);
            sb.Append(" platform=");
            sb.Append(Application.platform.ToString());
            sb.Append(" internet=");
            sb.Append(Application.internetReachability.ToString());
            sb.Append(" gcManagedBytes=");
            sb.Append(GC.GetTotalMemory(false).ToString(CultureInfo.InvariantCulture));
            sb.AppendLine();

            sb.Append("waveLoop: state=");
            sb.Append(_state.ToString());
            sb.Append(" waveIndex0=");
            sb.Append(_waveIndex);
            sb.Append(" currentWave1=");
            sb.Append(CurrentWaveNumber);
            sb.Append(" inWaveElapsedUnscaledSec=");
            sb.Append(InWaveElapsedUnscaledSec.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(" wavesListCount=");
            sb.Append(_waves != null ? _waves.Count : 0);
            sb.AppendLine();

            sb.Append("economy: currency=");
            sb.Append(_currency);
            sb.Append(" bunkerHp=");
            sb.Append(_bunkerHealth);
            sb.Append(" bunkerMaxHp=");
            sb.Append(_bunkerMaxHealthRuntime);
            sb.AppendLine();

            WaveConfig_V2 waveCfg = GetCurrentWaveConfig();
            if (waveCfg != null)
            {
                sb.Append("waveConfig: enemyCount=");
                sb.Append(waveCfg.EnemyCount);
                sb.Append(" spawnIntervalSec=");
                sb.Append(waveCfg.SpawnIntervalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(" cfgHpMul=");
                sb.Append(waveCfg.EnemyHealthMultiplier.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(" cfgDmgMul=");
                sb.Append(waveCfg.EnemyDamageMultiplier.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(" bomberPasses=");
                sb.Append(waveCfg.BomberPassCount);
                sb.Append(" kamikazeDrones=");
                sb.Append(waveCfg.KamikazeDroneCount);
                sb.Append(" bombDrones=");
                sb.Append(waveCfg.BombDroneCount);
                sb.Append(" rewardCurrency=");
                sb.Append(waveCfg.WaveRewardCurrency);
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("waveConfig: (null)");
            }

            if (_hasScalingForActiveWave)
            {
                WaveRunScalingSnapshot s = _scalingForActiveWave;
                sb.Append("scalingActiveWave: ver=");
                sb.Append(s.ScalingVersion);
                sb.Append(" effHp=");
                sb.Append(s.EffectiveEnemyHpMultiplier.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(" effDmg=");
                sb.Append(s.EffectiveEnemyDamageMultiplier.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(" effSpawnInt=");
                sb.Append(s.EffectiveSpawnIntervalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(" effReward=");
                sb.Append(s.EffectiveWaveRewardCurrency);
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("scalingActiveWave: (none)");
            }

            sb.Append("watchdog: enable=");
            sb.Append(_enableGameErrorWatchdog);
            sb.Append(" noAimShootSec=");
            sb.Append(_autoHeroNoAimOrShootErrorSeconds.ToString("0.#", CultureInfo.InvariantCulture));
            sb.Append(" noSpawnSec=");
            sb.Append(_enemyNoSpawnErrorSeconds.ToString("0.#", CultureInfo.InvariantCulture));
            sb.AppendLine();

            if (_hero != null)
            {
                sb.Append("hero: hp=");
                sb.Append(_hero.GetCurrentHealth());
                sb.Append("/");
                sb.Append(_hero.GetMaxHealth());
                sb.Append(" dead=");
                sb.Append(_hero.IsDead());
                sb.Append(" weapon=");
                sb.Append(_hero.GetCurrentWeaponDisplayName());
                sb.Append(" type=");
                sb.Append(_hero.CurrentWeaponType.ToString());
                sb.Append(" ammo=");
                sb.Append(_hero.GetCurrentWeaponAmmo());
                sb.Append("/");
                sb.Append(_hero.GetCurrentWeaponMaxAmmo());
                sb.Append(" reserve=");
                sb.Append(_hero.GetCurrentWeaponReserveAmmo());
                sb.Append(" pos=");
                sb.Append(_hero.transform.position.x.ToString("0.##", CultureInfo.InvariantCulture));
                sb.Append(",");
                sb.Append(_hero.transform.position.y.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("hero: (null)");
            }

            if (_autoHero != null && _autoHero.isActiveAndEnabled)
            {
                float now = Time.unscaledTime;
                sb.Append("autoHero: lastAimUnscaledAge=");
                sb.Append((now - _autoHero.LastAimAtEnemyUnscaledTime).ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append("s lastShootHeldUnscaledAge=");
                sb.Append((now - _autoHero.LastShootHeldUnscaledTime).ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append("s");
                sb.AppendLine();
            }
            else if (_autoHero != null)
            {
                sb.AppendLine("autoHero: attached but inactive/disabled");
            }
            else
            {
                sb.AppendLine("autoHero: (null)");
            }

            if (_enemySpawner != null)
            {
                sb.Append("enemySpawner: ");
                sb.AppendLine(_enemySpawner.BuildDiagnosticsSnapshotForTelemetry());
            }
            else
            {
                sb.AppendLine("enemySpawner: (null)");
            }

            return sb.ToString();
        }

        private void EnterGameErrorState(string reason)
        {
            // Telemetry and other OnStateChanged listeners read this immediately; set before SetState
            // so WaveRunTelemetry_V2 can emit game_error:<detail> on the same callback tick.
            _lastGameErrorReason = string.IsNullOrWhiteSpace(reason) ? "Unknown error." : reason.Trim();
            string diagnostics = BuildGameErrorDiagnosticsSnapshot();
            Debug.LogError(
                "[WaveManager_V2] GameError — " + _lastGameErrorReason + "\n" + diagnostics);
            WaveRunTelemetry_V2.RecordSyntheticTelemetryError(
                "GameError",
                _lastGameErrorReason + "\n--- diagnostics (pre StopWave / pre state listeners) ---\n" + diagnostics);

            SetState(WaveLoopState_V2.GameError);
            if (_enemySpawner != null)
            {
                _enemySpawner.StopWave();
            }

            if (_shopPanel != null)
            {
                _shopPanel.Hide();
            }

            SetCameraFollowEnabled(false);
            SetHeroDeathGameOverUiVisible(false);
            SetGameWonUiVisible(false);
            ResolveGameErrorUiIfNeeded();
            SetGameErrorUiVisible(true);
            Log($"WaveManager entered GameError. reason={_lastGameErrorReason}");
        }

        private void ResolveHeroDeathGameOverUiIfNeeded()
        {
            if (_heroDeathGameOverRoot == null)
            {
                Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];
                    if (t == null || t.parent != null)
                    {
                        continue;
                    }

                    if (t.gameObject.name.Equals("GameOver", StringComparison.Ordinal))
                    {
                        _heroDeathGameOverRoot = t.gameObject;
                        break;
                    }
                }
            }

            if (_heroDeathTopBarTitle == null)
            {
                _heroDeathTopBarTitle = FindTmpInLoadedScenes("txt_topbar_gameOver");
            }

            if (_heroDeathContinueButton == null)
            {
                _heroDeathContinueButton = FindGameObjectInLoadedScenes("btn_gameOver_continue");
                if (_heroDeathContinueButton == null)
                {
                    _heroDeathContinueButton = FindGameObjectInLoadedScenes("bkg_gameOver_continue");
                }
            }

            if (_heroDeathTopBarContinue == null)
            {
                _heroDeathTopBarContinue = FindTmpInLoadedScenes("txt_topbar_gameOver_continue");
            }
        }

        private static TMP_Text FindTmpInLoadedScenes(string exactName)
        {
            if (string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text t = texts[i];
                if (t != null && t.gameObject.name.Equals(exactName, StringComparison.Ordinal))
                {
                    return t;
                }
            }

            return null;
        }

        private static GameObject FindGameObjectInLoadedScenes(string exactName)
        {
            if (string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject go = objects[i];
                if (go != null && go.name.Equals(exactName, StringComparison.Ordinal))
                {
                    return go;
                }
            }

            return null;
        }

        private void SetHeroDeathGameOverUiVisible(bool visible)
        {
            if (_heroDeathGameOverRoot != null)
            {
                _heroDeathGameOverRoot.SetActive(visible);
            }

            if (_heroDeathContinueButton != null)
            {
                _heroDeathContinueButton.SetActive(visible);
            }

            if (_heroDeathTopBarTitle != null)
            {
                _heroDeathTopBarTitle.gameObject.SetActive(visible);
            }

            if (_heroDeathTopBarContinue != null)
            {
                _heroDeathTopBarContinue.gameObject.SetActive(visible);
            }
        }

        private void ResolveGameWonUiIfNeeded()
        {
            if (_gameWonRoot == null)
            {
                _gameWonRoot = FindGameObjectInLoadedScenes("GameWon");
            }

            if (_gameWonContinueButton == null)
            {
                _gameWonContinueButton = FindGameObjectInLoadedScenes("btn_gameWon_continue");
            }

            if (_gameWonTopBarTitle == null)
            {
                _gameWonTopBarTitle = FindTmpInLoadedScenes("txt_topbar_gameWon");
            }

            if (_gameWonTopBarContinue == null)
            {
                _gameWonTopBarContinue = FindTmpInLoadedScenes("txt_topbar_gameWon_continue");
            }
        }

        private void SetGameWonUiVisible(bool visible)
        {
            if (_gameWonRoot != null)
            {
                _gameWonRoot.SetActive(visible);
            }

            if (_gameWonContinueButton != null)
            {
                _gameWonContinueButton.SetActive(visible);
            }

            if (_gameWonTopBarTitle != null)
            {
                _gameWonTopBarTitle.gameObject.SetActive(visible);
            }

            if (_gameWonTopBarContinue != null)
            {
                _gameWonTopBarContinue.gameObject.SetActive(visible);
            }
        }

        private void ResolveGameErrorUiIfNeeded()
        {
            if (_gameErrorRoot == null)
            {
                _gameErrorRoot = FindGameObjectInLoadedScenes("GameError");
            }

            if (_gameErrorContinueButton == null)
            {
                _gameErrorContinueButton = FindGameObjectInLoadedScenes("btn_gameError_continue");
            }

            if (_gameErrorTopBarTitle == null)
            {
                _gameErrorTopBarTitle = FindTmpInLoadedScenes("txt_topbar_gameError");
            }

            if (_gameErrorTopBarContinue == null)
            {
                _gameErrorTopBarContinue = FindTmpInLoadedScenes("txt_topbar_gameError_continue");
            }

            if (_gameErrorReasonText == null)
            {
                _gameErrorReasonText = FindTmpInLoadedScenes("txt_topbar_gameError_reason");
            }
        }

        private void SetGameErrorUiVisible(bool visible)
        {
            if (_gameErrorRoot != null)
            {
                _gameErrorRoot.SetActive(visible);
            }

            if (_gameErrorContinueButton != null)
            {
                _gameErrorContinueButton.SetActive(visible);
            }

            if (_gameErrorTopBarTitle != null)
            {
                _gameErrorTopBarTitle.gameObject.SetActive(visible);
            }

            if (_gameErrorTopBarContinue != null)
            {
                _gameErrorTopBarContinue.gameObject.SetActive(visible);
            }

            if (_gameErrorReasonText != null)
            {
                _gameErrorReasonText.gameObject.SetActive(visible);
                if (visible)
                {
                    _gameErrorReasonText.text = _lastGameErrorReason;
                }
            }
        }

        private bool TryTriggerGameErrorFromWatchdog()
        {
            if (!_enableGameErrorWatchdog || _state != WaveLoopState_V2.InWave)
            {
                return false;
            }

            float now = Time.unscaledTime;
            bool spawnStarvedNoLiving =
                _enemySpawner != null &&
                _enemySpawner.IsWaveActive &&
                _enemySpawner.IsSpawnStarvedThisWave &&
                _enemySpawner.GetLivingParatroopersTrackedCountForTelemetry() <= 0;

            if (_autoHero != null && _autoHero.isActiveAndEnabled)
            {
                float noAimOrShootSeconds = Mathf.Max(
                    0f,
                    now - Mathf.Max(_autoHero.LastAimAtEnemyUnscaledTime, _autoHero.LastShootHeldUnscaledTime));
                if (!spawnStarvedNoLiving &&
                    noAimOrShootSeconds >= Mathf.Max(5f, _autoHeroNoAimOrShootErrorSeconds))
                {
                    EnterGameErrorState(
                        $"AutoHero inactive: no aim/shoot for {noAimOrShootSeconds:0.0}s (threshold={_autoHeroNoAimOrShootErrorSeconds:0.0}s).");
                    return true;
                }
            }

            // Only while more paratroopers are still expected from the spawn schedule. After the routine finishes
            // and spawnedCount >= target, minutes can pass with no new drops while the player clears stragglers.
            if (_enemySpawner != null &&
                _enemySpawner.IsWaveActive &&
                !_enemySpawner.HasFinishedScheduledParatrooperSpawnsThisWave)
            {
                float noSpawnSeconds = Mathf.Max(0f, now - _enemySpawner.LastParatrooperSpawnUnscaledTime);
                if (noSpawnSeconds >= Mathf.Max(5f, _enemyNoSpawnErrorSeconds))
                {
                    if (_enemySpawner.IsSpawnStarvedThisWave &&
                        _enemySpawner.GetLivingParatroopersTrackedCountForTelemetry() <= 0)
                    {
                        if (_enemySpawner.TryRecoverSpawnStarvation(out string recoveryDetails))
                        {
                            Debug.LogWarning(
                                "[WaveManager_V2] Watchdog detected spawn starvation and recovered one spawn. " +
                                recoveryDetails);
                            return false;
                        }

                        EnterGameErrorState(
                            $"Spawn starvation: no spawns for {noSpawnSeconds:0.0}s " +
                            $"(threshold={_enemyNoSpawnErrorSeconds:0.0}s), target={_enemySpawner.TargetParatroopersThisWave}, " +
                            $"spawned={_enemySpawner.SpawnedParatroopersThisWave}, pending={_enemySpawner.PendingParatrooperDropsThisWave}, " +
                            $"living={_enemySpawner.GetLivingParatroopersTrackedCountForTelemetry()}, " +
                            $"exit='{_enemySpawner.SpawnRoutineExitReason}', lastAbort='{_enemySpawner.LastSpawnAbortReason}'.");
                        return true;
                    }

                    EnterGameErrorState(
                        $"No enemy spawns for {noSpawnSeconds:0.0}s (threshold={_enemyNoSpawnErrorSeconds:0.0}s), " +
                        $"spawnedThisWave={_enemySpawner.SpawnedParatroopersThisWave}.");
                    return true;
                }
            }

            return false;
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

            if (_topBarReloadText == null)
            {
                _topBarReloadText = FindTextInSceneByName("txt_topbar_reload");
            }

            if (_topBarBunkerHealthText == null)
            {
                _topBarBunkerHealthText = FindTextInSceneByName("txt_topbar_bunkerHealth");
            }

            if (_topBarWaveText == null)
            {
                _topBarWaveText = FindTextInSceneByName("txt_topbar_waveText");
            }
        }

        private void RefreshTopBar()
        {
            ResolveTopBarReferencesIfNeeded();

            if (_topBarBunkerHealthText != null)
            {
                _topBarBunkerHealthText.text = $"Bunker: {_bunkerHealth}/{_bunkerMaxHealthRuntime}";
            }

            if (_topBarBunkerHealthFill != null)
            {
                int maxBunker = Mathf.Max(1, _bunkerMaxHealthRuntime);
                _topBarBunkerHealthFill.fillAmount = Mathf.Clamp01((float)_bunkerHealth / maxBunker);
            }

            if (_hero == null)
            {
                return;
            }

            if (_topBarHealthText != null)
            {
                _topBarHealthText.text = $"HP: {_hero.GetCurrentHealth()}/{_hero.GetMaxHealth()}";
            }

            if (_topBarHeroHealthFill != null)
            {
                int maxHero = Mathf.Max(1, _hero.GetMaxHealth());
                _topBarHeroHealthFill.fillAmount = Mathf.Clamp01((float)_hero.GetCurrentHealth() / maxHero);
            }

            if (_topBarCurrentWeaponText != null)
            {
                _topBarCurrentWeaponText.text = $"Weapon: {_hero.GetCurrentWeaponDisplayName()}";
            }

            if (_topBarCurrentAmmoText != null)
            {
                bool isColt45 = _hero.CurrentWeaponType == WeaponType.Colt45;
                string reserveText = isColt45
                    ? "∞"
                    : _hero.GetCurrentWeaponReserveAmmo().ToString(CultureInfo.InvariantCulture);
                _topBarCurrentAmmoText.text =
                    $"Ammo: {_hero.GetCurrentWeaponAmmo()}/{reserveText}";
            }

            if (_topBarReloadText != null)
            {
                if (!_reloadPromptBaseColorCached)
                {
                    _reloadPromptBaseColor = _topBarReloadText.color;
                    _reloadPromptBaseColorCached = true;
                }

                bool showReloadPrompt = _hero.ShouldShowReloadPrompt();
                _topBarReloadText.gameObject.SetActive(showReloadPrompt);
                if (showReloadPrompt)
                {
                    if (_reloadPromptPulse)
                    {
                        float period = Mathf.Max(0.08f, _reloadPromptPulsePeriodSeconds);
                        float t = Mathf.PingPong(Time.time, period) / period;
                        _topBarReloadText.color = Color.Lerp(_reloadPromptBaseColor, _reloadPromptPulseAccent, t);
                    }
                    else
                    {
                        _topBarReloadText.color = _reloadPromptBaseColor;
                    }
                }
                else
                {
                    _topBarReloadText.color = _reloadPromptBaseColor;
                }
            }
        }

        private void CacheTopBarWaveTextBaseColorIfNeeded()
        {
            if (_topBarWaveTextBaseColorCached || _topBarWaveText == null)
            {
                return;
            }

            Color c = _topBarWaveText.color;
            c.a = 1f;
            _topBarWaveTextBaseColor = c;
            _topBarWaveTextBaseColorCached = true;
        }

        private void HideTopBarWaveTextImmediate()
        {
            if (_topBarWaveText == null)
            {
                return;
            }

            CacheTopBarWaveTextBaseColorIfNeeded();
            Color c = _topBarWaveTextBaseColor;
            c.a = 0f;
            _topBarWaveText.color = c;
        }

        /// <summary>
        /// After shop UI teardown, wait one frame so canvases/layout settle before showing the wave label.
        /// </summary>
        private IEnumerator DeferredTopBarWaveTextIntroNextFrame()
        {
            yield return null;
            BeginTopBarWaveTextIntro();
            _deferredTopBarWaveIntroRoutine = null;
        }

        /// <summary>
        /// Ensures ancestors from the wave label up to (and including) Topbar-canvas are active so TMP can render.
        /// </summary>
        private void EnsureTopbarBranchActiveForWaveLabel()
        {
            if (_topBarWaveText == null)
            {
                return;
            }

            Transform t = _topBarWaveText.transform;
            Transform topbarCanvas = null;
            Transform walk = t;
            while (walk != null)
            {
                if (walk.name.Equals("Topbar-canvas", StringComparison.OrdinalIgnoreCase))
                {
                    topbarCanvas = walk;
                    break;
                }

                walk = walk.parent;
            }

            if (topbarCanvas == null)
            {
                return;
            }

            walk = _topBarWaveText.transform;
            while (walk != null)
            {
                if (!walk.gameObject.activeSelf)
                {
                    walk.gameObject.SetActive(true);
                }

                if (walk == topbarCanvas)
                {
                    break;
                }

                walk = walk.parent;
            }
        }

        private IEnumerator TopBarWaveTextIntroRoutine()
        {
            ResolveTopBarReferencesIfNeeded();
            if (_topBarWaveText == null)
            {
                _topBarWaveTextRoutine = null;
                yield break;
            }

            EnsureTopbarBranchActiveForWaveLabel();
            CacheTopBarWaveTextBaseColorIfNeeded();
            _topBarWaveText.gameObject.SetActive(true);
            _topBarWaveText.text = $"Wave {CurrentWaveNumber}";
            Color c = _topBarWaveTextBaseColor;
            c.a = 1f;
            _topBarWaveText.color = c;

            float hold = Mathf.Max(0f, _topBarWaveTextVisibleSeconds);
            if (hold > 0f)
            {
                yield return new WaitForSecondsRealtime(hold);
            }

            float fade = Mathf.Max(0.01f, _topBarWaveTextFadeOutSeconds);
            float t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(t / fade);
                c = _topBarWaveTextBaseColor;
                c.a = a;
                _topBarWaveText.color = c;
                yield return null;
            }

            HideTopBarWaveTextImmediate();
            _topBarWaveTextRoutine = null;
        }

        private static TMP_Text FindTextInSceneByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            string wanted = objectName.Trim();
            TMP_Text[] allTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < allTexts.Length; i++)
            {
                TMP_Text current = allTexts[i];
                if (current == null)
                {
                    continue;
                }

                string n = current.gameObject.name.Trim();
                if (n.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }
            }

            return null;
        }
    }
}
