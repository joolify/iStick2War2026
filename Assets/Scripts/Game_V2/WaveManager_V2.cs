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
    /*
 * WaveLoopState_V2 (Run loop phase)
 *
 * PURPOSE:
 * Discriminates WaveManager_V2 phases for UI, spawner gates, and telemetry: prepare countdown, active wave,
 * shop, terminal GameOver / GameWon, or GameError when watchdogs fire.
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Public enum at namespace scope so callers avoid stringly-typed state; values are ordered for simple comparisons only.
 *
 * NAVIGATION: read alongside WaveManager_V2 (phase gates), WaveRunTelemetry_V2 (snapshots), EnemySpawner_V2 (InWave spawn).
 */
    public enum WaveLoopState_V2
    {
        Preparing,
        InWave,
        Shop,
        LifeOver,
        GameOver,
        GameWon,
        GameError
    }

    /*
 * DeathContinueTier_V2 (Hero death continue policy tier)
 *
 * PURPOSE:
 * Selects which WaveManager_V2 death-continue path is active after hero death: full restart, checkpoint continue
 * with extra pressure, or clutch revive with configured HP fraction and cost scaling.
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Separate enum from WaveLoopState_V2 so economy / UI can branch on continue policy without overloading loop states.
 *
 * NAVIGATION: only WaveManager_V2 mutates paths after hero death; GameOverUI_V2 / shop UI consume outcomes.
 */
    public enum DeathContinueTier_V2
    {
        RestartRun,
        CheckpointContinue,
        ClutchSave
    }

    /*
 * WaveManager_V2 (Run / wave loop orchestration)
 *
 * PURPOSE:
 * Owns the high-level gameplay loop: Prepare → InWave → Shop, plus GameOver, GameWon, and GameError.
 * Tracks currency, bunker and hero meta, applies wave configs (with optional WaveBalanceConfig_V2 scaling),
 * drives EnemySpawner_V2 for the active wave, coordinates shop UI, top bar, death-continue tiers, run lives,
 * and telemetry-friendly events.
 *
 * ---------------------------------------------------------
 * KEY DEPENDENCIES
 *
 * - Hero_V2, ShopPanel_V2, EnemySpawner_V2 (serialized references).
 * - WaveConfig_V2 list + optional WaveBalanceConfig_V2 for per-wave tuning.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Implement per-enemy AI or per-paratrooper animation (entity *_V2 scripts).
 * - Encode low-level spawn placement rules that belong in EnemySpawner_V2 (spawner owns prefab / anchor logic).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2 + hero)
 *
 * Wave / shop data → WaveConfig_V2.cs, ShopOfferConfig_V2.cs (offers list on ShopPanel_V2)
 * Spawning → EnemySpawner_V2.cs
 * Shop UI → ShopPanel_V2.cs (+ ShopBuyButton_V2 / ShopNavArrow_V2 / ShopStartWaveButton_V2)
 * Scene policy / Colt-only → GameplaySceneProfile_V2.cs, GameplaySceneRules_V2.cs, GameplaySceneProfileApplier_V2.cs
 * Run telemetry → WaveRunTelemetry_V2.cs
 * Hero stack → Assets/Scripts/Hero_V2/Hero_V2.cs
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Single place for "what phase is the run in?" and economy / bunker pressure; delegate spawning to EnemySpawner_V2.
 */
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
        [SerializeField] private TMP_Text _topBarWaveCountText;
        [Header("Game Over UI")]
        [Tooltip("Optional; if unset, resolved once when entering Game Over. Shown only when the hero is dead.")]
        [SerializeField] private GameOverUI_V2 _gameOverUi;
        [Header("Game Over UI — hero death only")]
        [Tooltip("e.g. world-space GameOver root. Hidden until Hero_V2 dies.")]
        [SerializeField] private GameObject _heroDeathGameOverRoot;
        [Tooltip("Child button on GameOver root, e.g. btn_gameOver_continue / bkg_gameOver_continue.")]
        [SerializeField] private GameObject _heroDeathContinueButton;
        [Tooltip("Instantiated under GameOver when the root has no continue child.")]
        [SerializeField] private GameObject _gameOverContinueButtonPrefab;
        [Tooltip("Top bar label, e.g. txt_topbar_gameOver. Hidden until Hero_V2 dies.")]
        [SerializeField] private TMP_Text _heroDeathTopBarTitle;
        [Tooltip("Top bar label, e.g. txt_topbar_gameOver_continue. Hidden until Hero_V2 dies.")]
        [SerializeField] private TMP_Text _heroDeathTopBarContinue;
        [Header("Game Won UI — final wave clear")]
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
        [SerializeField] private int _startingCurrency = 750;
        [SerializeField] private int _healthPurchaseCost = 79;
        [SerializeField] private int _healthPurchaseAmount = 25;
        [SerializeField] private int _bunkerRepairCost = 425;
        [SerializeField] private int _bunkerRepairAmount = 25;
        [Tooltip("Starting bunker max HP for the run (can be raised via shop BunkerMaxUpgrade).")]
        [SerializeField] private int _bunkerMaxHealth = 250;
        [SerializeField] private int _startingBunkerHealth = 250;
        [Tooltip("Base cost for bunker max upgrade (flat; does not scale per purchase).")]
        [SerializeField] private int _bunkerMaxUpgradeBaseCost = 200;
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
        [Header("Run lives")]
        [SerializeField] private int _maxLivesPerRun = 3;
        [Tooltip("LifeOver UI root, e.g. LifeOver-canvas (preferred) or LifeOver V2.")]
        [SerializeField] private GameObject _lifeOverRoot;
        [Tooltip("Life lost message, e.g. txt_lifeOver_info on LifeOver-canvas.")]
        [SerializeField] private TMP_Text _lifeOverInfoText;
        [Tooltip("Start label on LifeOver, e.g. txt_lifeOver_startNewGame on LifeOver-canvas.")]
        [SerializeField] private TMP_Text _lifeOverStartNewGameText;
        [Tooltip("Continue control, e.g. TextBTN_MediumStartNewGame (LifeOverNavButton_V2).")]
        [SerializeField] private GameObject _lifeOverStartNewGameButton;
        [Tooltip("Go-to-shop label, e.g. txt_lifeOver_goToShop on LifeOver-canvas.")]
        [SerializeField] private TMP_Text _lifeOverGoToShopText;
        [Tooltip("Go-to-shop control, e.g. TextBTN_MediumGoToShop (LifeOverNavButton_V2).")]
        [SerializeField] private GameObject _lifeOverGoToShopButton;
        [Tooltip("Main-menu label, e.g. txt_lifeOver_goToMainMenu on LifeOver-canvas.")]
        [SerializeField] private TMP_Text _lifeOverGoToMainMenuText;
        [Tooltip("Main-menu control, e.g. TextBTN_MediumGoToMainMenu (LifeOverNavButton_V2).")]
        [SerializeField] private GameObject _lifeOverGoToMainMenuButton;
        [SerializeField] private string _lifeOverGoToShopLabel = "Go to shop";
        [SerializeField] private string _lifeOverGoToMainMenuLabel = "Main menu";
        [SerializeField] private string _lifeOverInfoMessage =
            "You died. Press \"Start Game\" to try the wave again";
        [Tooltip("Optional top-bar hold while LifeOver is visible.")]
        [SerializeField] private float _lifeLostTopBarHoldSeconds = 2.5f;
        [Tooltip("Seconds to wait after hero death before LifeOver UI appears (death animation beat).")]
        [SerializeField] private float _lifeOverShowDelaySeconds = 3f;
        [Tooltip("Seconds to wait after final life before GameOver UI appears. Uses Life Over Show Delay when <= 0.")]
        [SerializeField] private float _gameOverShowDelaySeconds = 3f;
        [SerializeField] private HeartLifeBar_V2 _heartLifeBar;
        [SerializeField] private GameOverContinueUi_V2 _gameOverContinueUi;

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
        private Coroutine _lifeOverRevealRoutine;
        private bool _lifeOverRevealPending;
        private Coroutine _gameOverRevealRoutine;
        private bool _gameOverRevealPending;
        private Color _topBarWaveTextBaseColor = Color.white;
        private bool _topBarWaveTextBaseColorCached;
        private WaveRunScalingSnapshot _scalingForActiveWave;
        private bool _hasScalingForActiveWave;
        private AutoHero_V2 _autoHero;
        private float _inWaveEnteredUnscaledTime;
        private string _lastGameErrorReason = "";
        private float _extraPrepareDelaySecondsForNextWave;
        private float _continueEnemyPressureMultiplierRuntime = 1f;
        private HeroWeaponDefinition_V2 _lastShopPurchasedWeapon;
        private static float s_restartRunPermanentDamageBonus01;
        private static bool s_skipMainMenuRedirectOnce;
        private static bool s_notifyGameplayFromMainMenuPending;
        private static bool s_skipPrepareDelayAfterMainMenuPlay;
        private static bool s_loadSavedRunPending;
        private const string MainMenuSceneName = "MainMenuScene";
        private int _livesRemaining;
        private GameOverContinueUi_V2 _resolvedGameOverContinueUi;
        private Canvas _shopCanvasActivatedForLifeOver;
        // When true, leaving shop resumes the same wave (life lost -> shop -> retry) instead of advancing.
        private bool _shopExitRetriesSameWave;

        public event Action<WaveLoopState_V2> OnStateChanged;
        public event Action<int, int> OnLivesChanged;
        public event Action<int, int, int> OnMetaChanged;

        // Raised when ApplyBunkerDamage applies a positive amount (enemy fire, etc.).
        public event Action<int> OnBunkerDamaged;

        public WaveLoopState_V2 State => _state;
        public EnemySpawner_V2 EnemySpawner => _enemySpawner;
        public int CurrentWaveNumber => _waveIndex + 1;

        // Unscaled seconds since this run entered WaveLoopState_V2.InWave; -1 if not InWave.
        public float InWaveElapsedUnscaledSec =>
            _state == WaveLoopState_V2.InWave ? Time.unscaledTime - _inWaveEnteredUnscaledTime : -1f;
        public int Currency => _currency;
        public int BunkerHealth => _bunkerHealth;
        public int BunkerMaxHealth => _bunkerMaxHealthRuntime;
        public ShopPanel_V2 ShopPanel => _shopPanel;
        public Hero_V2 Hero => _hero;
        public int DefaultHealthPackHealAmount => _healthPurchaseAmount;
        public int DefaultBunkerRepairAmount => _bunkerRepairAmount;
        public int DefaultBunkerMaxUpgradeAmount => _bunkerMaxUpgradeAmount;
        // Kill counter for the active wave (reset when a new wave starts).
        public int EnemiesKilledThisWave => _enemiesKilledThisWave;
        public int CheckpointContinueCost => Mathf.Max(0, _checkpointContinueCost);
        public int ClutchSaveCost => Mathf.Max(0, _clutchSaveCost);
        public float RestartRunPermanentDamageBonus01 => s_restartRunPermanentDamageBonus01;
        public int LivesRemaining => _livesRemaining;
        public int MaxLivesPerRun => Mathf.Max(1, _maxLivesPerRun);

        // Last scaling snapshot for the wave that entered WaveLoopState_V2.InWave (still valid in Shop until the next wave starts).
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

        // Call from MainMenu_V2 before loading the gameplay scene, or after Play when menu lives in-scene.
        public static void MarkGameplayEnteredFromMainMenu()
        {
            s_skipMainMenuRedirectOnce = true;
            s_notifyGameplayFromMainMenuPending = true;
            s_skipPrepareDelayAfterMainMenuPlay = true;
        }

        // Main menu Continue: load SampleScene and restore run_save.json instead of starting a fresh run.
        public static void MarkLoadSavedRunPending()
        {
            s_loadSavedRunPending = true;
            MarkGameplayEnteredFromMainMenu();
        }

        public static bool HasSavedRunAvailable() => RunSaveService_V2.HasSave();

        // Call from MainMenu_V2 after Play: shows Wave N for _topBarWaveTextVisibleSeconds, then fades out.
        public void NotifyGameStartedFromMainMenu()
        {
            HideLifeOverUiCompletely();
            AudioManager_V2.SetGameplayMusic();
            BeginTopBarWaveTextIntro();
        }

        public void EnsureLifeOverUiHidden()
        {
            HideLifeOverUiCompletely();
        }

        // Shows the top-bar wave label (hold + fade) using CurrentWaveNumber.
        private void BeginTopBarWaveTextIntro()
        {
            BeginTopBarStatusIntro($"Wave {CurrentWaveNumber}", _topBarWaveTextVisibleSeconds);
        }

        private void BeginTopBarStatusIntro(string message, float holdSeconds)
        {
            if (_topBarWaveTextRoutine != null)
            {
                StopCoroutine(_topBarWaveTextRoutine);
                _topBarWaveTextRoutine = null;
            }

            _topBarWaveTextRoutine = StartCoroutine(TopBarStatusTextIntroRoutine(message, holdSeconds));
        }

        // Enemy fire etc. — reduces current bunker HP and refreshes UI.
        public void ApplyBunkerDamage(int amount)
        {
            ApplyBunkerDamage(amount, BunkerDamageTelemetrySource.Other);
        }

        // telemetrySource: forwarded to WaveRunTelemetry_V2 (bomb vs other bunker hits).
        public void ApplyBunkerDamage(int amount, BunkerDamageTelemetrySource telemetrySource)
        {
            if (amount <= 0)
            {
                return;
            }

            _bunkerHealth = Mathf.Max(0, _bunkerHealth - amount);
            Log($"Bunker took {amount} damage. hp={_bunkerHealth}/{_bunkerMaxHealthRuntime}");
            WorldShake_V2.AddImpulse(WorldShakeImpulseKind_V2.BunkerHit);
            WaveRunTelemetry_V2.NotifyBunkerDamageTaken(amount, telemetrySource);
            OnBunkerDamaged?.Invoke(amount);
            EmitMetaChanged();
        }

        // When true, hero HP damage from enemies is blocked while in the bunker zone.
        // If BunkerHealth is 0, cover is breached - always false (hero can be hit).
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
            AudioManager_V2.EnsureInstance();
            ResetRunLivesForNewRun();
            EnsureHeartLifeBar();
            EnsureGameOverContinueUi();
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
            HideLifeOverUiCompletely();
            if (_shopPanel != null)
            {
                _shopPanel.Initialize(this);
                _shopPanel.Hide();
            }

            if (TryRedirectBootToMainMenuScene())
            {
                return;
            }

            if (TryApplyPendingSavedRun())
            {
                return;
            }

            EnterPreparingState();
            if (s_notifyGameplayFromMainMenuPending)
            {
                s_notifyGameplayFromMainMenuPending = false;
                NotifyGameStartedFromMainMenu();
            }

            EmitMetaChanged();
            ApplyGameplayHudVisibility();
            RefreshTopBar();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                TrySaveActiveRun();
            }
        }

        private void OnApplicationQuit()
        {
            TrySaveActiveRun();
        }

        private bool TryApplyPendingSavedRun()
        {
            if (!s_loadSavedRunPending)
            {
                return false;
            }

            s_loadSavedRunPending = false;
            s_notifyGameplayFromMainMenuPending = false;
            s_skipPrepareDelayAfterMainMenuPlay = false;

            if (!RunSaveService_V2.TryLoad(out RunSaveFile_V2 save))
            {
                Log("[WaveManager_V2] Continue requested but no valid run save was found.");
                EnterPreparingState();
                EmitMetaChanged();
                RefreshTopBar();
                return true;
            }

            ApplyRunSave(save);
            EmitMetaChanged();
            RefreshTopBar();
            AudioManager_V2.SetGameplayMusic();
            return true;
        }

        private void ApplyRunSave(RunSaveFile_V2 save)
        {
            _waveIndex = Mathf.Max(0, save.waveIndex);
            _currency = Mathf.Max(0, save.currency);
            _bunkerMaxHealthRuntime = Mathf.Max(1, save.bunkerMaxHealth);
            _bunkerHealth = Mathf.Clamp(save.bunkerHealth, 0, _bunkerMaxHealthRuntime);
            _livesRemaining = Mathf.Clamp(save.livesRemaining, 0, MaxLivesPerRun);
            _healthPurchasesThisRun = Mathf.Max(0, save.healthPurchasesThisRun);
            _bunkerRepairsThisRun = Mathf.Max(0, save.bunkerRepairsThisRun);
            _bunkerMaxUpgradesThisRun = Mathf.Max(0, save.bunkerMaxUpgradesThisRun);
            _shopExitRetriesSameWave = save.shopExitRetriesSameWave;
            _continueEnemyPressureMultiplierRuntime = Mathf.Max(1f, save.continueEnemyPressureMultiplier);
            s_restartRunPermanentDamageBonus01 = Mathf.Clamp01(save.restartRunPermanentDamageBonus01);

            if (_hero != null && save.hero != null)
            {
                _hero.ApplyRunSaveHeroState(save.hero, _shopPanel);
            }

            WaveLoopState_V2 restoredState = (WaveLoopState_V2)save.loopState;
            RestoreLoopStateAfterSave(restoredState, save.shopOfferIndex);
        }

        private void RestoreLoopStateAfterSave(WaveLoopState_V2 restoredState, int shopOfferIndex)
        {
            HideLifeOverUiCompletely();
            SetHeroDeathGameOverUiVisible(false);
            SetGameWonUiVisible(false);
            SetGameErrorUiVisible(false);

            if (_enemySpawner != null)
            {
                _enemySpawner.StopWave();
            }

            switch (restoredState)
            {
                case WaveLoopState_V2.Shop:
                    SetState(WaveLoopState_V2.Shop);
                    SetCameraFollowEnabled(false);
                    if (_shopPanel != null)
                    {
                        _shopPanel.Show();
                        _shopPanel.SetCarouselOfferIndex(shopOfferIndex);
                        _shopPanel.Refresh();
                    }
                    break;
                case WaveLoopState_V2.LifeOver:
                    EnterLifeOverState();
                    break;
                case WaveLoopState_V2.InWave:
                    EnterInWaveState(allowDevWaveShortcuts: false);
                    break;
                case WaveLoopState_V2.Preparing:
                default:
                    s_skipPrepareDelayAfterMainMenuPlay = true;
                    EnterPreparingState();
                    break;
            }

            if (_hero != null)
            {
                _hero.RefreshWaveLoopCombatGate();
            }
        }

        private bool TrySaveActiveRun()
        {
            if (!CanPersistCurrentRunState())
            {
                return false;
            }

            RunSaveFile_V2 save = CaptureRunSaveSnapshot();
            if (save == null)
            {
                return false;
            }

            bool ok = RunSaveService_V2.TrySave(save);
            if (ok && _debugWaveLogs)
            {
                Log($"[WaveManager_V2] Run saved. wave={CurrentWaveNumber}, state={_state}, currency={_currency}");
            }

            return ok;
        }

        private bool CanPersistCurrentRunState()
        {
            return _state == WaveLoopState_V2.Preparing ||
                   _state == WaveLoopState_V2.InWave ||
                   _state == WaveLoopState_V2.Shop ||
                   _state == WaveLoopState_V2.LifeOver;
        }

        private RunSaveFile_V2 CaptureRunSaveSnapshot()
        {
            Scene active = SceneManager.GetActiveScene();
            RunSaveFile_V2 save = new RunSaveFile_V2
            {
                gameplaySceneName = active.name,
                waveIndex = _waveIndex,
                loopState = (int)_state,
                currency = _currency,
                bunkerHealth = _bunkerHealth,
                bunkerMaxHealth = _bunkerMaxHealthRuntime,
                livesRemaining = _livesRemaining,
                healthPurchasesThisRun = _healthPurchasesThisRun,
                bunkerRepairsThisRun = _bunkerRepairsThisRun,
                bunkerMaxUpgradesThisRun = _bunkerMaxUpgradesThisRun,
                shopExitRetriesSameWave = _shopExitRetriesSameWave,
                continueEnemyPressureMultiplier = _continueEnemyPressureMultiplierRuntime,
                shopOfferIndex = _shopPanel != null ? _shopPanel.GetCarouselOfferIndex() : 0,
                restartRunPermanentDamageBonus01 = s_restartRunPermanentDamageBonus01,
                hero = _hero != null ? _hero.CaptureRunSaveHeroState() : new HeroSaveBlock_V2()
            };
            return save;
        }

        private void PersistRunSaveIfPossible()
        {
            TrySaveActiveRun();
        }

        // Called before leaving SampleScene via pause menu (GameplayPauseButton_V2).
        public void PersistActiveRunSave()
        {
            TrySaveActiveRun();
        }

        private void ClearPersistedRunSave()
        {
            RunSaveService_V2.ClearSave();
        }

        private bool TryRedirectBootToMainMenuScene()
        {
            if (s_skipMainMenuRedirectOnce)
            {
                s_skipMainMenuRedirectOnce = false;
                return false;
            }

            if (_waveIndex != 0)
            {
                return false;
            }

            WaveConfig_V2 wave = GetCurrentWaveConfig();
            if (wave == null || !wave.StartAtMainMenuOnSceneLoad)
            {
                return false;
            }

            // Dev wave shortcuts need SampleScene to stay loaded (shop / LifeOver testing).
            if (wave.OpenShopDirectly || wave.OpenLifeOverDirectly || wave.OpenGameOverDirectly ||
                wave.OpenGameWonDirectly || wave.OpenGameErrorDirectly)
            {
                Log(
                    "[WaveManager_V2] StartAtMainMenuOnSceneLoad ignored: a dev wave shortcut is enabled on wave 1.");
                return false;
            }

            Log("[WaveManager_V2] Wave 1 StartAtMainMenuOnSceneLoad: loading MainMenuScene.");
            Time.timeScale = 0f;
            AudioManager_V2.SetMenuMusic();
            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
            return true;
        }

        private void Update()
        {
            if (_state != WaveLoopState_V2.GameOver &&
                _state != WaveLoopState_V2.GameWon &&
                _state != WaveLoopState_V2.GameError &&
                _state != WaveLoopState_V2.LifeOver &&
                !_lifeOverRevealPending &&
                !_gameOverRevealPending &&
                _hero != null &&
                _hero.IsDead())
            {
                HandleHeroDeathWhileInRun();
                return;
            }

            switch (_state)
            {
                case WaveLoopState_V2.LifeOver:
                    break;
                case WaveLoopState_V2.Preparing:
                    if (Time.time >= _stateEndTime)
                    {
                        EnterInWaveState();
                    }
                    break;
                case WaveLoopState_V2.InWave:
                    if (!_lifeOverRevealPending && !_gameOverRevealPending)
                    {
                        TickInWaveState();
                    }

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

        // Unified purchase for the shop carousel (configured on ShopPanel_V2).
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
                    _bunkerHealth = _bunkerMaxHealthRuntime;
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
                        return TryPurchaseAmmoOnWeaponRow(offer);
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

                    RememberShopPurchasedWeapon(offer.Weapon);
                    Log($"Weapon unlocked: {offer.Weapon.DisplayName} for {weaponCost} (full mag + reserve).");
                    WaveRunTelemetry_V2.NotifyShopPurchase("WeaponUnlock", weaponCost);
                    EmitMetaChanged();
                    return true;

                case ShopOfferKind_V2.AmmoRefill:
                    // Legacy rows: redirect to the weapon row for the same weapon if it exists.
                    if (offer.Weapon != null &&
                        _shopPanel != null &&
                        _shopPanel.TryGetWeaponRowOffer(offer.Weapon, out ShopOfferConfig_V2 weaponRow) &&
                        weaponRow != null)
                    {
                        return TryPurchaseAmmoOnWeaponRow(weaponRow);
                    }

                    return TryPurchaseAmmoOnWeaponRow(offer);

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

        private bool TryPurchaseAmmoOnWeaponRow(ShopOfferConfig_V2 weaponRowOffer)
        {
            if (_hero == null || weaponRowOffer == null || weaponRowOffer.Weapon == null)
            {
                return false;
            }

            HeroWeaponDefinition_V2 weapon = weaponRowOffer.Weapon;
            if (!_hero.HasWeaponUnlocked(weapon))
            {
                return false;
            }

            if (_hero.IsWeaponMagazineFull(weapon))
            {
                return false;
            }

            int ammoCost = ResolveAmmoRefillCostForWeaponRow(weaponRowOffer);
            if (!TrySpend(ammoCost))
            {
                return false;
            }

            bool refilled = _hero.TryRefillWeaponMagazine(weapon);
            if (!refilled)
            {
                _currency += ammoCost;
                return false;
            }

            RememberShopPurchasedWeapon(weapon);
            Log($"Ammo refilled: {weapon.DisplayName} for {ammoCost} (weapon row).");
            WaveRunTelemetry_V2.NotifyShopPurchase("AmmoRefill", ammoCost);
            EmitMetaChanged();
            return true;
        }

        private int ResolveAmmoRefillCostForWeaponRow(ShopOfferConfig_V2 weaponRowOffer)
        {
            if (weaponRowOffer == null)
            {
                return 0;
            }

            if (_shopPanel != null)
            {
                return _shopPanel.ResolveAmmoRefillCostForWeaponRow(weaponRowOffer);
            }

            return weaponRowOffer.AmmoRefillCost > 0 ? weaponRowOffer.AmmoRefillCost : Mathf.Max(0, weaponRowOffer.Cost);
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
            ClearPersistedRunSave();
            Time.timeScale = 1f;
            // Full scene reload resets wave index, economy defaults, bunker, and run lives.
            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.path.Length > 0 ? active.path : active.name);
        }

        private void HandleHeroDeathWhileInRun()
        {
            if (_livesRemaining > 0)
            {
                TryConsumeLifeAndRetryCurrentWave();
                return;
            }

            EnterGameOverState();
        }

        private void TryConsumeLifeAndRetryCurrentWave()
        {
            _livesRemaining = Mathf.Max(0, _livesRemaining - 1);
            EmitLivesChanged();

            if (_livesRemaining <= 0)
            {
                Log("Final life consumed. Entering Game Over.");
                BeginGameOverRevealAfterDeath();
                return;
            }

            if (_enemySpawner != null)
            {
                _enemySpawner.ClearActiveWaveCombatForLifeRetry("Hero life retry");
            }
            else
            {
                CombatProjectileCleanup_V2.DespawnAllActiveProjectiles();
                EnemyLootCleanup_V2.DespawnAllActiveGroundLoot();
            }

            _continueEnemyPressureMultiplierRuntime = 1f;
            _bunkerHealth = _bunkerMaxHealthRuntime;
            EmitMetaChanged();
            Log(
                $"Life lost. livesRemaining={_livesRemaining}, bunker restored to {_bunkerHealth}/{_bunkerMaxHealthRuntime}, " +
                $"retry wave={CurrentWaveNumber} (LifeOver in {Mathf.Max(0f, _lifeOverShowDelaySeconds):0.##}s).");

            CancelGameOverRevealRoutine();
            CancelLifeOverRevealRoutine();
            SetHeroDeathGameOverUiVisible(false);
            float delay = Mathf.Max(0f, _lifeOverShowDelaySeconds);
            if (delay <= 0f)
            {
                FinishLifeOverRevealAfterDeath();
                return;
            }

            _lifeOverRevealPending = true;
            _lifeOverRevealRoutine = StartCoroutine(LifeOverRevealAfterDelay(delay));
        }

        private IEnumerator LifeOverRevealAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            _lifeOverRevealRoutine = null;
            _lifeOverRevealPending = false;
            FinishLifeOverRevealAfterDeath();
        }

        private void FinishLifeOverRevealAfterDeath()
        {
            if (_hero == null || !_hero.TryReviveForLifeRetry())
            {
                Log("Life retry failed (hero revive). Falling back to Game Over.");
                EnterGameOverState();
                return;
            }

            SetHeroDeathGameOverUiVisible(false);
            SetGameErrorUiVisible(false);
            SetGameWonUiVisible(false);
            SetCameraFollowEnabled(true);
            EnterLifeOverState();
        }

        private void CancelLifeOverRevealRoutine()
        {
            if (_lifeOverRevealRoutine != null)
            {
                StopCoroutine(_lifeOverRevealRoutine);
                _lifeOverRevealRoutine = null;
            }

            _lifeOverRevealPending = false;
        }

        private void BeginGameOverRevealAfterDeath()
        {
            ClearActiveCombatForDeathUi("Final hero death");

            CancelLifeOverRevealRoutine();
            CancelGameOverRevealRoutine();

            float delay = _gameOverShowDelaySeconds > 0f
                ? _gameOverShowDelaySeconds
                : Mathf.Max(0f, _lifeOverShowDelaySeconds);
            if (delay <= 0f)
            {
                EnterGameOverState();
                return;
            }

            Log($"Game Over in {delay:0.##}s (final life lost).");
            _gameOverRevealPending = true;
            _gameOverRevealRoutine = StartCoroutine(GameOverRevealAfterDelay(delay));
        }

        private IEnumerator GameOverRevealAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            _gameOverRevealRoutine = null;
            _gameOverRevealPending = false;
            EnterGameOverState();
        }

        private void CancelGameOverRevealRoutine()
        {
            if (_gameOverRevealRoutine != null)
            {
                StopCoroutine(_gameOverRevealRoutine);
                _gameOverRevealRoutine = null;
            }

            _gameOverRevealPending = false;
        }

        private void ClearActiveCombatForDeathUi(string reason)
        {
            if (_enemySpawner != null)
            {
                _enemySpawner.ClearActiveWaveCombatForLifeRetry(reason);
            }
            else
            {
                CombatProjectileCleanup_V2.DespawnAllActiveProjectiles();
                EnemyLootCleanup_V2.DespawnAllActiveGroundLoot();
            }
        }

        // Called from LifeOverNavButton_V2 (e.g. TextBTN_MediumStartNewGame). Restarts the same wave index.
        public bool TryContinueAfterLifeLost()
        {
            if (_state != WaveLoopState_V2.LifeOver)
            {
                if (_debugWaveLogs)
                {
                    Log($"TryContinueAfterLifeLost ignored (state={_state}).");
                }

                return false;
            }

            BeginTopBarWaveTextIntro();
            EnterInWaveState(allowDevWaveShortcuts: false);

            if (_hero != null)
            {
                _hero.ResumeCombatAfterLifeRetry();
                _hero.RefreshWaveLoopCombatGate();
            }

            EmitMetaChanged();
            Log($"LifeOver continue -> wave {CurrentWaveNumber}.");
            return true;
        }

        // Called from LifeOverNavButton_V2 (e.g. TextBTN_MediumGoToShop). Opens shop; leaving shop retries this wave.
        public bool TryGoToShopAfterLifeLost()
        {
            if (_state != WaveLoopState_V2.LifeOver)
            {
                if (_debugWaveLogs)
                {
                    Log($"TryGoToShopAfterLifeLost ignored (state={_state}).");
                }

                return false;
            }

            HideLifeOverUiCompletely();
            _shopExitRetriesSameWave = true;
            SetState(WaveLoopState_V2.Shop);
            SetCameraFollowEnabled(false);
            if (_shopPanel != null)
            {
                _shopPanel.Show();
                _shopPanel.Refresh();
            }

            EmitMetaChanged();
            Log($"LifeOver -> shop for wave {CurrentWaveNumber} retry.");
            return true;
        }

        // Called from LifeOverNavButton_V2 (e.g. TextBTN_MediumGoToMainMenu). Saves run and loads MainMenuScene.
        public bool TryGoToMainMenuAfterLifeLost()
        {
            if (_state != WaveLoopState_V2.LifeOver)
            {
                if (_debugWaveLogs)
                {
                    Log($"TryGoToMainMenuAfterLifeLost ignored (state={_state}).");
                }

                return false;
            }

            PersistActiveRunSave();
            Log("LifeOver -> main menu (run saved).");
            GameplayPauseButton_V2.ReturnToMainMenuScene();
            return true;
        }

        private bool TryStartRetryWaveFromShopAfterLifeLost()
        {
            if (!_shopExitRetriesSameWave)
            {
                return false;
            }

            _shopExitRetriesSameWave = false;
            BeginTopBarWaveTextIntro();
            SetCameraFollowEnabled(true);
            EnterInWaveState(allowDevWaveShortcuts: false);
            if (_hero != null)
            {
                _hero.ResumeCombatAfterLifeRetry();
                _hero.RefreshWaveLoopCombatGate();
            }

            EmitMetaChanged();
            Log($"Shop continue -> retry wave {CurrentWaveNumber}.");
            return true;
        }

        private void ResetRunLivesForNewRun()
        {
            _livesRemaining = MaxLivesPerRun;
            _shopExitRetriesSameWave = false;
            EmitLivesChanged();
        }

        private void EmitLivesChanged()
        {
            OnLivesChanged?.Invoke(_livesRemaining, MaxLivesPerRun);
        }

        private void EnsureHeartLifeBar()
        {
            if (_heartLifeBar == null)
            {
                _heartLifeBar = FindAnyObjectByType<HeartLifeBar_V2>(FindObjectsInactive.Include);
            }

            if (_heartLifeBar != null)
            {
                _heartLifeBar.Initialize(this, MaxLivesPerRun);
                return;
            }

            if (_debugWaveLogs)
            {
                Debug.LogWarning(
                    "[WaveManager_V2] HeartLifeBar_V2 not found. Add HeartLifeBar_V2 to your HeartLifeBar object in the scene.");
            }
        }

        private void EnsureGameOverContinueUi()
        {
            if (_gameOverContinueUi == null)
            {
                _gameOverContinueUi = GetComponent<GameOverContinueUi_V2>();
            }

            if (_gameOverContinueUi == null)
            {
                _gameOverContinueUi = gameObject.AddComponent<GameOverContinueUi_V2>();
            }

            _resolvedGameOverContinueUi = _gameOverContinueUi;
        }

        private void RefreshGameOverContinuePrompt()
        {
            _resolvedGameOverContinueUi?.RefreshPromptFromWaveManager();
            if (_heroDeathTopBarContinue != null &&
                (_resolvedGameOverContinueUi == null || _heroDeathTopBarContinue.text == "Continue"))
            {
                _heroDeathTopBarContinue.text =
                    $"Checkpoint {CheckpointContinueCost} [1]  |  Clutch {ClutchSaveCost} [2]  |  Restart run [R]";
            }
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

            ApplyLastShopPurchasedWeaponBeforeWave();

            if (_shopPanel != null)
            {
                _shopPanel.Hide();
            }

            if (TryStartRetryWaveFromShopAfterLifeLost())
            {
                return;
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
            return Mathf.Max(0, _bunkerMaxUpgradeBaseCost);
        }

        // Effective price for carousel UI and purchases (health / bunker max scale per buy; other kinds use offer.Cost).
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
                    return Mathf.Max(0, basis);
                }
                case ShopOfferKind_V2.BunkerRepair:
                {
                    int basis = offer.Cost > 0 ? offer.Cost : _bunkerRepairCost;
                    return GetScaledPurchaseCost(Mathf.Max(0, basis), _bunkerRepairsThisRun);
                }
                case ShopOfferKind_V2.WeaponUnlock:
                    if (offer.Weapon != null && IsWeaponOwned(offer.Weapon))
                    {
                        return ResolveAmmoRefillCostForWeaponRow(offer);
                    }

                    return Mathf.Max(0, offer.Cost);
                case ShopOfferKind_V2.AmmoRefill:
                    if (offer.Weapon != null &&
                        _shopPanel != null &&
                        _shopPanel.TryGetWeaponRowOffer(offer.Weapon, out ShopOfferConfig_V2 weaponRow) &&
                        weaponRow != null)
                    {
                        return ResolveAmmoRefillCostForWeaponRow(weaponRow);
                    }

                    return ResolveAmmoRefillCostForWeaponRow(offer);
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

            EnemyLootCleanup_V2.DespawnAllActiveGroundLoot();

            bool clearedLastWave = _waves != null && _waves.Count > 0 && _waveIndex >= _waves.Count - 1;
            if (clearedLastWave)
            {
                EnterGameWonState();
                return;
            }

            ApplyBetweenWavePressureReset();
            AudioManager_V2.PlayWaveComplete();

            int reward = _hasScalingForActiveWave
                ? _scalingForActiveWave.EffectiveWaveRewardCurrency
                : wave.WaveRewardCurrency;
            _currency += reward;
            HideLifeOverUiCompletely();
            SetState(WaveLoopState_V2.Shop);
            SetCameraFollowEnabled(false);
            if (_shopPanel != null)
            {
                _shopPanel.Show();
                _shopPanel.Refresh();
            }
            Log($"Wave {CurrentWaveNumber} cleared. reward={reward}, currency={_currency}");
            EmitMetaChanged();
            PersistRunSaveIfPossible();
        }

        private void EnterPreparingState()
        {
            HideLifeOverUiCompletely();
            HideGameOverChromeCompletely();
            SetState(WaveLoopState_V2.Preparing);
            WaveConfig_V2 wave = GetCurrentWaveConfig();
            float prepare = 0f;
            if (wave == null || (!wave.OpenShopDirectly && !wave.OpenLifeOverDirectly && !wave.OpenGameOverDirectly &&
                                 !wave.OpenGameWonDirectly && !wave.OpenGameErrorDirectly))
            {
                if (s_skipPrepareDelayAfterMainMenuPlay)
                {
                    s_skipPrepareDelayAfterMainMenuPlay = false;
                }
                else
                {
                    prepare = Mathf.Max(0.1f, _prepareDurationSeconds) +
                              Mathf.Max(0f, _extraPrepareDelaySecondsForNextWave);
                }
            }

            _extraPrepareDelaySecondsForNextWave = 0f;
            _stateEndTime = Time.time + prepare;
            _enemiesKilledThisWave = 0;
            PersistRunSaveIfPossible();
        }

        private void EnterLifeOverState()
        {
            SetHeroDeathGameOverUiVisible(false);
            InvalidateLifeOverUiCache();
            if (!LifeOverRuntimeLayout_V2.IsInspectorLayoutAuthoritative())
            {
                LifeOverUiFactory_V2.EnsureLabelsExist(_lifeOverInfoMessage, _lifeOverGoToShopLabel, _debugWaveLogs);
            }

            ResolveLifeOverUiIfNeeded();

            if (_shopPanel != null)
            {
                _shopPanel.PrepareLifeOverCanvasForDisplay();
                _shopPanel.Hide();
            }

            SetState(WaveLoopState_V2.LifeOver);
            _enemiesKilledThisWave = 0;
            ResolveLifeOverUiIfNeeded();
            SetLifeOverUiVisible(true);
            EnsureAllLifeOverNavButtonClickTargets();
            EnsureLifeOverContinueClickTargets();
            if (_debugWaveLogs)
            {
                if (_lifeOverRoot == null)
                {
                    Debug.LogWarning(
                        "[WaveManager_V2] LifeOver UI root not found (expected LifeOver V2). Assign Life Over Root on WaveManager.");
                }

                if (_lifeOverInfoText == null)
                {
                    Debug.LogWarning("[WaveManager_V2] txt_lifeOver_info not found under LifeOver UI root.");
                }

                if (_lifeOverStartNewGameText == null)
                {
                    Debug.LogWarning("[WaveManager_V2] txt_lifeOver_startNewGame not found under LifeOver UI root.");
                }

                if (_lifeOverGoToShopText == null)
                {
                    Debug.LogWarning("[WaveManager_V2] txt_lifeOver_goToShop not found under LifeOver UI root.");
                }

                if (_lifeOverGoToMainMenuText == null)
                {
                    Debug.LogWarning("[WaveManager_V2] txt_lifeOver_goToMainMenu not found under LifeOver UI root.");
                }
            }

            float hold = Mathf.Max(0f, _lifeLostTopBarHoldSeconds);
            if (hold > 0f)
            {
                BeginTopBarStatusIntro("Life lost", hold);
            }

            PersistRunSaveIfPossible();
            HideGameOverChromeCompletely();
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

        private void EnterInWaveState(bool allowDevWaveShortcuts = true)
        {
            HideLifeOverUiCompletely();
            SetHeroDeathGameOverUiVisible(false);
            WaveConfig_V2 wave = GetCurrentWaveConfig();
            if (wave == null)
            {
                EnterGameOverState();
                return;
            }

            if (allowDevWaveShortcuts && TryOpenLifeOverDirectlyFromWave(wave))
            {
                return;
            }

            if (allowDevWaveShortcuts && TryOpenGameOverDirectlyFromWave(wave))
            {
                return;
            }

            if (allowDevWaveShortcuts && TryOpenGameWonDirectlyFromWave(wave))
            {
                return;
            }

            if (allowDevWaveShortcuts && TryOpenGameErrorDirectlyFromWave(wave))
            {
                return;
            }

            if (allowDevWaveShortcuts && TryOpenShopDirectlyFromWave(wave))
            {
                return;
            }

            if (wave.MechRobotBossCount > 0)
            {
                AudioManager_V2.SetBossMusic();
            }
            else
            {
                AudioManager_V2.SetGameplayMusic();
            }

            SetState(WaveLoopState_V2.InWave);
            ApplyGameplayHudVisibility();
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
            PersistRunSaveIfPossible();
        }

        private bool TryOpenShopDirectlyFromWave(WaveConfig_V2 wave)
        {
            if (wave == null || !wave.OpenShopDirectly)
            {
                return false;
            }

            _scalingForActiveWave = BuildScalingSnapshot(wave, CurrentWaveNumber);
            _hasScalingForActiveWave = true;
            _inWaveEnteredUnscaledTime = Time.unscaledTime;
            Log($"Wave {CurrentWaveNumber} skipped (OpenShopDirectly). Opening shop.");
            CompleteWave();
            return true;
        }

        private bool TryOpenLifeOverDirectlyFromWave(WaveConfig_V2 wave)
        {
            if (wave == null || !wave.OpenLifeOverDirectly)
            {
                return false;
            }

            if (_livesRemaining > 0)
            {
                _livesRemaining = Mathf.Max(0, _livesRemaining - 1);
                EmitLivesChanged();
            }

            if (_enemySpawner != null)
            {
                _enemySpawner.ClearActiveWaveCombatForLifeRetry("OpenLifeOverDirectly dev shortcut");
            }
            else
            {
                CombatProjectileCleanup_V2.DespawnAllActiveProjectiles();
                EnemyLootCleanup_V2.DespawnAllActiveGroundLoot();
            }

            if (_hero == null || !_hero.TryReviveForLifeRetry())
            {
                Log("OpenLifeOverDirectly failed (hero revive). Falling back to Game Over.");
                EnterGameOverState();
                return true;
            }

            SetHeroDeathGameOverUiVisible(false);
            SetGameErrorUiVisible(false);
            SetGameWonUiVisible(false);
            SetCameraFollowEnabled(true);
            _continueEnemyPressureMultiplierRuntime = 1f;
            _bunkerHealth = _bunkerMaxHealthRuntime;
            _scalingForActiveWave = BuildScalingSnapshot(wave, CurrentWaveNumber);
            _hasScalingForActiveWave = true;
            _inWaveEnteredUnscaledTime = Time.unscaledTime;
            Log($"Wave {CurrentWaveNumber} skipped (OpenLifeOverDirectly). Opening LifeOver.");
            EnterLifeOverState();
            EmitMetaChanged();
            return true;
        }

        private bool TryOpenGameOverDirectlyFromWave(WaveConfig_V2 wave)
        {
            if (wave == null || !wave.OpenGameOverDirectly)
            {
                return false;
            }

            CancelLifeOverRevealRoutine();
            CancelGameOverRevealRoutine();
            HideLifeOverUiCompletely();

            _livesRemaining = 0;
            EmitLivesChanged();
            ClearActiveCombatForDeathUi("OpenGameOverDirectly dev shortcut");

            _scalingForActiveWave = BuildScalingSnapshot(wave, CurrentWaveNumber);
            _hasScalingForActiveWave = true;
            _inWaveEnteredUnscaledTime = Time.unscaledTime;
            Log($"Wave {CurrentWaveNumber} skipped (OpenGameOverDirectly). Opening GameOver.");
            EnterGameOverState(forceHeroDeathPresentation: true);
            return true;
        }

        private bool TryOpenGameWonDirectlyFromWave(WaveConfig_V2 wave)
        {
            if (wave == null || !wave.OpenGameWonDirectly)
            {
                return false;
            }

            CancelLifeOverRevealRoutine();
            CancelGameOverRevealRoutine();
            HideLifeOverUiCompletely();
            ClearActiveCombatForDeathUi("OpenGameWonDirectly dev shortcut");

            _scalingForActiveWave = BuildScalingSnapshot(wave, CurrentWaveNumber);
            _hasScalingForActiveWave = true;
            _inWaveEnteredUnscaledTime = Time.unscaledTime;
            Log($"Wave {CurrentWaveNumber} skipped (OpenGameWonDirectly). Opening GameWon.");
            EnterGameWonState();
            return true;
        }

        private bool TryOpenGameErrorDirectlyFromWave(WaveConfig_V2 wave)
        {
            if (wave == null || !wave.OpenGameErrorDirectly)
            {
                return false;
            }

            CancelLifeOverRevealRoutine();
            CancelGameOverRevealRoutine();
            HideLifeOverUiCompletely();
            ClearActiveCombatForDeathUi("OpenGameErrorDirectly dev shortcut");

            _scalingForActiveWave = BuildScalingSnapshot(wave, CurrentWaveNumber);
            _hasScalingForActiveWave = true;
            _inWaveEnteredUnscaledTime = Time.unscaledTime;
            Log($"Wave {CurrentWaveNumber} skipped (OpenGameErrorDirectly). Opening GameError.");
            EnterGameErrorState("OpenGameErrorDirectly dev shortcut");
            return true;
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

        private void EnterGameOverState(bool forceHeroDeathPresentation = false)
        {
            CancelLifeOverRevealRoutine();
            CancelGameOverRevealRoutine();
            _shopExitRetriesSameWave = false;
            SetLifeOverUiVisible(false);
            AudioManager_V2.PlayFailure();
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

            bool heroDeath = forceHeroDeathPresentation || (_hero != null && _hero.IsDead());
            if (heroDeath)
            {
                Paratrooper.StandDownAllLivingForHeroDeath();
            }

            ResolveHeroDeathGameOverUiIfNeeded();
            if (heroDeath)
            {
                SetHeroDeathGameOverUiVisible(true);
                EnsureGameOverNavUiButtonClickTargets();
                RefreshGameOverContinuePrompt();
            }
            else
            {
                SetHeroDeathGameOverUiVisible(false);
            }

            Log($"WaveManager entered GameOver (heroDeath={heroDeath}).");
            ClearPersistedRunSave();
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
            EnsureGameWonNavUiButtonClickTargets();
            Log($"WaveManager entered GameWon at wave {CurrentWaveNumber}.");
            ClearPersistedRunSave();
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
            CancelLifeOverRevealRoutine();
            CancelGameOverRevealRoutine();
            // Telemetry and other OnStateChanged listeners read this immediately; set before SetState
            // so WaveRunTelemetry_V2 can emit game_error:<detail> on the same callback tick.
            _lastGameErrorReason = string.IsNullOrWhiteSpace(reason) ? "Unknown error." : reason.Trim();
            string diagnostics = BuildGameErrorDiagnosticsSnapshot();
            Debug.LogError(
                "[WaveManager_V2] GameError — " + _lastGameErrorReason + "\n" + diagnostics);
            WaveRunTelemetry_V2.RecordSyntheticTelemetryError(
                "GameError",
                _lastGameErrorReason + "\n--- diagnostics (pre StopWave / pre state listeners) ---\n" + diagnostics);
            AudioManager_V2.PlayFailure();

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
            EnsureGameErrorNavUiButtonClickTargets();
            Log($"WaveManager entered GameError. reason={_lastGameErrorReason}");
        }

        private void ResolveHeroDeathGameOverUiIfNeeded()
        {
            GameObject gameOverV2 = FindGameObjectInLoadedScenes("GameOver V2");
            if (gameOverV2 != null)
            {
                _heroDeathGameOverRoot = gameOverV2;
            }
            else if (_heroDeathGameOverRoot == null)
            {
                _heroDeathGameOverRoot = FindGameObjectInLoadedScenes("GameOver");
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

        private void InvalidateLifeOverUiCache()
        {
            _lifeOverInfoText = null;
            _lifeOverStartNewGameText = null;
            _lifeOverStartNewGameButton = null;
            _lifeOverGoToShopText = null;
            _lifeOverGoToShopButton = null;
            _lifeOverGoToMainMenuText = null;
            _lifeOverGoToMainMenuButton = null;
        }

        private void ResolveLifeOverUiIfNeeded()
        {
            if (_lifeOverRoot == null)
            {
                _lifeOverRoot = ResolveLifeOverUiRoot();
            }

            Transform lifeOverScope = _lifeOverRoot != null ? _lifeOverRoot.transform : null;

            if (_lifeOverInfoText == null)
            {
                _lifeOverInfoText = FindLifeOverTmpByNames(
                    lifeOverScope,
                    "txt_lifeOver_info",
                    "txt_shop_info");
            }

            if (_lifeOverStartNewGameText == null)
            {
                _lifeOverStartNewGameText = FindLifeOverTmpByNames(
                    lifeOverScope,
                    "txt_lifeOver_startNewGame",
                    "txt_shop_startNewGame",
                    "txt_shop_startGame");
            }

            if (_lifeOverStartNewGameButton == null)
            {
                _lifeOverStartNewGameButton = FindLifeOverGameObjectByNames(
                    lifeOverScope,
                    "TextBTN_MediumStartNewGame",
                    "TextBTN_MediumStartGame");
            }

            if (_lifeOverGoToShopText == null)
            {
                _lifeOverGoToShopText = FindLifeOverTmpByNames(
                    lifeOverScope,
                    "txt_lifeOver_goToShop");
            }

            if (_lifeOverGoToShopButton == null)
            {
                _lifeOverGoToShopButton = FindLifeOverGameObjectByNames(
                    lifeOverScope,
                    "TextBTN_MediumGoToShop");
            }

            if (_lifeOverGoToMainMenuText == null)
            {
                _lifeOverGoToMainMenuText = FindLifeOverTmpByNames(
                    lifeOverScope,
                    "txt_lifeOver_goToMainMenu");
            }

            if (_lifeOverGoToMainMenuButton == null)
            {
                _lifeOverGoToMainMenuButton = FindLifeOverGameObjectByNames(
                    lifeOverScope,
                    "TextBTN_MediumGoToMainMenu");
            }
        }

        private TMP_Text FindLifeOverTmpByNames(Transform scope, params string[] exactNames)
        {
            for (int i = 0; i < exactNames.Length; i++)
            {
                TMP_Text text = FindLifeOverTmp(scope, exactNames[i]);
                if (text != null)
                {
                    return text;
                }
            }

            return null;
        }

        private GameObject FindLifeOverGameObjectByNames(Transform scope, params string[] exactNames)
        {
            for (int i = 0; i < exactNames.Length; i++)
            {
                GameObject go = FindGameObjectUnderTransform(scope, exactNames[i]);
                if (go != null)
                {
                    return go;
                }
            }

            GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject go = objects[i];
                if (go == null)
                {
                    continue;
                }

                for (int n = 0; n < exactNames.Length; n++)
                {
                    if (!go.name.Equals(exactNames[n], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (HasLifeOverAncestor(go.transform))
                    {
                        return go;
                    }

                    break;
                }
            }

            return null;
        }

        private TMP_Text FindLifeOverTmp(Transform scope, string exactName)
        {
            TMP_Text underScope = FindTmpUnderTransform(scope, exactName);
            if (underScope != null)
            {
                return underScope;
            }

            return FindTmpWithLifeOverAncestor(exactName);
        }

        private static TMP_Text FindTmpWithLifeOverAncestor(string exactName)
        {
            if (string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || !text.gameObject.name.Equals(exactName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsShopActionUiTransform(text.transform))
                {
                    continue;
                }

                if (HasLifeOverAncestor(text.transform) || IsUnderLifeOverCanvas(text.transform))
                {
                    return text;
                }
            }

            return null;
        }

        private static bool IsExcludedDeathMenuChromeName(string name)
        {
            return name.Equals("GameOver", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("GameOver V2", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("LifeOver V2 old", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUnderLifeOverCanvas(Transform node)
        {
            Transform walk = node;
            while (walk != null)
            {
                string name = walk.gameObject.name;
                if (IsExcludedDeathMenuChromeName(name))
                {
                    return false;
                }

                if (name.Equals("LifeOver-canvas", StringComparison.OrdinalIgnoreCase))
                {
                    return HasLifeOverChromeRootAncestor(walk);
                }

                walk = walk.parent;
            }

            return false;
        }

        private static bool HasLifeOverChromeRootAncestor(Transform fromNode)
        {
            Transform walk = fromNode != null ? fromNode.parent : null;
            while (walk != null)
            {
                string name = walk.gameObject.name;
                if (name.Equals("LifeOver V2", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("LifeOver", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (IsExcludedDeathMenuChromeName(name))
                {
                    return false;
                }

                walk = walk.parent;
            }

            return false;
        }

        private static bool HasLifeOverAncestor(Transform node)
        {
            Transform walk = node;
            while (walk != null)
            {
                string name = walk.gameObject.name;
                if (name.Equals("LifeOver V2", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("LifeOver", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (IsExcludedDeathMenuChromeName(name))
                {
                    return false;
                }

                walk = walk.parent;
            }

            return false;
        }

        private static GameObject ResolveLifeOverUiRoot()
        {
            string[] rootNames =
            {
                "LifeOver V2",
                "LifeOver-canvas",
                "LifeOver",
            };

            for (int i = 0; i < rootNames.Length; i++)
            {
                GameObject root = FindGameObjectInLoadedScenes(rootNames[i]);
                if (root != null)
                {
                    return root;
                }
            }

            return null;
        }

        private static TMP_Text FindTmpUnderTransform(Transform scope, string exactName)
        {
            if (scope == null || string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            TMP_Text[] texts = scope.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && text.gameObject.name.Equals(exactName, StringComparison.Ordinal))
                {
                    return text;
                }
            }

            return null;
        }

        private static GameObject FindGameObjectUnderTransform(Transform scope, string exactName)
        {
            if (scope == null || string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            Transform[] transforms = scope.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t != null && t.gameObject.name.Equals(exactName, StringComparison.Ordinal))
                {
                    return t.gameObject;
                }
            }

            return null;
        }

        private void SetLifeOverUiVisible(bool visible)
        {
            if (!visible)
            {
                HideLifeOverUiCompletely();
                return;
            }

            SetHeroDeathGameOverUiVisible(false);
            ResolveLifeOverUiIfNeeded();
            EnsureLifeOverUiRootsVisible();
            if (!LifeOverRuntimeLayout_V2.IsInspectorLayoutAuthoritative())
            {
                ShowLifeOverNamedObjectsInScene();
            }

            ActivateLifeOverTextHierarchy();

            PrepareLifeOverTmp(_lifeOverInfoText, _lifeOverInfoMessage);
            PrepareLifeOverTmp(_lifeOverStartNewGameText, "Start Game");
            PrepareLifeOverTmp(_lifeOverGoToShopText, _lifeOverGoToShopLabel);
            PrepareLifeOverTmp(_lifeOverGoToMainMenuText, _lifeOverGoToMainMenuLabel);
            EnsureLifeOverLabelUiClickTargets();

            if (_lifeOverStartNewGameButton != null)
            {
                EnsureLifeOverControlVisible(_lifeOverStartNewGameButton);
            }

            if (_lifeOverGoToShopText != null)
            {
                EnsureLifeOverControlVisible(_lifeOverGoToShopText.gameObject);
            }

            if (_lifeOverGoToShopButton != null)
            {
                EnsureLifeOverControlVisible(_lifeOverGoToShopButton);
            }

            if (_lifeOverGoToMainMenuText != null)
            {
                EnsureLifeOverControlVisible(_lifeOverGoToMainMenuText.gameObject);
            }

            if (_lifeOverGoToMainMenuButton != null)
            {
                EnsureLifeOverControlVisible(_lifeOverGoToMainMenuButton);
            }

            HideGameOverChromeCompletely();
        }

        private void EnsureLifeOverLabelUiClickTargets()
        {
            LifeOverLabelUiButton_V2.EnsureInfoLabelNonBlocking(_lifeOverInfoText);
            LifeOverLabelUiButton_V2.EnsureOnLabel(
                _lifeOverStartNewGameText,
                LifeOverLabelUiButton_V2.LifeOverLabelAction.ContinueAfterLifeLost);
            LifeOverLabelUiButton_V2.EnsureOnLabel(
                _lifeOverGoToShopText,
                LifeOverLabelUiButton_V2.LifeOverLabelAction.GoToShopAfterLifeLost);
            LifeOverLabelUiButton_V2.EnsureOnLabel(
                _lifeOverGoToMainMenuText,
                LifeOverLabelUiButton_V2.LifeOverLabelAction.GoToMainMenuAfterLifeLost);
        }

        private void EnsureLifeOverUiRootsVisible()
        {
            GameObject chromeRoot = FindLifeOverChromeRoot();
            if (chromeRoot != null)
            {
                SetUiRootHierarchyVisible(chromeRoot, true);
            }

            GameObject textCanvas = ResolveLifeOverTextCanvasRoot();
            if (textCanvas != null)
            {
                SetUiRootHierarchyVisible(textCanvas, true);
            }

            if (_lifeOverRoot != null &&
                (chromeRoot == null || _lifeOverRoot != chromeRoot))
            {
                SetUiRootHierarchyVisible(_lifeOverRoot, true);
            }
        }

        private void ActivateLifeOverTextHierarchy()
        {
            Transform chrome = FindLifeOverChromeRoot()?.transform;
            if (chrome != null && !chrome.gameObject.activeSelf)
            {
                chrome.gameObject.SetActive(true);
            }

            Transform canvas = chrome != null ? FindNamedChildRecursive(chrome, "LifeOver-canvas") : null;
            if (canvas == null)
            {
                GameObject canvasRoot = ResolveLifeOverTextCanvasRoot();
                canvas = canvasRoot != null ? canvasRoot.transform : null;
            }

            if (canvas == null)
            {
                return;
            }

            if (!canvas.gameObject.activeSelf)
            {
                canvas.gameObject.SetActive(true);
            }

            Canvas canvasComponent = canvas.GetComponent<Canvas>();
            if (canvasComponent != null)
            {
                canvasComponent.enabled = true;
            }

            DeactivateDuplicateLifeOverCanvases(canvas);

            if (!LifeOverRuntimeLayout_V2.IsInspectorLayoutAuthoritative(chrome))
            {
                LifeOverUiFactory_V2.ApplyVisibleCanvasLayout(canvas.gameObject);
                LifeOverUiFactory_V2.RepairOffScreenLifeOverLabels(canvas);
            }

            TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                text.enabled = true;
                if (!text.gameObject.activeSelf)
                {
                    text.gameObject.SetActive(true);
                }
            }
        }

        private static void EnsureAllLifeOverNavButtonClickTargets()
        {
            EnsureLifeOverNavButtonOnTarget(
                FindLifeOverNamedControl("TextBTN_MediumStartNewGame"),
                LifeOverNavButton_V2.LifeOverAction.Continue);
            EnsureLifeOverNavButtonOnTarget(
                FindLifeOverNamedControl("TextBTN_MediumGoToShop"),
                LifeOverNavButton_V2.LifeOverAction.GoToShop);
            EnsureLifeOverNavButtonOnTarget(
                FindLifeOverNamedControl("TextBTN_MediumGoToMainMenu"),
                LifeOverNavButton_V2.LifeOverAction.GoToMainMenu);

            EnsureLifeOverNavUiButtonOnTarget(
                FindLifeOverNamedControl("LifeOver_Btn_Continue"),
                LifeOverNavButton_V2.LifeOverAction.Continue);
            EnsureLifeOverNavUiButtonOnTarget(
                FindLifeOverNamedControl("LifeOver_Btn_Shop"),
                LifeOverNavButton_V2.LifeOverAction.GoToShop);
            EnsureLifeOverNavUiButtonOnTarget(
                FindLifeOverNamedControl("LifeOver_Btn_MainMenu"),
                LifeOverNavButton_V2.LifeOverAction.GoToMainMenu);
        }

        private static void EnsureLifeOverContinueClickTargets()
        {
            GameObject chromeRoot = FindLifeOverChromeRoot();
            if (chromeRoot == null)
            {
                return;
            }

            StripBlanketLifeOverContinueFromChromeRoot(chromeRoot);

            // Blanket continue only — TextBTN_MediumStartNewGame uses LifeOverNavButton_V2.
            string[] clickNames =
            {
                "ShopPanel V2 background",
            };

            for (int n = 0; n < clickNames.Length; n++)
            {
                Transform target = FindNamedChildRecursive(chromeRoot.transform, clickNames[n]);
                if (target == null)
                {
                    continue;
                }

                EnsureLifeOverContinueButtonOnTarget(target.gameObject);
            }
        }

        private static void StripBlanketLifeOverContinueFromChromeRoot(GameObject chromeRoot)
        {
            if (chromeRoot == null)
            {
                return;
            }

            LifeOverContinueButton_V2 continueButton = chromeRoot.GetComponent<LifeOverContinueButton_V2>();
            if (continueButton != null)
            {
                global::UnityEngine.Object.Destroy(continueButton);
            }

            // Runtime added a full-screen fallback collider on the chrome root; it steals Go to shop clicks.
            if (chromeRoot.GetComponent<LifeOverNavButton_V2>() == null &&
                chromeRoot.GetComponent<LifeOverGoToShopButton_V2>() == null &&
                chromeRoot.GetComponent<LifeOverGoToMainMenuButton_V2>() == null &&
                chromeRoot.GetComponent<SpriteRenderer>() == null &&
                chromeRoot.GetComponent<Collider2D>() is BoxCollider2D boxCollider)
            {
                global::UnityEngine.Object.Destroy(boxCollider);
            }
        }

        private static bool IsDedicatedLifeOverTextButton(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            string name = target.name;
            return name.Equals("TextBTN_MediumStartNewGame", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("TextBTN_MediumGoToShop", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("TextBTN_MediumGoToMainMenu", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("LifeOver_Btn_Continue", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("LifeOver_Btn_Shop", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("LifeOver_Btn_MainMenu", StringComparison.OrdinalIgnoreCase);
        }

        private static GameObject FindLifeOverNamedControl(string exactName)
        {
            if (string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            GameObject chromeRoot = FindLifeOverChromeRoot();
            if (chromeRoot != null)
            {
                Transform underChrome = FindNamedChildRecursive(chromeRoot.transform, exactName);
                if (underChrome != null)
                {
                    return underChrome.gameObject;
                }
            }

            GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null ||
                    !candidate.name.Equals(exactName, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (HasLifeOverAncestor(candidate.transform))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void EnsureLifeOverNavButtonOnTarget(GameObject target, LifeOverNavButton_V2.LifeOverAction action)
        {
            if (target == null)
            {
                return;
            }

            StripLegacyLifeOverButtonComponents(target);

            LifeOverNavButton_V2 nav = target.GetComponent<LifeOverNavButton_V2>();
            if (nav == null)
            {
                nav = target.AddComponent<LifeOverNavButton_V2>();
            }

            WaveManager_V2 waveManager = UnityEngine.Object.FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Exclude);
            nav.Configure(waveManager, action);

            Collider2D collider = target.GetComponent<Collider2D>();
            if (collider == null)
            {
                BoxCollider2D box = target.AddComponent<BoxCollider2D>();
                box.isTrigger = false;
                collider = box;
            }

            RefitLifeOverButtonCollider(target, collider);
        }

        private static void EnsureLifeOverNavUiButtonOnTarget(
            GameObject target,
            LifeOverNavButton_V2.LifeOverAction action)
        {
            if (target == null)
            {
                return;
            }

            MainMenuNavUiButton_V2 mainMenuUi = target.GetComponent<MainMenuNavUiButton_V2>();
            if (mainMenuUi != null)
            {
                global::UnityEngine.Object.Destroy(mainMenuUi);
            }

            LifeOverNavUiButton_V2 nav = target.GetComponent<LifeOverNavUiButton_V2>();
            if (nav == null)
            {
                nav = target.AddComponent<LifeOverNavUiButton_V2>();
            }

            WaveManager_V2 waveManager =
                UnityEngine.Object.FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Exclude);
            nav.Configure(waveManager, action);
        }

        private static void StripLegacyLifeOverButtonComponents(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            LifeOverContinueButton_V2 continueButton = target.GetComponent<LifeOverContinueButton_V2>();
            if (continueButton != null)
            {
                continueButton.enabled = false;
                global::UnityEngine.Object.DestroyImmediate(continueButton);
            }

            LifeOverGoToShopButton_V2 goToShop = target.GetComponent<LifeOverGoToShopButton_V2>();
            if (goToShop != null)
            {
                goToShop.enabled = false;
                global::UnityEngine.Object.DestroyImmediate(goToShop);
            }

            LifeOverGoToMainMenuButton_V2 goToMainMenu = target.GetComponent<LifeOverGoToMainMenuButton_V2>();
            if (goToMainMenu != null)
            {
                goToMainMenu.enabled = false;
                global::UnityEngine.Object.DestroyImmediate(goToMainMenu);
            }

            ShopNavArrowUiButton_V2 shopTextButton = target.GetComponent<ShopNavArrowUiButton_V2>();
            if (shopTextButton != null)
            {
                shopTextButton.enabled = false;
                shopTextButton.ResetToNormalVisual();
                global::UnityEngine.Object.DestroyImmediate(shopTextButton);
            }
        }

        private static void EnsureLifeOverGoToShopButtonOnTarget(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            LifeOverContinueButton_V2 continueButton = target.GetComponent<LifeOverContinueButton_V2>();
            if (continueButton != null)
            {
                global::UnityEngine.Object.Destroy(continueButton);
            }

            ShopNavArrowUiButton_V2 shopTextButton = target.GetComponent<ShopNavArrowUiButton_V2>();
            if (shopTextButton != null)
            {
                shopTextButton.enabled = false;
            }

            if (target.GetComponent<LifeOverGoToShopButton_V2>() == null)
            {
                target.AddComponent<LifeOverGoToShopButton_V2>();
            }

            Collider2D collider = target.GetComponent<Collider2D>();
            if (collider == null)
            {
                BoxCollider2D box = target.AddComponent<BoxCollider2D>();
                box.isTrigger = false;
                collider = box;
            }

            RefitLifeOverButtonCollider(target, collider);
        }

        private static void EnsureLifeOverGoToMainMenuButtonOnTarget(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            LifeOverContinueButton_V2 continueButton = target.GetComponent<LifeOverContinueButton_V2>();
            if (continueButton != null)
            {
                global::UnityEngine.Object.Destroy(continueButton);
            }

            ShopNavArrowUiButton_V2 shopTextButton = target.GetComponent<ShopNavArrowUiButton_V2>();
            if (shopTextButton != null)
            {
                shopTextButton.enabled = false;
            }

            if (target.GetComponent<LifeOverGoToMainMenuButton_V2>() == null)
            {
                target.AddComponent<LifeOverGoToMainMenuButton_V2>();
            }

            Collider2D collider = target.GetComponent<Collider2D>();
            if (collider == null)
            {
                BoxCollider2D box = target.AddComponent<BoxCollider2D>();
                box.isTrigger = false;
                collider = box;
            }

            RefitLifeOverButtonCollider(target, collider);
        }

        private static void RefitLifeOverButtonCollider(GameObject target, Collider2D collider)
        {
            if (target == null || collider == null)
            {
                return;
            }

            SpriteRenderer sprite = target.GetComponent<SpriteRenderer>();
            if (sprite == null)
            {
                sprite = target.GetComponentInChildren<SpriteRenderer>(true);
            }

            if (sprite == null)
            {
                return;
            }

            Bounds bounds = sprite.bounds;
            if (collider is BoxCollider2D box)
            {
                Vector3 localCenter = target.transform.InverseTransformPoint(bounds.center);
                Vector3 localSize = target.transform.InverseTransformVector(bounds.size);
                box.offset = new Vector2(localCenter.x, localCenter.y);
                box.size = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
            }
        }

        private static void EnsureLifeOverContinueButtonOnTarget(GameObject target)
        {
            if (target == null || IsDedicatedLifeOverTextButton(target))
            {
                return;
            }

            if (target.GetComponent<LifeOverNavButton_V2>() != null ||
                target.GetComponent<LifeOverGoToShopButton_V2>() != null ||
                target.GetComponent<LifeOverGoToMainMenuButton_V2>() != null)
            {
                return;
            }

            if (target.GetComponent<LifeOverContinueButton_V2>() == null)
            {
                target.AddComponent<LifeOverContinueButton_V2>();
            }

            if (target.GetComponent<Collider2D>() == null)
            {
                BoxCollider2D box = target.AddComponent<BoxCollider2D>();
                box.isTrigger = false;
            }
        }

        private static GameObject FindLifeOverChromeRoot()
        {
            return FindGameObjectInLoadedScenes("LifeOver V2");
        }

        private static void DeactivateDuplicateLifeOverCanvases(Transform activeCanvas)
        {
            GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject go = objects[i];
                if (go == null ||
                    activeCanvas != null && go.transform == activeCanvas ||
                    !go.name.Equals("LifeOver-canvas", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                go.SetActive(false);
            }
        }

        private static Transform FindNamedChildRecursive(Transform root, string exactName)
        {
            if (root == null || string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            if (root.gameObject.name.Equals(exactName, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamedChildRecursive(root.GetChild(i), exactName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static GameObject ResolveLifeOverTextCanvasRoot()
        {
            Transform chrome = FindLifeOverChromeRoot()?.transform;
            Transform underChrome = chrome != null ? FindNamedChildRecursive(chrome, "LifeOver-canvas") : null;
            if (underChrome != null)
            {
                return underChrome.gameObject;
            }

            return FindGameObjectInLoadedScenes("LifeOver-canvas");
        }

        private void HideLifeOverUiCompletely()
        {
            ResolveLifeOverUiIfNeeded();

            SetLifeOverLeafActive(_lifeOverInfoText, false);
            SetLifeOverLeafActive(_lifeOverStartNewGameText, false);
            SetLifeOverLeafActive(_lifeOverGoToShopText, false);
            SetLifeOverLeafActive(_lifeOverGoToMainMenuText, false);

            if (_lifeOverStartNewGameButton != null)
            {
                _lifeOverStartNewGameButton.SetActive(false);
            }

            if (_lifeOverGoToShopButton != null)
            {
                _lifeOverGoToShopButton.SetActive(false);
            }

            if (_lifeOverGoToMainMenuButton != null)
            {
                _lifeOverGoToMainMenuButton.SetActive(false);
            }

            HideLifeOverNamedObjectsInScene();

            if (_shopPanel != null)
            {
                _shopPanel.RestoreLifeOverCanvasAfterDisplay();
            }

            GameObject chromeRoot = FindLifeOverChromeRoot();
            if (chromeRoot != null)
            {
                chromeRoot.SetActive(false);
            }
            else if (_lifeOverRoot != null)
            {
                _lifeOverRoot.SetActive(false);
            }
            else
            {
                GameObject root = ResolveLifeOverUiRoot();
                if (root != null)
                {
                    root.SetActive(false);
                }
            }

            RestoreShopCanvasActivatedForLifeOverIfNeeded();
        }

        private void HideLifeOverNamedObjectsInScene()
        {
            string[] names =
            {
                "txt_lifeOver_info",
                "txt_lifeOver_startNewGame",
                "txt_lifeOver_goToShop",
                "txt_lifeOver_goToMainMenu",
                "txt_shop_info",
                "txt_shop_startNewGame",
                "TextBTN_MediumStartNewGame",
                "TextBTN_MediumGoToShop",
                "TextBTN_MediumGoToMainMenu",
            };

            TMP_Text[] allTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int t = 0; t < allTexts.Length; t++)
            {
                TMP_Text text = allTexts[t];
                if (text == null)
                {
                    continue;
                }

                string objectName = text.gameObject.name;
                for (int n = 0; n < names.Length; n++)
                {
                    if (!objectName.Equals(names[n], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if ((objectName.Equals("txt_lifeOver_info", StringComparison.Ordinal) ||
                         objectName.Equals("txt_shop_info", StringComparison.Ordinal)) &&
                        !string.IsNullOrEmpty(_lifeOverInfoMessage) &&
                        !text.text.Equals(_lifeOverInfoMessage, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (IsShopActionUiTransform(text.transform))
                    {
                        continue;
                    }

                    bool lifeOverOnlyName =
                        objectName.Equals("txt_lifeOver_startNewGame", StringComparison.Ordinal) ||
                        objectName.Equals("txt_lifeOver_goToShop", StringComparison.Ordinal) ||
                        objectName.Equals("txt_lifeOver_goToMainMenu", StringComparison.Ordinal) ||
                        objectName.Equals("txt_shop_startNewGame", StringComparison.Ordinal);

                    if (!lifeOverOnlyName && !IsLifeOverUiTransform(text.transform))
                    {
                        continue;
                    }

                    text.gameObject.SetActive(false);
                    text.enabled = false;
                    break;
                }
            }

            HideLifeOverNamedControlsInScene();

            if (_lifeOverRoot != null)
            {
                TMP_Text[] underRoot = _lifeOverRoot.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < underRoot.Length; i++)
                {
                    if (underRoot[i] != null)
                    {
                        underRoot[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        // Re-enables life-over labels/buttons individually hidden during gameplay (parent chrome alone is not enough).
        private void ShowLifeOverNamedObjectsInScene()
        {
            string[] textNames =
            {
                "txt_lifeOver_info",
                "txt_lifeOver_startNewGame",
                "txt_lifeOver_goToShop",
                "txt_lifeOver_goToMainMenu",
                "txt_shop_info",
                "txt_shop_startNewGame",
            };

            TMP_Text[] allTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int t = 0; t < allTexts.Length; t++)
            {
                TMP_Text text = allTexts[t];
                if (text == null)
                {
                    continue;
                }

                string objectName = text.gameObject.name;
                for (int n = 0; n < textNames.Length; n++)
                {
                    if (!objectName.Equals(textNames[n], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if ((objectName.Equals("txt_lifeOver_info", StringComparison.Ordinal) ||
                         objectName.Equals("txt_shop_info", StringComparison.Ordinal)) &&
                        !string.IsNullOrEmpty(_lifeOverInfoMessage) &&
                        !text.text.Equals(_lifeOverInfoMessage, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (IsShopActionUiTransform(text.transform))
                    {
                        continue;
                    }

                    bool lifeOverOnlyName =
                        objectName.Equals("txt_lifeOver_startNewGame", StringComparison.Ordinal) ||
                        objectName.Equals("txt_lifeOver_goToShop", StringComparison.Ordinal) ||
                        objectName.Equals("txt_lifeOver_goToMainMenu", StringComparison.Ordinal) ||
                        objectName.Equals("txt_shop_startNewGame", StringComparison.Ordinal);

                    if (!lifeOverOnlyName && !IsLifeOverUiTransform(text.transform))
                    {
                        continue;
                    }

                    text.enabled = true;
                    text.gameObject.SetActive(true);
                    break;
                }
            }

            ShowLifeOverNamedControlsInScene();
        }

        private static void ShowLifeOverNamedControlsInScene()
        {
            string[] names =
            {
                "TextBTN_MediumStartNewGame",
                "TextBTN_MediumStartGame",
                "TextBTN_MediumGoToShop",
                "TextBTN_MediumGoToMainMenu",
            };

            GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject go = objects[i];
                if (go == null)
                {
                    continue;
                }

                for (int n = 0; n < names.Length; n++)
                {
                    if (!go.name.Equals(names[n], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!HasLifeOverAncestor(go.transform))
                    {
                        break;
                    }

                    go.SetActive(true);
                    break;
                }
            }
        }

        private static void HideLifeOverNamedControlsInScene()
        {
            string[] names =
            {
                "TextBTN_MediumStartNewGame",
                "TextBTN_MediumStartGame",
                "TextBTN_MediumGoToShop",
                "TextBTN_MediumGoToShop_Pressed",
                "TextBTN_MediumGoToMainMenu",
                "TextBTN_MediumGoToMainMenu_Pressed",
            };

            GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject go = objects[i];
                if (go == null)
                {
                    continue;
                }

                for (int n = 0; n < names.Length; n++)
                {
                    if (go.name.Equals(names[n], StringComparison.Ordinal))
                    {
                        go.SetActive(false);
                        break;
                    }
                }
            }
        }

        private bool IsLifeOverUiTransform(Transform node)
        {
            if (node == null)
            {
                return false;
            }

            if (HasLifeOverAncestor(node))
            {
                return true;
            }

            if (_lifeOverRoot != null && node.IsChildOf(_lifeOverRoot.transform))
            {
                return true;
            }

            return false;
        }

        // Shop action labels (e.g. ShopActionButtons-canvas) must not be toggled by life-over hide/show sweeps.
        private static bool IsShopActionUiTransform(Transform node)
        {
            Transform walk = node;
            while (walk != null)
            {
                string name = walk.gameObject.name;
                if (name.Equals("Shop-canvas", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ShopLabels-canvas", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ShopActionButtons-canvas", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ShopActionLabels-canvas", StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf("ShopPanel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                walk = walk.parent;
            }

            return false;
        }

        private static void SetLifeOverLeafActive(TMP_Text text, bool active)
        {
            if (text == null)
            {
                return;
            }

            text.enabled = active;
            text.gameObject.SetActive(active);
        }

        private void PrepareLifeOverTmp(TMP_Text text, string messageOrNull)
        {
            if (text == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(messageOrNull))
            {
                text.text = messageOrNull;
            }

            EnsureLifeOverTextVisible(text);

            Color color = text.color;
            color.a = 1f;
            text.color = color;
            text.ForceMeshUpdate();
        }

        private void EnsureLifeOverTextVisible(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            Canvas canvas = text.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                if (IsLifeOverCanvas(canvas))
                {
                    canvas.gameObject.SetActive(true);
                    canvas.enabled = true;
                }
                else
                {
                    TrackShopCanvasActivatedForLifeOver(canvas);
                    canvas.gameObject.SetActive(true);
                    canvas.enabled = true;
                }

                EnsureUiLeafHierarchyActiveUpTo(text.gameObject, canvas.transform);
            }
            else
            {
                EnsureUiLeafHierarchyActiveUpTo(text.gameObject, null);
            }

            text.enabled = true;
            text.gameObject.SetActive(true);
        }

        private void EnsureLifeOverControlVisible(GameObject control)
        {
            if (control == null)
            {
                return;
            }

            Transform stopAt = FindLifeOverChromeRootTransform(control.transform);
            if (stopAt != null)
            {
                SetUiRootHierarchyVisible(stopAt.gameObject, true);
            }

            EnsureUiLeafHierarchyActiveUpTo(control, stopAt);
            control.SetActive(true);
        }

        private static Transform FindLifeOverChromeRootTransform(Transform node)
        {
            Transform walk = node;
            while (walk != null)
            {
                if (walk.gameObject.name.Equals("LifeOver V2", StringComparison.OrdinalIgnoreCase))
                {
                    return walk;
                }

                walk = walk.parent;
            }

            return null;
        }

        private void TrackShopCanvasActivatedForLifeOver(Canvas canvas)
        {
            if (canvas == null || IsLifeOverCanvas(canvas))
            {
                return;
            }

            if (!canvas.gameObject.activeSelf)
            {
                canvas.gameObject.SetActive(true);
                canvas.enabled = true;
                _shopCanvasActivatedForLifeOver = canvas;
            }
        }

        private static bool IsLifeOverCanvas(Canvas canvas)
        {
            if (canvas == null)
            {
                return false;
            }

            string name = canvas.gameObject.name;
            return name.Equals("LifeOver-canvas", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("LifeOver", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RestoreShopCanvasActivatedForLifeOverIfNeeded()
        {
            if (_shopCanvasActivatedForLifeOver == null)
            {
                return;
            }

            if (_shopPanel != null && _shopPanel.IsShopVisible)
            {
                _shopCanvasActivatedForLifeOver = null;
                return;
            }

            Canvas canvas = _shopCanvasActivatedForLifeOver;
            _shopCanvasActivatedForLifeOver = null;
            if (canvas == null)
            {
                return;
            }

            canvas.enabled = false;
            canvas.gameObject.SetActive(false);
        }

        private static void EnsureUiLeafHierarchyActiveUpTo(GameObject leaf, Transform stopAtInclusive)
        {
            if (leaf == null)
            {
                return;
            }

            Transform walk = leaf.transform;
            while (walk != null)
            {
                if (!walk.gameObject.activeSelf)
                {
                    walk.gameObject.SetActive(true);
                }

                Canvas canvas = walk.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.enabled = true;
                }

                if (stopAtInclusive != null && walk == stopAtInclusive)
                {
                    break;
                }

                walk = walk.parent;
            }
        }

        private static void SetUiRootHierarchyVisible(GameObject root, bool visible)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null)
                {
                    canvas.enabled = true;
                }
            }
        }


        private void SetHeroDeathGameOverUiVisible(bool visible)
        {
            if (visible && (_state == WaveLoopState_V2.LifeOver || _lifeOverRevealPending))
            {
                return;
            }

            ResolveHeroDeathGameOverUiIfNeeded();

            if (!visible)
            {
                HideGameOverChromeCompletely();
                return;
            }

            if (_lifeOverRoot != null && _state != WaveLoopState_V2.LifeOver)
            {
                _lifeOverRoot.SetActive(false);
            }

            EnsureGameOverWorldContentIfNeeded();
            HideLegacyGameOverRoot();

            if (_heroDeathGameOverRoot != null)
            {
                _heroDeathGameOverRoot.SetActive(true);
                SetTransformHierarchyActive(_heroDeathGameOverRoot.transform, true);
            }

            EnsureGameOverNavUiButtonClickTargets();

            if (_heroDeathContinueButton != null)
            {
                _heroDeathContinueButton.SetActive(true);
            }

            if (_heroDeathTopBarTitle != null)
            {
                EnsureGameplayOverlayBranchActive(_heroDeathTopBarTitle.transform);
                _heroDeathTopBarTitle.gameObject.SetActive(true);
            }

            if (_heroDeathTopBarContinue != null)
            {
                EnsureGameplayOverlayBranchActive(_heroDeathTopBarContinue.transform);
                _heroDeathTopBarContinue.gameObject.SetActive(true);
            }

            if (_gameOverUi == null)
            {
                _gameOverUi = FindAnyObjectByType<GameOverUI_V2>(FindObjectsInactive.Include);
            }

            _gameOverUi?.Show();
        }

        private static void HideLegacyGameOverRoot()
        {
            GameObject legacy = FindGameObjectInLoadedScenes("GameOver");
            if (legacy != null)
            {
                legacy.SetActive(false);
            }
        }

        private static GameObject FindGameOverChromeRoot()
        {
            return FindGameObjectInLoadedScenes("GameOver V2");
        }

        private static GameObject FindGameOverNamedControl(string exactName)
        {
            if (string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            GameObject chromeRoot = FindGameOverChromeRoot();
            if (chromeRoot != null)
            {
                Transform underChrome = FindNamedChildRecursive(chromeRoot.transform, exactName);
                if (underChrome != null)
                {
                    return underChrome.gameObject;
                }
            }

            return null;
        }

        private static void EnsureGameWonNavUiButtonClickTargets()
        {
            EnsureGameOverNavUiButtonOnTarget(
                FindGameWonNamedControl("GameWon_Btn_MainMenu"),
                GameOverNavUiButton_V2.GameOverAction.ReturnToMainMenu);
        }

        private static void EnsureGameErrorNavUiButtonClickTargets()
        {
            EnsureGameOverNavUiButtonOnTarget(
                FindGameErrorNamedControl("GameError_Btn_MainMenu"),
                GameOverNavUiButton_V2.GameOverAction.ReturnToMainMenu);
        }

        private static GameObject FindGameErrorChromeRoot()
        {
            GameObject gameErrorV2 = FindGameObjectInLoadedScenes("GameError V2");
            if (gameErrorV2 != null)
            {
                return gameErrorV2;
            }

            return FindGameObjectInLoadedScenes("GameError");
        }

        private static GameObject FindGameErrorNamedControl(string exactName)
        {
            if (string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            GameObject chromeRoot = FindGameErrorChromeRoot();
            if (chromeRoot != null)
            {
                Transform underChrome = FindNamedChildRecursive(chromeRoot.transform, exactName);
                if (underChrome != null)
                {
                    return underChrome.gameObject;
                }
            }

            return FindGameObjectInLoadedScenes(exactName);
        }

        private static GameObject FindGameWonChromeRoot()
        {
            GameObject gameWonV2 = FindGameObjectInLoadedScenes("GameWon V2");
            if (gameWonV2 != null)
            {
                return gameWonV2;
            }

            return FindGameObjectInLoadedScenes("GameWon");
        }

        private static GameObject FindGameWonNamedControl(string exactName)
        {
            if (string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            GameObject chromeRoot = FindGameWonChromeRoot();
            if (chromeRoot != null)
            {
                Transform underChrome = FindNamedChildRecursive(chromeRoot.transform, exactName);
                if (underChrome != null)
                {
                    return underChrome.gameObject;
                }
            }

            return FindGameObjectInLoadedScenes(exactName);
        }

        private static void EnsureGameOverNavUiButtonClickTargets()
        {
            EnsureGameOverNavUiButtonOnTarget(
                FindGameOverNamedControl("LifeOver_Btn_StartGame"),
                GameOverNavUiButton_V2.GameOverAction.RestartRun);
            EnsureGameOverNavUiButtonOnTarget(
                FindGameOverNamedControl("GameOver_Btn_StartGame"),
                GameOverNavUiButton_V2.GameOverAction.RestartRun);
            EnsureGameOverNavUiButtonOnTarget(
                FindGameOverNamedControl("TextBTN_GameOver_MediumStartNewGame"),
                GameOverNavUiButton_V2.GameOverAction.RestartRun);
            EnsureGameOverNavUiButtonOnTarget(
                FindGameOverNamedControl("LifeOver_Btn_MainMenu"),
                GameOverNavUiButton_V2.GameOverAction.ReturnToMainMenu);
            EnsureGameOverNavUiButtonOnTarget(
                FindGameOverNamedControl("GameOver_Btn_MainMenu"),
                GameOverNavUiButton_V2.GameOverAction.ReturnToMainMenu);
            EnsureGameOverNavUiButtonOnTarget(
                FindGameOverNamedControl("TextBTN_GameOver_MediumGoToMainMenu"),
                GameOverNavUiButton_V2.GameOverAction.ReturnToMainMenu);
        }

        private static void EnsureGameOverNavUiButtonOnTarget(
            GameObject target,
            GameOverNavUiButton_V2.GameOverAction action)
        {
            if (target == null)
            {
                return;
            }

            LifeOverNavUiButton_V2 lifeOverUi = target.GetComponent<LifeOverNavUiButton_V2>();
            if (lifeOverUi != null)
            {
                global::UnityEngine.Object.Destroy(lifeOverUi);
            }

            MainMenuNavUiButton_V2 mainMenuUi = target.GetComponent<MainMenuNavUiButton_V2>();
            if (mainMenuUi != null)
            {
                global::UnityEngine.Object.Destroy(mainMenuUi);
            }

            GameOverNavUiButton_V2 nav = target.GetComponent<GameOverNavUiButton_V2>();
            if (nav == null)
            {
                nav = target.AddComponent<GameOverNavUiButton_V2>();
            }

            WaveManager_V2 waveManager =
                UnityEngine.Object.FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Exclude);
            nav.Configure(waveManager, action);
        }

        private void HideGameOverChromeCompletely()
        {
            LifeOverRuntimeLayout_V2.SuppressGameOverChrome();
            HideResolvedGameOverUi();

            if (_heroDeathGameOverRoot != null)
            {
                _heroDeathGameOverRoot.SetActive(false);
            }

            if (_heroDeathContinueButton != null)
            {
                _heroDeathContinueButton.SetActive(false);
            }

            if (_heroDeathTopBarTitle != null)
            {
                _heroDeathTopBarTitle.gameObject.SetActive(false);
            }

            if (_heroDeathTopBarContinue != null)
            {
                _heroDeathTopBarContinue.gameObject.SetActive(false);
            }
        }

        private void HideResolvedGameOverUi()
        {
            if (_gameOverUi == null)
            {
                _gameOverUi = FindAnyObjectByType<GameOverUI_V2>(FindObjectsInactive.Include);
            }

            _gameOverUi?.Hide();
        }

        private void EnsureGameOverWorldContentIfNeeded()
        {
            ResolveHeroDeathGameOverUiIfNeeded();
            if (_heroDeathGameOverRoot == null)
            {
                return;
            }

            if (_heroDeathContinueButton == null)
            {
                Transform existing = _heroDeathGameOverRoot.transform.Find("bkg_gameOver_continue");
                if (existing == null)
                {
                    existing = _heroDeathGameOverRoot.transform.Find("btn_gameOver_continue");
                }

                if (existing != null)
                {
                    _heroDeathContinueButton = existing.gameObject;
                }
            }

            if (_heroDeathContinueButton != null || _gameOverContinueButtonPrefab == null)
            {
                return;
            }

            for (int i = 0; i < _heroDeathGameOverRoot.transform.childCount; i++)
            {
                Transform child = _heroDeathGameOverRoot.transform.GetChild(i);
                if (child != null && child.GetComponentInChildren<SpriteRenderer>(true) != null)
                {
                    _heroDeathContinueButton = child.gameObject;
                    return;
                }
            }

            GameObject instance = Instantiate(
                _gameOverContinueButtonPrefab,
                _heroDeathGameOverRoot.transform,
                false);
            instance.name = "bkg_gameOver_continue";
            _heroDeathContinueButton = instance;
        }

        private static void SetTransformHierarchyActive(Transform root, bool active)
        {
            if (root == null)
            {
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                child.gameObject.SetActive(active);
                SetTransformHierarchyActive(child, active);
            }
        }

        // Activates ancestors up to Topbar-canvas / HUD-Canvas so legacy top-bar TMP can render.
        private static void EnsureGameplayOverlayBranchActive(Transform leaf)
        {
            if (leaf == null)
            {
                return;
            }

            Transform hudCanvas = null;
            Transform walk = leaf;
            while (walk != null)
            {
                if (IsGameplayHudCanvasRoot(walk))
                {
                    hudCanvas = walk;
                    break;
                }

                walk = walk.parent;
            }

            if (hudCanvas == null)
            {
                return;
            }

            EnsureGameplayHudCanvasVisible(hudCanvas);

            walk = leaf;
            while (walk != null)
            {
                if (!walk.gameObject.activeSelf)
                {
                    walk.gameObject.SetActive(true);
                }

                Canvas canvas = walk.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.enabled = true;
                }

                if (walk == hudCanvas)
                {
                    break;
                }

                walk = walk.parent;
            }
        }

        private void ResolveGameWonUiIfNeeded()
        {
            if (_gameWonRoot == null)
            {
                GameObject gameWonV2 = FindGameObjectInLoadedScenes("GameWon V2");
                if (gameWonV2 != null)
                {
                    _gameWonRoot = gameWonV2;
                }
                else
                {
                    _gameWonRoot = FindGameObjectInLoadedScenes("GameWon");
                }
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
                GameObject gameErrorV2 = FindGameObjectInLoadedScenes("GameError V2");
                if (gameErrorV2 != null)
                {
                    _gameErrorRoot = gameErrorV2;
                }
                else
                {
                    _gameErrorRoot = FindGameObjectInLoadedScenes("GameError");
                }
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
                // Helicopter flights count as spawn activity even before a paratrooper lands.
                float lastSpawnActivityUnscaledTime = Mathf.Max(
                    _enemySpawner.LastParatrooperSpawnUnscaledTime,
                    _enemySpawner.LastSpawnAttemptUnscaledTime);
                float noSpawnSeconds = Mathf.Max(0f, now - lastSpawnActivityUnscaledTime);
                int livingParatroopers = _enemySpawner.GetLivingParatroopersTrackedCountForTelemetry();
                int pendingDrops = _enemySpawner.PendingParatrooperDropsThisWave;
                if (noSpawnSeconds >= Mathf.Max(5f, _enemyNoSpawnErrorSeconds))
                {
                    // HitStop / menu pause / app backgrounding freeze scaled-time coroutines; do not false-trigger.
                    if (Time.timeScale <= 0.001f)
                    {
                        return false;
                    }

                    // Early-wave cap or player still fighting living infantry while more drops are scheduled.
                    if (livingParatroopers > 0 &&
                        (_enemySpawner.IsParatrooperSpawnScheduleStillRunning || pendingDrops > 0))
                    {
                        return false;
                    }

                    // Carrier drop pipelines can take multiple trigger/gate timeouts after flights are scheduled.
                    float pendingDropGraceSeconds = Mathf.Max(_enemyNoSpawnErrorSeconds * 2f, 120f);
                    if (pendingDrops > 0 && noSpawnSeconds < pendingDropGraceSeconds)
                    {
                        return false;
                    }

                    if (_enemySpawner.IsSpawnStarvedThisWave &&
                        livingParatroopers <= 0)
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
                            $"spawned={_enemySpawner.SpawnedParatroopersThisWave}, pending={pendingDrops}, " +
                            $"living={livingParatroopers}, " +
                            $"exit='{_enemySpawner.SpawnRoutineExitReason}', lastAbort='{_enemySpawner.LastSpawnAbortReason}'.");
                        return true;
                    }

                    EnterGameErrorState(
                        $"No enemy spawns for {noSpawnSeconds:0.0}s (threshold={_enemyNoSpawnErrorSeconds:0.0}s), " +
                        $"spawnedThisWave={_enemySpawner.SpawnedParatroopersThisWave}, pendingDrops={pendingDrops}, " +
                        $"living={livingParatroopers}, scheduleRunning={_enemySpawner.IsParatrooperSpawnScheduleStillRunning}, " +
                        $"exit='{_enemySpawner.SpawnRoutineExitReason}'.");
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

        private void RememberShopPurchasedWeapon(HeroWeaponDefinition_V2 definition)
        {
            if (definition == null || _hero == null)
            {
                return;
            }

            _lastShopPurchasedWeapon = definition;
            _hero.ApplyShopPurchasedWeapon(definition);
        }

        private void ApplyLastShopPurchasedWeaponBeforeWave()
        {
            if (_hero == null || _lastShopPurchasedWeapon == null)
            {
                return;
            }

            _hero.ApplyShopPurchasedWeapon(_lastShopPurchasedWeapon);
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

        // Called when hero-side state changes (e.g. AutoHero weapon test lock) without meta/currency changes.
        public void NotifyTopBarRefresh()
        {
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
                if (_followCamera == null)
                {
                    // Main camera often has no FollowCamera (e.g. cutscene cam tagged MainCamera);
                    // gameplay follow is usually on a dedicated rig elsewhere in the scene.
                    _followCamera = UnityEngine.Object.FindAnyObjectByType<FollowCamera>(
                        FindObjectsInactive.Exclude);
                }

                if (_debugCameraFollowLogs)
                {
                    if (_followCamera != null)
                    {
                        Debug.Log(
                            $"[WaveManager_V2] Found FollowCamera on '{_followCamera.gameObject.name}' " +
                            $"(Camera.main='{mainCam.name}'). enabled={_followCamera.enabled}");
                    }
                    else
                    {
                        Debug.Log(
                            $"[WaveManager_V2] No FollowCamera found on '{mainCam.name}' or elsewhere in loaded scenes.");
                    }
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

            if (_topBarWaveCountText == null)
            {
                _topBarWaveCountText = FindTextInSceneByName("txt_topbar_waveCount");
            }
        }

        private void RefreshTopBar()
        {
            ApplyGameplayHudVisibility();
            ResolveTopBarReferencesIfNeeded();

            if (_topBarBunkerHealthText != null)
            {
                _topBarBunkerHealthText.text = $"Bunker: {_bunkerHealth}/{_bunkerMaxHealthRuntime}";
            }

            if (_topBarWaveCountText != null)
            {
                _topBarWaveCountText.text = CurrentWaveNumber.ToString(CultureInfo.InvariantCulture);
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
                if (AutoHero_V2.WeaponTestLockShowsInfiniteAmmoOnTopBar)
                {
                    _topBarCurrentAmmoText.text = "Ammo: ∞/∞";
                }
                else if (_hero.CurrentWeaponType == WeaponType.Bazooka)
                {
                    // Tube holds one round; auto-chamber keeps mag at 1 until reserve is empty — show loaded/remaining total.
                    int loaded = _hero.GetCurrentWeaponAmmo();
                    int remainingTotal =
                        _hero.GetCurrentWeaponAmmo() + _hero.GetCurrentWeaponReserveAmmo();
                    _topBarCurrentAmmoText.text =
                        $"Ammo: {loaded}/{remainingTotal}";
                }
                else
                {
                    bool infiniteReserve =
                        HeroWeaponAmmoRules_V2.HasInfiniteReserveAmmo(_hero.CurrentWeaponType);
                    string reserveText = infiniteReserve
                        ? "∞"
                        : _hero.GetCurrentWeaponReserveAmmo().ToString(CultureInfo.InvariantCulture);
                    _topBarCurrentAmmoText.text =
                        $"Ammo: {_hero.GetCurrentWeaponAmmo()}/{reserveText}";
                }
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

        // After shop UI teardown, wait one frame so canvases/layout settle before showing the wave label.
        private IEnumerator DeferredTopBarWaveTextIntroNextFrame()
        {
            yield return null;
            BeginTopBarWaveTextIntro();
            _deferredTopBarWaveIntroRoutine = null;
        }

        // Ensures ancestors from the wave label up to the gameplay HUD canvas are active so TMP can render.
        private void EnsureTopbarBranchActiveForWaveLabel()
        {
            if (_topBarWaveText == null)
            {
                return;
            }

            EnsureGameplayOverlayBranchActive(_topBarWaveText.transform);
        }

        // Keeps HUD-Canvas / HUD-Sprites-Canvas in sync with loop state (hidden while shop is open).
        public void ApplyGameplayHudVisibility()
        {
            if (ShouldShowGameplayHud())
            {
                EnsureGameplayHudVisible();
            }
            else
            {
                HideGameplayHudChrome();
            }
        }

        private bool ShouldShowGameplayHud()
        {
            if (_state == WaveLoopState_V2.Shop)
            {
                return false;
            }

            if (_shopPanel != null && _shopPanel.IsShopVisible)
            {
                return false;
            }

            return _state == WaveLoopState_V2.Preparing || _state == WaveLoopState_V2.InWave;
        }

        private void EnsureGameplayHudVisible()
        {
            Transform hudCanvas = FindSceneTransformByName("HUD-Canvas");
            EnsureGameplayHudCanvasVisible(hudCanvas);
            EnsureGameplayHudCanvasVisible(FindSceneTransformByName("HUD-Sprites-Canvas"));

            Transform hudRoot = FindSceneTransformByName("HUDRoot");
            if (hudRoot != null)
            {
                hudRoot.gameObject.SetActive(true);
            }

            if (hudCanvas != null)
            {
                GameplayHudLayoutUtility_V2.EnsureGameplayHudLayoutReady(hudCanvas);
            }

            SafeAreaFitter[] fitters = FindObjectsByType<SafeAreaFitter>(FindObjectsInactive.Include);
            for (int i = 0; i < fitters.Length; i++)
            {
                SafeAreaFitter fitter = fitters[i];
                if (fitter != null && IsUnderGameplayHudRoot(fitter.transform))
                {
                    fitter.Refresh();
                }
            }

            Canvas.ForceUpdateCanvases();
        }

        private static void HideGameplayHudChrome()
        {
            Transform hudCanvas = FindSceneTransformByName("HUD-Canvas");
            if (hudCanvas != null)
            {
                hudCanvas.gameObject.SetActive(false);
            }

            Transform spritesCanvas = FindSceneTransformByName("HUD-Sprites-Canvas");
            if (spritesCanvas != null)
            {
                spritesCanvas.gameObject.SetActive(false);
            }
        }

        private static void EnsureGameplayHudCanvasVisible(Transform hudCanvas)
        {
            if (hudCanvas == null)
            {
                return;
            }

            if (!hudCanvas.gameObject.activeSelf)
            {
                hudCanvas.gameObject.SetActive(true);
            }

            if (hudCanvas is RectTransform canvasRect)
            {
                if (canvasRect.localScale.sqrMagnitude < 0.001f)
                {
                    canvasRect.localScale = Vector3.one;
                }

                if (canvasRect.anchorMax.x <= canvasRect.anchorMin.x ||
                    canvasRect.anchorMax.y <= canvasRect.anchorMin.y)
                {
                    canvasRect.anchorMin = Vector2.zero;
                    canvasRect.anchorMax = Vector2.one;
                    canvasRect.pivot = new Vector2(0.5f, 0.5f);
                    canvasRect.anchoredPosition = Vector2.zero;
                    canvasRect.sizeDelta = Vector2.zero;
                }
            }

            Canvas canvas = hudCanvas.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                {
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        canvas.worldCamera = mainCamera;
                    }
                }
            }
        }

        private static bool IsUnderGameplayHudRoot(Transform transform)
        {
            Transform walk = transform;
            while (walk != null)
            {
                string name = walk.name;
                if (name.Equals("HUD-Canvas", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("HUD-Sprites-Canvas", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                walk = walk.parent;
            }

            return false;
        }

        private static Transform FindSceneTransformByName(string exactName)
        {
            if (string.IsNullOrWhiteSpace(exactName))
            {
                return null;
            }

            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    candidate.name.Equals(exactName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static int ScoreTopBarTextCandidate(TMP_Text candidate)
        {
            if (candidate == null)
            {
                return int.MinValue;
            }

            int score = candidate.gameObject.activeInHierarchy ? 100 : 0;
            Transform walk = candidate.transform;
            while (walk != null)
            {
                string name = walk.name;
                if (name.Equals("HUD-Canvas", StringComparison.OrdinalIgnoreCase))
                {
                    return score + 200;
                }

                if (name.Equals("Topbar-canvas", StringComparison.OrdinalIgnoreCase))
                {
                    return score - 200;
                }

                walk = walk.parent;
            }

            return score;
        }

        private static bool IsGameplayHudCanvasRoot(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            string name = transform.name;
            return name.Equals("Topbar-canvas", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("HUD-Canvas", StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerator TopBarStatusTextIntroRoutine(string message, float holdSeconds)
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
            _topBarWaveText.text = message;
            Color c = _topBarWaveTextBaseColor;
            c.a = 1f;
            _topBarWaveText.color = c;

            float hold = Mathf.Max(0f, holdSeconds);
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
            TMP_Text best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < allTexts.Length; i++)
            {
                TMP_Text current = allTexts[i];
                if (current == null)
                {
                    continue;
                }

                string n = current.gameObject.name.Trim();
                if (!n.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int score = ScoreTopBarTextCandidate(current);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = current;
                }
            }

            return best;
        }
    }
}
