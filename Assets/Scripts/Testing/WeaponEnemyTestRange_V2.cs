using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using iStick2War;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>Subset of hero weapons mapped to <see cref="WeaponType"/> for automated weapon-vs-enemy checks.</summary>
    public enum WeaponEnemyTestWeaponKind
    {
        Colt,
        Thompson,
        Bazooka,
        TeslaGun,
        Flamethrower,
    }

    /// <summary>Enemy archetype for the test range — assign matching prefab in <see cref="WeaponEnemyTestRange_V2"/>.</summary>
    public enum WeaponEnemyTestEnemyKind
    {
        Paratrooper,
        Helicopter,
        Bomber,
        KamikazeDrone,
        BombDrone,
        MechBoss,
    }

    [Serializable]
    public sealed class WeaponEnemyTestCase
    {
        public WeaponEnemyTestWeaponKind weapon = WeaponEnemyTestWeaponKind.Colt;
        public WeaponEnemyTestEnemyKind enemy = WeaponEnemyTestEnemyKind.Paratrooper;
        [Min(0.5f)] public float timeoutSec = 12f;
        [Tooltip("When false, PASS if damage was applied (e.g. boss chip damage).")]
        public bool expectKill = true;
    }

    [Serializable]
    public sealed class WeaponEnemyEnemyPrefabBinding
    {
        public WeaponEnemyTestEnemyKind kind;
        [Tooltip("Root prefab with ParatrooperModel_V2, AircraftHealth_V2, or MechRobotBossModel_V2.")]
        public GameObject prefab;
    }

    /// <summary>
    /// Play-mode test range: one weapon vs one spawned enemy, aim override + hold fire, assert damage/kill within timeout.
    /// Wire prefabs per <see cref="WeaponEnemyTestEnemyKind"/>; place hero and spawn anchors in a dedicated scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponEnemyTestRange_V2 : MonoBehaviour
    {
        [Header("Run")]
        [Tooltip("Leave off if another driver (e.g. Play Mode test) calls BeginTestRun — avoids duplicate spawns.")]
        [SerializeField] private bool _runOnPlay = true;
        [SerializeField] private int _framesToSettleAfterSpawn = 4;
        [SerializeField] private float _settleMaxRealtime = 2f;

        [Header("References")]
        [SerializeField] private Hero_V2 _hero;
        [SerializeField] private HeroView_V2 _heroView;
        [SerializeField] private HeroInput_V2 _heroInput;
        [SerializeField] private Transform _enemySpawnPoint;
        [SerializeField] private Transform _heroStandPoint;

        [Header("Enemy prefabs (one per kind used in cases)")]
        [SerializeField] private List<WeaponEnemyEnemyPrefabBinding> _enemyPrefabs = new List<WeaponEnemyEnemyPrefabBinding>();

        [Header("Cases")]
        [SerializeField] private List<WeaponEnemyTestCase> _testCases = new List<WeaponEnemyTestCase>();

        [Header("Optional — reduce interference")]
        [Tooltip("Paratrooper (V2) safety despawn (camera/bounds) in minimal test scenes.")]
        [SerializeField] private bool _disableParatrooperSafetyDespawnOnSpawn = true;
        [SerializeField] private List<Behaviour> _disableWhileRunning = new List<Behaviour>();

        [Header("Optional UI")]
        [SerializeField] private TMP_Text _logText;
        [SerializeField] private int _logTextMaxChars = 12000;

        private readonly StringBuilder _logBuilder = new StringBuilder(512);
        private bool _running;
        private bool _unityErrorThisCase;
        private GameObject _spawnedEnemy;
        private bool _committedFire;
        private bool _resolverHit;
        private bool _logHandlerAttached;

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
                StartCoroutine(RunAllTestsRoutine());
            }
        }

        /// <summary>Start the full matrix from code (e.g. external automation).</summary>
        public void BeginTestRun()
        {
            if (_running)
            {
                Debug.LogWarning("[WeaponEnemyTestRange_V2] Run already in progress.");
                return;
            }

            StartCoroutine(RunAllTestsRoutine());
        }

        private IEnumerator RunAllTestsRoutine()
        {
            if (_running)
            {
                Debug.LogWarning("[WeaponEnemyTestRange_V2] Skipping duplicate run (already in progress).");
                yield break;
            }

            _running = true;
            if (!_logHandlerAttached)
            {
                Application.logMessageReceived += LogHandler;
                _logHandlerAttached = true;
            }

            BindReferencesIfNeeded();
            SetBehavioursEnabled(_disableWhileRunning, false);

            for (int i = 0; i < _testCases.Count; i++)
            {
                WeaponEnemyTestCase test = _testCases[i];
                yield return RunSingleCaseRoutine(test);
                CleanupAfterCase();
            }

            SetBehavioursEnabled(_disableWhileRunning, true);
            _running = false;
            Debug.Log("[WeaponEnemyTestRange_V2] All tests completed.");
        }

        private void OnDestroy()
        {
            if (_logHandlerAttached)
            {
                Application.logMessageReceived -= LogHandler;
                _logHandlerAttached = false;
            }
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

        private IEnumerator RunSingleCaseRoutine(WeaponEnemyTestCase test)
        {
            _unityErrorThisCase = false;
            _committedFire = false;
            _resolverHit = false;
            WeaponType weaponType = MapWeapon(test.weapon);
            string weaponName = weaponType.ToString();
            string enemyName = test.enemy.ToString();

            bool spawned = false;
            bool equipped = false;
            bool killed = false;
            bool damaged = false;
            float startHp = 0f;
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
                    LogCaseEnd(weaponName, enemyName, false, equipped, false, damaged, killed, timer, result);
                    yield break;
                }

                GameObject prefab = ResolvePrefab(test.enemy);
                if (prefab == null)
                {
                    result = "FAIL_NO_PREFAB";
                    LogCaseEnd(weaponName, enemyName, false, equipped, false, damaged, killed, timer, result);
                    yield break;
                }

                if (_heroStandPoint != null)
                {
                    _hero.transform.SetPositionAndRotation(
                        _heroStandPoint.position,
                        _heroStandPoint.rotation);
                }

                _spawnedEnemy = Instantiate(prefab, _enemySpawnPoint.position, _enemySpawnPoint.rotation);
                spawned = _spawnedEnemy != null;
                if (!spawned)
                {
                    result = "FAIL_SPAWN";
                    LogCaseEnd(weaponName, enemyName, false, equipped, false, damaged, killed, timer, result);
                    yield break;
                }

                _spawnedEnemy.SetActive(true);

                if (_disableParatrooperSafetyDespawnOnSpawn && _spawnedEnemy != null)
                {
                    Paratrooper para = _spawnedEnemy.GetComponentInChildren<Paratrooper>(true);
                    para?.DisableAutomationHarnessSafetyDespawns();
                }

                yield return SettleSpawnedEnemyRoutine();

                bool already = _hero.CurrentWeaponType == weaponType;
                equipped = already || _hero.TrySwitchToWeaponType(weaponType);
                if (!equipped)
                {
                    result = "FAIL_EQUIP";
                    LogCaseEnd(weaponName, enemyName, spawned, false, false, damaged, killed, timer, result);
                    yield break;
                }

                ws.TryRefillMagazineForWeaponType(weaponType);

                if (!TryReadEnemyHp(_spawnedEnemy, out startHp))
                {
                    result = "FAIL_NO_HP_COMPONENT";
                    LogCaseEnd(weaponName, enemyName, spawned, equipped, false, damaged, killed, timer, result);
                    yield break;
                }

                _heroInput.SetBotDriving(true);

                float timeout = Mathf.Max(0.5f, test.timeoutSec);
                while (timer < timeout)
                {
                    Vector2 aim = GetAimWorldPoint(_spawnedEnemy);
                    _heroView.SetAutoAimWorldOverride(aim);
                    _heroInput.SetBotFrame(Vector2.zero, shootHeld: true, reloadPressed: false);

                    float hpNow = startHp;
                    TryReadEnemyHp(_spawnedEnemy, out hpNow);
                    if (hpNow < startHp - 0.001f)
                    {
                        damaged = true;
                    }

                    if (IsEnemyDefeated(_spawnedEnemy))
                    {
                        killed = true;
                        break;
                    }

                    timer += Time.deltaTime;
                    yield return null;
                }

                if (_unityErrorThisCase)
                {
                    result = "FAIL_UNITY_ERROR";
                }
                else if (!_committedFire)
                {
                    result = "FAIL_NOT_FIRED";
                }
                else if (!damaged)
                {
                    result = _resolverHit ? "FAIL_NO_DAMAGE" : "FAIL_HIT_TIMEOUT";
                }
                else if (test.expectKill && !killed)
                {
                    result = "FAIL_NOT_KILLED";
                }
                else
                {
                    result = "PASS";
                }

                LogCaseEnd(weaponName, enemyName, spawned, equipped, _committedFire, damaged, killed, timer, result);
            }
            finally
            {
                if (ws != null)
                {
                    ws.OnCommittedAttack -= OnCommittedAttack;
                }

                _heroInput.SetBotDriving(false);
                _heroInput.SetBotFrame(Vector2.zero, false, false);
                if (_heroView != null)
                {
                    _heroView.SetAutoAimWorldOverride(null);
                }
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

        private GameObject ResolvePrefab(WeaponEnemyTestEnemyKind kind)
        {
            if (_enemyPrefabs == null)
            {
                return null;
            }

            for (int i = 0; i < _enemyPrefabs.Count; i++)
            {
                WeaponEnemyEnemyPrefabBinding b = _enemyPrefabs[i];
                if (b != null && b.kind == kind && b.prefab != null)
                {
                    return b.prefab;
                }
            }

            return null;
        }

        private static WeaponType MapWeapon(WeaponEnemyTestWeaponKind w)
        {
            switch (w)
            {
                case WeaponEnemyTestWeaponKind.Colt:
                    return WeaponType.Colt45;
                case WeaponEnemyTestWeaponKind.Thompson:
                    return WeaponType.Thompson;
                case WeaponEnemyTestWeaponKind.Bazooka:
                    return WeaponType.Bazooka;
                case WeaponEnemyTestWeaponKind.TeslaGun:
                    return WeaponType.Tesla;
                case WeaponEnemyTestWeaponKind.Flamethrower:
                    return WeaponType.Flamethrower;
                default:
                    return WeaponType.Colt45;
            }
        }

        private static Vector2 GetAimWorldPoint(GameObject enemy)
        {
            if (enemy == null)
            {
                return Vector2.zero;
            }

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

        private void LogCaseEnd(
            string weapon,
            string enemy,
            bool spawned,
            bool equipped,
            bool fired,
            bool damaged,
            bool killed,
            float timeToKillOrTimeout,
            string result)
        {
            string line =
                $"Weapon={weapon} Enemy={enemy} Spawned={spawned} Equipped={equipped} WeaponFired={fired} " +
                $"Hit={_resolverHit || damaged} DamageApplied={damaged} EnemyKilled={killed} " +
                $"TimeToKill={timeToKillOrTimeout:F1}s Result={result}";
            Debug.Log($"[WeaponEnemyTestRange_V2] {line}");

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
    }
}
