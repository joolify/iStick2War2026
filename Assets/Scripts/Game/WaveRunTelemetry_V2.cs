using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using iStick2War;

namespace iStick2War_V2
{
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
        private float _timeInBunkerUnscaledDuringWave;
        private int _shotsFiredDuringWave;
        private int _rayHitsDuringWave;
        private int _rayMissesDuringWave;
        private int _projectileLaunchesDuringWave;
        private int _reloadsDuringWave;

        // Snapshots at wave start (InWave)
        private int _heroHpAtWaveStart;
        private int _bunkerHpAtWaveStart;
        private int _currencyAtWaveStart;

        // Shop intermission before the wave that just cleared (set when leaving Shop → Preparing)
        private int _shopPurchasesPriorToWaveJustCleared;
        private int _shopCurrencySpentPriorToWaveJustCleared;

        private int _intermissionShopPurchaseCount;
        private int _intermissionShopCurrencySpent;

        private float _minBunkerHpRatioThisWave;
        private List<BunkerHpSamplePoint> _bunkerHpSamplesThisWave;
        private float _nextBunkerSampleRealtime;
        private float _bunkerLowPressureTimeUnscaledDuringWave;

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
            public float timeInBunkerSec;
            public int shotsFired;
            public int rayHits;
            public int rayMisses;
            public int projectileLaunches;
            public int reloads;
            public int shopPurchasesPrior;
            public int shopCurrencySpentPrior;
            public int heroHpWaveStart;
            public int bunkerHpWaveStart;
            public int currencyWaveStart;
            public string weaponType;
            /// <summary><see cref="AutoHeroTestProfileKind_V2"/> on the hero when present; empty if no bot / disabled.</summary>
            public string autoHeroTestProfile;

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
            /// (sustained low-bunker pressure). Meaningful on wave_cleared and run_end after abort during InWave; 0 otherwise.
            /// </summary>
            public float bunkerPressureTimeSec;

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
        }

        private void Awake()
        {
            _sessionId = Guid.NewGuid().ToString("N");
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
                _bunkerLowPressureTimeUnscaledDuringWave += Time.unscaledDeltaTime;
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
        }

        private void OnApplicationQuit()
        {
            TryWriteSessionQuit();
        }

        /// <summary>Called from <see cref="WaveManager_V2"/> when bunker loses HP.</summary>
        public static void NotifyBunkerDamageTaken(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            ActiveInstance?.RegisterBunkerDamageTaken(amount);
        }

        /// <summary>Called from <see cref="WaveManager_V2"/> after a successful shop spend.</summary>
        public static void NotifyShopPurchase(string offerKind, int currencySpent)
        {
            ActiveInstance?.RegisterShopPurchase(offerKind, currencySpent);
        }

        private void RegisterBunkerDamageTaken(int amount)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _bunkerDamageTakenDuringWave += Mathf.Max(0, amount);
        }

        private void RegisterShopPurchase(string offerKind, int currencySpent)
        {
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.Shop)
            {
                return;
            }

            _intermissionShopPurchaseCount++;
            _intermissionShopCurrencySpent += Mathf.Max(0, currencySpent);
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
            if (!_telemetryEnabled || _waveManager == null || _waveManager.State != WaveLoopState_V2.InWave)
            {
                return;
            }

            _heroDamageTakenDuringWave += Mathf.Max(0, amount);
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
                _intermissionShopPurchaseCount = 0;
                _intermissionShopCurrencySpent = 0;
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

            if (newState == WaveLoopState_V2.GameOver)
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
            _timeInBunkerUnscaledDuringWave = 0f;
            _shotsFiredDuringWave = 0;
            _rayHitsDuringWave = 0;
            _rayMissesDuringWave = 0;
            _projectileLaunchesDuringWave = 0;
            _reloadsDuringWave = 0;
            _bunkerLowPressureTimeUnscaledDuringWave = 0f;
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
            if (kind == "wave_cleared" || (kind == "run_end" && runEndIncludeAbortWaveBunkerCurve))
            {
                bunkerPressureTimeForRow = _bunkerLowPressureTimeUnscaledDuringWave;
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
                timeInBunkerSec = _timeInBunkerUnscaledDuringWave,
                shotsFired = _shotsFiredDuringWave,
                rayHits = _rayHitsDuringWave,
                rayMisses = _rayMissesDuringWave,
                projectileLaunches = _projectileLaunchesDuringWave,
                reloads = _reloadsDuringWave,
                shopPurchasesPrior = _shopPurchasesPriorToWaveJustCleared,
                shopCurrencySpentPrior = _shopCurrencySpentPriorToWaveJustCleared,
                heroHpWaveStart = _heroHpAtWaveStart,
                bunkerHpWaveStart = _bunkerHpAtWaveStart,
                currencyWaveStart = _currencyAtWaveStart,
                weaponType = weaponTypeStr,
                autoHeroTestProfile = autoHeroProfile,
                sceneProfileId = sceneProfileIdValue,
                bunkerBreached = bunkerBreached,
                bunkerCriticalLow = bunkerCriticalLow,
                heroDead = heroDeadSnap,
                waveStressScore = waveStress,
                bunkerPressureTimeSec = bunkerPressureTimeForRow,
                minBunkerHpRatioThisWave = minBunkerRatio,
                bunkerHpSamples = bunkerSamples
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
            string reason = ResolveGameOverReason(previousState);
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

        private string ResolveGameOverReason(WaveLoopState_V2 previousState)
        {
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
                }

                root.events = list.ToArray();
                ApplyTelemetryDocumentation(root);
                string json = JsonUtility.ToJson(root, prettyPrint: true);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WaveRunTelemetry_V2] Failed to write telemetry: {ex.Message}");
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
                "iStick2War wave-run telemetry (JSON). Root contains _comment, glossary, bunkerCriticalHpFractionUsed, and events[]. " +
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
                "bunkerPressureTimeSec (wave_cleared / applicable run_end) uses bunkerPressureHpRatioThresholdUsed on the root.";
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
                            "(sustained low-bunker time), not a full 'feel' model."
                    },
                    new TelemetryGlossaryEntry
                    {
                        property = "bunkerPressureTimeSec",
                        meaning =
                            "wave_cleared, and run_end after abort during InWave: unscaled seconds spent InWave while " +
                            "bunkerHp/bunkerMaxHp was strictly below root.bunkerPressureHpRatioThresholdUsed (" +
                            bunkerPressureHpRatioThresholdUsed.ToString("0.###", CultureInfo.InvariantCulture) +
                            " in this file). 0 on other kinds. Measures sustained cover pressure, not damage spikes alone."
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
                    }
                }
            };
        }

        private TelemetryFileRoot ReadRootOrNew()
        {
            if (!File.Exists(_filePath))
            {
                return new TelemetryFileRoot { events = Array.Empty<TelemetryEvent>() };
            }

            string text = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new TelemetryFileRoot { events = Array.Empty<TelemetryEvent>() };
            }

            try
            {
                TelemetryFileRoot root = JsonUtility.FromJson<TelemetryFileRoot>(text);
                if (root == null)
                {
                    return new TelemetryFileRoot { events = Array.Empty<TelemetryEvent>() };
                }

                if (root.events == null)
                {
                    root.events = Array.Empty<TelemetryEvent>();
                }

                return root;
            }
            catch (Exception)
            {
                return new TelemetryFileRoot { events = Array.Empty<TelemetryEvent>() };
            }
        }
    }
}
