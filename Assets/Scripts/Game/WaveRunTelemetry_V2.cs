using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using iStick2War;

namespace iStick2War_V2
{
    /// <summary>
    /// Bunker damage attribution for <see cref="WaveRunTelemetry_V2"/> (subset; extend as needed).
    /// </summary>
    public enum BunkerDamageTelemetrySource
    {
        Other = 0,
        /// <summary>HP absorbed from <see cref="BombProjectile_V2"/> explosions.</summary>
        Bomb = 1
    }

    /// <summary>
    /// Writes one JSON document per session: <c>{ "events": [ ... ] }</c> (pretty-printed).
    /// Tracks combat, bunker time, bunker damage, and shop spend between waves (attributed to the wave that just ended).
    /// <c>session_begin</c> is written one frame after enable so <see cref="WaveManager_V2"/> economy/bunker has initialized.
    /// </summary>
    public sealed class WaveRunTelemetry_V2 : MonoBehaviour
    {
        public static WaveRunTelemetry_V2 ActiveInstance { get; private set; }

        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private bool _telemetryEnabled = true;
        [SerializeField] private string _fileNamePrefix = "wave_run";
        [SerializeField] private bool _logFilePathOnce = true;

        [Header("Unity console → telemetry (errors / exceptions)")]
        [Tooltip("Subscribe to Application.logMessageReceivedThreaded and append rows to root.unityLogs[] in the session JSON.")]
        [SerializeField] private bool _captureUnityConsoleErrors = true;

        [Tooltip("Also persist LogType.Warning (noisy in some projects).")]
        [SerializeField] private bool _captureUnityWarningsToo;

        [Tooltip("Merge consecutive identical fingerprints into repeatCount instead of spamming rows.")]
        [SerializeField] private bool _unityLogCoalesceConsecutiveDuplicates = true;

        [Tooltip("Cap stored unityLogs[] rows per session (oldest dropped when exceeded).")]
        [SerializeField] [Range(16, 500)]
        private int _unityLogsMaxRowsPerSession = 200;

        [SerializeField] [Range(1024, 65535)]
        private int _unityLogMessageMaxChars = 12000;

        [SerializeField] [Range(0, 65535)]
        private int _unityLogStackMaxChars = 48000;

        [Tooltip("Max queued log lines from worker threads before dropping (burst protection).")]
        [SerializeField] [Range(8, 500)]
        private int _unityLogPendingQueueMax = 100;

        [Header("Derived bunker flags")]
        [Tooltip(
            "bunkerCriticalLow is true when snapshot bunkerHp/maxHp is <= this fraction and bunkerHp > 0. " +
            "bunkerBreached remains strictly bunkerHp == 0.")]
        [SerializeField] [Range(0.02f, 0.5f)]
        private float _bunkerCriticalHpFraction = 0.1f;

        [Header("Bunker HP curve (InWave only)")]
        [Tooltip("How often to append a bunkerHpSamples[] point while InWave (realtime seconds).")]
        [SerializeField] [Range(0.05f, 5f)]
        private float _bunkerHpSampleIntervalSec = 0.5f;

        [Tooltip("Cap samples per wave to keep JSON size bounded.")]
        [SerializeField] [Range(8, 400)]
        private int _bunkerHpSamplesMaxPerWave = 200;

        [Header("Bunker sustained pressure (InWave)")]
        [Tooltip(
            "While InWave, bunkerPressureTimeSec accumulates unscaled time when bunkerHp/maxHp is strictly below this ratio. " +
            "Separate from bunkerCriticalLow (different threshold).")]
        [SerializeField] [Range(0.05f, 0.99f)]
        private float _bunkerPressureHpRatioThreshold = 0.8f;

        private string _sessionId;
        private string _filePath;
        private WaveLoopState_V2 _lastState;
        private float _waveStartedRealtime;
        private bool _runEndWritten;
        private bool _sessionBeginWritten;
        private Coroutine _sessionBeginCoroutine;

        private Hero_V2 _subscribedHero;

        // --- Per-wave combat (reset when entering InWave) ---
        private int _heroDamageTakenDuringWave;
        private int _heroHealedDuringWave;
        private int _bunkerDamageTakenDuringWave;
        private int _bunkerDamageFromBombsTakenDuringWave;
        private float _timeInBunkerUnscaledDuringWave;
        private int _shotsFiredDuringWave;
        private int _rayHitsDuringWave;
        private int _rayMissesDuringWave;
        private int _projectileLaunchesDuringWave;
        private int _reloadsDuringWave;
        private int _empStunsAppliedDuringWave;

        // Snapshots at wave start (InWave)
        private int _heroHpAtWaveStart;
        private int _bunkerHpAtWaveStart;
        private int _currencyAtWaveStart;

        // Shop intermission before the wave that just cleared (set when leaving Shop → Preparing)
        private int _shopPurchasesPriorToWaveJustCleared;
        private int _shopCurrencySpentPriorToWaveJustCleared;
        private string[] _shopOffersBoughtPriorToWaveJustCleared = Array.Empty<string>();

        private int _intermissionShopPurchaseCount;
        private int _intermissionShopCurrencySpent;
        private readonly List<string> _intermissionShopOfferKinds = new List<string>(8);

        private float _minBunkerHpRatioThisWave;
        private List<BunkerHpSamplePoint> _bunkerHpSamplesThisWave;
        private float _nextBunkerSampleRealtime;
        private float _bunkerLowPressureTimeUnscaledDuringWave;
        private float _bunkerLowPressureTimeAfterFirstDamageUnscaledDuringWave;
        private bool _bunkerDamageReceivedThisWave;

        // --- Feel / retention proxies (session; persisted on root.feelSession) ---
        private float _feelSessionBeginRealtime = -1f;
        private float _feelFirstKillRealtime = -1f;
        private float _feelFirstHeroDamageRealtime = -1f;
        private float _feelFirstHeroDeathRealtime = -1f;
        private float _feelFirstShopPurchaseRealtime = -1f;
        private int _feelFirstShopPurchaseCurrencySpent;
        private string _feelFirstShopOfferKind = "";
        private int _feelFirstPurchaseHeroHp;
        private int _feelFirstPurchaseHeroMaxHp;
        private string _feelFirstPurchaseWeaponType = "";

        private bool _suppressUnityLogCapture;
        private bool _unityLogHookRegistered;
        private readonly object _pendingUnityLogsLock = new object();
        private readonly List<PendingUnityLog> _pendingUnityLogs = new List<PendingUnityLog>(8);

        private struct PendingUnityLog
        {
            public string condition;
            public string stackTrace;
            public LogType type;
        }

        [Serializable]
        private sealed class TelemetryUnityLogRow
        {
            public string sessionId;
            public string utcIso8601;
            public float realtimeSinceStartup;
            public string unityEditorOrPlayerVersion;
            public string logType;
            public string messageTruncated;
            public string stackTraceTruncated;
            public string fingerprint;
            public int repeatCount;
            public int wave;
            public string waveLoopState;
            public int heroHp;
            public int heroMaxHp;
            public string weapon;
            public string weaponType;
            public string autoHeroTestProfile;
            public bool autoHeroHasTarget;
            public bool autoHeroInRange;
            public bool autoHeroCanHoldFire;
            public bool autoHeroTargetShootableOnCamera;
            public bool autoHeroShootBlockedByBunkerMove;
            public bool autoHeroRawShootHeld;
            public bool autoHeroImmediateGroundParatrooperThreat;
            public string autoHeroTargetKind;
            public string autoHeroTargetParatrooperState;
            public string autoHeroFallbackStage;
            public int autoHeroFallbackLivingParatrooperModels;
            public int autoHeroFallbackEnabledEnemyBodyPartColliders;
            public string sceneProfileId;
            public float heroPosX;
            public float heroPosY;
            public int heroAmmoInMag;
            public int heroAmmoMagMax;
            public int heroReserveAmmo;
            public int bunkerHp;
            public int bunkerMaxHp;
            public int currency;
            public int enemiesKilledThisWave;
            public int trackedLivingParatroopers;
            public float timeUnscaled;
            public float timeSinceLevelLoad;
            public float timeScale;
            public int frameCount;
            public string activeScenePathOrName;
            public int loadedSceneCount;
            public bool isEditor;
            public string platform;
            public string internetReachability;
            public long managedHeapBytes;
            public float inWaveUnscaledSec;
            public int damageTakenHeroThisWave;
            public int damageTakenBunkerThisWave;
            public int damageTakenBunkerFromBombsThisWave;
            public int shotsFiredThisWave;
            public float bunkerPressureTimeUnscaledThisWave;
            public string scalingSnapshotShort;
            public string spawnerDiagnosticsLine;
        }

        [Serializable]
        private sealed class BunkerHpSamplePoint
        {
            /// <summary>Seconds since this wave entered InWave (matches <see cref="TelemetryEvent.waveDurationSec"/> basis).</summary>
            public float waveTimeSecSinceInWaveRealtime;
            public int bunkerHp;
            public int bunkerMaxHp;
            public float bunkerHpRatio;
        }

        [Serializable]
        private sealed class TelemetryGlossaryEntry
        {
            public string property;
            public string meaning;
        }

        [Serializable]
        private sealed class TelemetryGlossary
        {
            public string title;
            public TelemetryGlossaryEntry[] entries;
        }

        [Serializable]
        private sealed class TelemetryFileRoot
        {
            /// <summary>Human-readable overview; safe for consumers to ignore when parsing <c>events</c>.</summary>
            public string _comment;
            public TelemetryGlossary glossary;

            /// <summary>Clamped fraction used for <see cref="TelemetryEvent.bunkerCriticalLow"/> on each event in this file.</summary>
            public float bunkerCriticalHpFractionUsed;

            public float bunkerHpSampleIntervalSecUsed;
            public int bunkerHpSamplesMaxPerWaveUsed;

            /// <summary>Clamped ratio threshold for <see cref="TelemetryEvent.bunkerPressureTimeSec"/> (Inspector echo).</summary>
            public float bunkerPressureHpRatioThresholdUsed;

            public TelemetryEvent[] events;

            /// <summary>Optional: Unity console errors/exceptions with a gameplay snapshot (same sessionId as events[]).</summary>
            public TelemetryUnityLogRow[] unityLogs;

            /// <summary>Optional: design/feel proxies (milestones + deltas vs session_begin realtime).</summary>
            public TelemetryFeelSessionSummary feelSession;

            /// <summary>Optional: prefab pool counters snapshot (SimplePrefabPool_V2) at write time.</summary>
            public SimplePrefabPool_V2.PoolStatsSnapshot poolStats;
        }

        [Serializable]
        private sealed class TelemetryFeelSessionSummary
        {
            /// <summary><see cref="Time.realtimeSinceStartup"/> when session_begin row was written; -1 if unknown.</summary>
            public float sessionBeginRealtimeSinceStartup;

