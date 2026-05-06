using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using iStick2War;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Regression integration runner: weapons × enemies with <see cref="CombatExpectation"/>,
    /// dual timeouts, console log + JSON/TSV export. Use a dedicated scene or drive from Play Mode tests.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatMatrixIntegrationTestRunner_V2 : MonoBehaviour
    {
        public const string SuiteLogPrefix = "[CombatMatrixIntegrationTest_V2]";

        [Header("Run")]
        [Tooltip("If on, Start() runs the matrix when you press Play. Turn off when the Play Mode test (or code) already calls RunMatrixAndExport — otherwise two runs spawn duplicate enemies.")]
        [SerializeField] private bool _runOnPlay;
        [SerializeField] private int _framesToSettleAfterSpawn = 4;
        [SerializeField] private float _settleMaxRealtime = 2f;
        [Tooltip(
            "Extra frames after switching/refilling the test weapon, before the enemy is spawned. " +
            "Keeps Spine/view in sync so each row fires the intended weapon and the next case does not look like it ends right after the previous kill.")]
        [SerializeField] private int _framesToSettleAfterWeaponSwitch = 2;

        [Header("References")]
        [SerializeField] private Hero_V2 _hero;
        [SerializeField] private HeroView_V2 _heroView;
        [SerializeField] private HeroInput_V2 _heroInput;
        [SerializeField] private Transform _enemySpawnPoint;
        [SerializeField] private Transform _heroStandPoint;

        [Header("Enemy prefabs")]
        [SerializeField] private List<CombatMatrixEnemyPrefabBinding> _enemyPrefabs = new List<CombatMatrixEnemyPrefabBinding>();

        [Header("Matrix")]
        [SerializeField] private List<CombatMatrixTestCase_V2> _testCases = new List<CombatMatrixTestCase_V2>();

        [Header("Matrix weapons (optional)")]
        [Tooltip(
            "Weapon definitions unlocked (added to loadout) once before the matrix runs. " +
            "If a WeaponType in the matrix is missing from the hero prefab's initial weapons, assign its definition here — otherwise you get FAIL_EQUIP.")]
        [SerializeField] private List<HeroWeaponDefinition_V2> _weaponDefinitionsToUnlock = new List<HeroWeaponDefinition_V2>();

        [Header("Optional")]
        [Tooltip(
            "If the hero has AutoHero_V2, it is disabled for the whole matrix run. " +
            "Otherwise TickBeforeHeroFrame forces bot driving and picks Thompson vs infantry, undoing each row's weapon.")]
        [SerializeField] private bool _disableAutoHeroDuringMatrixRun = true;
        [Tooltip(
            "Paratrooper can despawn when outside MainCamera / world bounds — common in minimal test scenes. " +
            "When true, spawned Paratrooper instances call DisableAutomationHarnessSafetyDespawns().")]
        [SerializeField] private bool _disableParatrooperSafetyDespawnOnSpawn = true;
        [Tooltip(
            "When true: spawned aircraft (Bombplane, fly-across helper, bomb/kamikaze drones) do not move or self-despawn — " +
            "stable targets for weapon matrix shots.")]
        [SerializeField] private bool _freezeAircraftAiForCombatMatrix = true;
        [SerializeField] private List<Behaviour> _disableWhileRunning = new List<Behaviour>();
        [SerializeField] private TMP_Text _logText;
        [SerializeField] private int _logTextMaxChars = 16000;
        [Tooltip("If null, writes under project folder ../TestResults relative to Assets.")]
        [SerializeField] private string _exportDirectory;

        private readonly List<CombatMatrixRowResult> _lastResults = new List<CombatMatrixRowResult>(32);
        private readonly StringBuilder _logBuilder = new StringBuilder(512);
        private bool _running;
        private bool _unityErrorThisCase;
        private GameObject _spawnedEnemy;
        private bool _committedFire;
        private bool _resolverHit;
        private bool _logHandlerAttached;
        private AutoHero_V2 _suppressedAutoHero;
        private bool _restoreAutoHeroEnabled;

        public bool LastRunAllPassed { get; private set; }

        public IReadOnlyList<CombatMatrixRowResult> LastResults => _lastResults;

        /// <summary>True when hero, input, view, spawn point and at least one prefab + one case exist.</summary>
        public bool IsReadyForAutomatedRun()
        {
            BindReferencesIfNeeded();
            return _hero != null &&
                   _heroView != null &&
                   _heroInput != null &&
                   _enemySpawnPoint != null &&
                   _testCases != null &&
                   _testCases.Count > 0 &&
                   _enemyPrefabs != null &&
                   _enemyPrefabs.Exists(b => b != null && b.prefab != null);
        }

        private void LogHandler(string condition, string stacktrace, LogType type)
        {
            if (!_running)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _unityErrorThisCase = true;
            }
        }

        private void Start()
        {
            if (_runOnPlay)
            {
                StartCoroutine(RunMatrixAndExportRoutine());
            }
        }

        /// <summary>Play Mode tests and external tools call this.</summary>
        public Coroutine RunMatrixAndExport()
        {
            if (_running)
            {
                Debug.LogWarning($"{SuiteLogPrefix} Skipping StartCoroutine: a matrix run is already in progress.");
                return null;
            }

            return StartCoroutine(RunMatrixAndExportRoutine());
        }

        public IEnumerator RunMatrixAndExportRoutine()
        {
            if (_running)
            {
                Debug.LogWarning($"{SuiteLogPrefix} Skipping duplicate matrix run (already in progress).");
                yield break;
            }

            _running = true;
            _lastResults.Clear();
            LastRunAllPassed = true;

            if (!_logHandlerAttached)
            {
                Application.logMessageReceived += LogHandler;
                _logHandlerAttached = true;
            }

            BindReferencesIfNeeded();
            SetBehavioursEnabled(_disableWhileRunning, false);

            if (!IsReadyForAutomatedRun())
            {
                Debug.LogWarning($"{SuiteLogPrefix} Not ready (missing references, prefabs, or cases). Skipping run.");
                LastRunAllPassed = false;
                _running = false;
                SetBehavioursEnabled(_disableWhileRunning, true);
                yield break;
            }

            EnsureMatrixWeaponsUnlocked();

            if (_disableAutoHeroDuringMatrixRun)
            {
                SuppressAutoHeroDuringMatrixIfPresent();
            }

            try
            {
                for (int i = 0; i < _testCases.Count; i++)
                {
                    CombatMatrixTestCase_V2 test = _testCases[i];
                    yield return RunSingleCaseRoutine(test);
                    CleanupAfterCase();
                }

                string exportStamp = UtcStamp();
                WriteResultsToTsv(exportStamp);
                WriteResultsToJson(exportStamp);

                Debug.Log($"{SuiteLogPrefix} Complete. allPassed={LastRunAllPassed}. Rows={_lastResults.Count}");
            }
            finally
            {
                SetBehavioursEnabled(_disableWhileRunning, true);
                RestoreAutoHeroAfterMatrixIfNeeded();
                _running = false;
            }
        }

        private void OnDestroy()
        {
            if (_logHandlerAttached)
            {
                Application.logMessageReceived -= LogHandler;
                _logHandlerAttached = false;
            }

            // Stopping/destroying this object can abort the coroutine without running try/finally.
            RestoreAutoHeroAfterMatrixIfNeeded();
        }

        private void BindReferencesIfNeeded()
        {
            if (_hero == null)
            {
                _hero = FindFirstObjectByType<Hero_V2>();
            }

            if (_hero != null)
            {
                if (_heroView == null)
                {
                    _heroView = _hero.GetComponent<HeroView_V2>();
                }

                if (_heroInput == null)
                {
                    _heroInput = _hero.GetComponent<HeroInput_V2>();
                }
            }
        }

        private static void SetBehavioursEnabled(List<Behaviour> behaviours, bool enabled)
        {
            if (behaviours == null)
            {
                return;
            }

            for (int i = 0; i < behaviours.Count; i++)
            {
                if (behaviours[i] != null)
                {
                    behaviours[i].enabled = enabled;
                }
            }
        }

        private IEnumerator RunSingleCaseRoutine(CombatMatrixTestCase_V2 test)
        {
            _unityErrorThisCase = false;
            _committedFire = false;
            _resolverHit = false;

            string weaponName = test.weapon.ToString();
            string enemyName = test.enemy.ToString();
            string expName = test.expectation.ToString();

            bool spawned = false;
            bool equipped = false;
            bool damaged = false;
            bool killed = false;
            float startHp = 0f;
            float timeToDamage = -1f;
            float timeToKill = -1f;
            float timer = 0f;
            string result = "FAIL_SETUP";

            HeroWeaponSystem_V2 ws = _hero != null ? _hero.WeaponSystem : null;

            try
            {
                if (ws != null)
                {
                    ws.OnCommittedAttack += OnCommittedAttack;
                }

                if (_hero == null || _heroView == null || _heroInput == null || _enemySpawnPoint == null)
                {
                    result = "FAIL_MISSING_REFERENCE";
                    FailCaseEarly(test, result);
                    yield break;
                }

                GameObject prefab = ResolvePrefab(test.enemy);
                if (prefab == null)
                {
                    result = "FAIL_NO_PREFAB";
                    FailCaseEarly(test, result);
                    yield break;
                }

                if (_heroStandPoint != null)
                {
                    _hero.transform.SetPositionAndRotation(
                        _heroStandPoint.position,
                        _heroStandPoint.rotation);
                }

                // Equip before spawning the enemy so each case shoots with the matrix weapon (not the previous row's),
                // and the hero model does not appear to swap weapons only after the new enemy appears.
                // Do not short-circuit on CurrentWeaponType alone: HeroModel_V2 can disagree with inventory after
                // programmatic switches; TrySwitchToWeaponType aligns inventory + model.
                equipped = _hero.TrySwitchToWeaponType(test.weapon);
                if (!equipped)
                {
                    result = "FAIL_EQUIP";
                    FailCaseEarly(test, result);
                    yield break;
                }

                ws?.TryRefillMagazineForWeaponType(test.weapon);
                yield return SettleAfterWeaponSwitchRoutine();
                _heroView?.RefreshWeaponVisualsForCurrentState();

                _spawnedEnemy = Instantiate(prefab, _enemySpawnPoint.position, _enemySpawnPoint.rotation);
                spawned = _spawnedEnemy != null;
                if (!spawned)
                {
                    result = "FAIL_SPAWN";
                    FailCaseEarly(test, result);
                    yield break;
                }

                // Instantiate(…, rotation) overwrites the prefab root's saved rotation. MechRobot V2 (and any
                // similar rig) keeps a non-identity root rotation so Spine faces the orthographic XY plane;
                // without it the mesh is edge-on to the camera and appears invisible.
                _spawnedEnemy.transform.SetPositionAndRotation(
                    _enemySpawnPoint.position,
                    _enemySpawnPoint.rotation * prefab.transform.localRotation);

                // PARATROOPER V2 prefab root is saved inactive; Instantiate keeps it inactive until enabled.
                _spawnedEnemy.SetActive(true);

                ApplyHarnessSpawnGuards(_spawnedEnemy);

                yield return SettleSpawnedEnemyRoutine();

                if (!TryReadEnemyHp(_spawnedEnemy, out startHp))
                {
                    result = "FAIL_NO_HP_COMPONENT";
                    FailCaseEarly(test, result);
                    yield break;
                }

                float damageDeadline = Mathf.Max(0.25f, test.damageTimeoutSec);
                float killDeadline = Mathf.Max(damageDeadline, test.killTimeoutSec);

                _heroInput.SetBotDriving(true);

                while (timer < killDeadline)
                {
                    Vector2 aim = GetAimWorldPoint(_spawnedEnemy);
                    _heroView.SetAutoAimWorldOverride(aim);
                    _heroInput.SetBotFrame(Vector2.zero, shootHeld: true, reloadPressed: false);

                    float hpNow = startHp;
                    TryReadEnemyHp(_spawnedEnemy, out hpNow);
                    if (!damaged && hpNow < startHp - 0.001f)
                    {
                        damaged = true;
                        timeToDamage = timer;
                    }

                    if (IsEnemyDefeated(_spawnedEnemy))
                    {
                        killed = true;
                        timeToKill = timer;
                        break;
                    }

                    if (test.expectation == CombatExpectation.MustDamage && damaged)
                    {
                        break;
                    }

                    if (test.expectation == CombatExpectation.ShouldNotDamage && timer >= damageDeadline)
                    {
                        break;
                    }

                    timer += Time.deltaTime;
                    yield return null;
                }

                bool hit = _resolverHit || damaged;
                result = EvaluateResult(test, damaged, killed, timer, damageDeadline, killDeadline);

                if (_unityErrorThisCase)
                {
                    result = "FAIL_UNITY_ERROR";
                    LastRunAllPassed = false;
                }
                else if (spawned &&
                         equipped &&
                         !_committedFire &&
                         result != "FAIL_UNEXPECTED_DAMAGE")
                {
                    result = "FAIL_NOT_FIRED";
                }

                if (result != "PASS" && result != "PASS_DAMAGE_ONLY")
                {
                    LastRunAllPassed = false;
                }

                LogTsvLine(weaponName, enemyName, expName, _committedFire, hit, damaged, killed, timeToDamage, timeToKill, result);
                PushRow(weaponName, enemyName, expName, _committedFire, hit, damaged, killed, timeToDamage, timeToKill, result);
            }
            finally
            {
                if (ws != null)
                {
                    ws.OnCommittedAttack -= OnCommittedAttack;
                }

                if (_heroInput != null)
                {
                    _heroInput.SetBotDriving(false);
                    _heroInput.SetBotFrame(Vector2.zero, false, false);
                }

                if (_heroView != null)
                {
                    _heroView.SetAutoAimWorldOverride(null);
                }
            }
        }

        private static string EvaluateResult(
            CombatMatrixTestCase_V2 test,
            bool damaged,
            bool killed,
            float timer,
            float damageDeadline,
            float killDeadline)
        {
            switch (test.expectation)
            {
                case CombatExpectation.ShouldNotDamage:
                    if (damaged)
                    {
                        return "FAIL_UNEXPECTED_DAMAGE";
                    }

                    return "PASS";

                case CombatExpectation.MustDamage:
                    if (!damaged)
                    {
                        return "FAIL_NO_DAMAGE";
                    }

                    return killed ? "PASS" : "PASS_DAMAGE_ONLY";

                case CombatExpectation.MustKill:
                    if (!damaged)
                    {
                        return "FAIL_NO_DAMAGE";
                    }

                    if (!killed)
                    {
                        return "FAIL_NOT_KILLED";
                    }

                    return "PASS";

                default:
                    return "FAIL_UNKNOWN_EXPECTATION";
            }
        }

        private void OnCommittedAttack(WeaponType type, bool isProjectile, bool didHit)
        {
            _committedFire = true;
            if (!isProjectile && didHit)
            {
                _resolverHit = true;
            }
        }

        private IEnumerator SettleAfterWeaponSwitchRoutine()
        {
            int n = Mathf.Max(0, _framesToSettleAfterWeaponSwitch);
            for (int i = 0; i < n; i++)
            {
                yield return null;
            }
        }

        private IEnumerator SettleSpawnedEnemyRoutine()
        {
            float waitUntil = Time.realtimeSinceStartup + Mathf.Max(0.1f, _settleMaxRealtime);
            for (int i = 0; i < _framesToSettleAfterSpawn && Time.realtimeSinceStartup < waitUntil; i++)
            {
                yield return null;
            }
        }

        private void CleanupAfterCase()
        {
            if (_spawnedEnemy != null)
            {
                Destroy(_spawnedEnemy);
                _spawnedEnemy = null;
            }
        }

        private void ApplyHarnessSpawnGuards(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (_disableParatrooperSafetyDespawnOnSpawn)
            {
                Paratrooper para = root.GetComponentInChildren<Paratrooper>(true);
                para?.DisableAutomationHarnessSafetyDespawns();
            }

            if (_freezeAircraftAiForCombatMatrix)
            {
                FreezeSpawnedAircraftForHarness(root);
            }
        }

        private static void FreezeSpawnedAircraftForHarness(GameObject root)
        {
            AircraftFlyAcrossScreen_V2[] flyers = root.GetComponentsInChildren<AircraftFlyAcrossScreen_V2>(true);
            for (int i = 0; i < flyers.Length; i++)
            {
                if (flyers[i] != null)
                {
                    flyers[i].FreezeForCombatMatrixHarness();
                }
            }

            Bombplane_V2[] bombPlanes = root.GetComponentsInChildren<Bombplane_V2>(true);
            for (int i = 0; i < bombPlanes.Length; i++)
            {
                bombPlanes[i]?.FreezeForCombatMatrixHarness();
            }

            EnemyBombDrone_V2[] bombDrones = root.GetComponentsInChildren<EnemyBombDrone_V2>(true);
            for (int i = 0; i < bombDrones.Length; i++)
            {
                bombDrones[i]?.FreezeForCombatMatrixHarness();
            }

            EnemyKamikazeDrone_V2[] kamis = root.GetComponentsInChildren<EnemyKamikazeDrone_V2>(true);
            for (int i = 0; i < kamis.Length; i++)
            {
                kamis[i]?.FreezeForCombatMatrixHarness();
            }
        }

        private GameObject ResolvePrefab(CombatMatrixEnemyKind kind)
        {
            if (_enemyPrefabs == null)
            {
                return null;
            }

            for (int i = 0; i < _enemyPrefabs.Count; i++)
            {
                CombatMatrixEnemyPrefabBinding b = _enemyPrefabs[i];
                if (b != null && b.kind == kind && b.prefab != null)
                {
                    return b.prefab;
                }
            }

            return null;
        }

        private void SuppressAutoHeroDuringMatrixIfPresent()
        {
            _suppressedAutoHero = null;
            _restoreAutoHeroEnabled = false;
            if (_hero == null)
            {
                return;
            }

            AutoHero_V2 auto = _hero.GetComponent<AutoHero_V2>();
            if (auto != null && auto.enabled)
            {
                _suppressedAutoHero = auto;
                _restoreAutoHeroEnabled = true;
                auto.enabled = false;
            }
        }

        private void RestoreAutoHeroAfterMatrixIfNeeded()
        {
            if (_suppressedAutoHero != null && _restoreAutoHeroEnabled)
            {
                _suppressedAutoHero.enabled = true;
            }

            _suppressedAutoHero = null;
            _restoreAutoHeroEnabled = false;
        }

        private void EnsureMatrixWeaponsUnlocked()
        {
            if (_hero == null || _weaponDefinitionsToUnlock == null || _weaponDefinitionsToUnlock.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _weaponDefinitionsToUnlock.Count; i++)
            {
                HeroWeaponDefinition_V2 def = _weaponDefinitionsToUnlock[i];
                if (def != null)
                {
                    _hero.UnlockWeapon(def, autoEquip: false);
                }
            }
        }

        private void FailCaseEarly(CombatMatrixTestCase_V2 test, string result)
        {
            string weaponName = test.weapon.ToString();
            string enemyName = test.enemy.ToString();
            string expName = test.expectation.ToString();
            LastRunAllPassed = false;
            LogTsvLine(weaponName, enemyName, expName, false, false, false, false, -1f, -1f, result);
            PushRow(weaponName, enemyName, expName, false, false, false, false, -1f, -1f, result);

            if (result == "FAIL_EQUIP")
            {
                Debug.LogWarning(
                    $"{SuiteLogPrefix} FAIL_EQUIP: '{weaponName}' is not in the hero loadout. " +
                    "Add it to the HERO prefab _initialWeapons, or assign the definition under " +
                    "CombatMatrixIntegrationTestRunner_V2 → Weapon Definitions To Unlock Before Run.");
            }

            if (result == "FAIL_NO_PREFAB")
            {
                Debug.LogWarning(
                    $"{SuiteLogPrefix} FAIL_NO_PREFAB: No prefab assigned for enemy kind '{enemyName}'. " +
                    "Open CombatMatrixIntegrationTest_V2 scene → CombatMatrixIntegrationTestRunner_V2 → Enemy Prefabs " +
                    $"and add a binding with kind {enemyName}.");
            }
        }

        private void PushRow(
            string weapon,
            string enemy,
            string expectation,
            bool fired,
            bool hit,
            bool damaged,
            bool killed,
            float timeToDamage,
            float timeToKill,
            string result)
        {
            _lastResults.Add(new CombatMatrixRowResult
            {
                weapon = weapon,
                enemy = enemy,
                expectation = expectation,
                fired = fired,
                hit = hit,
                damaged = damaged,
                killed = killed,
                timeToDamage = timeToDamage,
                timeToKill = timeToKill,
                result = result,
            });
        }

        private void LogTsvLine(
            string weapon,
            string enemy,
            string expectation,
            bool fired,
            bool hit,
            bool damaged,
            bool killed,
            float timeToDamage,
            float timeToKill,
            string result)
        {
            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}",
                weapon,
                enemy,
                expectation,
                fired.ToString().ToLowerInvariant(),
                hit.ToString().ToLowerInvariant(),
                damaged.ToString().ToLowerInvariant(),
                killed.ToString().ToLowerInvariant(),
                timeToDamage.ToString("0.###", CultureInfo.InvariantCulture),
                timeToKill.ToString("0.###", CultureInfo.InvariantCulture),
                result);
            Debug.Log($"{SuiteLogPrefix} {line}");

            if (_logText != null)
            {
                _logBuilder.Clear();
                _logBuilder.AppendLine(line);
                string prev = _logText.text ?? "";
                _logText.text = _logBuilder.Append(prev).ToString();
                if (_logText.text.Length > _logTextMaxChars)
                {
                    _logText.text = _logText.text.Substring(0, _logTextMaxChars);
                }
            }
        }

        private void WriteResultsToTsv(string stamp)
        {
            try
            {
                string dir = ResolveExportDir();
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"CombatMatrix_{stamp}.tsv");
                var sb = new StringBuilder();
                sb.AppendLine("weapon\tenemy\texpectation\tfired\thit\tdamaged\tkilled\ttimeToDamage\ttimeToKill\tresult");
                for (int i = 0; i < _lastResults.Count; i++)
                {
                    CombatMatrixRowResult r = _lastResults[i];
                    sb.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7:0.###}\t{8:0.###}\t{9}",
                        r.weapon,
                        r.enemy,
                        r.expectation,
                        r.fired.ToString().ToLowerInvariant(),
                        r.hit.ToString().ToLowerInvariant(),
                        r.damaged.ToString().ToLowerInvariant(),
                        r.killed.ToString().ToLowerInvariant(),
                        r.timeToDamage,
                        r.timeToKill,
                        r.result));
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                Debug.Log($"{SuiteLogPrefix} Wrote TSV: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{SuiteLogPrefix} TSV export failed: {e.Message}");
            }
        }

        private void WriteResultsToJson(string stamp)
        {
            try
            {
                string dir = ResolveExportDir();
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"CombatMatrix_{stamp}.json");
                var export = new CombatMatrixJsonExport
                {
                    timestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    allPassed = LastRunAllPassed,
                    rows = _lastResults.ToArray(),
                };
                string json = JsonUtility.ToJson(export, true);
                File.WriteAllText(path, json, Encoding.UTF8);
                Debug.Log($"{SuiteLogPrefix} Wrote JSON: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{SuiteLogPrefix} JSON export failed: {e.Message}");
            }
        }

        private string ResolveExportDir()
        {
            if (!string.IsNullOrWhiteSpace(_exportDirectory))
            {
                return _exportDirectory;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults", "CombatMatrix"));
        }

        private static string UtcStamp()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        }

        private static Vector2 GetAimWorldPoint(GameObject enemy)
        {
            if (enemy == null)
            {
                return Vector2.zero;
            }

            // Mech: first Collider2D in hierarchy is often a foot or small child — aim ends up beside the
            // visible hull (bazooka follows crosshair direction, so the miss is obvious). Match AutoHero:
            // use all MechRobotBossBodyPart_V2 colliders and merge AABBs to a center.
            MechRobotBossBodyPart_V2[] mechParts = enemy.GetComponentsInChildren<MechRobotBossBodyPart_V2>(true);
            if (mechParts != null && mechParts.Length > 0)
            {
                bool has = false;
                Bounds merged = default;
                for (int i = 0; i < mechParts.Length; i++)
                {
                    Collider2D col = mechParts[i].GetComponent<Collider2D>();
                    if (col == null || !col.enabled)
                    {
                        continue;
                    }

                    if (!has)
                    {
                        merged = col.bounds;
                        has = true;
                    }
                    else
                    {
                        merged.Encapsulate(col.bounds);
                    }
                }

                if (has)
                {
                    return merged.center;
                }
            }

            // Mech rig: root transform is not the visual center (Spine is offset under a -90° X root).
            // Repo prefab may omit hitboxes; aim at hip bone (see Assets/Animations/MechRobot/mech.json).
            if (enemy.GetComponentInChildren<MechRobotBossModel_V2>(true) != null)
            {
                Vector2? spineAim = TryGetMechBossAimFromSpineTorso(enemy);
                if (spineAim.HasValue)
                {
                    return spineAim.Value;
                }
            }

            Collider2D c = enemy.GetComponentInChildren<Collider2D>(true);
            if (c != null && c.enabled)
            {
                return c.bounds.center;
            }

            return enemy.transform.position;
        }

        private static Vector2? TryGetMechBossAimFromSpineTorso(GameObject enemy)
        {
            SkeletonAnimation skel = enemy.GetComponentInChildren<SkeletonAnimation>(true);
            if (skel == null)
            {
                return null;
            }

            skel.Initialize(false);
            if (skel.Skeleton == null)
            {
                return null;
            }

            Bone bone = skel.Skeleton.FindBone("hip") ?? skel.Skeleton.FindBone("root");
            if (bone == null)
            {
                return null;
            }

            Vector3 w = skel.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0f));
            return new Vector2(w.x, w.y);
        }

        private static bool TryReadEnemyHp(GameObject root, out float hp)
        {
            hp = 0f;
            if (root == null)
            {
                return false;
            }

            ParatrooperModel_V2 p = root.GetComponentInChildren<ParatrooperModel_V2>(true);
            if (p != null)
            {
                hp = p.health;
                return true;
            }

            MechRobotBossModel_V2 m = root.GetComponentInChildren<MechRobotBossModel_V2>(true);
            if (m != null)
            {
                hp = m.health;
                return true;
            }

            AircraftHealth_V2 a = root.GetComponentInChildren<AircraftHealth_V2>(true);
            if (a != null)
            {
                hp = a.CurrentHealth;
                return true;
            }

            return false;
        }

        private static bool IsEnemyDefeated(GameObject root)
        {
            if (root == null)
            {
                return true;
            }

            if (!root.activeInHierarchy)
            {
                return true;
            }

            ParatrooperModel_V2 p = root.GetComponentInChildren<ParatrooperModel_V2>(true);
            if (p != null)
            {
                return p.IsDead();
            }

            MechRobotBossModel_V2 m = root.GetComponentInChildren<MechRobotBossModel_V2>(true);
            if (m != null)
            {
                return m.IsDead();
            }

            AircraftHealth_V2 a = root.GetComponentInChildren<AircraftHealth_V2>(true);
            if (a != null)
            {
                return a.IsDefeated;
            }

            return false;
        }
    }
}
