using System.Collections;
using System.Collections.Generic;
using iStick2War;
using TMPro;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Minimal autonomous player for balance and integration-style runs: aim/shoot, bunker retreat,
    /// shop purchases, and starting the next wave. Add to the same GameObject as <see cref="Hero_V2"/> and enable it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoHero_V2 : MonoBehaviour
    {
        [Header("Test profile")]
        [SerializeField] private AutoHeroTestProfileKind_V2 _testProfile = AutoHeroTestProfileKind_V2.Perfect;
        [Tooltip("Force Perfect profile even if scene rules try to override.")]
        [SerializeField] private bool _forcePerfectProfile = true;

        [Header("Test / hero HP (Hero_V2 model)")]
        [Tooltip("When true, applies max/current HP once at startup (after Hero Awake). For repro tests e.g. orphaned enemies after hero death.")]
        [SerializeField] private bool _applyHeroHpOverrideForTesting;
        [SerializeField] private int _testHeroMaxHp = 100;
        [Tooltip("Current HP after override. Use -1 to set current = max.")]
        [SerializeField] private int _testHeroCurrentHp = -1;

        [Header("References (optional — resolved at runtime if empty)")]
        [SerializeField] private Hero_V2 _hero;
        [SerializeField] private HeroModel_V2 _model;
        [SerializeField] private HeroView_V2 _view;
        [SerializeField] private HeroInput_V2 _input;
        [SerializeField] private WaveManager_V2 _waveManager;

        [Header("Combat")]
        [Tooltip("If set, only colliders whose world bounds intersect this camera's view frustum are targeted or shot. " +
                 "If empty, uses Camera.main (the usual MainCamera with FollowCamera).")]
        [SerializeField] private Camera _shootVisibilityCamera;
        [SerializeField] private float _enemySearchRadius = 90f;
        [SerializeField] private float _shootIfEnemyWithinWeaponRangeFactor = 1f;
        [SerializeField] private float _bazookaPreferWithinHorizontal = 42f;
        [Tooltip("Extra horizontal reach for Bazooka preference when the selected target is a bombing aircraft (Bombplane_V2).")]
        [SerializeField] private float _bazookaPreferWithinHorizontalForBombingAircraft = 95f;
        [Tooltip(
            "When the selected target is any AircraftHealth_V2, Bazooka-vs-hitscan distance uses world distance (not X-only), " +
            "capped by this value so fast/high planes still trigger rocket selection when unlocked.")]
        [SerializeField] private float _bazookaAntiAirMaxWorldDistance = 140f;
        [Tooltip("Shoot gate vs aircraft: aim point is often far above the hero; scales max weapon range (1 = strict box, ~2.6+ = practical AA).")]
        [SerializeField] private float _aircraftShootRangeSlackMultiplier = 2.65f;
        [Tooltip(
            "When true, any Paratrooper body hitbox in the search radius wins over the helicopter. " +
            "Otherwise the single nearest EnemyBodyPart collider is used (aircraft often steals the aim).")]
        [SerializeField] private bool _prioritizeInfantryOverAircraft = true;
        [Tooltip("When true, grounded/combat-ready paratroopers get higher target priority than airborne paratroopers.")]
        [SerializeField] private bool _prioritizeGroundedParatroopers = true;
        [Tooltip("Extra score for grounded/combat-ready paratroopers when selecting target.")]
        [SerializeField] private float _groundedParatrooperPriorityBonus = 220f;
        [Tooltip("Strong priority bonus for paratroopers actively shooting/throwing grenades.")]
        [SerializeField] private float _combatParatrooperPriorityBonus = 12000f;
        [Tooltip("Penalty for airborne parachute states so grounded threats win target selection.")]
        [SerializeField] private float _airborneParatrooperPriorityPenalty = 1500f;
        [Tooltip("Logs chosen enemy target score/state at intervals during InWave.")]
        [SerializeField] private bool _debugTargetSelectionLogs;
        [SerializeField] private float _debugTargetSelectionLogIntervalSeconds = 0.5f;
        [Tooltip("Logs when fallback scan picks a living paratrooper because normal overlap/filter produced no valid target.")]
        [SerializeField] private bool _debugFallbackTargetingLogs = false;
        [Tooltip("When true, airborne paratroopers are ignored if any grounded paratrooper is targetable.")]
        [SerializeField] private bool _ignoreAirborneWhenGroundedExists = true;

        [Header("Bomb run survival")]
        [Tooltip("When true, retreat toward bunker interior when falling bombs are likely to splash the hero (ignores low-HP bunker rule).")]
        [SerializeField] private bool _retreatToBunkerOnBombSplashThreat = true;
        [Tooltip("Extra horizontal margin added to bomb explosion radius when judging splash threat.")]
        [SerializeField] private float _bombSplashThreatHorizExtra = 0.85f;
        [Tooltip("Also treat bombs as threatening when they are below the hero by at most this many world units (rolling bounce / low release).")]
        [SerializeField] private float _bombSplashThreatMaxBelowHero = 1.1f;
        [Tooltip(
            "When infantry is normally prioritized, temporarily aim the nearest Bombplane_V2 aircraft instead " +
            "if a bomb splash threat is active and the aircraft is within this horizontal distance.")]
        [SerializeField] private bool _aimBombingAircraftWhileBombsFall = true;
        [SerializeField] private float _aimBombingAircraftWhileBombsFallMaxHoriz = 70f;

        [Header("Bunker")]
        [SerializeField] [Range(0.05f, 0.95f)] private float _lowHealthEnterBunkerFraction = 0.35f;

        [Header("Shop")]
        [SerializeField] private int _maxShopPurchasesPerShopVisit = 24;
        [Tooltip(
            "Extra ScoreOffer priority when the Bazooka shop refill applies and the hero has no rounds in mag or reserve.")]
        [SerializeField] private int _shopScoreBonusBazookaAmmoWhenDry = 30_000;
        [Tooltip(
            "Extra priority when Bazooka refill applies but the hero still has some Bazooka ammo (top-up before other buys).")]
        [SerializeField] private int _shopScoreBonusBazookaAmmoTopUp = 12_000;

        [Header("Telemetry (optional)")]
        [SerializeField] private bool _logShopAndWave;
        [Tooltip("While InWave, logs periodically when no EnemyBodyPart target is chosen (helps diagnose last-wave soft locks).")]
        [SerializeField] private bool _logWaveCombatWhenNoTarget;
        [SerializeField] private float _logWaveCombatWhenNoTargetIntervalSeconds = 2f;

        [Header("Debug - Low HP warning sound")]
        [Tooltip("Debug aid: plays a warning sound while AutoHero HP is at or below threshold.")]
        [SerializeField] private bool _enableLowHpWarningSoundForDebug;
        [SerializeField] private int _lowHpWarningThresholdHp = 5000;
        [Tooltip("Optional dedicated source. If null, sound plays at hero position via PlayClipAtPoint.")]
        [SerializeField] private AudioSource _lowHpWarningAudioSource;
        [SerializeField] private AudioClip _lowHpWarningClip;
        [SerializeField] private float _lowHpWarningRepeatSeconds = 1.2f;
        [Header("Automation loop (runs)")]
        [SerializeField] private bool _enableAutomationRunLoop = true;
        [Tooltip(
            "Antal avslutade spelrundor (GameOver, GameWon eller GameError) innan automation slutar klicka vidare. " +
            "Tidigare räknades inte GameError → en watchdog-fail kunde ge en extra runda trots rätt siffra här.")]
        [SerializeField] private int _automationTotalRuns = 10;
        [Tooltip("Optional; if empty, resolved once by name txt_topbar_testRunDone (Topbar-canvas). Shown when automation batch completes.")]
        [SerializeField] private TMP_Text _testRunDoneTopBarText;
        [Tooltip(
            "Optional safety cap per editor session. When >0, automation pauses after this many completed runs " +
            "(or earlier if total runs are reached) so you can restart Unity and avoid long-session memory growth.")]
        [SerializeField] private int _automationChunkSize = 25;
        [Tooltip("Run lightweight memory cleanup every N completed runs (0 = off). Helps long overnight editor batches.")]
        [SerializeField] private int _automationMemoryCleanupEveryRuns = 3;
        [Tooltip("Calls GC.Collect + Resources.UnloadUnusedAssets at configured cadence between runs.")]
        [SerializeField] private bool _automationEnableMemoryCleanup = true;
        [SerializeField] private float _automationActionDelaySeconds = 0.5f;
        [SerializeField] private bool _automationLogs = true;
        [Tooltip("Prevents test deadlock when bot is fully dry (0 mag + 0 reserve). Refills active weapon via Hero API.")]
        [SerializeField] private bool _autoRefillAmmoWhenDryInAutomation = true;

        private readonly Collider2D[] _overlapBuffer = new Collider2D[64];
        private readonly List<ParatrooperBodyPart_V2> _paratrooperBodyPartBuffer = new List<ParatrooperBodyPart_V2>(16);
        private int _enemyBodyPartLayer = -1;
        private WaveLoopState_V2 _lastWaveState = WaveLoopState_V2.Preparing;
        private int _shopPhasesCompleted;
        private bool _shopExitScheduledThisVisit;
        private float _nextWaveCombatNoTargetLogUnscaledTime;
        private float _nextTargetSelectionDebugLogUnscaledTime;
        private bool _sessionInProgress;
        private int _completedRuns;
        private float _automationBatchStartRealtime = -1f;
        private int _automationGameErrorCount;
        private TMP_Text _resolvedTestRunDoneTopBarText;
        private bool _automationChunkPaused;
        private bool _automationCleanupInProgress;
        private float _nextAutomationActionAtUnscaled;
        private PendingAutomationAction _pendingAutomationAction = PendingAutomationAction.None;
        private float _lastAimAtEnemyUnscaledTime;
        private float _lastShootHeldUnscaledTime;
        private bool _heroHpTestOverrideApplied;
        private float _nextLowHpWarningAtUnscaled;
        private bool _lowHpWarningActiveLastTick;
        private bool _telemetryHasTarget;
        private bool _telemetryInRange;
        private bool _telemetryCanHoldFire;
        private bool _telemetryTargetShootableOnCamera;
        private bool _telemetryShootBlockedByBunkerMove;
        private bool _telemetryRawShootHeld;
        private bool _telemetryImmediateGroundParatrooperThreat;
        private string _telemetryTargetKind = "none";
        private StickmanBodyState _telemetryTargetParatrooperState = StickmanBodyState.Die;
        // Keep these fallback diagnostics in telemetry for future regression triage.
        private string _telemetryLastFallbackStage = "not_used";
        private int _telemetryFallbackLivingParatrooperModels;
        private int _telemetryFallbackEnabledEnemyBodyPartColliders;

        private enum PendingAutomationAction
        {
            None,
            ClickGameOverContinue,
            ClickGameWonContinue,
            ClickGameErrorContinue,
            ClickMainMenuPlay
        }

        // --- Test profile: aim noise (world units), resampled on a timer ---
        private Vector2 _aimNoiseOffset;
        private float _nextAimNoiseResampleUnscaled;

        // --- Test profile: first-frame delay when shoot conditions become true ---
        private bool _lastRawShootHeld;
        private float _shootEngageAllowedAfterUnscaled;

        /// <summary>Active preset for balance/telemetry runs (Inspector).</summary>
        public AutoHeroTestProfileKind_V2 TestProfile => _testProfile;
        public float LastAimAtEnemyUnscaledTime => _lastAimAtEnemyUnscaledTime;
        public float LastShootHeldUnscaledTime => _lastShootHeldUnscaledTime;
        public bool TelemetryHasTarget => _telemetryHasTarget;
        public bool TelemetryInRange => _telemetryInRange;
        public bool TelemetryCanHoldFire => _telemetryCanHoldFire;
        public bool TelemetryTargetShootableOnCamera => _telemetryTargetShootableOnCamera;
        public bool TelemetryShootBlockedByBunkerMove => _telemetryShootBlockedByBunkerMove;
        public bool TelemetryRawShootHeld => _telemetryRawShootHeld;
        public bool TelemetryImmediateGroundParatrooperThreat => _telemetryImmediateGroundParatrooperThreat;
        public string TelemetryTargetKind => _telemetryTargetKind ?? "none";
        public string TelemetryTargetParatrooperState => _telemetryTargetParatrooperState.ToString();
        public string TelemetryLastFallbackStage => _telemetryLastFallbackStage ?? "not_used";
        public int TelemetryFallbackLivingParatrooperModels => _telemetryFallbackLivingParatrooperModels;
        public int TelemetryFallbackEnabledEnemyBodyPartColliders => _telemetryFallbackEnabledEnemyBodyPartColliders;

        private void Awake()
        {
            CacheReferences();
            _enemyBodyPartLayer = LayerMask.NameToLayer("EnemyBodyPart");
            ApplySceneGameplayBotOverride();
            _lastAimAtEnemyUnscaledTime = Time.unscaledTime;
            _lastShootHeldUnscaledTime = Time.unscaledTime;
            if (_forcePerfectProfile)
            {
                _testProfile = AutoHeroTestProfileKind_V2.Perfect;
            }

            _automationBatchStartRealtime = -1f;
            _automationGameErrorCount = 0;
            _automationChunkPaused = false;
            _automationCleanupInProgress = false;
            _resolvedTestRunDoneTopBarText = null;
            HideTestRunDoneTopBarBanner();
        }

        private void Start()
        {
            TryApplyHeroHpTestOverride();
        }

        private void ApplySceneGameplayBotOverride()
        {
            if (GameplaySceneRules_V2.TryGetAutoHeroOverride(out AutoHeroTestProfileKind_V2 kind))
            {
                _testProfile = kind;
            }
        }

        private void OnDisable()
        {
            _shopExitScheduledThisVisit = false;
            _lastRawShootHeld = false;
            _shootEngageAllowedAfterUnscaled = 0f;
            if (_input != null)
            {
                _input.SetBotDriving(false);
            }

            if (_view != null)
            {
                _view.SetAutoAimWorldOverride(null);
            }
        }

        private void CacheReferences()
        {
            if (_hero == null)
            {
                _hero = GetComponent<Hero_V2>();
            }

            if (_model == null)
            {
                _model = GetComponent<HeroModel_V2>();
            }

            if (_view == null)
            {
                _view = GetComponent<HeroView_V2>();
            }

            if (_input == null)
            {
                _input = GetComponent<HeroInput_V2>();
            }
        }

        private void TryApplyHeroHpTestOverride()
        {
            if (!_applyHeroHpOverrideForTesting || _heroHpTestOverrideApplied)
            {
                return;
            }

            CacheReferences();
            if (_model == null)
            {
                return;
            }

            int maxHp = Mathf.Max(1, _testHeroMaxHp);
            int current = _testHeroCurrentHp < 0 ? maxHp : _testHeroCurrentHp;
            _model.ApplyHealthOverrideForTesting(maxHp, current);
            _heroHpTestOverrideApplied = true;
        }

        private void CacheWaveManagerIfNeeded()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>();
            }
        }

        /// <summary>Called from <see cref="Hero_V2.Update"/> before input and controller tick.</summary>
        public void TickBeforeHeroFrame(float deltaTime)
        {
            CacheReferences();
            CacheWaveManagerIfNeeded();
            TryApplyHeroHpTestOverride();

            if (_hero == null || _model == null || _view == null || _input == null)
            {
                return;
            }

            if (!_hero.isActiveAndEnabled)
            {
                return;
            }

            _input.SetBotDriving(true);
            TickLowHpWarningSoundForDebug();

            if (_waveManager == null)
            {
                if (_hero.IsDead())
                {
                    _view.SetAutoAimWorldOverride(null);
                    _input.SetBotFrame(Vector2.zero, false, false);
                    return;
                }

                TickCombatOnly(deltaTime);
                return;
            }

            WaveLoopState_V2 state = _waveManager.State;
            TickAutomationRunLoop(state);

            if (_pendingAutomationAction != PendingAutomationAction.None)
            {
                _view.SetAutoAimWorldOverride(null);
                _input.SetBotFrame(Vector2.zero, false, false);
                _lastWaveState = state;
                return;
            }

            if (_hero.IsDead())
            {
                _view.SetAutoAimWorldOverride(null);
                _input.SetBotFrame(Vector2.zero, false, false);
                _lastWaveState = state;
                return;
            }

            if (state == WaveLoopState_V2.GameOver)
            {
                _view.SetAutoAimWorldOverride(null);
                _input.SetBotFrame(Vector2.zero, false, false);
                _lastWaveState = state;
                return;
            }

            if (state == WaveLoopState_V2.GameWon)
            {
                _view.SetAutoAimWorldOverride(null);
                _input.SetBotFrame(Vector2.zero, false, false);
                _lastWaveState = state;
                return;
            }

            if (state == WaveLoopState_V2.Shop)
            {
                if (_lastWaveState != WaveLoopState_V2.Shop)
                {
                    _shopExitScheduledThisVisit = false;
                    _shopPhasesCompleted++;
                    if (_logShopAndWave)
                    {
                        Debug.Log(
                            $"[AutoHero_V2] Shop phase #{_shopPhasesCompleted} (after wave context: manager wave # = {_waveManager.CurrentWaveNumber}).");
                    }
                }

                if (!_shopExitScheduledThisVisit)
                {
                    TickShop();
                    _shopExitScheduledThisVisit = true;
                }

                _view.SetAutoAimWorldOverride(null);
                _input.SetBotFrame(Vector2.zero, false, false);
                _lastWaveState = state;
                return;
            }

            if (state == WaveLoopState_V2.Preparing)
            {
                _shopExitScheduledThisVisit = false;
                _view.SetAutoAimWorldOverride(null);
                _input.SetBotFrame(Vector2.zero, false, false);
                _lastWaveState = state;
                return;
            }

            _lastWaveState = state;
            TickCombatOnly(deltaTime);
        }

        private void TickAutomationRunLoop(WaveLoopState_V2 state)
        {
            if (!_enableAutomationRunLoop || _waveManager == null)
            {
                return;
            }

            if (_automationChunkPaused || _automationCleanupInProgress)
            {
                return;
            }

            TryExecutePendingAutomationAction();
            if (_pendingAutomationAction != PendingAutomationAction.None)
            {
                return;
            }

            if (state == WaveLoopState_V2.InWave && !_hero.IsDead())
            {
                if (_automationBatchStartRealtime < 0f && _completedRuns == 0)
                {
                    _automationBatchStartRealtime = Time.realtimeSinceStartup;
                    HideTestRunDoneTopBarBanner();
                }

                _sessionInProgress = true;
                return;
            }

            if (state == WaveLoopState_V2.GameOver)
            {
                if (_sessionInProgress)
                {
                    _completedRuns++;
                    _sessionInProgress = false;
                    LogAutomation($"Run {_completedRuns}/{Mathf.Max(1, _automationTotalRuns)} ended: GameOver.");
                    if (TryPauseAtChunkBoundary())
                    {
                        return;
                    }

                    TryRunAutomationCleanupAtCadence();
                }

                if (_completedRuns < Mathf.Max(1, _automationTotalRuns))
                {
                    QueueAutomationAction(PendingAutomationAction.ClickGameOverContinue);
                }
                else
                {
                    FinishAutomationBatchRunLoop("GameOver");
                }

                return;
            }

            if (state == WaveLoopState_V2.GameWon)
            {
                if (_sessionInProgress)
                {
                    _completedRuns++;
                    _sessionInProgress = false;
                    LogAutomation($"Run {_completedRuns}/{Mathf.Max(1, _automationTotalRuns)} ended: GameWon.");
                    if (TryPauseAtChunkBoundary())
                    {
                        return;
                    }

                    TryRunAutomationCleanupAtCadence();
                }

                if (_completedRuns < Mathf.Max(1, _automationTotalRuns))
                {
                    QueueAutomationAction(PendingAutomationAction.ClickGameWonContinue);
                }
                else
                {
                    FinishAutomationBatchRunLoop("GameWon");
                }

                return;
            }

            if (state == WaveLoopState_V2.GameError)
            {
                if (_sessionInProgress)
                {
                    _automationGameErrorCount++;
                    _completedRuns++;
                    _sessionInProgress = false;
                    LogAutomation($"Run {_completedRuns}/{Mathf.Max(1, _automationTotalRuns)} ended: GameError.");
                    if (TryPauseAtChunkBoundary())
                    {
                        return;
                    }

                    TryRunAutomationCleanupAtCadence();
                }

                if (_completedRuns < Mathf.Max(1, _automationTotalRuns))
                {
                    QueueAutomationAction(PendingAutomationAction.ClickGameErrorContinue);
                }
                else
                {
                    FinishAutomationBatchRunLoop("GameError");
                }

                return;
            }

            bool menuLikelyOpen = state == WaveLoopState_V2.Preparing && Time.timeScale <= 0.001f;
            if (menuLikelyOpen && !_sessionInProgress && _completedRuns < Mathf.Max(1, _automationTotalRuns))
            {
                QueueAutomationAction(PendingAutomationAction.ClickMainMenuPlay);
            }
        }

        private void QueueAutomationAction(PendingAutomationAction action)
        {
            if (_pendingAutomationAction != PendingAutomationAction.None)
            {
                return;
            }

            _pendingAutomationAction = action;
            _nextAutomationActionAtUnscaled = Time.unscaledTime + Mathf.Max(0.05f, _automationActionDelaySeconds);
        }

        private void TryExecutePendingAutomationAction()
        {
            if (_pendingAutomationAction == PendingAutomationAction.None)
            {
                return;
            }

            if (Time.unscaledTime < _nextAutomationActionAtUnscaled)
            {
                return;
            }

            bool clicked = false;
            switch (_pendingAutomationAction)
            {
                case PendingAutomationAction.ClickGameOverContinue:
                    LogAutomation("Trying click: GameOver Continue (bkg_gameOver_continue -> btn_gameOver_continue -> ReturnToMainMenu fallback)");
                    clicked =
                        TryClickMainMenuButtonByObjectName("bkg_gameOver_continue", "gameOver-continue-primary") ||
                        TryClickMainMenuButtonByObjectName("btn_gameOver_continue", "gameOver-continue-secondary") ||
                        TryClickReturnToMainMenuButtonFallback("gameOver-return-fallback");
                    break;
                case PendingAutomationAction.ClickGameWonContinue:
                    LogAutomation("Trying click: GameWon Continue (btn_gameWon_continue -> bkg_gameWon_continue -> ReturnToMainMenu fallback)");
                    clicked =
                        TryClickMainMenuButtonByObjectName("btn_gameWon_continue", "gameWon-continue-primary") ||
                        TryClickMainMenuButtonByObjectName("bkg_gameWon_continue", "gameWon-continue-secondary") ||
                        TryClickReturnToMainMenuButtonFallback("gameWon-return-fallback");
                    break;
                case PendingAutomationAction.ClickGameErrorContinue:
                    LogAutomation("Trying click: GameError Continue (btn_gameError_continue -> ReturnToMainMenu fallback)");
                    clicked =
                        TryClickMainMenuButtonByObjectName("btn_gameError_continue", "gameError-continue-primary") ||
                        TryClickReturnToMainMenuButtonFallback("gameError-return-fallback");
                    break;
                case PendingAutomationAction.ClickMainMenuPlay:
                    LogAutomation("Trying click: MainMenu Play (btn_main_menu_play -> Play fallback)");
                    clicked =
                        TryClickMainMenuButtonByObjectName("btn_main_menu_play", "mainMenu-play-primary") ||
                        TryClickPlayButtonFallback("mainMenu-play-fallback");
                    if (clicked)
                    {
                        _sessionInProgress = true;
                        LogAutomation($"Run {_completedRuns + 1}/{Mathf.Max(1, _automationTotalRuns)} started.");
                    }
                    break;
            }

            if (clicked)
            {
                LogAutomation($"Automation click executed: {_pendingAutomationAction}");
                _pendingAutomationAction = PendingAutomationAction.None;
                return;
            }

            LogAutomation($"Automation click failed this tick: {_pendingAutomationAction} (will retry)");
            _nextAutomationActionAtUnscaled = Time.unscaledTime + 0.4f;
        }

        private bool TryClickMainMenuButtonByObjectName(string objectName, string reasonTag)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            MainMenuNavButton_V2[] navButtons =
                FindObjectsByType<MainMenuNavButton_V2>(FindObjectsInactive.Include);
            for (int i = 0; i < navButtons.Length; i++)
            {
                MainMenuNavButton_V2 nav = navButtons[i];
                if (nav == null || nav.gameObject == null || !nav.gameObject.name.Equals(objectName))
                {
                    continue;
                }

                if (!nav.gameObject.activeInHierarchy)
                {
                    continue;
                }

                nav.TriggerAutomationClick();
                LogAutomation($"Clicked '{nav.gameObject.name}' [{reasonTag}]");
                return true;
            }

            LogAutomation($"Button not found/active: '{objectName}' [{reasonTag}]");
            return false;
        }

        private bool TryClickReturnToMainMenuButtonFallback(string reasonTag)
        {
            MainMenuNavButton_V2[] navButtons =
                FindObjectsByType<MainMenuNavButton_V2>(FindObjectsInactive.Include);
            for (int i = 0; i < navButtons.Length; i++)
            {
                MainMenuNavButton_V2 nav = navButtons[i];
                if (nav != null && nav.gameObject.activeInHierarchy && nav.IsReturnToMainMenuAction())
                {
                    nav.TriggerAutomationClick();
                    LogAutomation($"Clicked fallback ReturnToMainMenu '{nav.gameObject.name}' [{reasonTag}]");
                    return true;
                }
            }

            LogAutomation($"No active ReturnToMainMenu button found [{reasonTag}]");
            return false;
        }

        private bool TryClickPlayButtonFallback(string reasonTag)
        {
            MainMenuNavButton_V2[] navButtons =
                FindObjectsByType<MainMenuNavButton_V2>(FindObjectsInactive.Include);
            for (int i = 0; i < navButtons.Length; i++)
            {
                MainMenuNavButton_V2 nav = navButtons[i];
                if (nav != null && nav.gameObject.activeInHierarchy && nav.IsPlayAction())
                {
                    nav.TriggerAutomationClick();
                    LogAutomation($"Clicked fallback Play '{nav.gameObject.name}' [{reasonTag}]");
                    return true;
                }
            }

            LogAutomation($"No active Play button found [{reasonTag}]");
            return false;
        }

        private void HideTestRunDoneTopBarBanner()
        {
            TMP_Text t = ResolveTestRunDoneTopBarText();
            if (t == null)
            {
                return;
            }

            t.text = "";
            t.gameObject.SetActive(false);
        }

        private bool TryPauseAtChunkBoundary()
        {
            int total = Mathf.Max(1, _automationTotalRuns);
            int chunk = Mathf.Max(0, _automationChunkSize);
            if (chunk <= 0 || _completedRuns <= 0 || _completedRuns >= total)
            {
                return false;
            }

            if (_completedRuns % chunk != 0)
            {
                return false;
            }

            _automationChunkPaused = true;
            float elapsed = _automationBatchStartRealtime >= 0f
                ? Mathf.Max(0f, Time.realtimeSinceStartup - _automationBatchStartRealtime)
                : 0f;
            string msg =
                $"Chunk pause at run {_completedRuns}/{total} after {FormatAutomationElapsedEnglish(elapsed)}. " +
                "Restart Unity, then resume automation.";
            TMP_Text tmp = ResolveTestRunDoneTopBarText();
            if (tmp != null)
            {
                tmp.text = msg;
                tmp.gameObject.SetActive(true);
            }

            LogAutomation(msg);
            return true;
        }

        private void TryRunAutomationCleanupAtCadence()
        {
            if (!_automationEnableMemoryCleanup || _automationCleanupInProgress)
            {
                return;
            }

            int cadence = Mathf.Max(0, _automationMemoryCleanupEveryRuns);
            if (cadence <= 0 || _completedRuns <= 0 || (_completedRuns % cadence) != 0)
            {
                return;
            }

            StartCoroutine(AutomationCleanupRoutine());
        }

        private IEnumerator AutomationCleanupRoutine()
        {
            _automationCleanupInProgress = true;
            LogAutomation("Automation memory cleanup: GC.Collect + UnloadUnusedAssets.");
            System.GC.Collect();
            AsyncOperation unload = Resources.UnloadUnusedAssets();
            if (unload != null)
            {
                yield return unload;
            }

            System.GC.Collect();
            _automationCleanupInProgress = false;
            LogAutomation("Automation memory cleanup finished.");
        }

        private void FinishAutomationBatchRunLoop(string terminalKindForLog)
        {
            LogAutomation($"Automation finished after max runs ({terminalKindForLog}).");
            int total = Mathf.Max(1, _automationTotalRuns);
            float elapsed = _automationBatchStartRealtime >= 0f
                ? Mathf.Max(0f, Time.realtimeSinceStartup - _automationBatchStartRealtime)
                : 0f;
            string duration = FormatAutomationElapsedEnglish(elapsed);
            string errPart = _automationGameErrorCount <= 0
                ? "No errors."
                : _automationGameErrorCount == 1
                    ? "1 error."
                    : $"{_automationGameErrorCount} errors.";
            string msg = $"Test run {total}/{total} games done in {duration}. {errPart}";
            TMP_Text tmp = ResolveTestRunDoneTopBarText();
            if (tmp != null)
            {
                tmp.text = msg;
                tmp.gameObject.SetActive(true);
            }
        }

        private TMP_Text ResolveTestRunDoneTopBarText()
        {
            if (_testRunDoneTopBarText != null)
            {
                return _testRunDoneTopBarText;
            }

            if (_resolvedTestRunDoneTopBarText != null)
            {
                return _resolvedTestRunDoneTopBarText;
            }

            _resolvedTestRunDoneTopBarText = FindTmpTextInLoadedScenesByExactName("txt_topbar_testRunDone");
            return _resolvedTestRunDoneTopBarText;
        }

        private static string FormatAutomationElapsedEnglish(float elapsedSec)
        {
            if (elapsedSec < 0f)
            {
                elapsedSec = 0f;
            }

            int t = Mathf.FloorToInt(elapsedSec);
            int h = t / 3600;
            int m = (t % 3600) / 60;
            int s = t % 60;
            var parts = new List<string>(3);
            if (h > 0)
            {
                parts.Add(h == 1 ? "1 hour" : $"{h} hours");
            }

            if (m > 0)
            {
                parts.Add(m == 1 ? "1 minute" : $"{m} minutes");
            }

            if (s > 0 || parts.Count == 0)
            {
                parts.Add(s == 1 ? "1 second" : $"{s} seconds");
            }

            return string.Join(", ", parts);
        }

        private static TMP_Text FindTmpTextInLoadedScenesByExactName(string exactName)
        {
            if (string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && text.gameObject.name.Equals(exactName, System.StringComparison.Ordinal))
                {
                    return text;
                }
            }

            return null;
        }

        private void LogAutomation(string msg)
        {
            if (_automationLogs)
            {
                Debug.Log($"[AutoHero_V2] {msg}");
            }
        }

        private void TickShop()
        {
            for (int i = 0; i < _maxShopPurchasesPerShopVisit; i++)
            {
                ShopOfferConfig_V2 best = PickBestAffordableOffer(_waveManager);
                if (best == null)
                {
                    break;
                }

                if (!_waveManager.TryPurchaseOffer(best))
                {
                    break;
                }

                if (_logShopAndWave)
                {
                    Debug.Log($"[AutoHero_V2] Purchased shop offer: {best.DisplayName} ({best.Kind}).");
                }
            }

            _waveManager.StartNextWaveFromShop();
            if (_logShopAndWave)
            {
                Debug.Log("[AutoHero_V2] StartNextWaveFromShop() after purchases.");
            }
        }

        private ShopOfferConfig_V2 PickBestAffordableOffer(WaveManager_V2 wave)
        {
            ShopPanel_V2 panel = wave.ShopPanel;
            if (panel == null)
            {
                return null;
            }

            IReadOnlyList<ShopOfferConfig_V2> offers = panel.ConfiguredShopOffers;
            if (offers == null || offers.Count == 0)
            {
                return null;
            }

            ShopOfferConfig_V2 best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < offers.Count; i++)
            {
                ShopOfferConfig_V2 o = offers[i];
                if (o == null)
                {
                    continue;
                }

                if (!IsOfferApplicable(wave, o))
                {
                    continue;
                }

                int cost = ResolveOfferSpendCost(wave, o);
                if (!wave.CanAfford(cost))
                {
                    continue;
                }

                int score = ScoreOffer(wave, o);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = o;
                }
            }

            return best;
        }

        private static int ResolveOfferSpendCost(WaveManager_V2 wave, ShopOfferConfig_V2 o)
        {
            return o.Kind is ShopOfferKind_V2.HealthPack or ShopOfferKind_V2.BunkerMaxUpgrade
                ? wave.GetOfferEffectiveCost(o)
                : Mathf.Max(0, o.Cost);
        }

        private bool IsOfferApplicable(WaveManager_V2 wave, ShopOfferConfig_V2 o)
        {
            if (GameplaySceneRules_V2.IsShopOfferBlocked(o))
            {
                return false;
            }

            switch (o.Kind)
            {
                case ShopOfferKind_V2.HealthPack:
                    return !_hero.IsHealthFull();
                case ShopOfferKind_V2.BunkerRepair:
                    return !wave.IsBunkerFullHealth();
                case ShopOfferKind_V2.BunkerMaxUpgrade:
                    return !wave.IsBunkerMaxAtCap();
                case ShopOfferKind_V2.WeaponUnlock:
                    return o.Weapon != null && !_hero.HasWeaponUnlocked(o.Weapon);
                case ShopOfferKind_V2.AmmoRefill:
                    return o.Weapon != null &&
                           _hero.HasWeaponUnlocked(o.Weapon) &&
                           !_hero.IsWeaponMagazineFull(o.Weapon);
                default:
                    return false;
            }
        }

        private int ScoreOffer(WaveManager_V2 wave, ShopOfferConfig_V2 o)
        {
            float hpRatio = _model.maxHealth > 0 ? (float)_model.currentHealth / _model.maxHealth : 1f;
            float bunkerRatio = wave.BunkerMaxHealth > 0 ? (float)wave.BunkerHealth / wave.BunkerMaxHealth : 1f;

            switch (o.Kind)
            {
                case ShopOfferKind_V2.WeaponUnlock:
                    return 10_000 + Mathf.Clamp(o.Cost, 0, 5_000);
                case ShopOfferKind_V2.AmmoRefill:
                {
                    int baseScore = 4_000 + Mathf.Clamp(o.Cost, 0, 2_000);
                    if (o.Weapon != null &&
                        o.Weapon.WeaponType == WeaponType.Bazooka &&
                        _hero.HasUnlockedWeaponOfType(WeaponType.Bazooka))
                    {
                        if (!_hero.HasUsableAmmoForWeaponType(WeaponType.Bazooka))
                        {
                            return baseScore + Mathf.Max(0, _shopScoreBonusBazookaAmmoWhenDry);
                        }

                        return baseScore + Mathf.Max(0, _shopScoreBonusBazookaAmmoTopUp);
                    }

                    return baseScore;
                }
                case ShopOfferKind_V2.HealthPack:
                {
                    int urgency = Mathf.RoundToInt((1f - Mathf.Clamp01(hpRatio)) * 6_000f);
                    return 3_000 + urgency;
                }
                case ShopOfferKind_V2.BunkerRepair:
                {
                    int urgency = Mathf.RoundToInt((1f - Mathf.Clamp01(bunkerRatio)) * 5_000f);
                    return 2_500 + urgency;
                }
                case ShopOfferKind_V2.BunkerMaxUpgrade:
                    return 2_000 + Mathf.Clamp(o.Cost, 0, 1_000);
                default:
                    return 0;
            }
        }

        private void TickCombatOnly(float _)
        {
            TryPreventAmmoDeadlockInAutomation();

            Vector2 heroPos = _model.transform.position;
            Vector2? bunkerAnchor = TryGetBunkerInteriorWorldPoint();

            bool inside = _waveManager != null && _waveManager.IsHeroInsideBunker(_hero);
            float hpRatio = _model.maxHealth > 0 ? (float)_model.currentHealth / _model.maxHealth : 1f;
            bool bombThreat = _retreatToBunkerOnBombSplashThreat && IsBombSplashThreatActive(heroPos);
            bool wantBunkerFromHp =
                bunkerAnchor.HasValue &&
                hpRatio < _lowHealthEnterBunkerFraction &&
                !inside;
            bool wantBunkerFromBombs =
                bunkerAnchor.HasValue &&
                bombThreat &&
                !inside;
            bool wantBunker = wantBunkerFromHp || wantBunkerFromBombs;

            Plane[] shootFrustumPlanes = TryGetShootVisibilityFrustumPlanes();
            Collider2D target = FindNearestEnemyCollider(heroPos, shootFrustumPlanes, bombThreat);
            Vector2 aimPoint = heroPos + Vector2.right * 8f;
            bool hasTarget = false;

            if (_logWaveCombatWhenNoTarget &&
                _waveManager != null &&
                _waveManager.State == WaveLoopState_V2.InWave &&
                target == null &&
                Time.unscaledTime >= _nextWaveCombatNoTargetLogUnscaledTime)
            {
                _nextWaveCombatNoTargetLogUnscaledTime =
                    Time.unscaledTime + Mathf.Max(0.25f, _logWaveCombatWhenNoTargetIntervalSeconds);
                Camera visCam = _shootVisibilityCamera != null ? _shootVisibilityCamera : Camera.main;
                Debug.Log(
                    "[AutoHero_V2] InWave: no EnemyBodyPart target after overlap + filters " +
                    $"(wave={_waveManager.CurrentWaveNumber}, hero={heroPos}, " +
                    $"shootFrustum={(shootFrustumPlanes != null ? "active" : "inactive")}, " +
                    $"visCam={(visCam != null ? visCam.name : "null")})");
            }

            if (target != null)
            {
                aimPoint = ResolveAimPointForTarget(target, heroPos);
                hasTarget = true;
            }

            bool isImmediateGroundParatrooperThreat =
                target != null &&
                IsParatrooperCollider(target) &&
                IsGroundCombatParatrooper(target);

            RefreshAimNoiseForProfile(hasTarget);
            if (hasTarget && _testProfile != AutoHeroTestProfileKind_V2.Perfect)
            {
                aimPoint += _aimNoiseOffset;
            }

            MaybeSelectWeaponForThreat(heroPos, aimPoint, hasTarget, target);

            Vector2 move = Vector2.zero;
            if (wantBunker)
            {
                float dx = bunkerAnchor.Value.x - heroPos.x;
                move = new Vector2(dx > 0.08f ? 1f : dx < -0.08f ? -1f : 0f, 0f);
            }

            if (_testProfile == AutoHeroTestProfileKind_V2.Struggling && move.sqrMagnitude > 0.0001f)
            {
                move += new Vector2(0f, Random.Range(-0.35f, 0.35f));
                if (move.sqrMagnitude > 1.0001f)
                {
                    move.Normalize();
                }
            }

            float weaponRange = _model.currentWeaponDefinition != null
                ? _model.currentWeaponDefinition.Range
                : 40f;
            float rangeFactor = Mathf.Max(0.1f, _shootIfEnemyWithinWeaponRangeFactor);
            if (_testProfile == AutoHeroTestProfileKind_V2.Struggling)
            {
                rangeFactor *= 0.82f;
            }
            else if (_testProfile == AutoHeroTestProfileKind_V2.HumanLike)
            {
                rangeFactor *= 0.92f;
            }

            float maxShootDist = weaponRange * rangeFactor;
            bool inRange;
            if (hasTarget && target != null && IsAircraftCollider(target))
            {
                float slack = Mathf.Max(1f, _aircraftShootRangeSlackMultiplier);
                inRange = Vector2.Distance(aimPoint, heroPos) <= maxShootDist * slack;
            }
            else
            {
                float verticalSlack = IsParatrooperCollider(target) ? 1.35f : 0.85f;
                inRange =
                    hasTarget &&
                    Mathf.Abs(aimPoint.x - heroPos.x) <= maxShootDist &&
                    Mathf.Abs(aimPoint.y - heroPos.y) <= maxShootDist * verticalSlack;
            }

            // Do not hold fire when the active weapon is completely dry: HeroController_V2 requires a release to
            // clear _outOfAmmoLatched after a dry-fire; holding shoot forever would soft-lock shooting.
            bool canHoldFire =
                _model.currentAmmo > 0 || _model.currentReserveAmmo > 0;

            bool targetShootableOnCamera =
                target == null ||
                shootFrustumPlanes == null ||
                GeometryUtility.TestPlanesAABB(shootFrustumPlanes, target.bounds);
            if (isImmediateGroundParatrooperThreat)
            {
                // Emergency override: a grounded paratrooper actively threatening the hero/bunker must not be
                // blocked by camera-frustum gating (can fail transiently with large bounds/camera transitions).
                targetShootableOnCamera = true;
            }

            bool shootBlockedByBunkerMove = wantBunker && !isImmediateGroundParatrooperThreat;
            if (shootBlockedByBunkerMove &&
                wantBunkerFromBombs &&
                bombThreat &&
                target != null &&
                IsBombingAircraftCollider(target) &&
                _model.currentWeaponType == WeaponType.Bazooka &&
                _hero.HasUsableAmmoForWeaponType(WeaponType.Bazooka))
            {
                shootBlockedByBunkerMove = false;
            }

            bool rawShootHeld = inRange && !shootBlockedByBunkerMove && canHoldFire && targetShootableOnCamera;
            bool shootHeld = ApplyProfileToShootHeld(rawShootHeld);
            if (shootHeld)
            {
                _lastShootHeldUnscaledTime = Time.unscaledTime;
            }

            _telemetryHasTarget = hasTarget;
            _telemetryInRange = inRange;
            _telemetryCanHoldFire = canHoldFire;
            _telemetryTargetShootableOnCamera = targetShootableOnCamera;
            _telemetryShootBlockedByBunkerMove = shootBlockedByBunkerMove;
            _telemetryRawShootHeld = rawShootHeld;
            _telemetryImmediateGroundParatrooperThreat = isImmediateGroundParatrooperThreat;
            if (target == null)
            {
                _telemetryTargetKind = "none";
                _telemetryTargetParatrooperState = StickmanBodyState.Die;
            }
            else if (IsParatrooperCollider(target))
            {
                _telemetryTargetKind = "paratrooper";
                _telemetryTargetParatrooperState = GetParatrooperStateOrDie(target);
            }
            else if (IsAircraftCollider(target))
            {
                _telemetryTargetKind = "aircraft";
                _telemetryTargetParatrooperState = StickmanBodyState.Die;
            }
            else
            {
                _telemetryTargetKind = "other";
                _telemetryTargetParatrooperState = StickmanBodyState.Die;
            }

            // Watchdog uses max(aim, shoot): firing with a fallback aim point must still count as "aim" activity.
            if (hasTarget || rawShootHeld)
            {
                _lastAimAtEnemyUnscaledTime = Time.unscaledTime;
            }

            bool reload = _hero.ShouldShowReloadPrompt();

            _view.SetAutoAimWorldOverride(hasTarget ? aimPoint : heroPos + Vector2.right * 6f);
            _input.SetBotFrame(move, shootHeld, reload);
        }

        private void TickLowHpWarningSoundForDebug()
        {
            if (!_enableLowHpWarningSoundForDebug || _model == null || _model.isDead || _lowHpWarningClip == null)
            {
                _lowHpWarningActiveLastTick = false;
                return;
            }

            int threshold = Mathf.Max(1, _lowHpWarningThresholdHp);
            bool lowHpActive = _model.currentHealth > 0 && _model.currentHealth <= threshold;
            if (!lowHpActive)
            {
                _lowHpWarningActiveLastTick = false;
                return;
            }

            bool justActivated = !_lowHpWarningActiveLastTick;
            if (!justActivated && Time.unscaledTime < _nextLowHpWarningAtUnscaled)
            {
                _lowHpWarningActiveLastTick = true;
                return;
            }

            if (_lowHpWarningAudioSource != null)
            {
                _lowHpWarningAudioSource.PlayOneShot(_lowHpWarningClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(_lowHpWarningClip, _model.transform.position, 1f);
            }

            _nextLowHpWarningAtUnscaled =
                Time.unscaledTime + Mathf.Max(0.1f, _lowHpWarningRepeatSeconds);
            _lowHpWarningActiveLastTick = true;
        }

        private void TryPreventAmmoDeadlockInAutomation()
        {
            if (!_enableAutomationRunLoop || !_autoRefillAmmoWhenDryInAutomation)
            {
                return;
            }

            if (_hero == null || _model == null || _model.isDead)
            {
                return;
            }

            // Deadlock symptom from telemetry runs: bot has no rounds in mag nor reserve, can no longer
            // clear threats, and wave never progresses. For automation this should be treated as a test
            // harness refill, not gameplay balance.
            if (_model.currentAmmo > 0 || _model.currentReserveAmmo > 0)
            {
                return;
            }

            HeroWeaponDefinition_V2 activeDef = _model.currentWeaponDefinition;
            if (activeDef == null)
            {
                return;
            }

            bool refilled = _hero.TryRefillWeaponMagazine(activeDef);
            if (refilled)
            {
                LogAutomation(
                    $"Ammo deadlock prevented: refilled active weapon '{activeDef.WeaponType}' to full mag+reserve.");
            }
        }

        private void RefreshAimNoiseForProfile(bool hasTarget)
        {
            if (_testProfile == AutoHeroTestProfileKind_V2.Perfect || !hasTarget)
            {
                _aimNoiseOffset = Vector2.zero;
                return;
            }

            if (Time.unscaledTime >= _nextAimNoiseResampleUnscaled)
            {
                float radius = _testProfile == AutoHeroTestProfileKind_V2.Struggling
                    ? Random.Range(1.1f, 2.6f)
                    : Random.Range(0.35f, 1.05f);
                _aimNoiseOffset = Random.insideUnitCircle * radius;
                _nextAimNoiseResampleUnscaled =
                    Time.unscaledTime + Random.Range(0.14f, 0.42f);
            }
        }

        private bool ApplyProfileToShootHeld(bool rawShootHeld)
        {
            if (_testProfile == AutoHeroTestProfileKind_V2.Perfect)
            {
                _lastRawShootHeld = rawShootHeld;
                return rawShootHeld;
            }

            if (!rawShootHeld)
            {
                _lastRawShootHeld = false;
                return false;
            }

            if (!_lastRawShootHeld)
            {
                float minDelay = _testProfile == AutoHeroTestProfileKind_V2.HumanLike ? 0.05f : 0.14f;
                float maxDelay = _testProfile == AutoHeroTestProfileKind_V2.HumanLike ? 0.2f : 0.48f;
                _shootEngageAllowedAfterUnscaled = Time.unscaledTime + Random.Range(minDelay, maxDelay);
            }

            _lastRawShootHeld = true;
            return Time.unscaledTime >= _shootEngageAllowedAfterUnscaled;
        }

        private void MaybeSelectWeaponForThreat(Vector2 heroPos, Vector2 enemyPos, bool hasTarget, Collider2D targetCollider)
        {
            if (!hasTarget)
            {
                return;
            }

            if (GameplaySceneRules_V2.IsColtOnlyRun())
            {
                if (_hero.HasUsableAmmoForWeaponType(WeaponType.Colt45))
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Colt45);
                }
                else
                {
                    _hero.TrySwitchToAnyWeaponWithAmmo();
                }

                return;
            }

            float skipOptimalWeaponChance = _testProfile == AutoHeroTestProfileKind_V2.Struggling
                ? 0.38f
                : _testProfile == AutoHeroTestProfileKind_V2.HumanLike
                    ? 0.1f
                    : 0f;
            if (skipOptimalWeaponChance > 0f && Random.value < skipOptimalWeaponChance)
            {
                return;
            }

            float horiz = Mathf.Abs(enemyPos.x - heroPos.x);
            float worldDist = Vector2.Distance(heroPos, enemyPos);
            bool vsAircraft = targetCollider != null && IsAircraftCollider(targetCollider);
            float metric = vsAircraft ? worldDist : horiz;

            float bazookaRangeBudget = _bazookaPreferWithinHorizontal;
            if (vsAircraft)
            {
                bazookaRangeBudget = Mathf.Max(
                    bazookaRangeBudget,
                    _bazookaAntiAirMaxWorldDistance);
                if (IsBombingAircraftCollider(targetCollider))
                {
                    bazookaRangeBudget = Mathf.Max(
                        bazookaRangeBudget,
                        _bazookaPreferWithinHorizontalForBombingAircraft);
                }
            }

            bool wantBazookaRange = metric <= bazookaRangeBudget;
            bool wantCarbineRange = metric > bazookaRangeBudget * 1.15f;

            bool bazookaAmmo =
                _hero.HasUnlockedWeaponOfType(WeaponType.Bazooka) &&
                _hero.HasUsableAmmoForWeaponType(WeaponType.Bazooka);
            bool carbineAmmo =
                _hero.HasUnlockedWeaponOfType(WeaponType.Carbine) &&
                _hero.HasUsableAmmoForWeaponType(WeaponType.Carbine);
            bool thompsonAmmo =
                _hero.HasUnlockedWeaponOfType(WeaponType.Thompson) &&
                _hero.HasUsableAmmoForWeaponType(WeaponType.Thompson);

            // Grounded/combat-ready paratroopers can throw/shoot right after landing.
            // Prefer high sustained DPS weapons over range heuristics in this case.
            if (targetCollider != null &&
                IsParatrooperCollider(targetCollider) &&
                IsGroundCombatParatrooper(targetCollider))
            {
                if (thompsonAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Thompson);
                    return;
                }

                if (carbineAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Carbine);
                    return;
                }

                if (bazookaAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Bazooka);
                    return;
                }

                _hero.TrySwitchToAnyWeaponWithAmmo();
                return;
            }

            if (wantBazookaRange)
            {
                if (bazookaAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Bazooka);
                    return;
                }

                if (carbineAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Carbine);
                    return;
                }

                if (thompsonAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Thompson);
                    return;
                }

                _hero.TrySwitchToAnyWeaponWithAmmo();
                return;
            }

            if (wantCarbineRange)
            {
                if (carbineAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Carbine);
                    return;
                }

                if (thompsonAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Thompson);
                    return;
                }

                if (bazookaAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Bazooka);
                    return;
                }

                _hero.TrySwitchToAnyWeaponWithAmmo();
                return;
            }

            // Hysteresis band: do not bounce weapons, but escape a totally dry current weapon.
            if (!_hero.HasUsableAmmoForWeaponType(_model.currentWeaponType))
            {
                _hero.TrySwitchToAnyWeaponWithAmmo();
            }
        }

        private static Plane[] TryGetShootVisibilityFrustumPlanes(Camera cam)
        {
            if (cam == null || !cam.isActiveAndEnabled)
            {
                return null;
            }

            return GeometryUtility.CalculateFrustumPlanes(cam);
        }

        private Plane[] TryGetShootVisibilityFrustumPlanes()
        {
            Camera cam = _shootVisibilityCamera != null ? _shootVisibilityCamera : Camera.main;
            return TryGetShootVisibilityFrustumPlanes(cam);
        }

        private Collider2D FindNearestEnemyCollider(Vector2 from, Plane[] shootFrustumPlanes, bool bombSplashThreat)
        {
            if (_enemyBodyPartLayer < 0)
            {
                return null;
            }

            ContactFilter2D filter = default;
            filter.SetLayerMask(1 << _enemyBodyPartLayer);
            filter.useLayerMask = true;
            filter.useTriggers = true;

            int count = Physics2D.OverlapCircle(from, _enemySearchRadius, filter, _overlapBuffer);
            if (count <= 0)
            {
                Collider2D fallbackOnEmptyOverlap = FindAnyLivingParatrooperColliderFallback(from);
                if (_debugFallbackTargetingLogs && fallbackOnEmptyOverlap != null)
                {
                    Vector2 c = fallbackOnEmptyOverlap.bounds.center;
                    Debug.Log(
                        $"[AutoHero_V2 Fallback] overlap-empty stage={_telemetryLastFallbackStage} " +
                        $"livingModels={_telemetryFallbackLivingParatrooperModels} enabledHitboxes={_telemetryFallbackEnabledEnemyBodyPartColliders} " +
                        $"-> selected='{fallbackOnEmptyOverlap.name}' center=({c.x:0.##},{c.y:0.##}).");
                }

                return fallbackOnEmptyOverlap;
            }

            Collider2D bestAny = null;
            float bestAnyDist = float.MaxValue;
            Collider2D bestInfantry = null;
            float bestInfantryDist = float.MaxValue;
            float bestInfantryScore = float.NegativeInfinity;
            int bestInfantryTier = int.MinValue;
            bool hasGroundedInfantryCandidate = false;
            Collider2D bestAircraft = null;
            float bestAircraftDist = float.MaxValue;
            Collider2D bestBombingAircraft = null;
            float bestBombingAircraftDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider2D c = _overlapBuffer[i];
                if (c == null)
                {
                    continue;
                }

                bool isParatrooper = IsParatrooperCollider(c);
                if (isParatrooper && !IsViableParatrooperTarget(c))
                {
                    continue;
                }

                if (shootFrustumPlanes != null &&
                    !GeometryUtility.TestPlanesAABB(shootFrustumPlanes, c.bounds))
                {
                    continue;
                }

                Vector2 p = c.bounds.center;
                float d = (p - from).sqrMagnitude;
                if (d < bestAnyDist)
                {
                    bestAnyDist = d;
                    bestAny = c;
                }

                if (IsBombingAircraftCollider(c))
                {
                    float horiz = Mathf.Abs(p.x - from.x);
                    if (horiz <= Mathf.Max(0.5f, _aimBombingAircraftWhileBombsFallMaxHoriz) && d < bestBombingAircraftDist)
                    {
                        bestBombingAircraftDist = d;
                        bestBombingAircraft = c;
                    }
                }

                if (isParatrooper)
                {
                    StickmanBodyState paratrooperState = GetParatrooperStateOrDie(c);
                    int infantryTier = GetParatrooperPriorityTier(paratrooperState);
                    float infantryScore = -d;
                    if (_prioritizeGroundedParatroopers && IsGroundCombatState(paratrooperState))
                    {
                        infantryScore += Mathf.Max(0f, _groundedParatrooperPriorityBonus);
                    }
                    if (IsGroundCombatState(paratrooperState))
                    {
                        hasGroundedInfantryCandidate = true;
                    }
                    if (paratrooperState == StickmanBodyState.Shoot || paratrooperState == StickmanBodyState.Grenade)
                    {
                        infantryScore += Mathf.Max(0f, _combatParatrooperPriorityBonus);
                    }
                    else if (paratrooperState == StickmanBodyState.Deploy ||
                             paratrooperState == StickmanBodyState.Glide ||
                             paratrooperState == StickmanBodyState.GlideElectrocuted)
                    {
                        infantryScore -= Mathf.Max(0f, _airborneParatrooperPriorityPenalty);
                    }

                    if (infantryTier > bestInfantryTier ||
                        (infantryTier == bestInfantryTier && infantryScore > bestInfantryScore) ||
                        (Mathf.Approximately(infantryScore, bestInfantryScore) && d < bestInfantryDist))
                    {
                        bestInfantryTier = infantryTier;
                        bestInfantryScore = infantryScore;
                        bestInfantryDist = d;
                        bestInfantry = c;
                    }
                }
                else if (IsAircraftCollider(c))
                {
                    if (d < bestAircraftDist)
                    {
                        bestAircraftDist = d;
                        bestAircraft = c;
                    }
                }
            }

            if (_ignoreAirborneWhenGroundedExists && hasGroundedInfantryCandidate && bestInfantry != null)
            {
                StickmanBodyState bestState = GetParatrooperStateOrDie(bestInfantry);
                if (!IsGroundCombatState(bestState))
                {
                    // Re-scan candidates and pick the nearest grounded target deterministically.
                    Collider2D bestGroundedInfantry = null;
                    float bestGroundedDist = float.MaxValue;
                    for (int i = 0; i < count; i++)
                    {
                        Collider2D c = _overlapBuffer[i];
                        if (c == null || !IsParatrooperCollider(c) || !IsViableParatrooperTarget(c))
                        {
                            continue;
                        }

                        if (shootFrustumPlanes != null &&
                            !GeometryUtility.TestPlanesAABB(shootFrustumPlanes, c.bounds))
                        {
                            continue;
                        }

                        StickmanBodyState s = GetParatrooperStateOrDie(c);
                        if (!IsGroundCombatState(s))
                        {
                            continue;
                        }

                        Vector2 center = c.bounds.center;
                        float d = (center - from).sqrMagnitude;
                        if (d < bestGroundedDist)
                        {
                            bestGroundedDist = d;
                            bestGroundedInfantry = c;
                        }
                    }

                    if (bestGroundedInfantry != null)
                    {
                        bestInfantry = bestGroundedInfantry;
                        bestInfantryDist = bestGroundedDist;
                        bestInfantryScore = -bestGroundedDist;
                    }
                }
            }

            if (_aimBombingAircraftWhileBombsFall &&
                bombSplashThreat &&
                bestBombingAircraft != null &&
                (_prioritizeInfantryOverAircraft ? bestInfantry != null : true))
            {
                _telemetryLastFallbackStage = "not_used";
                MaybeLogTargetSelectionDebug(bestBombingAircraft, -bestBombingAircraftDist, "bombplane-override");
                return bestBombingAircraft;
            }

            if (_prioritizeInfantryOverAircraft && bestInfantry != null)
            {
                _telemetryLastFallbackStage = "not_used";
                MaybeLogTargetSelectionDebug(bestInfantry, bestInfantryScore, "infantry-priority");
                return bestInfantry;
            }

            if (bestInfantry != null && bestAircraft != null)
            {
                _telemetryLastFallbackStage = "not_used";
                Collider2D selected = bestInfantryDist <= bestAircraftDist ? bestInfantry : bestAircraft;
                float selectedScore = selected == bestInfantry ? bestInfantryScore : -bestAircraftDist;
                MaybeLogTargetSelectionDebug(selected, selectedScore, "nearest-infantry-vs-aircraft");
                return selected;
            }

            if (bestInfantry != null)
            {
                _telemetryLastFallbackStage = "not_used";
                MaybeLogTargetSelectionDebug(bestInfantry, bestInfantryScore, "infantry-only");
                return bestInfantry;
            }

            if (bestAircraft != null)
            {
                _telemetryLastFallbackStage = "not_used";
                MaybeLogTargetSelectionDebug(bestAircraft, -bestAircraftDist, "aircraft-only");
                return bestAircraft;
            }

            Collider2D fallback = FindAnyLivingParatrooperColliderFallback(from);
            if (fallback != null)
            {
                if (_debugFallbackTargetingLogs)
                {
                    Vector2 c = fallback.bounds.center;
                    Debug.Log(
                        $"[AutoHero_V2 Fallback] post-filter-no-target stage={_telemetryLastFallbackStage} " +
                        $"livingModels={_telemetryFallbackLivingParatrooperModels} enabledHitboxes={_telemetryFallbackEnabledEnemyBodyPartColliders} " +
                        $"-> selected='{fallback.name}' center=({c.x:0.##},{c.y:0.##}).");
                }
                MaybeLogTargetSelectionDebug(fallback, float.NegativeInfinity, "fallback-living-paratrooper");
                return fallback;
            }

            _telemetryLastFallbackStage = "none_found";
            MaybeLogTargetSelectionDebug(null, float.NegativeInfinity, "no-target");
            return bestAny;
        }

        /// <summary>
        /// Safety fallback for watchdog-sensitive end-of-wave cases where overlap/frustum/layer filtering misses
        /// the last living paratrooper even though one is still active in the scene.
        /// </summary>
        private Collider2D FindAnyLivingParatrooperColliderFallback(Vector2 from)
        {
            _telemetryLastFallbackStage = "none_found";
            _telemetryFallbackLivingParatrooperModels = 0;
            _telemetryFallbackEnabledEnemyBodyPartColliders = 0;

            ParatrooperBodyPart_V2[] parts = Object.FindObjectsByType<ParatrooperBodyPart_V2>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Collider2D best = null;
            float bestDist = float.MaxValue;
            int enemyBodyPartLayer = LayerMask.NameToLayer("EnemyBodyPart");
            if (parts != null)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    ParatrooperBodyPart_V2 part = parts[i];
                    if (part == null || !part.isActiveAndEnabled || !part.IsLivingCharacterForTargeting())
                    {
                        continue;
                    }

                    Collider2D col = part.GetComponent<Collider2D>();
                    if (col == null || !col.enabled)
                    {
                        continue;
                    }
                    if (col.gameObject.layer == enemyBodyPartLayer)
                    {
                        _telemetryFallbackEnabledEnemyBodyPartColliders++;
                    }

                    float d = (((Vector2)col.bounds.center) - from).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = col;
                    }
                }
            }

            if (best != null)
            {
                _telemetryLastFallbackStage = "bodypart";
                return best;
            }

            // Second-pass fallback by model/state in case bodypart cache or component wiring is temporarily stale.
            ParatrooperModel_V2[] models = Object.FindObjectsByType<ParatrooperModel_V2>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            if (models == null || models.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < models.Length; i++)
            {
                ParatrooperModel_V2 model = models[i];
                if (model == null || model.IsDead())
                {
                    continue;
                }
                _telemetryFallbackLivingParatrooperModels++;

                Collider2D[] cols = model.GetComponentsInChildren<Collider2D>(true);
                for (int c = 0; c < cols.Length; c++)
                {
                    Collider2D col = cols[c];
                    if (col == null || !col.enabled)
                    {
                        continue;
                    }

                    // Prefer true hitboxes used by targeting/raycasting.
                    if (col.gameObject.layer != enemyBodyPartLayer)
                    {
                        continue;
                    }
                    _telemetryFallbackEnabledEnemyBodyPartColliders++;

                    float d = (((Vector2)col.bounds.center) - from).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = col;
                    }
                }
            }

            if (best != null)
            {
                _telemetryLastFallbackStage = "model";
            }
            return best;
        }

        private void MaybeLogTargetSelectionDebug(Collider2D selected, float score, string reason)
        {
            if (!_debugTargetSelectionLogs)
            {
                return;
            }

            if (_waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < _nextTargetSelectionDebugLogUnscaledTime)
            {
                return;
            }

            _nextTargetSelectionDebugLogUnscaledTime =
                now + Mathf.Max(0.1f, _debugTargetSelectionLogIntervalSeconds);

            if (selected == null)
            {
                Debug.Log($"[AutoHero_V2 TargetDbg] selected=(none) reason={reason}");
                return;
            }

            StickmanBodyState s = GetParatrooperStateOrDie(selected);
            bool isPara = IsParatrooperCollider(selected);
            Vector2 c = selected.bounds.center;
            Debug.Log(
                $"[AutoHero_V2 TargetDbg] selected='{selected.name}' para={isPara} state={s} " +
                $"center=({c.x:0.##},{c.y:0.##}) score={score:0.##} reason={reason}");
        }

        private static bool IsParatrooperCollider(Collider2D c)
        {
            if (c == null)
            {
                return false;
            }

            return c.GetComponent<ParatrooperBodyPart_V2>() != null ||
                   c.GetComponentInParent<ParatrooperBodyPart_V2>() != null;
        }

        /// <summary>
        /// Dead paratroopers often keep EnemyBodyPart colliders during ragdoll/ground clips; overlap would otherwise
        /// keep aiming at the corpse while a new paratrooper spawns at nearly the same X.
        /// </summary>
        private static bool IsViableParatrooperTarget(Collider2D c)
        {
            ParatrooperBodyPart_V2 part =
                c.GetComponent<ParatrooperBodyPart_V2>() ?? c.GetComponentInParent<ParatrooperBodyPart_V2>();
            if (part == null)
            {
                return false;
            }

            return part.IsLivingCharacterForTargeting();
        }

        private static bool IsAircraftCollider(Collider2D c)
        {
            if (c == null)
            {
                return false;
            }

            return c.GetComponent<AircraftHealth_V2>() != null ||
                   c.GetComponentInParent<AircraftHealth_V2>() != null;
        }

        private static bool IsBombingAircraftCollider(Collider2D c)
        {
            if (!IsAircraftCollider(c))
            {
                return false;
            }

            return c.GetComponentInParent<Bombplane_V2>() != null;
        }

        private bool IsBombSplashThreatActive(Vector2 heroPos)
        {
            BombProjectile_V2[] bombs = Object.FindObjectsByType<BombProjectile_V2>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            if (bombs == null || bombs.Length == 0)
            {
                return false;
            }

            float maxBelow = Mathf.Max(0f, _bombSplashThreatMaxBelowHero);
            float horizExtra = Mathf.Max(0f, _bombSplashThreatHorizExtra);

            for (int i = 0; i < bombs.Length; i++)
            {
                BombProjectile_V2 bomb = bombs[i];
                if (bomb == null || !bomb.isActiveAndEnabled)
                {
                    continue;
                }

                Vector2 bp = bomb.transform.position;
                float dx = Mathf.Abs(bp.x - heroPos.x);
                float dy = bp.y - heroPos.y;

                if (dy > 26f)
                {
                    continue;
                }

                float threatRadius = 2.1f + horizExtra;
                Rigidbody2D rb = bomb.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    float speed = rb.linearVelocity.magnitude;
                    threatRadius = Mathf.Max(threatRadius, speed * 0.34f + horizExtra);
                }

                if (dx <= threatRadius && dy >= -maxBelow)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector2 ResolveAimPointForTarget(Collider2D target, Vector2 heroPos)
        {
            if (target == null)
            {
                return heroPos + Vector2.right * 8f;
            }

            // Perfect profile should lock paratrooper aim to torso/head hitboxes to avoid foot targeting.
            if (_testProfile == AutoHeroTestProfileKind_V2.Perfect && IsParatrooperCollider(target))
            {
                Vector2? preferredPoint = TryResolveParatrooperTorsoOrHeadAimPoint(target, heroPos);
                if (preferredPoint.HasValue)
                {
                    return preferredPoint.Value;
                }
            }

            return target.bounds.center;
        }

        private Vector2? TryResolveParatrooperTorsoOrHeadAimPoint(Collider2D target, Vector2 heroPos)
        {
            ParatrooperBodyPart_V2 selectedPart =
                target.GetComponent<ParatrooperBodyPart_V2>() ?? target.GetComponentInParent<ParatrooperBodyPart_V2>();
            if (selectedPart == null)
            {
                return null;
            }

            ParatrooperModel_V2 paratrooperModel = selectedPart.GetComponentInParent<ParatrooperModel_V2>();
            Transform paratrooperRoot = paratrooperModel != null ? paratrooperModel.transform : selectedPart.transform.root;
            _paratrooperBodyPartBuffer.Clear();
            paratrooperRoot.GetComponentsInChildren(true, _paratrooperBodyPartBuffer);

            Vector2? bestHead = null;
            float bestHeadDist = float.MaxValue;
            Vector2? bestTorso = null;
            float bestTorsoDist = float.MaxValue;

            for (int i = 0; i < _paratrooperBodyPartBuffer.Count; i++)
            {
                ParatrooperBodyPart_V2 part = _paratrooperBodyPartBuffer[i];
                if (part == null || !part.isActiveAndEnabled)
                {
                    continue;
                }

                Collider2D col = part.GetComponent<Collider2D>();
                if (col == null || !col.enabled)
                {
                    continue;
                }

                Vector2 center = col.bounds.center;
                float dist = (center - heroPos).sqrMagnitude;
                if (part.bodyPart == BodyPartType.Torso && dist < bestTorsoDist)
                {
                    bestTorsoDist = dist;
                    bestTorso = center;
                }
                else if (part.bodyPart == BodyPartType.Head && dist < bestHeadDist)
                {
                    bestHeadDist = dist;
                    bestHead = center;
                }
            }

            if (bestTorso.HasValue)
            {
                return bestTorso.Value;
            }

            if (bestHead.HasValue)
            {
                return bestHead.Value;
            }

            return null;
        }

        private static bool IsGroundCombatParatrooper(Collider2D c)
        {
            StickmanBodyState s = GetParatrooperStateOrDie(c);
            return IsGroundCombatState(s);
        }

        private static int GetParatrooperPriorityTier(StickmanBodyState s)
        {
            if (s == StickmanBodyState.Shoot ||
                s == StickmanBodyState.Grenade ||
                s == StickmanBodyState.CrouchShoot ||
                s == StickmanBodyState.CrouchGrenade)
            {
                return 3;
            }

            if (IsGroundCombatState(s))
            {
                return 2;
            }

            if (s == StickmanBodyState.Deploy || s == StickmanBodyState.Glide || s == StickmanBodyState.GlideDie ||
                s == StickmanBodyState.GlideElectrocuted)
            {
                return 0;
            }

            return 1;
        }

        private static bool IsGroundCombatState(StickmanBodyState s)
        {
            return s == StickmanBodyState.Land ||
                   s == StickmanBodyState.Shoot ||
                   s == StickmanBodyState.Grenade ||
                   s == StickmanBodyState.CrouchShoot ||
                   s == StickmanBodyState.CrouchGrenade ||
                   s == StickmanBodyState.CrouchIdle ||
                   s == StickmanBodyState.CrouchWalk ||
                   s == StickmanBodyState.CrouchReload ||
                   s == StickmanBodyState.Run ||
                   s == StickmanBodyState.Idle ||
                   s == StickmanBodyState.Electrocuted;
        }
        
        private static StickmanBodyState GetParatrooperStateOrDie(Collider2D c)
        {
            if (c == null)
            {
                return StickmanBodyState.Die;
            }

            ParatrooperBodyPart_V2 part =
                c.GetComponent<ParatrooperBodyPart_V2>() ?? c.GetComponentInParent<ParatrooperBodyPart_V2>();
            if (part != null)
            {
                ParatrooperModel_V2 model = part.GetComponentInParent<ParatrooperModel_V2>();
                if (model != null)
                {
                    return model.currentState;
                }
            }

            ParatrooperStateMachine_V2 sm = c.GetComponentInParent<ParatrooperStateMachine_V2>();
            if (sm == null)
            {
                if (part != null)
                {
                    ParatrooperModel_V2 model = part.GetComponentInParent<ParatrooperModel_V2>();
                    sm = model != null
                        ? model.GetComponent<ParatrooperStateMachine_V2>() ?? model.GetComponentInParent<ParatrooperStateMachine_V2>()
                        : null;
                }
            }

            return sm != null ? sm.CurrentState : StickmanBodyState.Die;
        }

        private static Vector2? TryGetBunkerInteriorWorldPoint()
        {
            BunkerInteriorZone_V2 zone =
                FindAnyObjectByType<BunkerInteriorZone_V2>(FindObjectsInactive.Include);
            Collider2D col = zone != null ? zone.GetComponent<Collider2D>() : null;
            if (col == null)
            {
                return null;
            }

            return col.bounds.center;
        }
    }
}