            public float firstKillRealtimeSinceStartup;
            public float firstHeroDamageRealtimeSinceStartup;
            public float firstHeroDeathRealtimeSinceStartup;
            public float firstShopPurchaseRealtimeSinceStartup;

            /// <summary>Seconds after session_begin; -1 if milestone never occurred.</summary>
            public float firstKillSecSinceSessionBegin;

            public float firstHeroDamageSecSinceSessionBegin;
            public float firstHeroDeathSecSinceSessionBegin;
            public float firstShopPurchaseSecSinceSessionBegin;

            public int firstShopPurchaseCurrencySpent;
            public string firstShopOfferKind;
            public int firstPurchaseHeroHp;
            public int firstPurchaseHeroMaxHp;
            public string firstPurchaseWeaponType;
        }

        [Serializable]
        private sealed class TelemetryWaveScaling
        {
            public string scalingVersion;
            public float balanceEnemyHpMultiplier;
            public float balanceEnemyDamageMultiplier;
            public float balanceSpawnRateMultiplier;
            public float balanceWaveRewardMultiplier;
            public float configEnemyHpMultiplier;
            public float configEnemyDamageMultiplier;
            public float configSpawnIntervalSeconds;
            public int configWaveRewardCurrency;
            public float effectiveEnemyHpMultiplier;
            public float effectiveEnemyDamageMultiplier;
            public float effectiveSpawnIntervalSeconds;
            public int effectiveWaveRewardCurrency;

            public static TelemetryWaveScaling FromSnapshot(WaveRunScalingSnapshot s)
            {
                return new TelemetryWaveScaling
                {
                    scalingVersion = s.ScalingVersion ?? "",
                    balanceEnemyHpMultiplier = s.BalanceEnemyHpMultiplier,
                    balanceEnemyDamageMultiplier = s.BalanceEnemyDamageMultiplier,
                    balanceSpawnRateMultiplier = s.BalanceSpawnRateMultiplier,
                    balanceWaveRewardMultiplier = s.BalanceWaveRewardMultiplier,
                    configEnemyHpMultiplier = s.ConfigEnemyHpMultiplier,
                    configEnemyDamageMultiplier = s.ConfigEnemyDamageMultiplier,
                    configSpawnIntervalSeconds = s.ConfigSpawnIntervalSeconds,
                    configWaveRewardCurrency = s.ConfigWaveRewardCurrency,
                    effectiveEnemyHpMultiplier = s.EffectiveEnemyHpMultiplier,
                    effectiveEnemyDamageMultiplier = s.EffectiveEnemyDamageMultiplier,
                    effectiveSpawnIntervalSeconds = s.EffectiveSpawnIntervalSeconds,
                    effectiveWaveRewardCurrency = s.EffectiveWaveRewardCurrency
                };
            }
        }

        [Serializable]
        private sealed class TelemetryEvent
        {
            public string kind;
            public string sessionId;
            public float realtimeSinceStartup;
            public int wave;
            public float waveDurationSec;
            public int heroHp;
            public int heroMaxHp;
            public int bunkerHp;
            public int bunkerMaxHp;
            public int currency;
            public int enemiesKilled;
            public string weapon;
            public string endReason;

            public int damageTakenHero;
            public int healingHero;
            public int damageTakenBunker;
            /// <summary>Subset of <see cref="damageTakenBunker"/> from bomb explosions (<see cref="BombProjectile_V2"/>).</summary>
            public int damageTakenBunkerFromBombs;
            public float timeInBunkerSec;
            public int shotsFired;
            public int rayHits;
            public int rayMisses;
            public int projectileLaunches;
            public int reloads;
            public int empStunsApplied;
            public int shopPurchasesPrior;
            public int shopCurrencySpentPrior;
            public string[] shopOffersBoughtPrior;
            public int heroHpWaveStart;
            public int bunkerHpWaveStart;
            public int currencyWaveStart;
            public string weaponType;
            /// <summary><see cref="AutoHeroTestProfileKind_V2"/> on the hero when present; empty if no bot / disabled.</summary>
            public string autoHeroTestProfile;
            public bool autoHeroHasTarget;
            public bool autoHeroInRange;
            public bool autoHeroCanHoldFire;
            public bool autoHeroTargetShootableOnCamera;
            public bool autoHeroShootBlockedByBunkerMove;
            public bool autoHeroRawShootHeld;
            public bool autoHeroImmediateGroundParatrooperThreat;
            public string autoHeroTargetKind;
            public string autoHeroTargetParatrooperState;
            public string autoHeroFallbackStage;
            public int autoHeroFallbackLivingParatrooperModels;
            public int autoHeroFallbackEnabledEnemyBodyPartColliders;

            /// <summary><see cref="GameplaySceneRules_V2.ProfileId"/> when a scene profile applier is active; empty otherwise.</summary>
            public string sceneProfileId;

            /// <summary>True when <see cref="bunkerHp"/> snapshot is &lt;= 0 (cover breached).</summary>
            public bool bunkerBreached;

            /// <summary>
            /// True when bunker is still alive but low: 0 &lt; bunkerHp &lt;= bunkerMaxHp × fraction (see root bunkerCriticalHpFractionUsed).
            /// </summary>
            public bool bunkerCriticalLow;

            /// <summary>True when hero is dead at snapshot (see <see cref="run_end"/> / <see cref="session_quit"/>).</summary>
            public bool heroDead;

            /// <summary>
            /// Rough burst-ish scalar: meaningful on <c>wave_cleared</c> and on <c>run_end</c> when GameOver occurs during
            /// InWave (same formula and accumulators as the aborted wave). Otherwise 0.
            /// </summary>
            public float waveStressScore;

            /// <summary>
            /// Unscaled seconds in InWave with bunkerHp/maxHp strictly below root bunkerPressureHpRatioThresholdUsed
            /// (includes time spent already low from prior wave / shop state). wave_cleared and run_end abort; 0 otherwise.
            /// </summary>
            public float bunkerPressureTimeSec;

            /// <summary>
            /// Same threshold as bunkerPressureTimeSec, but only counts time after the first bunker damage event this InWave
            /// (NotifyBunkerDamageTaken). Separates carried-in low cover from pressure driven by new hits. 0 if no bunker damage.
            /// </summary>
            public float bunkerPressureTimeAfterFirstDamageSec;

            /// <summary>
            /// Minimum bunkerHp/bunkerMaxHp observed during the InWave just ended; -1 if not applicable.
            /// Filled on wave_cleared and on run_end when GameOver happens during InWave (no wave_cleared row for that wave).
            /// </summary>
            public float minBunkerHpRatioThisWave;

            /// <summary>
            /// Sampled bunker HP during InWave (see root bunkerHpSampleIntervalSecUsed); empty unless kind is wave_cleared,
            /// or run_end after an abort during InWave (same sampling as wave_cleared).
            /// </summary>
            public BunkerHpSamplePoint[] bunkerHpSamples;

            /// <summary>
            /// Non-empty only on wave_cleared / run_end (abort): JSON from <see cref="TelemetryWaveScaling"/> via JsonUtility.
            /// Empty on other kinds (avoids Unity JsonUtility emitting empty nested objects for session_begin / session_quit).
            /// </summary>
            public string waveScalingJson;

            // Spawner snapshot for diagnosing spawn starvation / watchdog failures.
            public int spawnerTargetSpawn;
            public int spawnerSpawned;
            public int spawnerPendingDrops;
            public bool spawnerSpawnStarved;
            public string spawnerSpawnRoutineExitReason;
            public string spawnerLastSpawnAbortReason;
            public int spawnerFailedSpawnAttempts;
            public int spawnerRecoveryCount;
        }

        private void Awake()
        {
            _sessionId = Guid.NewGuid().ToString("N");
            _feelSessionBeginRealtime = -1f;
            _feelFirstKillRealtime = -1f;
            _feelFirstHeroDamageRealtime = -1f;
            _feelFirstHeroDeathRealtime = -1f;
            _feelFirstShopPurchaseRealtime = -1f;
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            ResolveWaveManager();
            if (_waveManager == null)
            {
                return;
            }

            _lastState = _waveManager.State;
            _waveManager.OnStateChanged += OnWaveStateChanged;
            if (_waveManager.State == WaveLoopState_V2.InWave)
            {
                _waveStartedRealtime = Time.realtimeSinceStartup;
            }

            SubscribeHeroIfPossible();

            if (_telemetryEnabled && !_sessionBeginWritten)
            {
                if (_sessionBeginCoroutine != null)
                {
                    StopCoroutine(_sessionBeginCoroutine);
                }

                _sessionBeginCoroutine = StartCoroutine(WriteSessionBeginAfterManagersInitialized());
            }

            if (_telemetryEnabled && _captureUnityConsoleErrors)
            {
                Application.logMessageReceivedThreaded += OnUnityLogMessageThreaded;
                _unityLogHookRegistered = true;
            }
        }

        private IEnumerator WriteSessionBeginAfterManagersInitialized()
        {
            yield return null;
            _sessionBeginCoroutine = null;
            SubscribeHeroIfPossible();
            TryWriteSessionBegin();
        }

        private void Update()
        {
            if (_telemetryEnabled)
            {
                FlushPendingUnityLogsFromMainThread();
            }

            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            TickBunkerHpCurveTracking();
            TickBunkerPressureAccumulation();

            Hero_V2 hero = FindHero();
            if (hero != null && _waveManager.IsHeroInsideBunker(hero))
            {
                _timeInBunkerUnscaledDuringWave += Time.unscaledDeltaTime;
            }
        }

        private void TickBunkerHpCurveTracking()
        {
            if (_bunkerHpSamplesThisWave == null)
            {
                return;
            }

            int maxHp = Mathf.Max(1, _waveManager.BunkerMaxHealth);
            int hp = _waveManager.BunkerHealth;
            float ratio = hp / (float)maxHp;
            if (_minBunkerHpRatioThisWave < 0f)
            {
                _minBunkerHpRatioThisWave = ratio;
            }
            else
            {
                _minBunkerHpRatioThisWave = Mathf.Min(_minBunkerHpRatioThisWave, ratio);
            }

            float interval = Mathf.Max(0.05f, _bunkerHpSampleIntervalSec);
            while (Time.realtimeSinceStartup >= _nextBunkerSampleRealtime &&
                   _bunkerHpSamplesThisWave.Count < _bunkerHpSamplesMaxPerWave)
            {
                int mx = Mathf.Max(1, _waveManager.BunkerMaxHealth);
                int h = _waveManager.BunkerHealth;
                float r = h / (float)mx;
                _bunkerHpSamplesThisWave.Add(
                    new BunkerHpSamplePoint
                    {
                        waveTimeSecSinceInWaveRealtime = Mathf.Max(0f, _nextBunkerSampleRealtime - _waveStartedRealtime),
                        bunkerHp = h,
                        bunkerMaxHp = mx,
                        bunkerHpRatio = r
                    });
                _nextBunkerSampleRealtime += interval;
            }
        }

