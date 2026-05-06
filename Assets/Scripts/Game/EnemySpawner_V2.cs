using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Serialization;

namespace iStick2War_V2
{
    public sealed class EnemySpawner_V2 : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private Paratrooper _paratrooperPrefab;
        [Tooltip("Optional. Spawned after paratrooper flights when WaveConfig.MechRobotBossCount > 0.")]
        [SerializeField] private MechRobotBoss _mechRobotBossPrefab;

        [Header("Mech Robot Boss spawn")]
        [Tooltip(
            "When true, spawns past the orthographic camera left/right edge on X and snaps Y to ground (raycast). " +
            "MechRobotBossController then walks/runs into combat range and stops to shoot. When false, uses Spawn X/Y range + Clamp To Camera View.")]
        [SerializeField] private bool _spawnMechBossFromOffscreenGround = true;
        [Tooltip("Extra horizontal distance beyond the frustum edge (added to Offscreen Beyond Frustum Horizontal World).")]
        [SerializeField] private float _mechBossExtraOffscreenHorizontalWorld = 2f;
        [Tooltip("Added to ground hit Y when using off-screen ground spawn. Larger values spawn the mech higher so gravity drops stay inside the game view longer.")]
        [SerializeField] private float _mechBossGroundYOffsetWorld = 4.5f;
        [Tooltip("Fallback Y when no ground hit: lerp between bottom and top of visible frustum (0 = bottom).")]
        [Range(0f, 1f)]
        [SerializeField] private float _mechBossSpawnVerticalNormalized01 = 0.62f;

        [Header("Spawn Anchors (left/right)")]
        [SerializeField] private Transform[] _leftSpawnPoints;
        [SerializeField] private Transform[] _rightSpawnPoints;
        [Tooltip("If left/right arrays are empty, assign transforms from the scene by GameObject name.")]
        [SerializeField] private bool _autoBindSpawnPointsByName = true;
        [SerializeField] private string _spawnPointLeftName = "spawnPointLeft";
        [SerializeField] private string _spawnPointRightName = "spawnPointRight";
        [Tooltip(
            "OFF: uses pin world positions. For fly-across aircraft, place pins past the camera’s left/right in X; " +
            "keep Y within the orthographic top/bottom (flight is horizontal only — too-high Y stays above the Game view). " +
            "ON: pins snap to the visible left/right edges when the camera moves.")]
        [SerializeField] private bool _snapSpawnsToCameraViewEdges = false;
        [Tooltip("Moves spawn X slightly inward from the left/right screen edge (world units).")]
        [SerializeField] private float _anchorExtraHorizontalInset = 0.35f;
        [SerializeField] private float _anchorSpawnWorldZ = 0f;

        [Header("Frustum off-screen spawn (when no pin transforms)")]
        [Tooltip(
            "If left/right anchor arrays are empty and auto-bind finds no named objects, spawn aircraft just past " +
            "the orthographic frustum left/right (follows the spawn camera).")]
        [SerializeField] private bool _useFrustumOffscreenSpawnWhenNoAnchors = true;
        [Tooltip("World units beyond the visible left/right frustum edge (larger = further outside Game view).")]
        [SerializeField] private float _offscreenBeyondFrustumHorizontalWorld = 2f;
        [Tooltip("Fly lane height: 0 = bottom inset of frame, 1 = top inset (orthographic, uses Frustum Inset Padding).")]
        [Range(0f, 1f)]
        [SerializeField] private float _flyLaneVerticalNormalized01 = 0.82f;
        [Tooltip(
            "When using computed frustum spawns, raycast down from above the camera to the Ground layer and place the fly lane " +
            "that many world units above the hit. Falls back to the normalized frustum lane if nothing is hit (keeps old behaviour).")]
        [SerializeField] private bool _alignFlyLaneYToGroundSurface = true;
        [SerializeField] private LayerMask _groundSurfaceProbeMask = 0;
        [Tooltip("Helicopter / drop lane sits this far above the probed ground surface (world Y).")]
        [SerializeField] private float _flyLaneHeightAboveGroundWorld = 5.75f;
        [Tooltip("Ray origin is this far above the top inset of the orthographic frustum before casting down.")]
        [SerializeField] private float _groundProbeStartPaddingAboveFrustumTop = 1.5f;
        [Tooltip("Max ray length when probing for ground (world units).")]
        [SerializeField] private float _groundProbeMaxDistanceWorld = 220f;

        [Header("Aircraft (optional)")]
        [Tooltip("e.g. Helicopter V2 — instantiated at the chosen left/right spawn anchor when anchors are used.")]
        [FormerlySerializedAs("_aircraftPrefab")]
        [SerializeField] private GameObject _helicopterPrefab;
        [Tooltip("Optional bomber pass prefab (Bombplane_V2 + AircraftHealth_V2). Spawned by WaveConfig.BomberPassCount.")]
        [SerializeField] private GameObject _bomberPrefab;
        [SerializeField] private float _bomberPassIntervalSeconds = 4.5f;
        [Tooltip("Optional KamikazeDrone prefab (EnemyKamikazeDrone_V2 + AircraftHealth_V2).")]
        [FormerlySerializedAs("_kamikazeDronePrefab")]
        [SerializeField] private GameObject _kamikazeDronePrefabAsset;
        [SerializeField] private float _kamikazeDroneSpawnIntervalSeconds = 3.2f;
        [Tooltip("Optional bomb drone prefab (EnemyBombDrone_V2 + AircraftHealth_V2).")]
        [SerializeField] private GameObject _bombDronePrefab;
        [SerializeField] private float _bombDroneSpawnIntervalSeconds = 4.1f;
        [SerializeField] private float _aircraftAutoDestroySeconds = 8f;
        [Tooltip("Child transform name on aircraft used as paratrooper drop origin (recommended: place at helicopter door center).")]
        [SerializeField] private string _paratrooperMountChildName = "ParatrooperDoorMount";
        [Tooltip("Optional Spine bone used as paratrooper drop origin (checked before mount child fallback).")]
        [SerializeField] private string _paratrooperSpawnBoneName = "paratrooperSpawnPoint";
        [Tooltip("Optional extra offset when using Spine bone as drop origin. Keep zero for exact bone position.")]
        [SerializeField] private Vector3 _paratrooperOffsetFromSpineBone = Vector3.zero;
        [SerializeField] private Vector3 _paratrooperOffsetFromMount = Vector3.zero;
        [Tooltip("If the resolved mount point is inside aircraft bounds, auto-place drop point below aircraft.")]
        [SerializeField] private bool _forceDropBelowAircraftWhenMountInsideBounds = true;
        [SerializeField] private float _forceDropBelowAircraftExtraMargin = 0.2f;
        [Tooltip("When spawning from the right anchor, flip root localScale.x to face inward.")]
        [SerializeField] private bool _flipAircraftScaleXWhenFromRightSpawn = true;
        [Tooltip("Raises helicopter spawn altitude (world Y). 0.01 = 1 cm.")]
        [SerializeField] private float _helicopterSpawnYOffsetWorld = 0.01f;
        [SerializeField] private bool _overrideAircraftSpriteSorting = true;
        [Tooltip("Empty = keep prefab sorting layer; only change Sorting Order.")]
        [SerializeField] private string _aircraftSortingLayerName = "";
        [SerializeField] private int _aircraftSpriteSortingOrder = 40;
        [Tooltip("Fly horizontally across the view (left spawn → +X, right spawn → -X). Off-screen = destroyed.")]
        [SerializeField] private bool _aircraftEnableHorizontalFlight = true;
        [SerializeField] private float _aircraftHorizontalFlySpeed = 5.5f;
        [SerializeField] private float _aircraftFlyOffscreenMarginWorld = 4f;
        [SerializeField] private float _aircraftFlightMaxLifetimeSeconds = 45f;
        [Tooltip("If enabled, paratrooper is spawned only after aircraft enters camera view.")]
        [SerializeField] private bool _spawnParatrooperWhenAircraftIsVisible = true;
        [Header("Paratrooper drops per helicopter flight")]
        [Tooltip("Minimum paratroopers dropped by one helicopter flight.")]
        [SerializeField] private int _minParatroopersPerFlight = 1;
        [Tooltip("Maximum paratroopers dropped by one helicopter flight.")]
        [SerializeField] private int _maxParatroopersPerFlight = 5;
        [Tooltip("Delay between drops from the same helicopter flight (seconds).")]
        [SerializeField] private float _paratrooperDropIntervalPerFlight = 0.4f;
        [Tooltip("Safety cap for early waves to avoid immediate overload spikes from one helicopter flight.")]
        [SerializeField] private bool _capParatroopersPerFlightInEarlyWaves = true;
        [Tooltip("Apply early-wave cap up to and including this wave number (1-based).")]
        [SerializeField] private int _earlyWaveCapMaxWaveInclusive = 3;
        [Tooltip("Maximum paratroopers per helicopter flight while early-wave cap is active.")]
        [SerializeField] private int _earlyWaveMaxParatroopersPerFlight = 2;
        [Header("Early-wave stabilization (wave 1-3)")]
        [Tooltip("Apply deterministic anti-spike pacing in early waves.")]
        [SerializeField] private bool _enableEarlyWaveStabilization = true;
        [SerializeField] private int _earlyWaveStabilizationMaxWaveInclusive = 3;
        [Tooltip("Multiplier on flight spawn interval in early waves (higher = slower spawns).")]
        [SerializeField] private float _earlyWaveFlightSpawnIntervalMultiplier = 1.25f;
        [Tooltip("Multiplier on per-flight drop spacing in early waves (higher = less stack).")]
        [SerializeField] private float _earlyWaveDropIntervalMultiplier = 1.35f;
        [Tooltip("Soft cap on simultaneously alive paratroopers during early waves.")]
        [SerializeField] private int _earlyWaveMaxSimultaneousAliveParatroopers = 3;
        [Header("Wave 2 pacing override (anti-spike)")]
        [Tooltip("Apply additional pacing dampening only on wave 2 to reduce immediate post-wave-1 overload.")]
        [SerializeField] private bool _enableWave2PacingOverride = true;
        [Tooltip("Wave 2 cap for paratroopers per helicopter flight.")]
        [SerializeField] private int _wave2MaxParatroopersPerFlight = 2;
        [Tooltip("Multiplier on helicopter-flight spawn interval during wave 2.")]
        [SerializeField] private float _wave2FlightSpawnIntervalMultiplier = 1.2f;
        [Tooltip("Multiplier on in-flight drop spacing during wave 2.")]
        [SerializeField] private float _wave2DropIntervalMultiplier = 1.4f;
        [Tooltip("Safety timeout for delayed drop; spawns anyway after this many seconds.")]
        [SerializeField] private float _maxSecondsToWaitForVisibleAircraftDrop = 6f;
        [Tooltip(
            "Inset (safe margin) inside camera rect before drop is allowed. " +
            "Higher values delay drop until aircraft is clearly inside view.")]
        [SerializeField] private float _aircraftVisibleCheckPaddingWorld = 0.25f;
        [Tooltip("Extra safety margin added to visible-check inset for actual paratrooper drop position.")]
        [SerializeField] private float _paratrooperDropSafetyMarginWorld = 0.2f;
        [Tooltip(
            "Extra world offset per spawn index further outside the playfield (left spawns: −X, right spawns: +X) " +
            "so multiple helicopters in one wave do not share the same approach X.")]
        [SerializeField] private float _aircraftSameWaveApproachAxisStagger = 5f;
        [Tooltip("If the helicopter sprite moves opposite to its nose, enable this to flip travel along X.")]
        [SerializeField] private bool _invertAircraftFlightDirectionX = false;
        [Tooltip(
            "Visual facing calibration: ON if sprite nose points right when localScale.x is positive. " +
            "OFF if sprite nose points left at positive scale.")]
        [SerializeField] private bool _aircraftSpriteFacesRightWhenScaleXPositive = false;
        [Tooltip("Flip spawned paratrooper root so it faces toward the playfield/hero based on spawn side.")]
        [SerializeField] private bool _faceParatrooperTowardPlayfieldOnSpawn = true;
        [Tooltip(
            "Paratrooper visual calibration: ON if paratrooper faces right when localScale.x is positive. " +
            "OFF if it faces left at positive scale.")]
        [SerializeField] private bool _paratrooperFacesRightWhenScaleXPositive = true;

        [Header("Fallback Random Area")]
        [SerializeField] private Vector2 _spawnXRange = new Vector2(-12f, 12f);
        [SerializeField] private Vector2 _spawnYRange = new Vector2(5f, 9f);
        [Tooltip(
            "Clamp random fallback spawn positions into the orthographic frustum (Spawn Camera / Main). " +
            "Does not clamp left/right anchor spawns — use off-screen pins + horizontal flight for fly-in.")]
        [SerializeField] private bool _clampSpawnInsideCameraView = true;
        [SerializeField] private Camera _spawnCamera;
        [Tooltip(
            "Inset from camera frustum when using snap or fallback clamp. Keep well below Orthographic Size " +
            "(e.g. 0.3–1). Values near or above Size collapse the usable frustum.")]
        [SerializeField] private float _orthographicFrustumInsetPadding = 0.6f;

        [Header("Debug")]
        [SerializeField] private bool _debugSpawnLogs = false;
        [Tooltip("Forces all spawned Paratrooper_V2 to use grenade-only behavior (disables MP40).")]
        [SerializeField] private bool _masterDebugGrenadeOnly = false;
        [Tooltip(
            "Logs anchor world positions, camera frustum snap, and fallback path. " +
            "If spawn positions look wrong: check Snap Spawns To Camera View Edges and Orthographic Frustum Inset Padding.")]
        [SerializeField] private bool _debugAnchorSpawnDiagnostics = true;

