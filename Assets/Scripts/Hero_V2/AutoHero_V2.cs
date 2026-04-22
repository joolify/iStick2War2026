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
        [Tooltip(
            "When true, any Paratrooper body hitbox in the search radius wins over the helicopter. " +
            "Otherwise the single nearest EnemyBodyPart collider is used (aircraft often steals the aim).")]
        [SerializeField] private bool _prioritizeInfantryOverAircraft = true;
        [Tooltip("When true, grounded/combat-ready paratroopers get higher target priority than airborne paratroopers.")]
        [SerializeField] private bool _prioritizeGroundedParatroopers = true;
        [Tooltip("Extra score for grounded/combat-ready paratroopers when selecting target.")]
        [SerializeField] private float _groundedParatrooperPriorityBonus = 220f;

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
        private int _enemyBodyPartLayer = -1;
        private WaveLoopState_V2 _lastWaveState = WaveLoopState_V2.Preparing;
        private int _shopPhasesCompleted;
        private bool _shopExitScheduledThisVisit;
        private float _nextWaveCombatNoTargetLogUnscaledTime;
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

            if (_hero == null || _model == null || _view == null || _input == null)
            {
                return;
            }

            if (!_hero.isActiveAndEnabled)
            {
                return;
            }

            _input.SetBotDriving(true);

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
            bool wantBunker =
                bunkerAnchor.HasValue &&
                hpRatio < _lowHealthEnterBunkerFraction &&
                !inside;

            Plane[] shootFrustumPlanes = TryGetShootVisibilityFrustumPlanes();
            Collider2D target = FindNearestEnemyCollider(heroPos, shootFrustumPlanes);
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
                aimPoint = target.bounds.center;
                hasTarget = true;
            }

            RefreshAimNoiseForProfile(hasTarget);
            if (hasTarget && _testProfile != AutoHeroTestProfileKind_V2.Perfect)
            {
                aimPoint += _aimNoiseOffset;
            }

            MaybeSelectWeaponForThreat(heroPos, aimPoint, hasTarget);

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
            float verticalSlack = IsParatrooperCollider(target) ? 1.35f : 0.85f;
            bool inRange =
                hasTarget &&
                Mathf.Abs(aimPoint.x - heroPos.x) <= maxShootDist &&
                Mathf.Abs(aimPoint.y - heroPos.y) <= maxShootDist * verticalSlack;

            // Do not hold fire when the active weapon is completely dry: HeroController_V2 requires a release to
            // clear _outOfAmmoLatched after a dry-fire; holding shoot forever would soft-lock shooting.
            bool canHoldFire =
                _model.currentAmmo > 0 || _model.currentReserveAmmo > 0;

            bool targetShootableOnCamera =
                target == null ||
                shootFrustumPlanes == null ||
                GeometryUtility.TestPlanesAABB(shootFrustumPlanes, target.bounds);

            bool rawShootHeld = inRange && !wantBunker && canHoldFire && targetShootableOnCamera;
            bool shootHeld = ApplyProfileToShootHeld(rawShootHeld);
            if (shootHeld)
            {
                _lastShootHeldUnscaledTime = Time.unscaledTime;
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

        private void MaybeSelectWeaponForThreat(Vector2 heroPos, Vector2 enemyPos, bool hasTarget)
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
            bool wantBazookaRange = horiz <= _bazookaPreferWithinHorizontal;
            bool wantCarbineRange = horiz > _bazookaPreferWithinHorizontal * 1.15f;

            bool bazookaAmmo =
                _hero.HasUnlockedWeaponOfType(WeaponType.Bazooka) &&
                _hero.HasUsableAmmoForWeaponType(WeaponType.Bazooka);
            bool carbineAmmo =
                _hero.HasUnlockedWeaponOfType(WeaponType.Carbine) &&
                _hero.HasUsableAmmoForWeaponType(WeaponType.Carbine);

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

        private Collider2D FindNearestEnemyCollider(Vector2 from, Plane[] shootFrustumPlanes)
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
                return null;
            }

            Collider2D bestAny = null;
            float bestAnyDist = float.MaxValue;
            Collider2D bestInfantry = null;
            float bestInfantryDist = float.MaxValue;
            float bestInfantryScore = float.NegativeInfinity;
            Collider2D bestAircraft = null;
            float bestAircraftDist = float.MaxValue;

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

                if (isParatrooper)
                {
                    float infantryScore = -d;
                    if (_prioritizeGroundedParatroopers && IsGroundCombatParatrooper(c))
                    {
                        infantryScore += Mathf.Max(0f, _groundedParatrooperPriorityBonus);
                    }

                    if (infantryScore > bestInfantryScore ||
                        (Mathf.Approximately(infantryScore, bestInfantryScore) && d < bestInfantryDist))
                    {
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

            if (_prioritizeInfantryOverAircraft && bestInfantry != null)
            {
                return bestInfantry;
            }

            if (bestInfantry != null && bestAircraft != null)
            {
                return bestInfantryDist <= bestAircraftDist ? bestInfantry : bestAircraft;
            }

            if (bestInfantry != null)
            {
                return bestInfantry;
            }

            if (bestAircraft != null)
            {
                return bestAircraft;
            }

            return bestAny;
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

        private static bool IsGroundCombatParatrooper(Collider2D c)
        {
            if (c == null)
            {
                return false;
            }

            ParatrooperStateMachine_V2 sm = c.GetComponentInParent<ParatrooperStateMachine_V2>();
            if (sm == null)
            {
                return false;
            }

            StickmanBodyState s = sm.CurrentState;
            return s == StickmanBodyState.Land ||
                   s == StickmanBodyState.Shoot ||
                   s == StickmanBodyState.Grenade ||
                   s == StickmanBodyState.Run ||
                   s == StickmanBodyState.Idle;
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