        private void TickBunkerPressureAccumulation()
        {
            if (_waveManager == null)
            {
                return;
            }

            int mx = Mathf.Max(1, _waveManager.BunkerMaxHealth);
            int hp = _waveManager.BunkerHealth;
            float ratio = hp / (float)mx;
            float thr = Mathf.Clamp(_bunkerPressureHpRatioThreshold, 0.05f, 0.99f);
            if (ratio < thr)
            {
                float dt = Time.unscaledDeltaTime;
                _bunkerLowPressureTimeUnscaledDuringWave += dt;
                if (_bunkerDamageReceivedThisWave)
                {
                    _bunkerLowPressureTimeAfterFirstDamageUnscaledDuringWave += dt;
                }
            }
        }

        private void OnDisable()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            if (_sessionBeginCoroutine != null)
            {
                StopCoroutine(_sessionBeginCoroutine);
                _sessionBeginCoroutine = null;
            }

            UnsubscribeHero();
            if (_waveManager != null)
            {
                _waveManager.OnStateChanged -= OnWaveStateChanged;
            }

            if (_unityLogHookRegistered)
            {
                Application.logMessageReceivedThreaded -= OnUnityLogMessageThreaded;
                _unityLogHookRegistered = false;
            }
        }

        private void OnApplicationQuit()
        {
            TryWriteSessionQuit();
        }

        /// <summary>Called from <see cref="WaveManager_V2"/> when bunker loses HP.</summary>
        public static void NotifyBunkerDamageTaken(int amount)
        {
            NotifyBunkerDamageTaken(amount, BunkerDamageTelemetrySource.Other);
        }

        /// <summary>Called from <see cref="WaveManager_V2.ApplyBunkerDamage"/> with a coarse damage source.</summary>
        public static void NotifyBunkerDamageTaken(int amount, BunkerDamageTelemetrySource source)
        {
            if (amount <= 0)
            {
                return;
            }

            ActiveInstance?.RegisterBunkerDamageTaken(amount, source);
        }

        /// <summary>Called from <see cref="WaveManager_V2"/> after a successful shop spend.</summary>
        public static void NotifyShopPurchase(string offerKind, int currencySpent)
        {
            ActiveInstance?.RegisterShopPurchase(offerKind, currencySpent);
        }

        /// <summary>Call from <see cref="WaveManager_V2.ReportEnemyKilled"/> for feel-KPI first-kill timing.</summary>
        public static void NotifyEnemyKilledForFeelKpis()
        {
            ActiveInstance?.RegisterFirstEnemyKillRealtime();
        }

        /// <summary>Call when a combat stun/EMP is applied to an enemy during InWave.</summary>
        public static void NotifyEmpCombatStunApplied()
        {
            ActiveInstance?.RegisterEmpCombatStunApplied();
        }

        /// <summary>
        /// Record a non-Unity exception signal (watchdog, sanity check, etc.) into <c>unityLogs[]</c> with the same
        /// gameplay snapshot as console errors. Thread-safe: can be called from any thread; flushed on the main thread.
        /// </summary>
        public static void RecordSyntheticTelemetryError(string code, string message)
        {
            ActiveInstance?.EnqueueSyntheticErrorFromAnyThread(code, message);
        }

        private void EnqueueSyntheticErrorFromAnyThread(string code, string message)
        {
            if (!_telemetryEnabled)
            {
                return;
            }

            string c = (code ?? "").Trim();
            string m = (message ?? "").Trim();
            string line = string.IsNullOrEmpty(c) ? m : $"[{c}] {m}";
            line = TruncateForTelemetryQueue(line, _unityLogMessageMaxChars);
            lock (_pendingUnityLogsLock)
            {
                if (_pendingUnityLogs.Count >= _unityLogPendingQueueMax)
                {
                    _pendingUnityLogs.RemoveAt(0);
                }

                _pendingUnityLogs.Add(
                    new PendingUnityLog
                    {
                        condition = line,
                        stackTrace = "",
                        type = LogType.Error
                    });
            }
        }

        private void RegisterBunkerDamageTaken(int amount, BunkerDamageTelemetrySource source)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            int a = Mathf.Max(0, amount);
            _bunkerDamageTakenDuringWave += a;
            if (source == BunkerDamageTelemetrySource.Bomb)
            {
                _bunkerDamageFromBombsTakenDuringWave += a;
            }