        private readonly HashSet<ParatrooperDeathHandler_V2> _trackedDeaths = new HashSet<ParatrooperDeathHandler_V2>();
        private readonly HashSet<MechRobotBossDeathHandler_V2> _trackedMechBossDeaths = new HashSet<MechRobotBossDeathHandler_V2>();
        private readonly HashSet<AircraftHealth_V2> _trackedAircraftDeaths = new HashSet<AircraftHealth_V2>();
        private readonly List<GameObject> _spawnedAircraftInstances = new List<GameObject>();
        private Coroutine _spawnRoutine;
        private Action _onEnemyKilled;
        private int _targetSpawnCount;
        private int _spawnedCount;
        private bool _spawnRoutineFinished;
        private bool _isWaveActive;
        private int _waveSessionId;
        /// <summary>1-based wave index from <see cref="WaveManager_V2.CurrentWaveNumber"/> for debug logs; 0 if unset.</summary>
        private int _waveNumberForDiagnostics;
        private WaveConfig_V2 _activeWaveConfig;
        private float _runtimeEnemyHealthMultiplier = 1f;
        private float _runtimeEnemyDamageMultiplier = 1f;
        private float _runtimeSpawnIntervalSeconds = 1f;
        private static bool _loggedFrustumPaddingClamp;
        private bool _loggedMissingParatrooperMountOnce;
        private bool _loggedMissingParatrooperSpawnBoneOnce;
        private int _paratrooperDebugSpawnSeq;
        private bool _lastResolvedMountUsedFallbackRoot;
        private int _pendingDelayedDropCoroutines;
        private float _lastParatrooperSpawnUnscaledTime;
        private float _lastSpawnAttemptUnscaledTime;
        private int _failedParatrooperSpawnAttemptsThisWave;
        private int _spawnStarvationRecoveryCount;
        private string _lastSpawnAbortReason = "";
        private string _spawnRoutineExitReason = "not_started";
        private int _activeAirThreatSpawnRoutines;
        private int _mechBossTargetCount;
        private int _mechBossSpawnedCount;
        private bool _mechBossScheduleComplete;

        public float LastParatrooperSpawnUnscaledTime => _lastParatrooperSpawnUnscaledTime;
        public float LastSpawnAttemptUnscaledTime => _lastSpawnAttemptUnscaledTime;
        public int SpawnedParatroopersThisWave => _spawnedCount;
        public int TargetParatroopersThisWave => _targetSpawnCount;
        public int PendingParatrooperDropsThisWave => _pendingDelayedDropCoroutines;
        public int FailedParatrooperSpawnAttemptsThisWave => _failedParatrooperSpawnAttemptsThisWave;
        public int SpawnStarvationRecoveryCountThisWave => _spawnStarvationRecoveryCount;
        public string LastSpawnAbortReason => _lastSpawnAbortReason ?? "";
        public string SpawnRoutineExitReason => _spawnRoutineExitReason ?? "";
        public bool IsWaveActive => _isWaveActive;
        public bool IsSpawnStarvedThisWave =>
            _isWaveActive &&
            _spawnRoutineFinished &&
            _pendingDelayedDropCoroutines == 0 &&
            _spawnedCount < _targetSpawnCount;

        /// <summary>
        /// True when the paratrooper spawn coroutine has finished and every scheduled drop has been accounted for
        /// (matches the spawn half of <see cref="IsWaveCleared"/>). While the wave is still active, long gaps without
        /// new paratrooper spawns are normal — only clearing living enemies remains. Watchdogs should not treat that as a stall.
        /// </summary>
        public bool HasFinishedScheduledParatrooperSpawnsThisWave =>
            _isWaveActive &&
            _spawnRoutineFinished &&
            _spawnedCount >= _targetSpawnCount &&
            _pendingDelayedDropCoroutines == 0;

        /// <summary>
        /// Approximate living paratroopers (tracked death handlers still active). For telemetry / diagnostics only.
        /// </summary>
        public int GetLivingParatroopersTrackedCountForTelemetry()
        {
            PruneInactiveTrackedDeaths();
            return _trackedDeaths.Count;
        }

        /// <summary>One-line spawner state for GameError / telemetry (call before <see cref="StopWave"/> if possible).</summary>
        public string BuildDiagnosticsSnapshotForTelemetry()
        {
            PruneInactiveTrackedDeaths();
            float lastSpawnAgeSec = Time.unscaledTime - _lastParatrooperSpawnUnscaledTime;
            PruneDestroyedAircraftFromTracking();
            return string.Format(
                CultureInfo.InvariantCulture,
                "isWaveActive={0} waveDiag={1} targetSpawn={2} spawned={3} spawnRoutineFinished={4} pendingDrops={5} " +
                "trackedLiving={6} trackedAircraft={7} activeAirThreatRoutines={8} activeAirThreats={9} " +
                "lastParatrooperSpawnAgeSec={10:0.###} lastSpawnAttemptAgeSec={11:0.###} spawnStarved={12} spawnExit='{13}' failedSpawnAttempts={14} " +
                "spawnRecoveries={15} lastAbort='{16}' waveSession={17}",
                _isWaveActive,
                FormatWaveNumberLabel(),
                _targetSpawnCount,
                _spawnedCount,
                _spawnRoutineFinished,
                _pendingDelayedDropCoroutines,
                _trackedDeaths.Count,
                _trackedAircraftDeaths.Count,
                _activeAirThreatSpawnRoutines,
                _spawnedAircraftInstances.Count,
                lastSpawnAgeSec,
                Mathf.Max(0f, Time.unscaledTime - _lastSpawnAttemptUnscaledTime),
                IsSpawnStarvedThisWave,
                SpawnRoutineExitReason,
                _failedParatrooperSpawnAttemptsThisWave,
                _spawnStarvationRecoveryCount,
                LastSpawnAbortReason,
                _waveSessionId);
        }

        private void Awake()
        {
            if (_autoBindSpawnPointsByName)
            {
                AutoBindSpawnAnchorsFromSceneIfMissing();
            }

            if (_debugAnchorSpawnDiagnostics)
            {
                LogAnchorSetupOnce();
            }

            if (_masterDebugGrenadeOnly)
            {
                Debug.LogWarning(
                    "[EnemySpawner_V2] Master Debug Grenade Only is ON: every spawned Paratrooper_V2 gets grenade-only AI " +
                    "and MP40 is hard-disabled. They will throw at most until grenade cooldown, then idle with no MP40. " +
                    "Disable _masterDebugGrenadeOnly on this spawner for normal combat.");
            }
        }

        public void BeginWave(
            WaveConfig_V2 config,
            Action onEnemyKilled,
            int waveNumberForLogs = 0,
            float enemyHealthMultiplier = -1f,
            float enemyDamageMultiplier = -1f,
            float spawnIntervalSeconds = -1f)
        {
            StopWave();
            if (config == null)
            {
                return;
            }

            // Allow pure air-threat waves (EnemyCount == 0) even when paratrooper prefab is not assigned.
            if (_paratrooperPrefab == null && config.EnemyCount > 0)
            {
                Debug.LogWarning(
                    "[EnemySpawner_V2] Wave has EnemyCount > 0 but Paratrooper prefab is missing. " +
                    "Cannot spawn paratroopers for this wave.");
                return;
            }

            _isWaveActive = true;
            _waveSessionId++;
            _waveNumberForDiagnostics = Mathf.Max(0, waveNumberForLogs);
            _activeWaveConfig = config;
            _runtimeEnemyHealthMultiplier = enemyHealthMultiplier > 0f
                ? enemyHealthMultiplier
                : config.EnemyHealthMultiplier;
            _runtimeEnemyDamageMultiplier = enemyDamageMultiplier > 0f
                ? enemyDamageMultiplier
                : config.EnemyDamageMultiplier;
            _runtimeSpawnIntervalSeconds = spawnIntervalSeconds > 0f
                ? spawnIntervalSeconds
                : config.SpawnIntervalSeconds;
            _onEnemyKilled = onEnemyKilled;
            _targetSpawnCount = Mathf.Max(0, config.EnemyCount);
            _spawnedCount = 0;
            _spawnRoutineFinished = false;
            _pendingDelayedDropCoroutines = 0;
            _lastParatrooperSpawnUnscaledTime = Time.unscaledTime;
            _lastSpawnAttemptUnscaledTime = _lastParatrooperSpawnUnscaledTime;
            _failedParatrooperSpawnAttemptsThisWave = 0;
            _spawnStarvationRecoveryCount = 0;
            _lastSpawnAbortReason = "";
            _spawnRoutineExitReason = "running";
            _activeAirThreatSpawnRoutines = 0;
            _mechBossTargetCount = config.MechRobotBossCount;
            _mechBossSpawnedCount = 0;
            _mechBossScheduleComplete = _mechBossTargetCount <= 0 || _mechRobotBossPrefab == null;
            if (_mechBossTargetCount > 0 && _mechRobotBossPrefab == null)
            {
                Debug.LogWarning(
                    "[EnemySpawner_V2] Wave requests MechRobotBossCount > 0 but Mech Robot Boss prefab is not assigned.");
            }

            _spawnRoutine = StartCoroutine(SpawnRoutine(config));
            if (config.BomberPassCount > 0 && _bomberPrefab != null)
            {
                StartTrackedAirThreatRoutine(
                    SpawnBomberPassRoutine(config.BomberPassCount, _runtimeSpawnIntervalSeconds));
            }
            else if (config.BomberPassCount > 0)
            {
                Debug.LogWarning(
                    "[EnemySpawner_V2] This wave requests " + config.BomberPassCount +
                    " bomber pass(es) but no bomber prefab is assigned.");
            }

            if (config.KamikazeDroneCount > 0 && _kamikazeDronePrefabAsset != null)
            {
                StartTrackedAirThreatRoutine(
                    SpawnKamikazeDroneRoutine(config.KamikazeDroneCount, _runtimeSpawnIntervalSeconds));
            }
            else if (config.KamikazeDroneCount > 0)
            {
                Debug.LogWarning(
                    "[EnemySpawner_V2] This wave requests " + config.KamikazeDroneCount +
                    " kamikaze drone(s) but no kamikaze prefab is assigned.");
            }

            if (config.BombDroneCount > 0 && _bombDronePrefab != null)
            {
                StartTrackedAirThreatRoutine(
                    SpawnBombDroneRoutine(config.BombDroneCount, _runtimeSpawnIntervalSeconds));
            }
            else if (config.BombDroneCount > 0)
            {
                Debug.LogWarning(
                    "[EnemySpawner_V2] This wave requests " + config.BombDroneCount +
                    " bomb drone(s) but no bomb drone prefab is assigned.");
            }
        }

        private void StartTrackedAirThreatRoutine(IEnumerator routine)
        {
            if (routine == null)
            {
                return;
            }

            _activeAirThreatSpawnRoutines++;
            StartCoroutine(RunTrackedAirThreatRoutine(routine));
        }

        public void StopWave()
        {
            _isWaveActive = false;
            _waveNumberForDiagnostics = 0;
            _activeWaveConfig = null;
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }

            DestroySpawnedAircraftNow();

