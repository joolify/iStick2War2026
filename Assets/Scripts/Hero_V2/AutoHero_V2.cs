using System.Collections.Generic;
using iStick2War;
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
        [SerializeField] private int _automationTotalRuns = 10;
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
        private float _nextAutomationActionAtUnscaled;
        private PendingAutomationAction _pendingAutomationAction = PendingAutomationAction.None;

        private enum PendingAutomationAction
        {
            None,
            ClickGameOverContinue,
            ClickGameWonContinue,
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

        private void Awake()
        {
            CacheReferences();
            _enemyBodyPartLayer = LayerMask.NameToLayer("EnemyBodyPart");
            ApplySceneGameplayBotOverride();
            if (_forcePerfectProfile)
            {
                _testProfile = AutoHeroTestProfileKind_V2.Perfect;
            }
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

            TryExecutePendingAutomationAction();
            if (_pendingAutomationAction != PendingAutomationAction.None)
            {
                return;
            }

            if (state == WaveLoopState_V2.InWave && !_hero.IsDead())
            {
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
                }

                if (_completedRuns < Mathf.Max(1, _automationTotalRuns))
                {
                    QueueAutomationAction(PendingAutomationAction.ClickGameOverContinue);
                }
                else
                {
                    LogAutomation("Automation finished after max runs (GameOver).");
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
                }

                if (_completedRuns < Mathf.Max(1, _automationTotalRuns))
                {
                    QueueAutomationAction(PendingAutomationAction.ClickGameWonContinue);
                }
                else
                {
                    LogAutomation("Automation finished after max runs (GameWon).");
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
                FindObjectsByType<MainMenuNavButton_V2>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
                FindObjectsByType<MainMenuNavButton_V2>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
                FindObjectsByType<MainMenuNavButton_V2>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
                    if (d < bestInfantryDist)
                    {
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