            _bunkerDamageReceivedThisWave = true;
        }

        private void RegisterShopPurchase(string offerKind, int currencySpent)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.Shop)
            {
                return;
            }

            if (_feelFirstShopPurchaseRealtime < 0f)
            {
                _feelFirstShopPurchaseRealtime = Time.realtimeSinceStartup;
                _feelFirstShopPurchaseCurrencySpent = Mathf.Max(0, currencySpent);
                _feelFirstShopOfferKind = string.IsNullOrWhiteSpace(offerKind) ? "" : offerKind.Trim();
                Hero_V2 h = FindHero();
                if (h != null)
                {
                    _feelFirstPurchaseHeroHp = h.GetCurrentHealth();
                    _feelFirstPurchaseHeroMaxHp = h.GetMaxHealth();
                    _feelFirstPurchaseWeaponType = h.CurrentWeaponType.ToString();
                }
                else
                {
                    _feelFirstPurchaseHeroHp = -1;
                    _feelFirstPurchaseHeroMaxHp = -1;
                    _feelFirstPurchaseWeaponType = "";
                }
            }

            _intermissionShopPurchaseCount++;
            _intermissionShopCurrencySpent += Mathf.Max(0, currencySpent);
            if (!string.IsNullOrWhiteSpace(offerKind))
            {
                _intermissionShopOfferKinds.Add(offerKind.Trim());
            }
        }

        private void RegisterFirstEnemyKillRealtime()
        {
            if (!_telemetryEnabled || _feelFirstKillRealtime >= 0f)
            {
                return;
            }

            _feelFirstKillRealtime = Time.realtimeSinceStartup;
        }

        private void RegisterEmpCombatStunApplied()
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _empStunsAppliedDuringWave++;
        }

        private void ResolveWaveManager()
        {
            if (_waveManager != null)
            {
                return;
            }

            _waveManager = FindAnyObjectByType<WaveManager_V2>();
            if (_waveManager == null)
            {
                Debug.LogWarning("[WaveRunTelemetry_V2] No WaveManager_V2 found; telemetry disabled.");
            }
        }

        private void SubscribeHeroIfPossible()
        {
            Hero_V2 hero = FindHero();
            if (hero == null)
            {
                UnsubscribeHero();
                return;
            }

            // First call can run before Hero_V2.Awake finishes (telemetry OnEnable vs script order).
            // Older logic bailed when hero == _subscribedHero, so WeaponSystem/DamageReceiver stayed
            // unsubscribed forever. Re-wire idempotently whenever the same hero gains systems.
            if (_subscribedHero != null && _subscribedHero != hero)
            {
                UnsubscribeHero();
            }

            _subscribedHero = hero;

            if (_subscribedHero.DamageReceiver != null)
            {
                _subscribedHero.DamageReceiver.OnDamageTaken -= OnHeroDamageTaken;
                _subscribedHero.DamageReceiver.OnDamageTaken += OnHeroDamageTaken;
                _subscribedHero.DamageReceiver.OnDeath -= OnHeroDeathForFeelKpis;
                _subscribedHero.DamageReceiver.OnDeath += OnHeroDeathForFeelKpis;
            }

            _subscribedHero.OnHealed -= OnHeroHealed;
            _subscribedHero.OnHealed += OnHeroHealed;

            if (_subscribedHero.WeaponSystem != null)
            {
                _subscribedHero.WeaponSystem.OnCommittedAttack -= OnWeaponCommittedAttack;
                _subscribedHero.WeaponSystem.OnCommittedAttack += OnWeaponCommittedAttack;
                _subscribedHero.WeaponSystem.OnReloadCompleted -= OnWeaponReloadCompleted;
                _subscribedHero.WeaponSystem.OnReloadCompleted += OnWeaponReloadCompleted;
            }
        }

        private void UnsubscribeHero()
        {
            if (_subscribedHero == null)
            {
                return;
            }

            if (_subscribedHero.DamageReceiver != null)
            {
                _subscribedHero.DamageReceiver.OnDamageTaken -= OnHeroDamageTaken;
                _subscribedHero.DamageReceiver.OnDeath -= OnHeroDeathForFeelKpis;
            }

            _subscribedHero.OnHealed -= OnHeroHealed;
            if (_subscribedHero.WeaponSystem != null)
            {
                _subscribedHero.WeaponSystem.OnCommittedAttack -= OnWeaponCommittedAttack;
                _subscribedHero.WeaponSystem.OnReloadCompleted -= OnWeaponReloadCompleted;
            }

            _subscribedHero = null;
        }

        private void OnHeroDamageTaken(int amount)
        {
            if (_telemetryEnabled && amount > 0 && _feelFirstHeroDamageRealtime < 0f)
            {
                _feelFirstHeroDamageRealtime = Time.realtimeSinceStartup;
            }

            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _heroDamageTakenDuringWave += Mathf.Max(0, amount);
        }

        private void OnHeroDeathForFeelKpis()
        {
            if (!_telemetryEnabled || _feelFirstHeroDeathRealtime >= 0f)
            {
                return;
            }

            _feelFirstHeroDeathRealtime = Time.realtimeSinceStartup;
        }

        private void OnHeroHealed(int amount)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _heroHealedDuringWave += Mathf.Max(0, amount);
        }

        private void OnWeaponCommittedAttack(WeaponType weaponType, bool isProjectile, bool rayDidHit)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _shotsFiredDuringWave++;
            if (isProjectile)
            {
                _projectileLaunchesDuringWave++;
            }
            else if (rayDidHit)
            {
                _rayHitsDuringWave++;
            }
            else
            {
                _rayMissesDuringWave++;
            }
        }

        private void OnWeaponReloadCompleted(WeaponType _)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _reloadsDuringWave++;
        }

        private void TryWriteSessionBegin()
        {
            if (!_telemetryEnabled || _waveManager == null || _sessionBeginWritten)
            {
                return;
            }

            _sessionBeginWritten = true;
            EnsureOutputPath();
            _feelSessionBeginRealtime = Time.realtimeSinceStartup;
            // session_begin is emitted before the first InWave transition, so wave-start fields would
            // otherwise stay default (0). Snapshot current economy/HP for a consistent first row.
            SnapshotWaveStartEconomy();
            AppendEvent(
                BuildTelemetryEvent(
                    "session_begin",
                    wave: _waveManager.CurrentWaveNumber,
                    waveDurationSec: 0f,
                    enemiesKilled: 0,
                    endReason: ""));
        }

        private void OnWaveStateChanged(WaveLoopState_V2 newState)
        {
            if (!_telemetryEnabled || _waveManager == null)
            {
                _lastState = newState;
                return;
            }

            if (_lastState == WaveLoopState_V2.Shop && newState == WaveLoopState_V2.Preparing)
            {
                _shopPurchasesPriorToWaveJustCleared = _intermissionShopPurchaseCount;
                _shopCurrencySpentPriorToWaveJustCleared = _intermissionShopCurrencySpent;
                _shopOffersBoughtPriorToWaveJustCleared = _intermissionShopOfferKinds.Count > 0
                    ? _intermissionShopOfferKinds.ToArray()
                    : Array.Empty<string>();
                _intermissionShopPurchaseCount = 0;
                _intermissionShopCurrencySpent = 0;
                _intermissionShopOfferKinds.Clear();
            }

            if (_lastState != WaveLoopState_V2.InWave && newState == WaveLoopState_V2.InWave)
            {
                _waveStartedRealtime = Time.realtimeSinceStartup;
                ResetWaveCombatAccumulators();
                SnapshotWaveStartEconomy();
                ResetBunkerHpCurveForWave();
                // Hero reference may point at an inactive duplicate; re-resolve before combat hooks.
                SubscribeHeroIfPossible();
            }

            if (_lastState == WaveLoopState_V2.InWave && newState == WaveLoopState_V2.Shop)
            {
                float duration = Mathf.Max(0f, Time.realtimeSinceStartup - _waveStartedRealtime);
                FinalizeBunkerHpCurveForWaveEnd(duration);
                AppendEvent(
                    BuildTelemetryEvent(
                        "wave_cleared",
                        wave: _waveManager.CurrentWaveNumber,
                        waveDurationSec: duration,
                        enemiesKilled: _waveManager.EnemiesKilledThisWave,
                        endReason: ""));
            }

            if (newState == WaveLoopState_V2.GameOver ||
                newState == WaveLoopState_V2.GameError ||
                newState == WaveLoopState_V2.GameWon)
            {
                WriteRunEnd(_lastState);
            }

            _lastState = newState;
        }

        private void ResetWaveCombatAccumulators()
        {
            _heroDamageTakenDuringWave = 0;
            _heroHealedDuringWave = 0;
            _bunkerDamageTakenDuringWave = 0;
            _bunkerDamageFromBombsTakenDuringWave = 0;
            _timeInBunkerUnscaledDuringWave = 0f;
            _shotsFiredDuringWave = 0;
            _rayHitsDuringWave = 0;
            _rayMissesDuringWave = 0;
            _projectileLaunchesDuringWave = 0;
            _reloadsDuringWave = 0;
            _empStunsAppliedDuringWave = 0;
            _bunkerLowPressureTimeUnscaledDuringWave = 0f;
            _bunkerLowPressureTimeAfterFirstDamageUnscaledDuringWave = 0f;
            _bunkerDamageReceivedThisWave = false;
        }

        private void ResetBunkerHpCurveForWave()
        {
            _bunkerHpSamplesThisWave = new List<BunkerHpSamplePoint>(Mathf.Min(64, _bunkerHpSamplesMaxPerWave));
            _minBunkerHpRatioThisWave = -1f;
            if (_waveManager == null)
            {
                return;
            }

            int mx = Mathf.Max(1, _waveManager.BunkerMaxHealth);
            int hp = _waveManager.BunkerHealth;
            float ratio = hp / (float)mx;
            _minBunkerHpRatioThisWave = ratio;
            _bunkerHpSamplesThisWave.Add(
                new BunkerHpSamplePoint
                {
                    waveTimeSecSinceInWaveRealtime = 0f,
                    bunkerHp = hp,
                    bunkerMaxHp = mx,
                    bunkerHpRatio = ratio
                });
            _nextBunkerSampleRealtime =
                Time.realtimeSinceStartup + Mathf.Max(0.05f, _bunkerHpSampleIntervalSec);
        }

        private void FinalizeBunkerHpCurveForWaveEnd(float waveDurationRealtimeSec)
        {
            if (_waveManager == null || _bunkerHpSamplesThisWave == null)
            {
                return;
            }

            int mx = Mathf.Max(1, _waveManager.BunkerMaxHealth);
            int hp = _waveManager.BunkerHealth;
            float ratio = hp / (float)mx;
            if (_minBunkerHpRatioThisWave < 0f)
            {
                _minBunkerHpRatioThisWave = ratio;
            }
            else
            {
                _minBunkerHpRatioThisWave = Mathf.Min(_minBunkerHpRatioThisWave, ratio);
            }

            float endT = Mathf.Max(0f, waveDurationRealtimeSec);
            if (_bunkerHpSamplesThisWave.Count >= _bunkerHpSamplesMaxPerWave)
            {
                return;
            }

            BunkerHpSamplePoint last = _bunkerHpSamplesThisWave[_bunkerHpSamplesThisWave.Count - 1];
            if (Mathf.Abs(last.waveTimeSecSinceInWaveRealtime - endT) > 0.02f)
            {
                _bunkerHpSamplesThisWave.Add(
                    new BunkerHpSamplePoint
                    {
                        waveTimeSecSinceInWaveRealtime = endT,
                        bunkerHp = hp,
                        bunkerMaxHp = mx,
                        bunkerHpRatio = ratio
                    });
            }
        }

        private void SnapshotWaveStartEconomy()
        {
            Hero_V2 h = FindHero();
            _heroHpAtWaveStart = h != null ? h.GetCurrentHealth() : -1;
            _bunkerHpAtWaveStart = _waveManager != null ? _waveManager.BunkerHealth : -1;
            _currencyAtWaveStart = _waveManager != null ? _waveManager.Currency : -1;
        }

        private TelemetryEvent BuildTelemetryEvent(
            string kind,
            int wave,
            float waveDurationSec,
            int enemiesKilled,
            string endReason,
            bool runEndIncludeAbortWaveBunkerCurve = false)
        {
            Hero_V2 h = FindHero();
            int heroHp = h != null ? h.GetCurrentHealth() : -1;
            int heroMax = h != null ? h.GetMaxHealth() : -1;
            string weaponLabel = h != null ? h.GetCurrentWeaponDisplayName() : "";
            string weaponTypeStr = h != null ? h.CurrentWeaponType.ToString() : "";
            string autoHeroProfile = ResolveAutoHeroTestProfileLabel(h);
            AutoHero_V2 autoHero = h != null ? h.GetComponent<AutoHero_V2>() : null;
            bool autoHeroHasTarget = autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryHasTarget;
            bool autoHeroInRange = autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryInRange;
            bool autoHeroCanHoldFire = autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryCanHoldFire;
            bool autoHeroTargetShootableOnCamera =
                autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryTargetShootableOnCamera;
            bool autoHeroShootBlockedByBunkerMove =
                autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryShootBlockedByBunkerMove;
            bool autoHeroRawShootHeld = autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryRawShootHeld;
            bool autoHeroImmediateGroundThreat =
                autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryImmediateGroundParatrooperThreat;
            string autoHeroTargetKind =
                autoHero != null && autoHero.isActiveAndEnabled ? autoHero.TelemetryTargetKind : "";
            string autoHeroTargetParatrooperState =
                autoHero != null && autoHero.isActiveAndEnabled ? autoHero.TelemetryTargetParatrooperState : "";
            string autoHeroFallbackStage =
                autoHero != null && autoHero.isActiveAndEnabled ? autoHero.TelemetryLastFallbackStage : "";
            int autoHeroFallbackLivingParatrooperModels =
                autoHero != null && autoHero.isActiveAndEnabled ? autoHero.TelemetryFallbackLivingParatrooperModels : 0;
            int autoHeroFallbackEnabledEnemyBodyPartColliders =
                autoHero != null && autoHero.isActiveAndEnabled ? autoHero.TelemetryFallbackEnabledEnemyBodyPartColliders : 0;
            string sceneProfileIdValue = GameplaySceneRules_V2.IsActive ? GameplaySceneRules_V2.ProfileId : "";
            int bunkerHpSnap = _waveManager != null ? _waveManager.BunkerHealth : -1;
            int bunkerMaxSnap = _waveManager != null ? _waveManager.BunkerMaxHealth : -1;
            bool bunkerBreached = bunkerHpSnap == 0;
            float fracUsed = Mathf.Clamp(_bunkerCriticalHpFraction, 0.02f, 0.5f);
            bool bunkerCriticalLow = ComputeBunkerCriticalLowStatic(bunkerHpSnap, bunkerMaxSnap, fracUsed);
            bool heroDeadSnap = h != null && h.IsDead();
            bool includeWaveStressForEndedInWave =
                kind == "wave_cleared" || (kind == "run_end" && runEndIncludeAbortWaveBunkerCurve);
            float waveStress = includeWaveStressForEndedInWave
                ? ComputeWaveStressScore(
                    _bunkerDamageTakenDuringWave,
                    _heroDamageTakenDuringWave,
                    enemiesKilled,
                    waveDurationSec)
                : 0f;

            float minBunkerRatio = -1f;
            BunkerHpSamplePoint[] bunkerSamples = Array.Empty<BunkerHpSamplePoint>();
            if (kind == "wave_cleared" || (kind == "run_end" && runEndIncludeAbortWaveBunkerCurve))
            {
                minBunkerRatio = _minBunkerHpRatioThisWave >= 0f
                    ? Mathf.Clamp01(_minBunkerHpRatioThisWave)
                    : Mathf.Clamp01(
                        bunkerMaxSnap > 0 && bunkerHpSnap >= 0
                            ? bunkerHpSnap / (float)Mathf.Max(1, bunkerMaxSnap)
                            : 1f);

                bunkerSamples = _bunkerHpSamplesThisWave != null && _bunkerHpSamplesThisWave.Count > 0
                    ? _bunkerHpSamplesThisWave.ToArray()
                    : Array.Empty<BunkerHpSamplePoint>();
            }

            float bunkerPressureTimeForRow = 0f;
            float bunkerPressureAfterFirstDamageForRow = 0f;
            if (kind == "wave_cleared" || (kind == "run_end" && runEndIncludeAbortWaveBunkerCurve))
            {
                bunkerPressureTimeForRow = _bunkerLowPressureTimeUnscaledDuringWave;
                bunkerPressureAfterFirstDamageForRow = _bunkerLowPressureTimeAfterFirstDamageUnscaledDuringWave;
            }

            string waveScalingJson = "";
            if ((kind == "wave_cleared" || (kind == "run_end" && runEndIncludeAbortWaveBunkerCurve)) &&
                _waveManager != null &&
                _waveManager.TryGetScalingSnapshotForTelemetry(out WaveRunScalingSnapshot scalingSnap))
            {
                TelemetryWaveScaling scalingObj = TelemetryWaveScaling.FromSnapshot(scalingSnap);
                waveScalingJson = JsonUtility.ToJson(scalingObj);
            }

            if (kind == "session_begin" || kind == "session_quit")
            {
                waveScalingJson = "";
            }

            EnemySpawner_V2 spawner = _waveManager != null ? _waveManager.EnemySpawner : null;
            int spawnerTarget = 0;
            int spawnerSpawned = 0;
            int spawnerPending = 0;
            bool spawnerStarved = false;
            string spawnerExit = "";
            string spawnerAbort = "";
            int spawnerFailed = 0;
            int spawnerRecoveries = 0;
            if (spawner != null)
            {
                spawnerTarget = spawner.TargetParatroopersThisWave;
                spawnerSpawned = spawner.SpawnedParatroopersThisWave;
                spawnerPending = spawner.PendingParatrooperDropsThisWave;
                spawnerStarved = spawner.IsSpawnStarvedThisWave;
                spawnerExit = spawner.SpawnRoutineExitReason;
                spawnerAbort = spawner.LastSpawnAbortReason;
                spawnerFailed = spawner.FailedParatrooperSpawnAttemptsThisWave;
                spawnerRecoveries = spawner.SpawnStarvationRecoveryCountThisWave;
            }

            return new TelemetryEvent
            {
                kind = kind,
                sessionId = _sessionId,
                realtimeSinceStartup = Time.realtimeSinceStartup,
                wave = wave,
                waveDurationSec = waveDurationSec,
                heroHp = heroHp,
                heroMaxHp = heroMax,
                bunkerHp = bunkerHpSnap,
                bunkerMaxHp = bunkerMaxSnap,
                currency = _waveManager != null ? _waveManager.Currency : -1,
                enemiesKilled = enemiesKilled,
                weapon = weaponLabel,
                endReason = endReason ?? "",
                damageTakenHero = _heroDamageTakenDuringWave,
                healingHero = _heroHealedDuringWave,
                damageTakenBunker = _bunkerDamageTakenDuringWave,
                damageTakenBunkerFromBombs = _bunkerDamageFromBombsTakenDuringWave,
                timeInBunkerSec = _timeInBunkerUnscaledDuringWave,
                shotsFired = _shotsFiredDuringWave,
                rayHits = _rayHitsDuringWave,
                rayMisses = _rayMissesDuringWave,
                projectileLaunches = _projectileLaunchesDuringWave,
                reloads = _reloadsDuringWave,
                empStunsApplied = _empStunsAppliedDuringWave,
                shopPurchasesPrior = _shopPurchasesPriorToWaveJustCleared,
                shopCurrencySpentPrior = _shopCurrencySpentPriorToWaveJustCleared,
                shopOffersBoughtPrior = _shopOffersBoughtPriorToWaveJustCleared ?? Array.Empty<string>(),
                heroHpWaveStart = _heroHpAtWaveStart,
                bunkerHpWaveStart = _bunkerHpAtWaveStart,
                currencyWaveStart = _currencyAtWaveStart,
                weaponType = weaponTypeStr,
                autoHeroTestProfile = autoHeroProfile,
                autoHeroHasTarget = autoHeroHasTarget,
                autoHeroInRange = autoHeroInRange,
                autoHeroCanHoldFire = autoHeroCanHoldFire,
                autoHeroTargetShootableOnCamera = autoHeroTargetShootableOnCamera,
                autoHeroShootBlockedByBunkerMove = autoHeroShootBlockedByBunkerMove,
                autoHeroRawShootHeld = autoHeroRawShootHeld,
                autoHeroImmediateGroundParatrooperThreat = autoHeroImmediateGroundThreat,
                autoHeroTargetKind = autoHeroTargetKind,
                autoHeroTargetParatrooperState = autoHeroTargetParatrooperState,
                autoHeroFallbackStage = autoHeroFallbackStage,
                autoHeroFallbackLivingParatrooperModels = autoHeroFallbackLivingParatrooperModels,
                autoHeroFallbackEnabledEnemyBodyPartColliders = autoHeroFallbackEnabledEnemyBodyPartColliders,
                sceneProfileId = sceneProfileIdValue,
                bunkerBreached = bunkerBreached,
                bunkerCriticalLow = bunkerCriticalLow,
                heroDead = heroDeadSnap,
                waveStressScore = waveStress,
                bunkerPressureTimeSec = bunkerPressureTimeForRow,
                bunkerPressureTimeAfterFirstDamageSec = bunkerPressureAfterFirstDamageForRow,
                minBunkerHpRatioThisWave = minBunkerRatio,
                bunkerHpSamples = bunkerSamples,
                waveScalingJson = waveScalingJson,
                spawnerTargetSpawn = spawnerTarget,
                spawnerSpawned = spawnerSpawned,
                spawnerPendingDrops = spawnerPending,
                spawnerSpawnStarved = spawnerStarved,
                spawnerSpawnRoutineExitReason = spawnerExit,
                spawnerLastSpawnAbortReason = spawnerAbort,
                spawnerFailedSpawnAttempts = spawnerFailed,
                spawnerRecoveryCount = spawnerRecoveries
            };
        }

        private static bool ComputeBunkerCriticalLowStatic(int bunkerHp, int bunkerMaxHp, float bunkerCriticalHpFractionClamped)
        {
            if (bunkerHp <= 0 || bunkerMaxHp <= 0)
            {
                return false;
            }

            return bunkerHp <= bunkerMaxHp * bunkerCriticalHpFractionClamped + 1e-4f;
        }

        /// <summary>Single scalar for sorting/graphing waves; not a game-design "difficulty tier".</summary>
        private static float ComputeWaveStressScore(
            int bunkerDamage,
            int heroDamage,
            int enemiesKilled,
            float waveDurationSec)
        {
            return bunkerDamage +
                   heroDamage +
                   enemiesKilled * 0.35f +
                   Mathf.Max(0f, waveDurationSec) * 0.05f;
        }

        private static string ResolveAutoHeroTestProfileLabel(Hero_V2 hero)
        {
            if (hero == null)
            {
                return "";
            }

            AutoHero_V2 bot = hero.GetComponent<AutoHero_V2>();
            if (bot == null || !bot.isActiveAndEnabled)
            {
                return "";
            }

            return bot.TestProfile.ToString();
        }

        private void WriteRunEnd(WaveLoopState_V2 previousState)
        {
            if (_runEndWritten || !_telemetryEnabled || _waveManager == null)
            {
                return;
            }

            _runEndWritten = true;
            string reason = ResolveRunEndReason(previousState);
            bool abortDuringInWave = previousState == WaveLoopState_V2.InWave;
            float inWaveDurationSec = 0f;
            if (abortDuringInWave)
            {
                inWaveDurationSec = Mathf.Max(0f, Time.realtimeSinceStartup - _waveStartedRealtime);
                FinalizeBunkerHpCurveForWaveEnd(inWaveDurationSec);
            }

            AppendEvent(
                BuildTelemetryEvent(
                    "run_end",
                    wave: _waveManager.CurrentWaveNumber,
                    waveDurationSec: inWaveDurationSec,
                    enemiesKilled: _waveManager.EnemiesKilledThisWave,
                    endReason: reason,
                    runEndIncludeAbortWaveBunkerCurve: abortDuringInWave));
        }

        private string ResolveRunEndReason(WaveLoopState_V2 previousState)
        {
            if (_waveManager != null &&
                (_waveManager.State == WaveLoopState_V2.GameError || previousState == WaveLoopState_V2.GameError))
            {
                if (_waveManager.TryGetLastGameErrorReason(out string gameErrorReason))
                {
                    return "game_error:" + gameErrorReason;
                }

                return "game_error:reason_unavailable";
            }

            if (_waveManager != null &&
                (_waveManager.State == WaveLoopState_V2.GameWon || previousState == WaveLoopState_V2.GameWon))
            {
                return "game_won";
            }

            Hero_V2 hero = FindHero();
            if (hero != null && hero.IsDead())
            {
                return "hero_death";
            }

            if (previousState == WaveLoopState_V2.InWave)
            {
                return "wave_config_missing_or_empty";
            }

            if (previousState == WaveLoopState_V2.Preparing)
            {
                return "no_wave_config_at_wave_start";
            }

            return "game_over_other";
        }

        private void TryWriteSessionQuit()
        {
            if (!_telemetryEnabled || _waveManager == null || _runEndWritten)
            {
                return;
            }

            if (_waveManager.State != WaveLoopState_V2.GameOver)
            {
                AppendEvent(
                    BuildTelemetryEvent(
                        "session_quit",
                        wave: _waveManager.CurrentWaveNumber,
                        waveDurationSec: 0f,
                        enemiesKilled: _waveManager.EnemiesKilledThisWave,
                        endReason: "editor_or_app_quit"));
            }
        }

        private Hero_V2 FindHero()
        {
            if (_waveManager != null && _waveManager.Hero != null)
            {
                Hero_V2 assigned = _waveManager.Hero;
                if (assigned.gameObject.activeInHierarchy)
                {
                    return assigned;
                }
            }

            return FindAnyObjectByType<Hero_V2>(FindObjectsInactive.Exclude);
        }

        private void EnsureOutputPath()
        {
            if (!string.IsNullOrEmpty(_filePath))
            {
                return;
            }

            string dir = Path.Combine(Application.persistentDataPath, "iStick2WarTelemetry");
            Directory.CreateDirectory(dir);
            string safePrefix = string.IsNullOrWhiteSpace(_fileNamePrefix) ? "wave_run" : _fileNamePrefix.Trim();
            _filePath = Path.Combine(dir, $"{safePrefix}_{_sessionId}.json");

            if (_logFilePathOnce)
            {
                Debug.Log($"[WaveRunTelemetry_V2] Writing session log to: {_filePath}");
            }
        }

        private void AppendEvent(TelemetryEvent payload)
        {
            try
            {
                EnsureOutputPath();
                TelemetryFileRoot root = ReadRootOrNew();
                var list = new List<TelemetryEvent>(root.events ?? Array.Empty<TelemetryEvent>());
                list.Add(payload);
                float fracUsed = Mathf.Clamp(_bunkerCriticalHpFraction, 0.02f, 0.5f);
                for (int i = 0; i < list.Count; i++)
                {
                    TelemetryEvent row = list[i];
                    row.bunkerBreached = row.bunkerHp == 0;
                    row.bunkerCriticalLow = ComputeBunkerCriticalLowStatic(row.bunkerHp, row.bunkerMaxHp, fracUsed);
                    if (row != null &&
                        (row.kind == "session_begin" || row.kind == "session_quit"))
                    {
                        row.waveScalingJson = "";
                    }
                }

                root.events = list.ToArray();
                PersistTelemetryRoot(root);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WaveRunTelemetry_V2] Failed to write telemetry: {ex.Message}");
            }
        }

        private TelemetryFeelSessionSummary BuildFeelSessionSnapshot()
        {
            var s = new TelemetryFeelSessionSummary
            {
                sessionBeginRealtimeSinceStartup = _feelSessionBeginRealtime,
                firstKillRealtimeSinceStartup = _feelFirstKillRealtime,
                firstHeroDamageRealtimeSinceStartup = _feelFirstHeroDamageRealtime,
                firstHeroDeathRealtimeSinceStartup = _feelFirstHeroDeathRealtime,
                firstShopPurchaseRealtimeSinceStartup = _feelFirstShopPurchaseRealtime,
                firstShopPurchaseCurrencySpent = _feelFirstShopPurchaseCurrencySpent,
                firstShopOfferKind = _feelFirstShopOfferKind ?? "",
                firstPurchaseHeroHp = _feelFirstPurchaseHeroHp,
                firstPurchaseHeroMaxHp = _feelFirstPurchaseHeroMaxHp,
                firstPurchaseWeaponType = _feelFirstPurchaseWeaponType ?? ""
            };

            float anchor = _feelSessionBeginRealtime;
            if (anchor >= 0f)
            {
                s.firstKillSecSinceSessionBegin = FeelDeltaSec(anchor, _feelFirstKillRealtime);
                s.firstHeroDamageSecSinceSessionBegin = FeelDeltaSec(anchor, _feelFirstHeroDamageRealtime);
                s.firstHeroDeathSecSinceSessionBegin = FeelDeltaSec(anchor, _feelFirstHeroDeathRealtime);
                s.firstShopPurchaseSecSinceSessionBegin = FeelDeltaSec(anchor, _feelFirstShopPurchaseRealtime);
            }
            else
            {
                s.firstKillSecSinceSessionBegin = -1f;
                s.firstHeroDamageSecSinceSessionBegin = -1f;
                s.firstHeroDeathSecSinceSessionBegin = -1f;
                s.firstShopPurchaseSecSinceSessionBegin = -1f;
            }

            return s;
        }

        private static float FeelDeltaSec(float sessionBeginRt, float milestoneRt)
        {
            if (sessionBeginRt < 0f || milestoneRt < 0f)
            {
                return -1f;
            }

            return milestoneRt - sessionBeginRt;
        }

        private void PersistTelemetryRoot(TelemetryFileRoot root)
        {
            _suppressUnityLogCapture = true;
            try
            {
                root.feelSession = BuildFeelSessionSnapshot();
                root.poolStats = SimplePrefabPool_V2.GetSnapshot();
                ApplyTelemetryDocumentation(root);
                string json = JsonUtility.ToJson(root, prettyPrint: true);
                File.WriteAllText(_filePath, json);
            }
            finally
            {
                _suppressUnityLogCapture = false;
            }
        }

        private void ApplyTelemetryDocumentation(TelemetryFileRoot root)
        {
            float bunkerFracUsed = Mathf.Clamp(_bunkerCriticalHpFraction, 0.02f, 0.5f);
            root.bunkerCriticalHpFractionUsed = bunkerFracUsed;
            float sampleInt = Mathf.Clamp(_bunkerHpSampleIntervalSec, 0.05f, 5f);
            int sampleMax = Mathf.Clamp(_bunkerHpSamplesMaxPerWave, 8, 400);
            root.bunkerHpSampleIntervalSecUsed = sampleInt;
            root.bunkerHpSamplesMaxPerWaveUsed = sampleMax;
            float pressureThrUsed = Mathf.Clamp(_bunkerPressureHpRatioThreshold, 0.05f, 0.99f);
            root.bunkerPressureHpRatioThresholdUsed = pressureThrUsed;
            root._comment =
                "iStick2War wave-run telemetry (JSON). Root contains _comment, glossary, bunkerCriticalHpFractionUsed, events[], " +
                "optional unityLogs[], optional feelSession (first-kill/damage/death/shop milestones vs session_begin), and optional " +
                "poolStats (prefab pool counters; see glossary). " +
                "Each object in events[] shares the same property names; meaning depends on events[].kind. " +
                "Numeric snapshots are taken when the row is written (Unity Time.realtimeSinceStartup). " +
                "Per-wave combat counters reset when a new InWave phase starts; wave_cleared attributes the " +
                "just-finished wave. JsonUtility omits optional empty strings on some Unity versions; booleans " +
                "default to false when absent on read. " +
                "bunkerBreached means bunkerHp==0 at snapshot; bunkerCriticalLow means 0<bunkerHp<=bunkerMaxHp×" +
                bunkerFracUsed.ToString("0.###", CultureInfo.InvariantCulture) +
                " (see glossary; threshold from WaveRunTelemetry_V2 Inspector, echoed as bunkerCriticalHpFractionUsed). " +
                "Bunker HP curve: wave_cleared rows include minBunkerHpRatioThisWave and bunkerHpSamples[]; run_end rows " +
                "include the same when GameOver occurs during InWave (fatal wave has no wave_cleared row). Sampling interval " +
                "and cap are bunkerHpSampleIntervalSecUsed / bunkerHpSamplesMaxPerWaveUsed on the root. " +
                "bunkerPressureTimeSec / bunkerPressureTimeAfterFirstDamageSec (wave_cleared / applicable run_end) use " +
                "bunkerPressureHpRatioThresholdUsed on the root. " +
                "waveScalingJson (non-empty on wave_cleared / applicable run_end) holds JsonUtility JSON for scaling; " +
                "empty on session_begin / session_quit. " +
                "damageTakenBunkerFromBombs (events[]) is a subset of damageTakenBunker from BombProjectile_V2 bunker absorption.";
            root.glossary = BuildTelemetryGlossary(bunkerFracUsed, sampleInt, sampleMax, pressureThrUsed);
        }

        private static TelemetryGlossary BuildTelemetryGlossary(
            float bunkerCriticalHpFractionUsed,
            float bunkerHpSampleIntervalSecUsed,
            int bunkerHpSamplesMaxPerWaveUsed,
            float bunkerPressureHpRatioThresholdUsed)
        {
            return new TelemetryGlossary
            {
                title = "Wave run telemetry — property reference (shared by all rows in events[])",
                entries = new[]
                {
                    new TelemetryGlossaryEntry
                    {
                        property = "kind",
                        meaning =
                            "Row type: session_begin (run start), wave_cleared (wave finished → shop), " +
                            "run_end (GameOver), session_quit (app/editor quit without GameOver)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "sessionId",
                        meaning = "Unique id (hex string without dashes) for this JSON file / play session."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "realtimeSinceStartup",
                        meaning = "Seconds since Unity started when this row was written."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "wave",
                        meaning =
                            "1-based wave number from WaveManager at write time. session_begin uses current wave; " +
                            "wave_cleared uses the wave that just ended; session_quit may show next wave if quit mid-flow."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "waveDurationSec",
                        meaning =
                            "For wave_cleared: real-time seconds spent in InWave for that wave. For run_end when GameOver " +
                            "happens during InWave: same basis for the aborted wave (aligns bunkerHpSamples[].waveTimeSecSinceInWaveRealtime). " +
                            "0 otherwise."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "heroHp / heroMaxHp",
                        meaning = "Hero current and max HP at snapshot (from resolved Hero_V2)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "bunkerHp / bunkerMaxHp",
                        meaning = "Bunker current and max HP at snapshot (WaveManager)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "currency",
                        meaning = "Player currency at snapshot (WaveManager)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "enemiesKilled",
                        meaning = "Kill counter for the wave when kind is wave_cleared; from WaveManager.EnemiesKilledThisWave."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "weapon",
                        meaning = "Display name of hero's current weapon at snapshot."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "weaponType",
                        meaning = "Enum name of current WeaponType at snapshot."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "endReason",
                        meaning =
                            "For run_end: why GameOver (e.g. hero_death, wave_config_missing_or_empty). " +
                            "For session_quit: editor_or_app_quit. Usually empty for session_begin / wave_cleared."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "damageTakenHero",
                        meaning =
                            "Damage applied to hero during the current InWave (via DamageReceiver), reset on next InWave."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "healingHero",
                        meaning = "Healing received during current InWave (hero Heal), reset on next InWave."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "damageTakenBunker",
                        meaning =
                            "Bunker damage during current InWave (WaveManager.ApplyBunkerDamage → NotifyBunkerDamageTaken), " +
                            "reset on next InWave."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "damageTakenBunkerFromBombs",
                        meaning =
                            "Subset of damageTakenBunker: HP absorbed from BombProjectile_V2 explosions this InWave " +
                            "(same reset as damageTakenBunker). Other sources (small arms, drones, grenades) stay in the remainder."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "timeInBunkerSec",
                        meaning =
                            "Unscaled seconds hero was inside bunker zone (WaveManager.IsHeroInsideBunker) during InWave; " +
                            "reset on next InWave."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "shotsFired",
                        meaning =
                            "Committed attacks in InWave: each ray shot or projectile launch increments once (WeaponSystem events)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "rayHits / rayMisses",
                        meaning =
                            "Ray weapon only: hit vs miss on committed ray shot. Projectiles do not increment these (see projectileLaunches)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "projectileLaunches",
                        meaning = "Projectile weapon commits (e.g. bazooka) during InWave."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "reloads",
                        meaning = "Reload completions during InWave (WeaponSystem OnReloadCompleted)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "shopPurchasesPrior / shopCurrencySpentPrior",
                        meaning =
                            "Shop intermission before the wave that just cleared: purchase count and currency spent " +
                            "(attributed on wave_cleared when leaving Shop → Preparing)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "shopOffersBoughtPrior[]",
                        meaning =
                            "Offer kind labels bought in the same intermission window as shopPurchasesPrior/shopCurrencySpentPrior " +
                            "(e.g. WeaponUnlock, HealthPack, AmmoRefill, BunkerRepair)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "heroHpWaveStart / bunkerHpWaveStart / currencyWaveStart",
                        meaning = "Snapshot at start of InWave for that wave (also filled on session_begin for first-row consistency)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "autoHeroTestProfile",
                        meaning =
                            "AutoHero_V2 test profile name when bot is active (Perfect, HumanLike, Struggling); empty if no bot."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "sceneProfileId",
                        meaning =
                            "GameplaySceneRules_V2.ProfileId when GameplaySceneProfileApplier_V2 is active (built-in or asset); " +
                            "empty when no scene profile. Describes shop/weapon policy for the run (e.g. Colt-only benchmark)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "bunkerBreached",
                        meaning =
                            "True only if bunkerHp snapshot is exactly 0 (cover breached). Distinct from bunkerCriticalLow: " +
                            "low non-zero HP (e.g. 5/275) is not breached."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "bunkerCriticalLow (inside each events[] row)",
                        meaning =
                            "True when 0 < bunkerHp <= bunkerMaxHp × bunkerCriticalHpFractionUsed (root). " +
                            "Flags critically low bunker without requiring HP==0. False when bunkerHp<=0 (use bunkerBreached) " +
                            "or when current HP ratio is above the fraction. This file's fraction is " +
                            bunkerCriticalHpFractionUsed.ToString("0.###", CultureInfo.InvariantCulture) +
                            " (same as root.bunkerCriticalHpFractionUsed; set on WaveRunTelemetry_V2 in Inspector)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "bunkerCriticalHpFractionUsed (root object, sibling of events[])",
                        meaning =
                            "Clamped copy of WaveRunTelemetry_V2._bunkerCriticalHpFraction (Inspector, 0.02–0.5) used for " +
                            "bunkerCriticalLow on every row in this file; repeated on each write so JSON stays self-describing."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "heroDead",
                        meaning = "True if hero is dead at snapshot (IsDead())."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "waveStressScore",
                        meaning =
                            "wave_cleared, and run_end after abort during InWave: same burst-ish scalar (bunker+hero damage, kills, " +
                            "small duration term using that row's waveDurationSec). 0 on other kinds. Complements bunkerPressureTimeSec " +
                            "and bunkerPressureTimeAfterFirstDamageSec (sustained low-bunker time), not a full 'feel' model."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "bunkerPressureTimeSec",
                        meaning =
                            "wave_cleared, and run_end after abort during InWave: unscaled seconds spent InWave while " +
                            "bunkerHp/bunkerMaxHp was strictly below root.bunkerPressureHpRatioThresholdUsed (" +
                            bunkerPressureHpRatioThresholdUsed.ToString("0.###", CultureInfo.InvariantCulture) +
                            " in this file). 0 on other kinds. Includes time already under the threshold before any new bunker " +
                            "damage this wave (e.g. low HP carried in from shop). Compare bunkerPressureTimeAfterFirstDamageSec."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "bunkerPressureTimeAfterFirstDamageSec",
                        meaning =
                            "wave_cleared, and run_end after abort during InWave: same threshold as bunkerPressureTimeSec, but " +
                            "only counts unscaled time after the first ApplyBunkerDamage notification this InWave. 0 if no bunker " +
                            "damage occurred. Use with bunkerPressureTimeSec to separate carried-in low cover from pressure after hits."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "bunkerPressureHpRatioThresholdUsed (root)",
                        meaning =
                            "Inspector echo: bunker HP ratio below which bunkerPressureTimeSec accumulates (WaveRunTelemetry_V2 " +
                            "_bunkerPressureHpRatioThreshold, clamped 0.05–0.99). Distinct from bunkerCriticalHpFractionUsed."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "minBunkerHpRatioThisWave",
                        meaning =
                            "wave_cleared, and run_end after abort during InWave: minimum of bunkerHp/bunkerMaxHp seen during " +
                            "that InWave (every frame), including end snapshot; clamped 0–1. -1 on other kinds. Captures " +
                            "'how low cover got' even when waveStressScore is modest."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "bunkerHpSamples[]",
                        meaning =
                            "wave_cleared, and run_end after abort during InWave: periodic samples while InWave. Each point: " +
                            "waveTimeSecSinceInWaveRealtime (same clock as waveDurationSec), bunkerHp, bunkerMaxHp, bunkerHpRatio. " +
                            "Interval/cap on root: bunkerHpSampleIntervalSecUsed=" +
                            bunkerHpSampleIntervalSecUsed.ToString("0.###", CultureInfo.InvariantCulture) +
                            "s, bunkerHpSamplesMaxPerWaveUsed=" +
                            bunkerHpSamplesMaxPerWaveUsed.ToString(CultureInfo.InvariantCulture) +
                            " (WaveRunTelemetry_V2 Inspector)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "bunkerHpSampleIntervalSecUsed / bunkerHpSamplesMaxPerWaveUsed (root)",
                        meaning =
                            "Echo of Inspector settings used when writing bunkerHpSamples for wave_cleared and applicable run_end rows."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "waveScalingJson (string on row, optional)",
                        meaning =
                            "Non-empty on wave_cleared or run_end after abort during InWave: JsonUtility JSON of the scaling " +
                            "snapshot (scalingVersion labels WaveBalanceConfig_V2; balance* / config* / effective* as documented). " +
                            "Parse with JsonUtility into your DTO. Empty string on session_begin / session_quit and other kinds. " +
                            "Replaces a nested object field so Unity JsonUtility does not emit bogus empty objects on those rows."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "spawner* fields on row (spawnerTargetSpawn, spawnerSpawned, spawnerPendingDrops, spawnerSpawnStarved, spawnerSpawnRoutineExitReason, spawnerLastSpawnAbortReason, spawnerFailedSpawnAttempts, spawnerRecoveryCount)",
                        meaning =
                            "Snapshot from EnemySpawner_V2 at row write time. Used to diagnose spawn starvation (spawn schedule " +
                            "finished but spawned<target), failed spawn attempts, and watchdog-driven recovery attempts."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "unityLogs[] (root, optional)",
                        meaning =
                            "Sibling of events[]: rows from Unity Application.logMessageReceivedThreaded when LogType is Error, " +
                            "Exception, or Assert (optional Warnings via Inspector). Each row includes message/stack fingerprints, " +
                            "repeatCount when identical consecutive errors are coalesced, and a gameplay snapshot: wave, " +
                            "waveLoopState, hero/bunker economy, ammo, weapon, hero position, enemies killed this wave, " +
                            "trackedLivingParatroopers, timeScale/frame/scene/platform, inWaveUnscaledSec, per-wave combat " +
                            "accumulators (including damageTakenBunkerFromBombsThisWave), scalingSnapshotShort, spawnerDiagnosticsLine. " +
                            "Same sessionId as events[]."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "feelSession (root, optional)",
                        meaning =
                            "Design/feel milestones using Time.realtimeSinceStartup: session anchor from session_begin, " +
                            "firstKill/Damage/Death/ShopPurchase absolute times and *SecSinceSessionBegin (-1 if not yet). " +
                            "First shop row includes currency spent on that first purchase line, offer kind, and hero HP/max/weapon " +
                            "snapshot for power-delta analysis vs later wave_cleared rows (offline)."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "poolStats (root, optional)",
                        meaning =
                            "Snapshot from SimplePrefabPool_V2 at file write time. totals: prefabTypeCount, totalInactiveCount, " +
                            "totalCreatedCount, totalReusedCount, totalDespawnCount. prefabs[] rows contain prefabName + " +
                            "inactiveCount/createdCount/reusedCount/despawnCount per pooled prefab key."
                    }
                }
            };
        }

        private void OnUnityLogMessageThreaded(string condition, string stackTrace, LogType type)
        {
            if (!_telemetryEnabled ||
                !_captureUnityConsoleErrors ||
                _suppressUnityLogCapture)
            {
                return;
            }

            if (type != LogType.Error &&
                type != LogType.Exception &&
                type != LogType.Assert &&
                !(_captureUnityWarningsToo && type == LogType.Warning))
            {
                return;
            }

            if (!string.IsNullOrEmpty(condition) &&
                condition.IndexOf("[WaveRunTelemetry_V2]", StringComparison.Ordinal) >= 0 &&
                condition.IndexOf("Failed to write", StringComparison.Ordinal) >= 0)
            {
                return;
            }

            string c = TruncateForTelemetryQueue(condition, _unityLogMessageMaxChars);
            string s = TruncateForTelemetryQueue(stackTrace, _unityLogStackMaxChars);
            lock (_pendingUnityLogsLock)
            {
                if (_pendingUnityLogs.Count >= _unityLogPendingQueueMax)
                {
                    _pendingUnityLogs.RemoveAt(0);
                }

                _pendingUnityLogs.Add(
                    new PendingUnityLog
                    {
                        condition = c,
                        stackTrace = s,
                        type = type
                    });
            }
        }

        private static string TruncateForTelemetryQueue(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            int cap = Mathf.Clamp(maxChars, 256, 65535);
            return value.Length <= cap ? value : value.Substring(0, cap);
        }

        private void FlushPendingUnityLogsFromMainThread()
        {
            List<PendingUnityLog> batch = null;
            lock (_pendingUnityLogsLock)
            {
                if (_pendingUnityLogs.Count == 0)
                {
                    return;
                }

                batch = new List<PendingUnityLog>(_pendingUnityLogs);
                _pendingUnityLogs.Clear();
            }

            for (int i = 0; i < batch.Count; i++)
            {
                PendingUnityLog p = batch[i];
                AppendUnityLogRowFromMainThread(p.type, p.condition, p.stackTrace);
            }
        }

        private void AppendUnityLogRowFromMainThread(LogType type, string message, string stackTrace)
        {
            if (!_telemetryEnabled)
            {
                return;
            }

            ResolveWaveManager();
            string fp = ComputeUnityLogFingerprint(message, stackTrace);
            TelemetryUnityLogRow row = BuildUnityLogRow(type, message, stackTrace, fp);
            AppendOrCoalesceUnityLogRow(row);
        }

        private TelemetryUnityLogRow BuildUnityLogRow(LogType type, string message, string stackTrace, string fingerprint)
        {
            Hero_V2 h = FindHero();
            int heroHp = h != null ? h.GetCurrentHealth() : -1;
            int heroMax = h != null ? h.GetMaxHealth() : -1;
            string weaponLabel = h != null ? h.GetCurrentWeaponDisplayName() : "";
            string weaponTypeStr = h != null ? h.CurrentWeaponType.ToString() : "";
            string autoHeroProfile = ResolveAutoHeroTestProfileLabel(h);
            AutoHero_V2 autoHero = h != null ? h.GetComponent<AutoHero_V2>() : null;
            bool autoHeroHasTarget = autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryHasTarget;
            bool autoHeroInRange = autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryInRange;
            bool autoHeroCanHoldFire = autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryCanHoldFire;
            bool autoHeroTargetShootableOnCamera =
                autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryTargetShootableOnCamera;
            bool autoHeroShootBlockedByBunkerMove =
                autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryShootBlockedByBunkerMove;
            bool autoHeroRawShootHeld = autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryRawShootHeld;
            bool autoHeroImmediateGroundThreat =
                autoHero != null && autoHero.isActiveAndEnabled && autoHero.TelemetryImmediateGroundParatrooperThreat;
            string autoHeroTargetKind =
                autoHero != null && autoHero.isActiveAndEnabled ? autoHero.TelemetryTargetKind : "";
            string autoHeroTargetParatrooperState =
                autoHero != null && autoHero.isActiveAndEnabled ? autoHero.TelemetryTargetParatrooperState : "";
            string autoHeroFallbackStage =
                autoHero != null && autoHero.isActiveAndEnabled ? autoHero.TelemetryLastFallbackStage : "";
            int autoHeroFallbackLivingParatrooperModels =
                autoHero != null && autoHero.isActiveAndEnabled ? autoHero.TelemetryFallbackLivingParatrooperModels : 0;
            int autoHeroFallbackEnabledEnemyBodyPartColliders =
                autoHero != null && autoHero.isActiveAndEnabled ? autoHero.TelemetryFallbackEnabledEnemyBodyPartColliders : 0;
            string sceneProfileIdValue = GameplaySceneRules_V2.IsActive ? GameplaySceneRules_V2.ProfileId : "";
            float px = 0f;
            float py = 0f;
            int ammoMag = -1;
            int ammoMagMax = -1;
            int ammoReserve = -1;
            if (h != null)
            {
                Vector2 p = h.transform.position;
                px = p.x;
                py = p.y;
                ammoMag = h.GetCurrentWeaponAmmo();
                ammoMagMax = h.GetCurrentWeaponMaxAmmo();
                ammoReserve = h.GetCurrentWeaponReserveAmmo();
            }

            int wave = _waveManager != null ? _waveManager.CurrentWaveNumber : -1;
            string loopState = _waveManager != null ? _waveManager.State.ToString() : "";
            int bunkerHp = _waveManager != null ? _waveManager.BunkerHealth : -1;
            int bunkerMax = _waveManager != null ? _waveManager.BunkerMaxHealth : -1;
            int currency = _waveManager != null ? _waveManager.Currency : -1;
            int killsThisWave = _waveManager != null ? _waveManager.EnemiesKilledThisWave : -1;
            int trackedLiving = -1;
            string spawnerLine = "";
            if (_waveManager != null && _waveManager.EnemySpawner != null)
            {
                EnemySpawner_V2 sp = _waveManager.EnemySpawner;
                trackedLiving = sp.GetLivingParatroopersTrackedCountForTelemetry();
                spawnerLine = sp.BuildDiagnosticsSnapshotForTelemetry();
            }

            Scene activeScene = SceneManager.GetActiveScene();
            string sceneLabel =
                activeScene.path != null && activeScene.path.Length > 0 ? activeScene.path : activeScene.name;
            float inWaveUnscaled = _waveManager != null ? _waveManager.InWaveElapsedUnscaledSec : -1f;
            string scalingShort = "";
            if (_waveManager != null && _waveManager.TryGetScalingSnapshotForTelemetry(out WaveRunScalingSnapshot snap))
            {
                scalingShort =
                    $"ver={snap.ScalingVersion} effHP={snap.EffectiveEnemyHpMultiplier.ToString("0.###", CultureInfo.InvariantCulture)} " +
                    $"effDmg={snap.EffectiveEnemyDamageMultiplier.ToString("0.###", CultureInfo.InvariantCulture)} " +
                    $"effSpawn={snap.EffectiveSpawnIntervalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} " +
                    $"effReward={snap.EffectiveWaveRewardCurrency}";
                if (scalingShort.Length > 400)
                {
                    scalingShort = scalingShort.Substring(0, 400);
                }
            }

            if (spawnerLine.Length > 500)
            {
                spawnerLine = spawnerLine.Substring(0, 500);
            }

            return new TelemetryUnityLogRow
            {
                sessionId = _sessionId ?? "",
                utcIso8601 = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                realtimeSinceStartup = Time.realtimeSinceStartup,
                unityEditorOrPlayerVersion = Application.unityVersion ?? "",
                logType = type.ToString(),
                messageTruncated = message ?? "",
                stackTraceTruncated = stackTrace ?? "",
                fingerprint = fingerprint,
                repeatCount = 1,
                wave = wave,
                waveLoopState = loopState,
                heroHp = heroHp,
                heroMaxHp = heroMax,
                weapon = weaponLabel,
                weaponType = weaponTypeStr,
                autoHeroTestProfile = autoHeroProfile,
                autoHeroHasTarget = autoHeroHasTarget,
                autoHeroInRange = autoHeroInRange,
                autoHeroCanHoldFire = autoHeroCanHoldFire,
                autoHeroTargetShootableOnCamera = autoHeroTargetShootableOnCamera,
                autoHeroShootBlockedByBunkerMove = autoHeroShootBlockedByBunkerMove,
                autoHeroRawShootHeld = autoHeroRawShootHeld,
                autoHeroImmediateGroundParatrooperThreat = autoHeroImmediateGroundThreat,
                autoHeroTargetKind = autoHeroTargetKind,
                autoHeroTargetParatrooperState = autoHeroTargetParatrooperState,
                autoHeroFallbackStage = autoHeroFallbackStage,
                autoHeroFallbackLivingParatrooperModels = autoHeroFallbackLivingParatrooperModels,
                autoHeroFallbackEnabledEnemyBodyPartColliders = autoHeroFallbackEnabledEnemyBodyPartColliders,
                sceneProfileId = sceneProfileIdValue,
                heroPosX = px,
                heroPosY = py,
                heroAmmoInMag = ammoMag,
                heroAmmoMagMax = ammoMagMax,
                heroReserveAmmo = ammoReserve,
                bunkerHp = bunkerHp,
                bunkerMaxHp = bunkerMax,
                currency = currency,
                enemiesKilledThisWave = killsThisWave,
                trackedLivingParatroopers = trackedLiving,
                timeUnscaled = Time.unscaledTime,
                timeSinceLevelLoad = Time.timeSinceLevelLoad,
                timeScale = Time.timeScale,
                frameCount = Time.frameCount,
                activeScenePathOrName = sceneLabel ?? "",
                loadedSceneCount = SceneManager.sceneCount,
                isEditor = Application.isEditor,
                platform = Application.platform.ToString(),
                internetReachability = Application.internetReachability.ToString(),
                managedHeapBytes = GC.GetTotalMemory(false),
                inWaveUnscaledSec = inWaveUnscaled,
                damageTakenHeroThisWave = _heroDamageTakenDuringWave,
                damageTakenBunkerThisWave = _bunkerDamageTakenDuringWave,
                damageTakenBunkerFromBombsThisWave = _bunkerDamageFromBombsTakenDuringWave,
                shotsFiredThisWave = _shotsFiredDuringWave,
                bunkerPressureTimeUnscaledThisWave = _bunkerLowPressureTimeUnscaledDuringWave,
                scalingSnapshotShort = scalingShort,
                spawnerDiagnosticsLine = spawnerLine
            };
        }

        private static string ComputeUnityLogFingerprint(string message, string stackTrace)
        {
            uint a = Fnv1a32Prefix(message, 8000);
            uint b = Fnv1a32Prefix(stackTrace, 12000);
            uint c = Fnv1a32Prefix((message ?? "") + "\n" + (stackTrace ?? ""), 16000);
            return a.ToString("X8", CultureInfo.InvariantCulture) +
                   b.ToString("X8", CultureInfo.InvariantCulture) +
                   c.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static uint Fnv1a32Prefix(string text, int maxChars)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offset;
            if (string.IsNullOrEmpty(text))
            {
                return hash;
            }

            int n = Mathf.Min(maxChars, text.Length);
            for (int i = 0; i < n; i++)
            {
                hash ^= text[i];
                hash *= prime;
            }

            return hash;
        }

        private void AppendOrCoalesceUnityLogRow(TelemetryUnityLogRow row)
        {
            if (!_telemetryEnabled || row == null)
            {
                return;
            }

            try
            {
                EnsureOutputPath();
                TelemetryFileRoot root = ReadRootOrNew();
                var list = new List<TelemetryUnityLogRow>(root.unityLogs ?? Array.Empty<TelemetryUnityLogRow>());
                int maxRows = Mathf.Clamp(_unityLogsMaxRowsPerSession, 16, 500);
                if (_unityLogCoalesceConsecutiveDuplicates && list.Count > 0)
                {
                    TelemetryUnityLogRow last = list[list.Count - 1];
                    if (last != null &&
                        !string.IsNullOrEmpty(last.fingerprint) &&
                        last.fingerprint == row.fingerprint)
                    {
                        last.repeatCount = Mathf.Max(1, last.repeatCount) + Mathf.Max(1, row.repeatCount);
                        root.unityLogs = list.ToArray();
                        PersistTelemetryRoot(root);
                        return;
                    }
                }

                while (list.Count >= maxRows)
                {
                    list.RemoveAt(0);
                }

                row.repeatCount = Mathf.Max(1, row.repeatCount);
                list.Add(row);
                root.unityLogs = list.ToArray();
                PersistTelemetryRoot(root);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WaveRunTelemetry_V2] Failed to write unity log row: {ex.Message}");
            }
        }

        private TelemetryFileRoot ReadRootOrNew()
        {
            if (!File.Exists(_filePath))
            {
                return new TelemetryFileRoot
                {
                    events = Array.Empty<TelemetryEvent>(),
                    unityLogs = Array.Empty<TelemetryUnityLogRow>()
                };
            }

            string text = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new TelemetryFileRoot
                {
                    events = Array.Empty<TelemetryEvent>(),
                    unityLogs = Array.Empty<TelemetryUnityLogRow>()
                };
            }

            try
            {
                TelemetryFileRoot root = JsonUtility.FromJson<TelemetryFileRoot>(text);
                if (root == null)
                {
                    return new TelemetryFileRoot
                    {
                        events = Array.Empty<TelemetryEvent>(),
                        unityLogs = Array.Empty<TelemetryUnityLogRow>()
                    };
                }

                if (root.events == null)
                {
                    root.events = Array.Empty<TelemetryEvent>();
                }
                else
                {
                    foreach (TelemetryEvent ev in root.events)
                    {
                        if (ev != null &&
                            (ev.kind == "session_begin" || ev.kind == "session_quit"))
                        {
                            ev.waveScalingJson = "";
                        }
                    }
                }

                if (root.unityLogs == null)
                {
                    root.unityLogs = Array.Empty<TelemetryUnityLogRow>();
                }

                return root;
            }
            catch (Exception)
            {
                return new TelemetryFileRoot
                {
                    events = Array.Empty<TelemetryEvent>(),
                    unityLogs = Array.Empty<TelemetryUnityLogRow>()
                };
            }
        }
    }
}