            foreach (ParatrooperDeathHandler_V2 deathHandler in _trackedDeaths)
            {
                if (deathHandler != null)
                {
                    deathHandler.OnDeathStarted -= HandleTrackedEnemyDeath;
                }
            }
            _trackedDeaths.Clear();
            foreach (MechRobotBossDeathHandler_V2 mechDeath in _trackedMechBossDeaths)
            {
                if (mechDeath != null)
                {
                    mechDeath.OnDeathStarted -= HandleTrackedMechBossDeath;
                }
            }
            _trackedMechBossDeaths.Clear();
            foreach (AircraftHealth_V2 aircraftHealth in _trackedAircraftDeaths)
            {
                if (aircraftHealth != null)
                {
                    aircraftHealth.OnDestroyed -= HandleTrackedAircraftDestroyed;
                }
            }
            _trackedAircraftDeaths.Clear();
            _onEnemyKilled = null;
            _targetSpawnCount = 0;
            _spawnedCount = 0;
            _spawnRoutineFinished = false;
            _pendingDelayedDropCoroutines = 0;
            _failedParatrooperSpawnAttemptsThisWave = 0;
            _spawnStarvationRecoveryCount = 0;
            _lastSpawnAbortReason = "";
            _spawnRoutineExitReason = "stopped";
            _activeAirThreatSpawnRoutines = 0;
            _mechBossTargetCount = 0;
            _mechBossSpawnedCount = 0;
            _mechBossScheduleComplete = true;
            // Keep a sane timestamp after StopWave: EnterGameErrorState calls StopWave while the watchdog may still
            // evaluate this frame; 0 would look like "no spawn since boot" and false-trigger no-spawn detection.
            _lastParatrooperSpawnUnscaledTime = Time.unscaledTime;
            _lastSpawnAttemptUnscaledTime = _lastParatrooperSpawnUnscaledTime;
        }

        private IEnumerator RunTrackedAirThreatRoutine(IEnumerator routine)
        {
            while (_isWaveActive && routine != null && routine.MoveNext())
            {
                yield return routine.Current;
            }

            _activeAirThreatSpawnRoutines = Mathf.Max(0, _activeAirThreatSpawnRoutines - 1);
        }

        private void OnDisable()
        {
            StopWave();
        }

        private IEnumerator SpawnRoutine(WaveConfig_V2 config)
        {
            int toSpawn = config.EnemyCount;
            float interval = GetRuntimeFlightSpawnIntervalSeconds();
            int waveSession = _waveSessionId;
            int plannedParatroopers = 0;
            int flightIndex = 0;
            _spawnRoutineExitReason = "running";
            while (plannedParatroopers < toSpawn)
            {
                if (!_isWaveActive || waveSession != _waveSessionId)
                {
                    _spawnRoutineExitReason = "wave-inactive-or-session-changed";
                    yield break;
                }

                if (IsEarlyWaveSimultaneousCapReached())
                {
                    // Hold next helicopter flight until live infantry pressure drops below cap.
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                int remaining = Mathf.Max(0, toSpawn - plannedParatroopers);
                int perFlight = ResolveParatroopersPerFlight(remaining);
                if (perFlight <= 0)
                {
                    _spawnRoutineExitReason = "resolve-per-flight-returned-zero";
                    break;
                }

                SpawnOne(flightIndex, perFlight);
                plannedParatroopers += perFlight;
                flightIndex++;

                if (plannedParatroopers < toSpawn)
                {
                    yield return new WaitForSeconds(interval);
                }
            }

            yield return RunMechRobotBossSpawnRoutine(config, waveSession);

            _spawnRoutine = null;
            _spawnRoutineFinished = true;
            if (_spawnRoutineExitReason == "running")
            {
                _spawnRoutineExitReason = "completed-planned-flights";
            }
            if (_spawnedCount < _targetSpawnCount && _pendingDelayedDropCoroutines == 0)
            {
                _spawnRoutineExitReason = "completed-with-missing-spawns";
            }
        }

        /// <summary>
        /// Attempts to recover one missing paratrooper when spawn schedule is finished but spawned&lt;target.
        /// Returns true only when a new paratrooper was actually spawned.
        /// </summary>
        public bool TryRecoverSpawnStarvation(out string details)
        {
            details = "";
            if (!IsSpawnStarvedThisWave)
            {
                details = "not-starved";
                return false;
            }

            Vector3 recoveryPos = new Vector3(
                UnityEngine.Random.Range(_spawnXRange.x, _spawnXRange.y),
                UnityEngine.Random.Range(_spawnYRange.x, _spawnYRange.y),
                0f);
            recoveryPos = ClampToCameraView(recoveryPos);
            bool spawned = SpawnParatrooper(recoveryPos, usedAnchorSpawn: false, fromLeft: false, aircraft: null);
            if (!spawned)
            {
                details = "recovery-spawn-failed";
                return false;
            }

            _spawnStarvationRecoveryCount++;
            _spawnRoutineExitReason = "starvation-recovered";
            details = $"recovered one missing spawn at {recoveryPos}. spawned={_spawnedCount}/{_targetSpawnCount}";
            return true;
        }

        private IEnumerator SpawnBomberPassRoutine(int bomberPasses, float basedOnSpawnIntervalSeconds)
        {
            int waveSession = _waveSessionId;
            int count = Mathf.Max(0, bomberPasses);
            if (count == 0)
            {
                yield break;
            }

            float interval = Mathf.Max(0.5f, _bomberPassIntervalSeconds);
            if (basedOnSpawnIntervalSeconds > 0f)
            {
                interval = Mathf.Max(0.5f, Mathf.Max(_bomberPassIntervalSeconds, basedOnSpawnIntervalSeconds * 1.25f));
            }

            for (int i = 0; i < count; i++)
            {
                if (!_isWaveActive || waveSession != _waveSessionId)
                {
                    yield break;
                }

                SpawnOneBomberPass(i);
                if (i < count - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        private IEnumerator SpawnKamikazeDroneRoutine(int droneCount, float basedOnSpawnIntervalSeconds)
        {
            int waveSession = _waveSessionId;
            int count = Mathf.Max(0, droneCount);
            if (count == 0)
            {
                yield break;
            }

            float interval = Mathf.Max(0.35f, _kamikazeDroneSpawnIntervalSeconds);
            if (basedOnSpawnIntervalSeconds > 0f)
            {
                interval = Mathf.Max(0.35f, Mathf.Max(_kamikazeDroneSpawnIntervalSeconds, basedOnSpawnIntervalSeconds * 0.9f));
            }

            for (int i = 0; i < count; i++)
            {
                if (!_isWaveActive || waveSession != _waveSessionId)
                {
                    yield break;
                }

                SpawnOneKamikazeDrone(i);
                if (i < count - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        private IEnumerator SpawnBombDroneRoutine(int droneCount, float basedOnSpawnIntervalSeconds)
        {
            int waveSession = _waveSessionId;
            int count = Mathf.Max(0, droneCount);
            if (_debugSpawnLogs)
            {
                Debug.Log(
                    $"[EnemySpawner_V2 BombDrone] Routine start: requested={count}, waveSession={waveSession}, " +
                    $"isWaveActive={_isWaveActive}, prefabAssigned={(_bombDronePrefab != null)}");
            }
            if (count == 0)
            {
                if (_debugSpawnLogs)
                {
                    Debug.Log("[EnemySpawner_V2 BombDrone] Routine exit: requested count is 0.");
                }
                yield break;
            }

            float interval = Mathf.Max(0.5f, _bombDroneSpawnIntervalSeconds);
            if (basedOnSpawnIntervalSeconds > 0f)
            {
                interval = Mathf.Max(0.5f, Mathf.Max(_bombDroneSpawnIntervalSeconds, basedOnSpawnIntervalSeconds * 1.15f));
            }
            if (_debugSpawnLogs)
            {
                Debug.Log($"[EnemySpawner_V2 BombDrone] Spawn interval={interval:0.###}s (base={_bombDroneSpawnIntervalSeconds:0.###}).");
            }

            for (int i = 0; i < count; i++)
            {
                if (!_isWaveActive || waveSession != _waveSessionId)
                {
                    if (_debugSpawnLogs)
                    {
                        Debug.LogWarning(
                            $"[EnemySpawner_V2 BombDrone] Routine aborted at index={i}: " +
                            $"isWaveActive={_isWaveActive}, waveSession={waveSession}, currentSession={_waveSessionId}.");
                    }
                    yield break;
                }

                if (_debugSpawnLogs)
                {
                    Debug.Log($"[EnemySpawner_V2 BombDrone] Spawn attempt {i + 1}/{count}.");
                }
                SpawnOneBombDrone(i);
                if (i < count - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }

            if (_debugSpawnLogs)
            {
                Debug.Log($"[EnemySpawner_V2 BombDrone] Routine completed: spawned attempts={count}.");
            }
        }

        private int ResolveParatroopersPerFlight(int remainingParatroopersInWave)
        {
            if (remainingParatroopersInWave <= 0)
            {
                return 0;
            }

            int minDrops = Mathf.Max(1, _minParatroopersPerFlight);
            int maxDrops = Mathf.Max(minDrops, _maxParatroopersPerFlight);
            if (_capParatroopersPerFlightInEarlyWaves &&
                _waveNumberForDiagnostics > 0 &&
                _waveNumberForDiagnostics <= Mathf.Max(1, _earlyWaveCapMaxWaveInclusive))
            {
                maxDrops = Mathf.Min(maxDrops, Mathf.Max(1, _earlyWaveMaxParatroopersPerFlight));
                minDrops = Mathf.Min(minDrops, maxDrops);
            }

            if (_enableWave2PacingOverride && _waveNumberForDiagnostics == 2)
            {
                int wave2Cap = Mathf.Max(1, _wave2MaxParatroopersPerFlight);
                maxDrops = Mathf.Min(maxDrops, wave2Cap);
                minDrops = Mathf.Min(minDrops, maxDrops);
            }
            int randomized = UnityEngine.Random.Range(minDrops, maxDrops + 1);
            return Mathf.Clamp(randomized, 1, remainingParatroopersInWave);
        }

        private float GetRuntimeFlightSpawnIntervalSeconds()
        {
            float interval = Mathf.Max(0.05f, _runtimeSpawnIntervalSeconds);
            if (IsEarlyWaveStabilizationActive())
            {
                interval *= Mathf.Max(0.5f, _earlyWaveFlightSpawnIntervalMultiplier);
            }

            if (_enableWave2PacingOverride && _waveNumberForDiagnostics == 2)
            {
                interval *= Mathf.Max(0.5f, _wave2FlightSpawnIntervalMultiplier);
            }

            return interval;
        }

        private float GetRuntimeDropIntervalPerFlightSeconds()
        {
            float interval = Mathf.Max(0f, _paratrooperDropIntervalPerFlight);
            if (IsEarlyWaveStabilizationActive())
            {
                interval *= Mathf.Max(0.5f, _earlyWaveDropIntervalMultiplier);
            }

            if (_enableWave2PacingOverride && _waveNumberForDiagnostics == 2)
            {
                interval *= Mathf.Max(0.5f, _wave2DropIntervalMultiplier);
            }

            return interval;
        }

        private bool IsEarlyWaveStabilizationActive()
        {
            return _enableEarlyWaveStabilization &&
                   _waveNumberForDiagnostics > 0 &&
                   _waveNumberForDiagnostics <= Mathf.Max(1, _earlyWaveStabilizationMaxWaveInclusive);
        }

        private bool IsEarlyWaveSimultaneousCapReached()
        {
            if (!IsEarlyWaveStabilizationActive())
            {
                return false;
            }

            int cap = Mathf.Max(1, _earlyWaveMaxSimultaneousAliveParatroopers);
            return _trackedDeaths.Count >= cap;
        }

        private void SpawnOne(int spawnIndexInWave, int paratroopersThisFlight)
        {
            if (!_isWaveActive)
            {
                return;
            }

            PruneDestroyedAircraftFromTracking();

            bool usedAnchorSpawn;
            GameObject aircraft = null;

            Transform anchor = null;
            bool fromLeft;
            Vector3 rawWorldForLog;
            Vector3 aircraftWorldPos;
            Quaternion aircraftRotation;
            bool resolvedFrustumOrAnchor = false;

            if (TryGetSpawnAnchor(out anchor, out fromLeft))
            {
                resolvedFrustumOrAnchor = true;
                rawWorldForLog = anchor.position;
                aircraftWorldPos = AdjustAnchorSpawnWorldPosition(rawWorldForLog, fromLeft);
                aircraftRotation = anchor.rotation;
            }
            else if (TryComputeOffscreenFrustumSpawn(out fromLeft, out rawWorldForLog, out aircraftWorldPos))
            {
                resolvedFrustumOrAnchor = true;
                aircraftRotation = Quaternion.identity;
            }
            else
            {
                rawWorldForLog = Vector3.zero;
                aircraftWorldPos = Vector3.zero;
                aircraftRotation = Quaternion.identity;
                fromLeft = false;
            }

            if (resolvedFrustumOrAnchor)
            {
                usedAnchorSpawn = true;
                aircraftWorldPos.y += _helicopterSpawnYOffsetWorld;

                float stagger = Mathf.Max(0f, _aircraftSameWaveApproachAxisStagger);
                Vector3 staggerOffset = Vector3.zero;
                if (stagger > 0f && spawnIndexInWave > 0)
                {
                    Vector3 furtherOutside = fromLeft ? Vector3.left : Vector3.right;
                    staggerOffset = furtherOutside * (spawnIndexInWave * stagger);
                    aircraftWorldPos += staggerOffset;
                }

                if (_debugAnchorSpawnDiagnostics)
                {
                    LogSpawnAnchorResolution(
                        spawnIndexInWave,
                        anchor,
                        fromLeft,
                        rawWorldForLog,
                        aircraftWorldPos,
                        staggerOffset);
                }

                if (_helicopterPrefab != null)
                {
                    aircraft = Instantiate(_helicopterPrefab, aircraftWorldPos, aircraftRotation);
                    ApplyAircraftFacing(aircraft, fromLeft);
                    ApplyAircraftSpriteSorting(aircraft);
                    SetupHelicopterSpineRuntime(aircraft);
                    _spawnedAircraftInstances.Add(aircraft);

                    if (_aircraftEnableHorizontalFlight && _aircraftHorizontalFlySpeed > 0f)
                    {
                        AircraftFlyAcrossScreen_V2 flight = aircraft.GetComponent<AircraftFlyAcrossScreen_V2>();
                        if (flight == null)
                        {
                            flight = aircraft.AddComponent<AircraftFlyAcrossScreen_V2>();
                        }

                        flight.BeginFlight(
                            fromLeft,
                            _aircraftHorizontalFlySpeed,
                            _spawnCamera,
                            _aircraftFlyOffscreenMarginWorld,
                            _aircraftFlightMaxLifetimeSeconds,
                            _invertAircraftFlightDirectionX);
                    }
                    else if (_aircraftAutoDestroySeconds > 0f)
                    {
                        Destroy(aircraft, _aircraftAutoDestroySeconds);
                    }

                    BeginHelicopterStateFlow(aircraft);

                    if (_spawnParatrooperWhenAircraftIsVisible)
                    {
                        int dropCount = Mathf.Max(1, paratroopersThisFlight);
                        float dropInterval = GetRuntimeDropIntervalPerFlightSeconds();
                        StartCoroutine(SpawnParatrooperBurstWhenAircraftVisible(
                            aircraft,
                            usedAnchorSpawn,
                            fromLeft,
                            dropCount,
                            dropInterval));
                        return;
                    }

                    int dropNow = Mathf.Max(1, paratroopersThisFlight);
                    float dropDelay = GetRuntimeDropIntervalPerFlightSeconds();
                    for (int dropIndex = 0; dropIndex < dropNow; dropIndex++)
                    {
                        StartCoroutine(SpawnParatrooperFromAircraftAfterDelay(
                            aircraft,
                            usedAnchorSpawn,
                            fromLeft,
                            dropIndex * dropDelay));
                    }
                    return;
                }
                else
                {
                    int dropNow = Mathf.Max(1, paratroopersThisFlight);
                    for (int dropIndex = 0; dropIndex < dropNow; dropIndex++)
                    {
                        Vector3 paratrooperWorldPositionNow = aircraftWorldPos + _paratrooperOffsetFromMount;
                        paratrooperWorldPositionNow.z = _anchorSpawnWorldZ;
                        if (!SpawnParatrooper(paratrooperWorldPositionNow, usedAnchorSpawn, fromLeft, aircraft))
                        {
                            RegisterCancelledPlannedParatrooperDrop("direct-anchor-drop-spawn-failed");
                        }
                    }
                    return;
                }
            }
            else
            {
                usedAnchorSpawn = false;
                int dropNow = Mathf.Max(1, paratroopersThisFlight);
                for (int dropIndex = 0; dropIndex < dropNow; dropIndex++)
                {
                    Vector3 paratrooperWorldPosition = new Vector3(
                        UnityEngine.Random.Range(_spawnXRange.x, _spawnXRange.y),
                        UnityEngine.Random.Range(_spawnYRange.x, _spawnYRange.y),
                        0f);
                    paratrooperWorldPosition = ClampToCameraView(paratrooperWorldPosition);

                    if (_debugAnchorSpawnDiagnostics)
                    {
                        Debug.LogWarning(
                            "[EnemySpawner_V2] Spawn used FALLBACK random area (no anchors and frustum off-screen spawn disabled or no ortho camera). " +
                            $"Enable '{nameof(_useFrustumOffscreenSpawnWhenNoAnchors)}' or assign spawn transforms. " +
                            $"Final pos={paratrooperWorldPosition}, waveSpawnIndex={spawnIndexInWave}");
                    }

                    if (!SpawnParatrooper(paratrooperWorldPosition, usedAnchorSpawn, fromLeft, aircraft))
                    {
                        RegisterCancelledPlannedParatrooperDrop("fallback-random-drop-spawn-failed");
                    }
                }
                return;
            }
        }

        private static void SetupHelicopterSpineRuntime(GameObject aircraft)
        {
            if (aircraft == null)
            {
                return;
            }

            if (aircraft.GetComponent<Helicopter_V2>() == null)
            {
                aircraft.AddComponent<Helicopter_V2>();
            }
        }

        private static void BeginHelicopterStateFlow(GameObject aircraft)
        {
            if (aircraft == null)
            {
                return;
            }

            Helicopter_V2 helicopter = aircraft.GetComponent<Helicopter_V2>();
            if (helicopter == null)
            {
                return;
            }

            helicopter.InitializeForSpawn();
            helicopter.BeginFlight();
        }

        private void SpawnOneBomberPass(int spawnIndexInWave)
        {
            if (!_isWaveActive || _bomberPrefab == null)
            {
                return;
            }

            bool fromLeft;
            Vector3 rawWorldForLog;
            Vector3 aircraftWorldPos;
            Quaternion aircraftRotation;
            Transform anchor = null;

            if (TryGetSpawnAnchor(out anchor, out fromLeft))
            {
                rawWorldForLog = anchor.position;
                aircraftWorldPos = AdjustAnchorSpawnWorldPosition(rawWorldForLog, fromLeft);
                aircraftRotation = anchor.rotation;
            }
            else if (TryComputeOffscreenFrustumSpawn(out fromLeft, out rawWorldForLog, out aircraftWorldPos))
            {
                aircraftRotation = Quaternion.identity;
            }
            else
            {
                return;
            }

            // Bomb runs must start just past the active camera frustum. Same-wave stagger (meant for helicopters)
            // stacks huge +X offsets for right-side passes and pushes Heinkel-style planes far off-screen.
            SnapBombplaneSpawnToFrustumApproach(ref aircraftWorldPos, fromLeft);

            GameObject bomber = SimplePrefabPool_V2.Spawn(_bomberPrefab, aircraftWorldPos, aircraftRotation);
            if (bomber == null)
            {
                return;
            }

            ApplyAircraftFacing(bomber, fromLeft);
            ApplyAircraftSpriteSorting(bomber);
            _spawnedAircraftInstances.Add(bomber);

            Bombplane_V2 bombplane = bomber.GetComponent<Bombplane_V2>();
            // Bombplane_V2 moves itself in Update; adding AircraftFlyAcrossScreen_V2 doubles speed and can despawn
            // before the first bomb drop. Skip duplicate flight for bomber prefabs.
            if (_aircraftEnableHorizontalFlight && _aircraftHorizontalFlySpeed > 0f && bombplane == null)
            {
                AircraftFlyAcrossScreen_V2 flight = bomber.GetComponent<AircraftFlyAcrossScreen_V2>();
                if (flight == null)
                {
                    flight = bomber.AddComponent<AircraftFlyAcrossScreen_V2>();
                }

                flight.BeginFlight(
                    fromLeft,
                    _aircraftHorizontalFlySpeed,
                    _spawnCamera,
                    _aircraftFlyOffscreenMarginWorld,
                    _aircraftFlightMaxLifetimeSeconds,
                    _invertAircraftFlightDirectionX);
            }
            else if (_aircraftAutoDestroySeconds > 0f)
            {
                PooledAutoDespawn_V2 timer = bomber.GetComponent<PooledAutoDespawn_V2>();
                if (timer == null)
                {
                    timer = bomber.AddComponent<PooledAutoDespawn_V2>();
                }

                timer.Arm(_aircraftAutoDestroySeconds);
            }

            if (bombplane != null)
            {
                bombplane.BeginBombRun(fromLeft);
            }

            AircraftHealth_V2 aircraftHealth =
                bomber.GetComponent<AircraftHealth_V2>() ??
                bomber.GetComponentInChildren<AircraftHealth_V2>(true);
            if (aircraftHealth == null)
            {
                aircraftHealth = bomber.AddComponent<AircraftHealth_V2>();
            }

            if (_trackedAircraftDeaths.Add(aircraftHealth))
            {
                aircraftHealth.OnDestroyed += HandleTrackedAircraftDestroyed;
            }
        }

        private void SpawnOneKamikazeDrone(int spawnIndexInWave)
        {
            SpawnOneAirThreat(
                _kamikazeDronePrefabAsset,
                spawnIndexInWave,
                applyGenericHorizontalFlight: false,
                applyAutoDespawnTimer: false,
                applyApproachStagger: false,
                onSpawned: go =>
                {
                    SetupKamikazeDroneSpineRuntime(go);
                    BeginKamikazeDroneStateFlow(go);

                    EnemyKamikazeDrone_V2 drone = go.GetComponent<EnemyKamikazeDrone_V2>();
                    if (drone != null)
                    {
                        drone.BeginRun();
                    }
                });
        }

        private static void SetupKamikazeDroneSpineRuntime(GameObject droneRoot)
        {
            if (droneRoot == null)
            {
                return;
            }

            if (droneRoot.GetComponent<KamikazeDrone_V2>() == null)
            {
                droneRoot.AddComponent<KamikazeDrone_V2>();
            }
        }

        private static void BeginKamikazeDroneStateFlow(GameObject droneRoot)
        {
            if (droneRoot == null)
            {
                return;
            }

            KamikazeDrone_V2 drone = droneRoot.GetComponent<KamikazeDrone_V2>();
            if (drone == null)
            {
                return;
            }

            drone.InitializeForSpawn();
            drone.BeginFlight();
        }

        private void SpawnOneBombDrone(int spawnIndexInWave)
        {
            int beforeTrackedAircraft = _trackedAircraftDeaths.Count;
            int beforeActiveAirThreats = _spawnedAircraftInstances.Count;
            SpawnOneAirThreat(
                _bombDronePrefab,
                spawnIndexInWave,
                applyGenericHorizontalFlight: false,
                applyAutoDespawnTimer: false,
                applyApproachStagger: false,
                onSpawned: go =>
                {
                    EnemyBombDrone_V2 drone = go.GetComponent<EnemyBombDrone_V2>();
                    if (drone == null)
                    {
                        drone = go.AddComponent<EnemyBombDrone_V2>();
                        Debug.LogWarning(
                            "[EnemySpawner_V2] Bomb drone prefab missing EnemyBombDrone_V2; component added at runtime.");
                    }

                    if (drone != null)
                    {
                        drone.BeginRun();
                    }
                });

            if (_debugSpawnLogs)
            {
                bool spawnedSomething =
                    _trackedAircraftDeaths.Count > beforeTrackedAircraft ||
                    _spawnedAircraftInstances.Count > beforeActiveAirThreats;
                Debug.Log(
                    $"[EnemySpawner_V2 BombDrone] Spawn result index={spawnIndexInWave}: " +
                    $"spawned={spawnedSomething}, trackedAircraft={_trackedAircraftDeaths.Count}, " +
                    $"activeAirThreats={_spawnedAircraftInstances.Count}.");
            }
        }

        private void SpawnOneAirThreat(
            GameObject prefab,
            int spawnIndexInWave,
            bool applyGenericHorizontalFlight,
            bool applyAutoDespawnTimer,
            bool applyApproachStagger,
            Action<GameObject> onSpawned)
        {
            if (!_isWaveActive || prefab == null)
            {
                if (_debugSpawnLogs)
                {
                    Debug.LogWarning(
                        $"[EnemySpawner_V2 AirThreat] Spawn skipped: isWaveActive={_isWaveActive}, prefabNull={prefab == null}, index={spawnIndexInWave}.");
                }
                return;
            }

            bool fromLeft;
            Vector3 rawWorldForLog;
            Vector3 aircraftWorldPos;
            Quaternion aircraftRotation;
            Transform anchor = null;

            if (TryGetSpawnAnchor(out anchor, out fromLeft))
            {
                rawWorldForLog = anchor.position;
                aircraftWorldPos = AdjustAnchorSpawnWorldPosition(rawWorldForLog, fromLeft);
                aircraftRotation = Quaternion.identity;
            }
            else if (TryComputeOffscreenFrustumSpawn(out fromLeft, out rawWorldForLog, out aircraftWorldPos))
            {
                aircraftRotation = Quaternion.identity;
            }
            else
            {
                if (_debugSpawnLogs)
                {
                    Debug.LogWarning(
                        $"[EnemySpawner_V2 AirThreat] Spawn skipped: no anchor/frustum position resolved, index={spawnIndexInWave}.");
                }
                return;
            }

            float stagger = applyApproachStagger ? Mathf.Max(0f, _aircraftSameWaveApproachAxisStagger) : 0f;
            if (stagger > 0f && spawnIndexInWave > 0)
            {
                Vector3 furtherOutside = fromLeft ? Vector3.left : Vector3.right;
                aircraftWorldPos += furtherOutside * (spawnIndexInWave * stagger);
            }

            GameObject spawned = SimplePrefabPool_V2.Spawn(prefab, aircraftWorldPos, aircraftRotation);
            if (spawned == null)
            {
                if (_debugSpawnLogs)
                {
                    Debug.LogWarning(
                        $"[EnemySpawner_V2 AirThreat] Pool spawn returned null for '{prefab.name}', index={spawnIndexInWave}, pos={aircraftWorldPos}.");
                }
                return;
            }

            if (_debugSpawnLogs)
            {
                Debug.Log(
                    $"[EnemySpawner_V2 AirThreat] Spawned '{spawned.name}' from prefab '{prefab.name}' at {aircraftWorldPos}, index={spawnIndexInWave}.");
            }

            EnsureAirThreatTargetingComponents(spawned);
            ApplyAircraftFacing(spawned, fromLeft);
            ApplyAircraftSpriteSorting(spawned);
            EnsureAirThreatCollidersUseEnemyBodyPartLayer(spawned);
            _spawnedAircraftInstances.Add(spawned);
            onSpawned?.Invoke(spawned);

            if (applyGenericHorizontalFlight && _aircraftEnableHorizontalFlight && _aircraftHorizontalFlySpeed > 0f)
            {
                AircraftFlyAcrossScreen_V2 flight = spawned.GetComponent<AircraftFlyAcrossScreen_V2>();
                if (flight == null)
                {
                    flight = spawned.AddComponent<AircraftFlyAcrossScreen_V2>();
                }

                flight.BeginFlight(
                    fromLeft,
                    _aircraftHorizontalFlySpeed,
                    _spawnCamera,
                    _aircraftFlyOffscreenMarginWorld,
                    _aircraftFlightMaxLifetimeSeconds,
                    _invertAircraftFlightDirectionX);
            }
            else if (applyAutoDespawnTimer && _aircraftAutoDestroySeconds > 0f)
            {
                PooledAutoDespawn_V2 timer = spawned.GetComponent<PooledAutoDespawn_V2>();
                if (timer == null)
                {
                    timer = spawned.AddComponent<PooledAutoDespawn_V2>();
                }

                timer.Arm(_aircraftAutoDestroySeconds);
            }

            AircraftHealth_V2 aircraftHealth = spawned.GetComponent<AircraftHealth_V2>();
            if (aircraftHealth != null && _trackedAircraftDeaths.Add(aircraftHealth))
            {
                aircraftHealth.OnDestroyed += HandleTrackedAircraftDestroyed;
            }
        }

        private static void EnsureAirThreatTargetingComponents(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            // AutoHero/hero raycasts expect an aircraft-like damage receiver.
            if (root.GetComponentInChildren<AircraftHealth_V2>(true) == null)
            {
                root.AddComponent<AircraftHealth_V2>();
            }

            // Some drone prefabs are visual-only and miss colliders. Add a trigger collider so
            // overlap/raycast targeting can still pick the drone.
            Collider2D[] existing = root.GetComponentsInChildren<Collider2D>(true);
            if (existing != null && existing.Length > 0)
            {
                return;
            }

            CircleCollider2D cc = root.AddComponent<CircleCollider2D>();
            cc.radius = 0.65f;
            cc.isTrigger = true;
        }

        private static void EnsureAirThreatCollidersUseEnemyBodyPartLayer(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            int enemyBodyPartLayer = LayerMask.NameToLayer("EnemyBodyPart");
            if (enemyBodyPartLayer < 0)
            {
                return;
            }

            Collider2D[] cols = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                Collider2D col = cols[i];
                if (col == null)
                {
                    continue;
                }

                col.gameObject.layer = enemyBodyPartLayer;
            }
        }

        private bool SpawnParatrooper(
            Vector3 worldPosition,
            bool usedAnchorSpawn,
            bool fromLeft,
            GameObject aircraft)
        {
            _lastSpawnAttemptUnscaledTime = Time.unscaledTime;
            if (!_isWaveActive)
            {
                _failedParatrooperSpawnAttemptsThisWave++;
                _lastSpawnAbortReason = "spawn-attempt-while-wave-inactive";
                return false;
            }

            Paratrooper spawned = SimplePrefabPool_V2.Spawn(_paratrooperPrefab, worldPosition, Quaternion.identity);
            if (spawned == null)
            {
                _failedParatrooperSpawnAttemptsThisWave++;
                _lastSpawnAbortReason = "pool-spawn-returned-null";
                return false;
            }

            // Prefab root may be saved inactive (children still show as activeSelf in YAML but do not render).
            // Deferring Awake breaks InitializeDependencies / snap logic and leaves the unit invisible.
            if (!spawned.gameObject.activeSelf)
            {
                spawned.gameObject.SetActive(true);
            }

            spawned.PrepareForSpawn();

            int spawnSeq = ++_paratrooperDebugSpawnSeq;

            // Ensure each drop starts with deterministic physics and cannot inherit stray prefab velocity.
            Rigidbody2D spawnedRb = spawned.GetComponent<Rigidbody2D>();
            if (spawnedRb != null)
            {
                spawnedRb.linearVelocity = Vector2.zero;
                spawnedRb.angularVelocity = 0f;
            }

            if (aircraft != null)
            {
                IgnoreParatrooperCollisionsWithAircraft(spawned, aircraft);
            }

            if (usedAnchorSpawn)
            {
                ApplyParatrooperSpawnFacing(spawned.transform, fromLeft);
                // Awake() may flatten Spine local offset while prefab scale is still default; facing flips root X
                // scale afterward and mirrors the skeleton in world space unless we compensate again.
                spawned.ReconcileRootPositionAfterSpawnFacing();
            }

            // Awake visual sanitize / missing reconcile (e.g. SkeletonAnimation on root) can leave rigidbody root off
            // the requested drop while Spine is elsewhere — snap so gameplay matches the spawn resolution logs.
            spawned.SnapSpawnAlignmentToRequestedWorld(worldPosition);
            ApplyMasterDebugFlagsToParatrooper(spawned);

            if (_activeWaveConfig != null)
            {
                spawned.ApplyWaveDifficultyMultipliers(
                    _runtimeEnemyHealthMultiplier,
                    _runtimeEnemyDamageMultiplier);
            }

            _spawnedCount++;
            _lastParatrooperSpawnUnscaledTime = Time.unscaledTime;

            ParatrooperDeathHandler_V2 deathHandler = spawned.GetComponent<ParatrooperDeathHandler_V2>();
            if (deathHandler == null)
            {
                deathHandler = spawned.GetComponentInChildren<ParatrooperDeathHandler_V2>(true);
            }
            if (deathHandler != null && _trackedDeaths.Add(deathHandler))
            {
                deathHandler.OnDeathStarted += HandleTrackedEnemyDeath;
            }

            if (_debugSpawnLogs)
            {
                Debug.Log(
                    $"[EnemySpawner_V2] Spawned Paratrooper at {worldPosition} " +
                    $"(anchorSpawn={usedAnchorSpawn}, aircraft={(aircraft != null ? aircraft.name : "none")})");
            }

            if (_debugAnchorSpawnDiagnostics)
            {
                LogParatrooperSpawnSnapshot(
                    "spawn-now",
                    spawnSeq,
                    worldPosition,
                    spawned.transform.position,
                    spawnedRb,
                    aircraft,
                    fromLeft);
                StartCoroutine(LogParatrooperSpawnTrace(spawned, spawnSeq, worldPosition, fromLeft));
            }

            return true;
        }

        private IEnumerator RunMechRobotBossSpawnRoutine(WaveConfig_V2 config, int waveSession)
        {
            int n = config != null ? config.MechRobotBossCount : 0;
            if (n <= 0 || _mechRobotBossPrefab == null)
            {
                _mechBossScheduleComplete = true;
                yield break;
            }

            try
            {
                for (int i = 0; i < n; i++)
                {
                    if (!_isWaveActive || waveSession != _waveSessionId)
                    {
                        break;
                    }

                    if (SpawnMechRobotBoss())
                    {
                        if (i < n - 1)
                        {
                            yield return new WaitForSeconds(0.4f);
                        }
                    }
                }

                if (_mechBossSpawnedCount < n)
                {
                    Debug.LogWarning(
                        "[EnemySpawner_V2] Mech Robot Boss spawn shortfall: spawned=" + _mechBossSpawnedCount +
                        ", requested=" + n + ". Wave clear will use the spawned count.");
                    _mechBossTargetCount = _mechBossSpawnedCount;
                }
            }
            finally
            {
                _mechBossScheduleComplete = true;
            }
        }

        private bool SpawnMechRobotBoss()
        {
            if (!_isWaveActive || _mechRobotBossPrefab == null)
            {
                return false;
            }

            Vector3 worldPosition;
            if (_spawnMechBossFromOffscreenGround && TryComputeMechBossOffscreenGroundSpawn(out worldPosition))
            {
                // Intentionally off-screen on X; do not clamp into view.
            }
            else
            {
                worldPosition = new Vector3(
                    UnityEngine.Random.Range(Mathf.Min(_spawnXRange.x, _spawnXRange.y), Mathf.Max(_spawnXRange.x, _spawnXRange.y)),
                    UnityEngine.Random.Range(Mathf.Min(_spawnYRange.x, _spawnYRange.y), Mathf.Max(_spawnYRange.x, _spawnYRange.y)),
                    _anchorSpawnWorldZ);
                worldPosition = ClampToCameraView(worldPosition);
                worldPosition.x = Mathf.Abs(worldPosition.x) < 2.5f
                    ? worldPosition.x + 6f * (UnityEngine.Random.value < 0.5f ? -1f : 1f)
                    : worldPosition.x;
            }

            MechRobotBoss spawned = SimplePrefabPool_V2.Spawn(_mechRobotBossPrefab, worldPosition, Quaternion.identity);
            if (spawned == null)
            {
                return false;
            }

            if (!spawned.gameObject.activeSelf)
            {
                spawned.gameObject.SetActive(true);
            }

            spawned.PrepareForSpawn();

            Rigidbody2D rb = spawned.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = spawned.GetComponentInChildren<Rigidbody2D>(true);
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            if (_activeWaveConfig != null)
            {
                spawned.ApplyWaveDifficultyMultipliers(_runtimeEnemyHealthMultiplier, _runtimeEnemyDamageMultiplier);
            }

            _mechBossSpawnedCount++;

            MechRobotBossDeathHandler_V2 deathHandler = spawned.GetComponent<MechRobotBossDeathHandler_V2>();
            if (deathHandler == null)
            {
                deathHandler = spawned.GetComponentInChildren<MechRobotBossDeathHandler_V2>(true);
            }

            if (deathHandler != null && _trackedMechBossDeaths.Add(deathHandler))
            {
                deathHandler.OnDeathStarted += HandleTrackedMechBossDeath;
            }

            if (_debugSpawnLogs)
            {
                Debug.Log($"[EnemySpawner_V2] Spawned MechRobotBoss at {worldPosition}");
            }

            return true;
        }

        private void ApplyMasterDebugFlagsToParatrooper(Paratrooper spawned)
        {
            if (spawned == null || !_masterDebugGrenadeOnly)
            {
                return;
            }

            spawned.SetMasterDebugGrenadeOnly(true);

            if (_debugSpawnLogs || _debugAnchorSpawnDiagnostics)
            {
                Debug.Log("[EnemySpawner_V2] Applied master debug grenade-only to spawned paratrooper (controller+weapon forced).");
            }
        }

        private IEnumerator SpawnParatrooperWhenAircraftVisible(
            GameObject aircraft,
            bool usedAnchorSpawn,
            bool fromLeft,
            float initialDelaySeconds = 0f)
        {
            _pendingDelayedDropCoroutines++;
            try
            {
                if (initialDelaySeconds > 0f)
                {
                    yield return new WaitForSeconds(initialDelaySeconds);
                }

                if (aircraft == null)
                {
                    yield break;
                }

                int waveSession = _waveSessionId;
                float timeoutAt = Time.time + Mathf.Max(0.1f, _maxSecondsToWaitForVisibleAircraftDrop);
                Vector3 fallbackPosition = GetParatrooperSpawnPositionFromAircraft(aircraft);

                while (_isWaveActive && waveSession == _waveSessionId && aircraft != null && Time.time < timeoutAt)
                {
                    fallbackPosition = GetParatrooperSpawnPositionFromAircraft(aircraft);
                    float requiredInset = Mathf.Max(0f, _aircraftVisibleCheckPaddingWorld) + Mathf.Max(0f, _paratrooperDropSafetyMarginWorld);
                    bool mountInsideForDrop = IsWorldPositionInsideOrthographicCameraView(fallbackPosition, requiredInset);
                    if (mountInsideForDrop)
                    {
                        LogParatrooperDropDecision(
                            "visible",
                            aircraft.transform.position,
                            fallbackPosition,
                            fromLeft);
                        Vector3 safeDropPosition = ClampDropPositionInsideOrthographicCameraView(fallbackPosition, requiredInset);
                        if (!SpawnParatrooper(safeDropPosition, usedAnchorSpawn, fromLeft, aircraft))
                        {
                            RegisterCancelledPlannedParatrooperDrop("visible-drop-spawn-failed");
                        }
                        yield break;
                    }

                    yield return null;
                }

                if (!_isWaveActive || waveSession != _waveSessionId)
                {
                    yield break;
                }

                // Aircraft was destroyed before it ever reached a valid drop state.
                // Account for the lost planned drop so IsWaveCleared() does not wait forever.
                if (aircraft == null)
                {
                    RegisterCancelledPlannedParatrooperDrop("aircraft-destroyed-before-visible-drop");
                    if (_debugAnchorSpawnDiagnostics)
                    {
                        Debug.Log(
                            "[EnemySpawner_V2] Paratrooper drop cancelled: aircraft destroyed before drop could happen. " +
                            FormatWaveDiagSuffix());
                    }
                    yield break;
                }

                // Safety net so waves don't get stuck if visibility condition was never met.
                LogParatrooperDropDecision(
                    "timeout",
                    aircraft != null ? aircraft.transform.position : fallbackPosition,
                    fallbackPosition,
                    fromLeft);
                float timeoutInset = Mathf.Max(0f, _aircraftVisibleCheckPaddingWorld);
                Vector3 timeoutDropPosition = ClampDropPositionInsideOrthographicCameraView(fallbackPosition, timeoutInset);
                if (!SpawnParatrooper(timeoutDropPosition, usedAnchorSpawn, fromLeft, aircraft))
                {
                    RegisterCancelledPlannedParatrooperDrop("timeout-visible-drop-spawn-failed");
                }
            }
            finally
            {
                _pendingDelayedDropCoroutines = Mathf.Max(0, _pendingDelayedDropCoroutines - 1);
            }
        }

        private IEnumerator SpawnParatrooperBurstWhenAircraftVisible(
            GameObject aircraft,
            bool usedAnchorSpawn,
            bool fromLeft,
            int dropCount,
            float dropIntervalSeconds)
        {
            int remainingDrops = Mathf.Max(1, dropCount);
            float spacing = Mathf.Max(0f, dropIntervalSeconds);
            while (remainingDrops > 0)
            {
                if (!_isWaveActive)
                {
                    yield break;
                }

                if (aircraft == null)
                {
                    // Account for all still-pending planned drops from this flight.
                    for (int i = 0; i < remainingDrops; i++)
                    {
                        RegisterCancelledPlannedParatrooperDrop("aircraft-destroyed-before-visible-burst-drop");
                    }
                    yield break;
                }

                yield return SpawnParatrooperWhenAircraftVisible(
                    aircraft,
                    usedAnchorSpawn,
                    fromLeft,
                    0f);

                if (!_isWaveActive)
                {
                    yield break;
                }

                remainingDrops--;
                if (remainingDrops > 0 && spacing > 0f)
                {
                    yield return new WaitForSeconds(spacing);
                }
            }
        }

        private IEnumerator SpawnParatrooperFromAircraftAfterDelay(
            GameObject aircraft,
            bool usedAnchorSpawn,
            bool fromLeft,
            float initialDelaySeconds)
        {
            _pendingDelayedDropCoroutines++;
            try
            {
                if (initialDelaySeconds > 0f)
                {
                    yield return new WaitForSeconds(initialDelaySeconds);
                }

                if (!_isWaveActive || aircraft == null)
                {
                    if (_isWaveActive && aircraft == null)
                    {
                        RegisterCancelledPlannedParatrooperDrop("aircraft-missing-before-delayed-drop");
                    }
                    yield break;
                }

                Vector3 dropPos = GetParatrooperSpawnPositionFromAircraft(aircraft);
                if (!SpawnParatrooper(dropPos, usedAnchorSpawn, fromLeft, aircraft))
                {
                    RegisterCancelledPlannedParatrooperDrop("delayed-aircraft-drop-spawn-failed");
                }
            }
            finally
            {
                _pendingDelayedDropCoroutines = Mathf.Max(0, _pendingDelayedDropCoroutines - 1);
            }
        }

        /// <summary>
        /// Prevents wave soft-lock when a scheduled drop is lost (e.g. aircraft destroyed before delayed release).
        /// </summary>
        private void RegisterCancelledPlannedParatrooperDrop(string reason)
        {
            int before = _targetSpawnCount;
            _targetSpawnCount = Mathf.Max(0, _targetSpawnCount - 1);
            _lastSpawnAbortReason = reason ?? "cancelled-planned-drop";
            if (_debugSpawnLogs || _debugAnchorSpawnDiagnostics)
            {
                Debug.Log(
                    $"[EnemySpawner_V2] Planned drop cancelled ({reason}) -> targetSpawn {before} -> {_targetSpawnCount}. " +
                    FormatWaveDiagSuffix());
            }
        }

        private Vector3 GetParatrooperSpawnPositionFromAircraft(GameObject aircraft)
        {
            if (aircraft == null)
            {
                return Vector3.zero;
            }

            string mountSourceLabel;
            Vector3 p;
            if (TryResolveParatrooperSpineBoneWorldPosition(aircraft, out Vector3 spineBoneWorld))
            {
                _lastResolvedMountUsedFallbackRoot = false;
                mountSourceLabel = $"SpineBone:{_paratrooperSpawnBoneName}";
                p = spineBoneWorld + _paratrooperOffsetFromSpineBone;
            }
            else
            {
                Transform mount = ResolveParatrooperMountTransform(aircraft);
                mountSourceLabel = mount != null ? mount.name : "(none)";
                p = mount != null ? mount.position + _paratrooperOffsetFromMount : aircraft.transform.position + _paratrooperOffsetFromMount;
            }

            if (_lastResolvedMountUsedFallbackRoot && _paratrooperOffsetFromMount == Vector3.zero)
            {
                // If no dedicated door mount exists, place the drop point slightly below aircraft bounds
                // so the paratrooper does not immediately overlap and collide with the aircraft body.
                p = GetFallbackDropPositionBelowAircraft(aircraft, p);
            }

            if (_forceDropBelowAircraftWhenMountInsideBounds &&
                TryGetAircraftBounds(aircraft, out Bounds aircraftBounds) &&
                aircraftBounds.Contains(p))
            {
                float margin = Mathf.Max(0.01f, _forceDropBelowAircraftExtraMargin);
                p.y = aircraftBounds.min.y - margin;
            }

            p.z = _anchorSpawnWorldZ;

            if (_debugAnchorSpawnDiagnostics)
            {
                Debug.Log(
                    "[EnemySpawner_V2] Paratrooper mount sample\n" +
                    FormatWaveDiagLine() +
                    $"  aircraft='{aircraft.name}' aircraftPos={aircraft.transform.position}\n" +
                    $"  mountName='{mountSourceLabel}'\n" +
                    $"  offset={_paratrooperOffsetFromMount} finalDropPos={p}");
            }

            return p;
        }

        private bool TryResolveParatrooperSpineBoneWorldPosition(GameObject aircraftInstance, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (aircraftInstance == null || string.IsNullOrWhiteSpace(_paratrooperSpawnBoneName))
            {
                return false;
            }

            Spine.Unity.SkeletonAnimation[] skeletonAnimations =
                aircraftInstance.GetComponentsInChildren<Spine.Unity.SkeletonAnimation>(true);
            if (skeletonAnimations == null || skeletonAnimations.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < skeletonAnimations.Length; i++)
            {
                Spine.Unity.SkeletonAnimation skeletonAnimation = skeletonAnimations[i];
                if (skeletonAnimation == null || skeletonAnimation.Skeleton == null)
                {
                    continue;
                }

                var bone = skeletonAnimation.Skeleton.FindBone(_paratrooperSpawnBoneName);
                if (bone == null)
                {
                    continue;
                }

                worldPosition = skeletonAnimation.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0f));
                return true;
            }

            if (_debugAnchorSpawnDiagnostics && !_loggedMissingParatrooperSpawnBoneOnce)
            {
                _loggedMissingParatrooperSpawnBoneOnce = true;
                Debug.LogWarning(
                    "[EnemySpawner_V2] Paratrooper Spine spawn bone was not found on aircraft. " +
                    $"Expected bone '{_paratrooperSpawnBoneName}'. Falling back to mount child/root.");
            }

            return false;
        }

        private IEnumerator LogParatrooperSpawnTrace(
            Paratrooper spawned,
            int spawnSeq,
            Vector3 requestedSpawnPos,
            bool fromLeft)
        {
            const int maxSamples = 12;
            const float interval = 0.15f;

            int sampleIndex = 0;
            while (sampleIndex < maxSamples && spawned != null)
            {
                yield return new WaitForSeconds(interval);
                sampleIndex++;

                if (spawned == null)
                {
                    yield break;
                }

                Rigidbody2D rb = spawned.GetComponent<Rigidbody2D>();
                ParatrooperStateMachine_V2 sm = spawned.GetComponent<ParatrooperStateMachine_V2>();
                string stateLabel = sm != null ? sm.CurrentState.ToString() : "n/a";
                LogParatrooperSpawnSnapshot(
                    $"trace-{sampleIndex}",
                    spawnSeq,
                    requestedSpawnPos,
                    spawned.transform.position,
                    rb,
                    null,
                    fromLeft,
                    stateLabel);
            }
        }

        private void LogParatrooperSpawnSnapshot(
            string phase,
            int spawnSeq,
            Vector3 requestedSpawnPos,
            Vector3 actualTransformPos,
            Rigidbody2D rb,
            GameObject aircraft,
            bool fromLeft,
            string stateLabel = "n/a")
        {
            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            string camInfo = "missing-or-non-orthographic";
            bool reqInside = true;
            bool actualInside = true;
            bool rbInside = true;

            if (cam != null && cam.orthographic)
            {
                float halfHeight = cam.orthographicSize;
                float halfWidth = halfHeight * cam.aspect;
                Vector3 c = cam.transform.position;
                float inset = GetClampedVisibilityInset(_aircraftVisibleCheckPaddingWorld, halfWidth, halfHeight);
                float minX = c.x - halfWidth + inset;
                float maxX = c.x + halfWidth - inset;
                float minY = c.y - halfHeight + inset;
                float maxY = c.y + halfHeight - inset;

                reqInside = requestedSpawnPos.x >= minX && requestedSpawnPos.x <= maxX
                    && requestedSpawnPos.y >= minY && requestedSpawnPos.y <= maxY;
                actualInside = actualTransformPos.x >= minX && actualTransformPos.x <= maxX
                    && actualTransformPos.y >= minY && actualTransformPos.y <= maxY;
                Vector2 rbPos = rb != null ? rb.position : (Vector2)actualTransformPos;
                rbInside = rbPos.x >= minX && rbPos.x <= maxX
                    && rbPos.y >= minY && rbPos.y <= maxY;

                camInfo =
                    $"cam='{cam.name}' pos={c} inset={inset:0.###} " +
                    $"rectX=[{minX:0.###}..{maxX:0.###}] rectY=[{minY:0.###}..{maxY:0.###}]";
            }

            Vector2 rbPosition = rb != null ? rb.position : (Vector2)actualTransformPos;
            Vector2 rbVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
            string aircraftInfo = aircraft != null ? $"aircraft='{aircraft.name}' aircraftPos={aircraft.transform.position}" : "aircraft=(none)";

            // Noisy when _debugAnchorSpawnDiagnostics + spawn trace runs; uncomment to inspect spawn snapshots.
            // Debug.Log(
            //     "[EnemySpawner_V2] Paratrooper spawn snapshot\n" +
            //     FormatWaveDiagLine() +
            //     $"  id={spawnSeq} phase={phase} side={(fromLeft ? "LEFT" : "RIGHT")} state={stateLabel}\n" +
            //     $"  requestedPos={requestedSpawnPos} inside={reqInside}\n" +
            //     $"  actualTransformPos={actualTransformPos} inside={actualInside}\n" +
            //     $"  rbPos={rbPosition} inside={rbInside} rbVel={rbVelocity}\n" +
            //     $"  {aircraftInfo}\n" +
            //     $"  {camInfo}");
        }

        private bool IsWorldPositionInsideOrthographicCameraView(Vector3 worldPosition, float padding)
        {
            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return true;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;
            float inset = GetClampedVisibilityInset(padding, halfWidth, halfHeight);

            float minX = camPos.x - halfWidth + inset;
            float maxX = camPos.x + halfWidth - inset;
            float minY = camPos.y - halfHeight + inset;
            float maxY = camPos.y + halfHeight - inset;

            return worldPosition.x >= minX && worldPosition.x <= maxX
                && worldPosition.y >= minY && worldPosition.y <= maxY;
        }

        private Vector3 ClampDropPositionInsideOrthographicCameraView(Vector3 worldPosition, float padding)
        {
            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return worldPosition;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;
            float inset = GetClampedVisibilityInset(padding, halfWidth, halfHeight);

            float minX = camPos.x - halfWidth + inset;
            float maxX = camPos.x + halfWidth - inset;
            float minY = camPos.y - halfHeight + inset;
            float maxY = camPos.y + halfHeight - inset;

            worldPosition.x = Mathf.Clamp(worldPosition.x, minX, maxX);
            worldPosition.y = Mathf.Clamp(worldPosition.y, minY, maxY);
            return worldPosition;
        }

        private static float GetClampedVisibilityInset(float rawInset, float halfWidth, float halfHeight)
        {
            float limit = Mathf.Max(0f, Mathf.Min(halfWidth, halfHeight) - 0.01f);
            return Mathf.Min(Mathf.Max(0f, rawInset), limit);
        }

        private string FormatWaveNumberLabel()
        {
            return _waveNumberForDiagnostics > 0 ? _waveNumberForDiagnostics.ToString() : "(unknown)";
        }

        /// <summary>Indented line for multi-line debug blocks (mount sample, spawn snapshot, drop decision).</summary>
        private string FormatWaveDiagLine()
        {
            return
                $"  wave={FormatWaveNumberLabel()} targetSpawn={_targetSpawnCount} spawned={_spawnedCount} " +
                $"pendingDrops={_pendingDelayedDropCoroutines} trackedLiving={_trackedDeaths.Count} session={_waveSessionId}\n";
        }

        /// <summary>Single-line suffix for short log messages.</summary>
        private string FormatWaveDiagSuffix()
        {
            return
                $"wave={FormatWaveNumberLabel()} targetSpawn={_targetSpawnCount} spawned={_spawnedCount} " +
                $"pendingDrops={_pendingDelayedDropCoroutines} trackedLiving={_trackedDeaths.Count} session={_waveSessionId}";
        }

        private void LogParatrooperDropDecision(string reason, Vector3 aircraftPos, Vector3 mountDropPos, bool fromLeft)
        {
            if (!_debugAnchorSpawnDiagnostics)
            {
                return;
            }

            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                Debug.Log(
                    "[EnemySpawner_V2] Paratrooper drop decision\n" +
                    FormatWaveDiagLine() +
                    $"  reason={reason}, side={(fromLeft ? "LEFT" : "RIGHT")}\n" +
                    $"  aircraftPos={aircraftPos}, mountDropPos={mountDropPos}\n" +
                    "  cam=(missing or non-orthographic)");
                return;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 c = cam.transform.position;
            float inset = GetClampedVisibilityInset(_aircraftVisibleCheckPaddingWorld, halfWidth, halfHeight);
            float minX = c.x - halfWidth + inset;
            float maxX = c.x + halfWidth - inset;
            float minY = c.y - halfHeight + inset;
            float maxY = c.y + halfHeight - inset;

            bool aircraftInside =
                aircraftPos.x >= minX && aircraftPos.x <= maxX &&
                aircraftPos.y >= minY && aircraftPos.y <= maxY;

            bool mountInside =
                mountDropPos.x >= minX && mountDropPos.x <= maxX &&
                mountDropPos.y >= minY && mountDropPos.y <= maxY;

            Debug.Log(
                "[EnemySpawner_V2] Paratrooper drop decision\n" +
                FormatWaveDiagLine() +
                $"  reason={reason}, side={(fromLeft ? "LEFT" : "RIGHT")}\n" +
                $"  cam='{cam.name}' camPos={c} orthoSize={halfHeight:0.###} halfWidth={halfWidth:0.###} visibleInset={inset:0.###}\n" +
                $"  visibleRectX=[{minX:0.###}..{maxX:0.###}] visibleRectY=[{minY:0.###}..{maxY:0.###}]\n" +
                $"  aircraftPos={aircraftPos} inside={aircraftInside}\n" +
                $"  mountDropPos={mountDropPos} inside={mountInside}");
        }

        private void HandleTrackedEnemyDeath(ParatrooperDeathHandler_V2 deathHandler)
        {
            if (deathHandler != null)
            {
                deathHandler.OnDeathStarted -= HandleTrackedEnemyDeath;
                _trackedDeaths.Remove(deathHandler);
            }

            _onEnemyKilled?.Invoke();
        }

        private void HandleTrackedMechBossDeath(MechRobotBossDeathHandler_V2 deathHandler)
        {
            if (deathHandler != null)
            {
                deathHandler.OnDeathStarted -= HandleTrackedMechBossDeath;
                _trackedMechBossDeaths.Remove(deathHandler);
            }

            _onEnemyKilled?.Invoke();
        }

        private void HandleTrackedAircraftDestroyed(AircraftHealth_V2 aircraftHealth)
        {
            if (aircraftHealth != null)
            {
                aircraftHealth.OnDestroyed -= HandleTrackedAircraftDestroyed;
                _trackedAircraftDeaths.Remove(aircraftHealth);
            }

            _onEnemyKilled?.Invoke();
        }

        public bool IsWaveCleared()
        {
            PruneInactiveTrackedDeaths();
            PruneDestroyedAircraftFromTracking();

            // BeginWave failed (null prefab / config) leaves target 0 and no active wave config.
            // Valid bomber-only waves intentionally use target 0 helicopter drops.
            if (_targetSpawnCount < 0 || (!_spawnRoutineFinished && _targetSpawnCount == 0))
            {
                return false;
            }

            // Require BOTH: spawn coroutine finished AND every scheduled drop produced a paratrooper.
            // Old logic used (_spawnRoutineFinished || _spawnedCount >= target), which was true when
            // target==0 (0 >= 0) and when the routine finished before delayed drops spawned anyone.
            bool allSpawned = _spawnRoutineFinished && _spawnedCount >= _targetSpawnCount;
            PruneInactiveTrackedMechBossDeaths();
            bool mechBossCleared =
                _mechBossTargetCount <= 0 ||
                (_mechBossScheduleComplete &&
                 _mechBossSpawnedCount >= _mechBossTargetCount &&
                 _trackedMechBossDeaths.Count == 0);
            return allSpawned
                && mechBossCleared
                && _pendingDelayedDropCoroutines == 0
                && _activeAirThreatSpawnRoutines == 0
                && _trackedDeaths.Count == 0
                && _trackedAircraftDeaths.Count == 0
                && _spawnedAircraftInstances.Count == 0;
        }

        private void PruneInactiveTrackedMechBossDeaths()
        {
            if (_trackedMechBossDeaths.Count == 0)
            {
                return;
            }

            List<MechRobotBossDeathHandler_V2> stale = null;
            foreach (MechRobotBossDeathHandler_V2 h in _trackedMechBossDeaths)
            {
                if (h == null || !h.gameObject.activeInHierarchy)
                {
                    stale ??= new List<MechRobotBossDeathHandler_V2>();
                    stale.Add(h);
                }
            }

            if (stale == null)
            {
                return;
            }

            for (int i = 0; i < stale.Count; i++)
            {
                MechRobotBossDeathHandler_V2 h = stale[i];
                if (h != null)
                {
                    h.OnDeathStarted -= HandleTrackedMechBossDeath;
                }

                _trackedMechBossDeaths.Remove(h);
            }
        }

        private void PruneInactiveTrackedDeaths()
        {
            if (_trackedDeaths.Count > 0)
            {
                List<ParatrooperDeathHandler_V2> stale = null;
                foreach (ParatrooperDeathHandler_V2 deathHandler in _trackedDeaths)
                {
                    if (deathHandler == null || !deathHandler.gameObject.activeInHierarchy)
                    {
                        if (stale == null)
                        {
                            stale = new List<ParatrooperDeathHandler_V2>();
                        }

                        stale.Add(deathHandler);
                    }
                }

                if (stale != null)
                {
                    for (int i = 0; i < stale.Count; i++)
                    {
                        ParatrooperDeathHandler_V2 deathHandler = stale[i];
                        if (deathHandler != null)
                        {
                            deathHandler.OnDeathStarted -= HandleTrackedEnemyDeath;
                        }

                        _trackedDeaths.Remove(deathHandler);
                    }
                }
            }

            if (_trackedAircraftDeaths.Count > 0)
            {
                List<AircraftHealth_V2> staleAircraft = null;
                foreach (AircraftHealth_V2 aircraftHealth in _trackedAircraftDeaths)
                {
                    if (aircraftHealth == null || !aircraftHealth.gameObject.activeInHierarchy)
                    {
                        if (staleAircraft == null)
                        {
                            staleAircraft = new List<AircraftHealth_V2>();
                        }

                        staleAircraft.Add(aircraftHealth);
                    }
                }

                if (staleAircraft != null)
                {
                    for (int i = 0; i < staleAircraft.Count; i++)
                    {
                        AircraftHealth_V2 aircraftHealth = staleAircraft[i];
                        if (aircraftHealth != null)
                        {
                            aircraftHealth.OnDestroyed -= HandleTrackedAircraftDestroyed;
                        }

                        _trackedAircraftDeaths.Remove(aircraftHealth);
                    }
                }
            }
        }

        private bool TryGetSpawnAnchor(out Transform anchor, out bool fromLeft)
        {
            fromLeft = UnityEngine.Random.value < 0.5f;
            Transform[] candidates = fromLeft ? _leftSpawnPoints : _rightSpawnPoints;
            if (candidates != null && candidates.Length > 0)
            {
                int idx = UnityEngine.Random.Range(0, candidates.Length);
                if (candidates[idx] != null)
                {
                    anchor = candidates[idx];
                    return true;
                }
            }

            anchor = null;
            return false;
        }

        /// <summary>
        /// Spawns just past the orthographic frustum on X; Y from <see cref="_flyLaneVerticalNormalized01"/>.
        /// </summary>
        private bool TryComputeOffscreenFrustumSpawn(out bool fromLeft, out Vector3 rawWorldForLog, out Vector3 aircraftWorldPos)
        {
            fromLeft = false;
            rawWorldForLog = Vector3.zero;
            aircraftWorldPos = Vector3.zero;

            if (!_useFrustumOffscreenSpawnWhenNoAnchors)
            {
                return false;
            }

            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return false;
            }

            fromLeft = UnityEngine.Random.value < 0.5f;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float rawPad = Mathf.Max(0f, _orthographicFrustumInsetPadding);
            float pad = GetClampedFrustumPadding(rawPad, halfWidth, halfHeight);
            Vector3 camPos = cam.transform.position;

            float margin = Mathf.Max(0f, _offscreenBeyondFrustumHorizontalWorld);
            float visibleMinX = camPos.x - halfWidth;
            float visibleMaxX = camPos.x + halfWidth;
            float minY = camPos.y - halfHeight + pad;
            float maxY = camPos.y + halfHeight - pad;
            float yFromFrustum = Mathf.Lerp(minY, maxY, Mathf.Clamp01(_flyLaneVerticalNormalized01));
            float y = ResolveFlyLaneWorldY(cam, camPos, minY, maxY, yFromFrustum);

            float x = fromLeft ? visibleMinX - margin : visibleMaxX + margin;
            aircraftWorldPos = new Vector3(x, y, _anchorSpawnWorldZ);
            rawWorldForLog = aircraftWorldPos;
            return true;
        }

        /// <summary>
        /// Ground boss: place X just outside the orthographic frustum; Y from Ground raycast at that X (walk-in to hero).
        /// </summary>
        private bool TryComputeMechBossOffscreenGroundSpawn(out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return false;
            }

            bool fromLeft = UnityEngine.Random.value < 0.5f;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float rawPad = Mathf.Max(0f, _orthographicFrustumInsetPadding);
            float pad = GetClampedFrustumPadding(rawPad, halfWidth, halfHeight);
            Vector3 camPos = cam.transform.position;
            float margin =
                Mathf.Max(0f, _offscreenBeyondFrustumHorizontalWorld + Mathf.Max(0f, _mechBossExtraOffscreenHorizontalWorld));
            float visibleMinX = camPos.x - halfWidth;
            float visibleMaxX = camPos.x + halfWidth;
            float x = fromLeft ? visibleMinX - margin : visibleMaxX + margin;

            float minY = camPos.y - halfHeight + pad;
            float maxY = camPos.y + halfHeight - pad;
            int mask = ResolveGroundSurfaceProbeMask();
            if (mask != 0)
            {
                float topY = camPos.y + halfHeight - pad + Mathf.Max(0f, _groundProbeStartPaddingAboveFrustumTop);
                float probeDist = Mathf.Max(1f, _groundProbeMaxDistanceWorld);
                Vector2 originOffscreen = new Vector2(x, topY);
                RaycastHit2D hit = Physics2D.Raycast(originOffscreen, Vector2.down, probeDist, mask);
                // Off-screen X often has no colliders (level floor only under playfield). Reuse ground height from camera lane.
                if (hit.collider == null && TryProbeGroundSurfaceY(cam, camPos, mask, out float groundYAtCam))
                {
                    worldPosition = new Vector3(
                        x,
                        groundYAtCam + Mathf.Max(0f, _mechBossGroundYOffsetWorld),
                        _anchorSpawnWorldZ);
                    return true;
                }

                if (hit.collider != null)
                {
                    worldPosition = new Vector3(
                        x,
                        hit.point.y + Mathf.Max(0f, _mechBossGroundYOffsetWorld),
                        _anchorSpawnWorldZ);
                    return true;
                }
            }

            float yFallback = Mathf.Lerp(minY, maxY, Mathf.Clamp01(_mechBossSpawnVerticalNormalized01));
            worldPosition = new Vector3(x, yFallback, _anchorSpawnWorldZ);
            return true;
        }

        /// <summary>
        /// Forces horizontal spawn to match <see cref="TryComputeOffscreenFrustumSpawn"/> so bombplanes always
        /// approach from just outside the orthographic band (pins far away in the scene cannot strand passes off-view).
        /// </summary>
        private void SnapBombplaneSpawnToFrustumApproach(ref Vector3 aircraftWorldPos, bool fromLeft)
        {
            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float rawPad = Mathf.Max(0f, _orthographicFrustumInsetPadding);
            float pad = GetClampedFrustumPadding(rawPad, halfWidth, halfHeight);
            Vector3 camPos = cam.transform.position;

            float margin = Mathf.Max(0f, _offscreenBeyondFrustumHorizontalWorld);
            float visibleMinX = camPos.x - halfWidth;
            float visibleMaxX = camPos.x + halfWidth;
            float minY = camPos.y - halfHeight + pad;
            float maxY = camPos.y + halfHeight - pad;
            float spawnX = fromLeft ? visibleMinX - margin : visibleMaxX + margin;
            float y = Mathf.Clamp(aircraftWorldPos.y, minY, maxY);
            aircraftWorldPos = new Vector3(spawnX, y, _anchorSpawnWorldZ);
        }

        private int ResolveGroundSurfaceProbeMask()
        {
            if (_groundSurfaceProbeMask.value != 0)
            {
                return _groundSurfaceProbeMask.value;
            }

            int ground = LayerMask.NameToLayer("Ground");
            return ground >= 0 ? 1 << ground : 0;
        }

        private float ResolveFlyLaneWorldY(Camera cam, Vector3 camPos, float minY, float maxY, float yFromFrustum)
        {
            if (!_alignFlyLaneYToGroundSurface || cam == null)
            {
                return yFromFrustum;
            }

            int mask = ResolveGroundSurfaceProbeMask();
            if (mask == 0)
            {
                return yFromFrustum;
            }

            if (!TryProbeGroundSurfaceY(cam, camPos, mask, out float groundY))
            {
                return yFromFrustum;
            }

            float candidate = groundY + Mathf.Max(0f, _flyLaneHeightAboveGroundWorld);
            return Mathf.Clamp(candidate, minY, maxY);
        }

        private bool TryProbeGroundSurfaceY(Camera cam, Vector3 camPos, int mask, out float groundY)
        {
            groundY = 0f;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float rawPad = Mathf.Max(0f, _orthographicFrustumInsetPadding);
            float pad = GetClampedFrustumPadding(rawPad, halfWidth, halfHeight);
            float topY = camPos.y + halfHeight - pad + Mathf.Max(0f, _groundProbeStartPaddingAboveFrustumTop);
            Vector2 origin = new Vector2(camPos.x, topY);
            float dist = Mathf.Max(1f, _groundProbeMaxDistanceWorld);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, dist, mask);
            if (hit.collider == null)
            {
                return false;
            }

            groundY = hit.point.y;
            return true;
        }

        private void AutoBindSpawnAnchorsFromSceneIfMissing()
        {
            if (IsNullOrEmptyAnchorArray(_leftSpawnPoints))
            {
                Transform t = FindSceneTransformByName(_spawnPointLeftName);
                if (t != null)
                {
                    _leftSpawnPoints = new[] { t };
                    if (_debugSpawnLogs)
                    {
                        Debug.Log($"[EnemySpawner_V2] Auto-bound left spawn anchor '{t.name}'.");
                    }
                }
            }

            if (IsNullOrEmptyAnchorArray(_rightSpawnPoints))
            {
                Transform t = FindSceneTransformByName(_spawnPointRightName);
                if (t != null)
                {
                    _rightSpawnPoints = new[] { t };
                    if (_debugSpawnLogs)
                    {
                        Debug.Log($"[EnemySpawner_V2] Auto-bound right spawn anchor '{t.name}'.");
                    }
                }
            }
        }

        private static bool IsNullOrEmptyAnchorArray(Transform[] arr)
        {
            if (arr == null || arr.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static Transform FindSceneTransformByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                Transform tr = all[i];
                if (tr != null && tr.gameObject.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return tr;
                }
            }

            return null;
        }

        private Transform ResolveParatrooperMountTransform(GameObject aircraftInstance)
        {
            _lastResolvedMountUsedFallbackRoot = false;
            if (aircraftInstance == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(_paratrooperMountChildName))
            {
                Transform child = aircraftInstance.transform.Find(_paratrooperMountChildName);
                if (child != null)
                {
                    return child;
                }

                if (_debugAnchorSpawnDiagnostics && !_loggedMissingParatrooperMountOnce)
                {
                    _loggedMissingParatrooperMountOnce = true;
                    Debug.LogWarning(
                        "[EnemySpawner_V2] Paratrooper mount child was not found on aircraft. " +
                        $"Expected child name '{_paratrooperMountChildName}'. Falling back to aircraft root. " +
                        "Add an empty child at the helicopter door and use that name for accurate drop position.");
                }
            }

            _lastResolvedMountUsedFallbackRoot = true;
            return aircraftInstance.transform;
        }

        private Vector3 GetFallbackDropPositionBelowAircraft(GameObject aircraft, Vector3 fallback)
        {
            if (!TryGetAircraftBounds(aircraft, out Bounds bounds))
            {
                return fallback;
            }

            float extraBelow = 0.2f;
            return new Vector3(bounds.center.x, bounds.min.y - extraBelow, fallback.z);
        }

        private static bool TryGetAircraftBounds(GameObject aircraft, out Bounds bounds)
        {
            bounds = default;
            if (aircraft == null)
            {
                return false;
            }

            bool hasBounds = false;
            Collider2D[] colliders = aircraft.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D c = colliders[i];
                if (c == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            if (hasBounds)
            {
                return true;
            }

            SpriteRenderer[] renderers = aircraft.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = sr.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(sr.bounds);
                }
            }

            return hasBounds;
        }

        private static void IgnoreParatrooperCollisionsWithAircraft(Paratrooper paratrooper, GameObject aircraft)
        {
            if (paratrooper == null || aircraft == null)
            {
                return;
            }

            Collider2D[] paraColliders = paratrooper.GetComponentsInChildren<Collider2D>(true);
            Collider2D[] aircraftColliders = aircraft.GetComponentsInChildren<Collider2D>(true);
            if (paraColliders == null || aircraftColliders == null || paraColliders.Length == 0 || aircraftColliders.Length == 0)
            {
                return;
            }

            for (int i = 0; i < paraColliders.Length; i++)
            {
                Collider2D p = paraColliders[i];
                if (p == null)
                {
                    continue;
                }

                for (int j = 0; j < aircraftColliders.Length; j++)
                {
                    Collider2D a = aircraftColliders[j];
                    if (a == null)
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(p, a, true);
                }
            }
        }

        private void ApplyAircraftFacing(GameObject aircraft, bool spawnedFromLeftAnchor)
        {
            if (!_flipAircraftScaleXWhenFromRightSpawn || aircraft == null)
            {
                return;
            }

            Vector3 s = aircraft.transform.localScale;
            float baseDir = spawnedFromLeftAnchor ? 1f : -1f;
            float travelDirX = _invertAircraftFlightDirectionX ? -baseDir : baseDir;
            bool shouldFaceRight = travelDirX > 0f;
            bool usePositiveScaleX = shouldFaceRight == _aircraftSpriteFacesRightWhenScaleXPositive;
            s.x = usePositiveScaleX ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);

            aircraft.transform.localScale = s;
        }

        private void ApplyParatrooperSpawnFacing(Transform paratrooperRoot, bool spawnedFromLeftSide)
        {
            if (!_faceParatrooperTowardPlayfieldOnSpawn || paratrooperRoot == null)
            {
                return;
            }

            // From LEFT side -> face RIGHT toward center. From RIGHT side -> face LEFT toward center.
            bool shouldFaceRight = spawnedFromLeftSide;
            bool usePositiveScaleX = shouldFaceRight == _paratrooperFacesRightWhenScaleXPositive;

            Vector3 s = paratrooperRoot.localScale;
            s.x = usePositiveScaleX ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
            paratrooperRoot.localScale = s;
        }

        private void ApplyAircraftSpriteSorting(GameObject aircraft)
        {
            if (!_overrideAircraftSpriteSorting || aircraft == null)
            {
                return;
            }

            int? layerId = null;
            if (!string.IsNullOrWhiteSpace(_aircraftSortingLayerName))
            {
                int id = SortingLayer.NameToID(_aircraftSortingLayerName);
                if (id != 0)
                {
                    layerId = id;
                }
            }

            SpriteRenderer[] renderers = aircraft.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null)
                {
                    continue;
                }

                if (layerId.HasValue)
                {
                    sr.sortingLayerID = layerId.Value;
                }

                sr.sortingOrder = _aircraftSpriteSortingOrder;
            }
        }

        /// <summary>
        /// Maps a designer-placed anchor to a world position that stays on-screen for the active orthographic camera
        /// (e.g. while the hero follow script moves the camera).
        /// </summary>
        private Vector3 AdjustAnchorSpawnWorldPosition(Vector3 rawWorldPosition, bool fromLeft)
        {
            if (!_snapSpawnsToCameraViewEdges)
            {
                Vector3 p = rawWorldPosition;
                p.z = _anchorSpawnWorldZ;
                return p;
            }

            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                Vector3 p = rawWorldPosition;
                p.z = _anchorSpawnWorldZ;
                return p;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float rawPad = Mathf.Max(0f, _orthographicFrustumInsetPadding);
            float pad = GetClampedFrustumPadding(rawPad, halfWidth, halfHeight);
            if (pad < rawPad - 0.0001f && !_loggedFrustumPaddingClamp)
            {
                _loggedFrustumPaddingClamp = true;
                Debug.LogWarning(
                    "[EnemySpawner_V2] Orthographic Frustum Inset Padding is larger than the orthographic frustum allows " +
                    $"(padding={rawPad}, ortho halfH={halfHeight:0.###}, halfW={halfWidth:0.###}). " +
                    $"Using effective padding={pad:0.###} for snap. " +
                    "Lower padding or disable Snap Spawns To Camera View Edges to use pin positions.");
            }

            float inset = Mathf.Max(0f, _anchorExtraHorizontalInset);
            Vector3 camPos = cam.transform.position;

            float frustumMinX = camPos.x - halfWidth + pad;
            float frustumMaxX = camPos.x + halfWidth - pad;
            float minY = camPos.y - halfHeight + pad;
            float maxY = camPos.y + halfHeight - pad;

            float leftX = frustumMinX + inset;
            float rightX = frustumMaxX - inset;
            float x = fromLeft ? leftX : rightX;
            x = Mathf.Clamp(x, frustumMinX, frustumMaxX);
            float y = Mathf.Clamp(rawWorldPosition.y, minY, maxY);

            return new Vector3(x, y, _anchorSpawnWorldZ);
        }

        private void PruneDestroyedAircraftFromTracking()
        {
            for (int i = _spawnedAircraftInstances.Count - 1; i >= 0; i--)
            {
                GameObject go = _spawnedAircraftInstances[i];
                if (go == null || !go.activeInHierarchy)
                {
                    _spawnedAircraftInstances.RemoveAt(i);
                }
            }
        }

        private void DestroySpawnedAircraftNow()
        {
            for (int i = 0; i < _spawnedAircraftInstances.Count; i++)
            {
                GameObject go = _spawnedAircraftInstances[i];
                if (go != null)
                {
                    SimplePrefabPool_V2.Despawn(go);
                }
            }

            _spawnedAircraftInstances.Clear();
        }

        private Vector3 ClampToCameraView(Vector3 worldPosition)
        {
            if (!_clampSpawnInsideCameraView)
            {
                return worldPosition;
            }

            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return worldPosition;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float rawPad = Mathf.Max(0f, _orthographicFrustumInsetPadding);
            float pad = GetClampedFrustumPadding(rawPad, halfWidth, halfHeight);
            Vector3 camPos = cam.transform.position;

            float minX = camPos.x - halfWidth + pad;
            float maxX = camPos.x + halfWidth - pad;
            float minY = camPos.y - halfHeight + pad;
            float maxY = camPos.y + halfHeight - pad;

            worldPosition.x = Mathf.Clamp(worldPosition.x, minX, maxX);
            worldPosition.y = Mathf.Clamp(worldPosition.y, minY, maxY);
            return worldPosition;
        }

        /// <summary>
        /// Padding must be &lt; min(halfWidth, halfHeight) or the visible frustum in that axis collapses (min == max).
        /// </summary>
        private static float GetClampedFrustumPadding(float rawPad, float halfWidth, float halfHeight)
        {
            float limit = Mathf.Max(0f, Mathf.Min(halfWidth, halfHeight) - 0.01f);
            return Mathf.Min(Mathf.Max(0f, rawPad), limit);
        }

        private void LogAnchorSetupOnce()
        {
            string camLabel = _spawnCamera != null ? $"'{_spawnCamera.name}'" : "null (will use Camera.main at spawn time)";
            Debug.Log(
                "[EnemySpawner_V2] --- Anchor diagnostics (Awake) ---\n" +
                $"  autoBindByName={_autoBindSpawnPointsByName}, " +
                $"snapSpawnsToCameraViewEdges={_snapSpawnsToCameraViewEdges}, " +
                $"{nameof(_useFrustumOffscreenSpawnWhenNoAnchors)}={_useFrustumOffscreenSpawnWhenNoAnchors}\n" +
                $"  spawnCamera={camLabel}\n" +
                "  If snap is ON: anchor spawns clamp to the visible frustum edges (moving camera). " +
                "If anchors are empty: off-screen spawn uses frustum left/right + horizontal margin.\n" +
                $"  Optional auto-bind names: '{_spawnPointLeftName}', '{_spawnPointRightName}'");

            int leftNameCount = CountSceneTransformsWithName(_spawnPointLeftName);
            int rightNameCount = CountSceneTransformsWithName(_spawnPointRightName);
            if (leftNameCount > 1)
            {
                Debug.LogWarning(
                    $"[EnemySpawner_V2] Scene contains {leftNameCount} transforms named '{_spawnPointLeftName}' " +
                    "(auto-bind uses the first match).");
            }

            if (rightNameCount > 1)
            {
                Debug.LogWarning(
                    $"[EnemySpawner_V2] Scene contains {rightNameCount} transforms named '{_spawnPointRightName}' " +
                    "(auto-bind uses the first match).");
            }

            LogAnchorArray("LEFT", _leftSpawnPoints);
            LogAnchorArray("RIGHT", _rightSpawnPoints);
        }

        private void LogAnchorArray(string label, Transform[] arr)
        {
            if (arr == null || arr.Length == 0)
            {
                Debug.Log($"[EnemySpawner_V2] Anchor array {label}: (empty).");
                return;
            }

            for (int i = 0; i < arr.Length; i++)
            {
                Transform t = arr[i];
                if (t == null)
                {
                    Debug.LogWarning($"[EnemySpawner_V2] Anchor array {label}[{i}]: NULL reference.");
                }
                else
                {
                    Debug.Log(
                        $"[EnemySpawner_V2] Anchor array {label}[{i}]: '{t.name}' " +
                        $"worldPos={t.position} worldRot={t.rotation.eulerAngles} path={GetTransformHierarchyPath(t)}");
                }
            }
        }

        private void LogSpawnAnchorResolution(
            int spawnIndexInWave,
            Transform anchor,
            bool fromLeft,
            Vector3 rawAnchorWorld,
            Vector3 finalWorldPosAfterSnapAndStagger,
            Vector3 staggerOffset)
        {
            string path = anchor != null ? GetTransformHierarchyPath(anchor) : "(no pin — computed frustum)";
            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;

            string snapExplain = anchor != null
                ? DescribeCameraSnapForLog(cam, rawAnchorWorld, fromLeft)
                : DescribeFrustumOffscreenSpawnForLog(cam, rawAnchorWorld, fromLeft);

            Debug.Log(
                "[EnemySpawner_V2] --- Spawn resolution ---\n" +
                $"  waveSpawnIndex={spawnIndexInWave} side={(fromLeft ? "LEFT" : "RIGHT")}\n" +
                $"  anchor='{(anchor != null ? anchor.name : "(computed)")}' path={path}\n" +
                $"  rawAnchorWorld={rawAnchorWorld}\n" +
                $"  afterSnapAndStagger={finalWorldPosAfterSnapAndStagger} (staggerOffset={staggerOffset})\n" +
                $"  {snapExplain}");
        }

        private string DescribeFrustumOffscreenSpawnForLog(Camera cam, Vector3 computedWorld, bool fromLeft)
        {
            if (cam == null || !cam.orthographic)
            {
                return "computed off-screen spawn (no ortho camera at log time).";
            }

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            float margin = Mathf.Max(0f, _offscreenBeyondFrustumHorizontalWorld);
            return
                $"{nameof(_useFrustumOffscreenSpawnWhenNoAnchors)}: base X is past frustum " +
                $"{(fromLeft ? "LEFT" : "RIGHT")} by margin={margin:0.###} (visible X≈[{c.x - halfW:0.###}..{c.x + halfW:0.###}]), " +
                $"laneY={computedWorld.y:0.###} (normalized={_flyLaneVerticalNormalized01:0.##}).";
        }

        private string DescribeCameraSnapForLog(Camera cam, Vector3 rawWorld, bool fromLeft)
        {
            if (!_snapSpawnsToCameraViewEdges)
            {
                return "snap=OFF → raw pin X/Y (+ stagger), not clamped to frustum. " +
                       "Clamp Inside Camera View applies only to random fallback spawns.";
            }

            if (cam == null || !cam.orthographic)
            {
                return $"snap=ON but camera missing or not orthographic (cam={(cam != null ? cam.name : "null")}, " +
                       "ortho=" + (cam != null && cam.orthographic) + ") → using RAW anchor position.";
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float rawPad = Mathf.Max(0f, _orthographicFrustumInsetPadding);
            float pad = GetClampedFrustumPadding(rawPad, halfWidth, halfHeight);
            float inset = Mathf.Max(0f, _anchorExtraHorizontalInset);
            Vector3 camPos = cam.transform.position;

            float frustumMinX = camPos.x - halfWidth + pad;
            float frustumMaxX = camPos.x + halfWidth - pad;
            float minY = camPos.y - halfHeight + pad;
            float maxY = camPos.y + halfHeight - pad;
            float leftX = frustumMinX + inset;
            float rightX = frustumMaxX - inset;
            float chosenEdgeX = fromLeft ? leftX : rightX;

            string padNote = Mathf.Approximately(rawPad, pad)
                ? $"pad={pad:0.###}"
                : $"rawPad={rawPad:0.###} effectivePad={pad:0.###} (clamped so frustum is valid)";

            return
                $"snap=ON cam='{cam.name}' camPos={camPos} orthoSize={halfHeight:0.###} halfWidth={halfWidth:0.###}\n" +
                $"  frustumX=[{frustumMinX:0.###} .. {frustumMaxX:0.###}] frustumY=[{minY:0.###} .. {maxY:0.###}] {padNote} inset={inset}\n" +
                $"  chosenScreenEdgeX={chosenEdgeX:0.###} (pin had rawX={rawWorld.x:0.###}) " +
                $"clampedY from rawY={rawWorld.y:0.###}\n" +
                "  → Snap maps spawn to CAMERA EDGE (inside view), not pin world X. Set snap OFF for off-screen pins.";
        }

        private static int CountSceneTransformsWithName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return 0;
            }

            int count = 0;
            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                Transform tr = all[i];
                if (tr != null && tr.gameObject.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static string GetTransformHierarchyPath(Transform t)
        {
            if (t == null)
            {
                return "";
            }

            if (t.parent == null)
            {
                return "/" + t.name;
            }

            return GetTransformHierarchyPath(t.parent) + "/" + t.name;
        }
    }
}
