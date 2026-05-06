using System;
using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>What the combat matrix expects for a weapon vs enemy pair.</summary>
    public enum CombatExpectation
    {
        /// <summary>First damage must occur within <see cref="CombatMatrixTestCase_V2.damageTimeoutSec"/>.</summary>
        MustDamage,

        /// <summary>Damage within damage timeout, then kill within kill timeout (both from case start).</summary>
        MustKill,

        /// <summary>HP must not drop during the damage window (friendly-fire / immunity regressions).</summary>
        ShouldNotDamage,
    }

    /// <summary>Enemy archetype — wire a matching prefab on <see cref="CombatMatrixIntegrationTestRunner_V2"/>.</summary>
    public enum CombatMatrixEnemyKind
    {
        Paratrooper,
        Helicopter,
        Bomber,
        KamikazeDrone,
        BombDrone,
        MechBoss,
    }

    [Serializable]
    public sealed class CombatMatrixTestCase_V2
    {
        public WeaponType weapon = WeaponType.Colt45;
        public CombatMatrixEnemyKind enemy = CombatMatrixEnemyKind.Paratrooper;
        public CombatExpectation expectation = CombatExpectation.MustKill;
        [Min(0.25f)] public float damageTimeoutSec = 3f;
        [Min(0.5f)] public float killTimeoutSec = 10f;
    }

    [Serializable]
    public sealed class CombatMatrixEnemyPrefabBinding
    {
        public CombatMatrixEnemyKind kind;
        [Tooltip("Root with ParatrooperModel_V2, AircraftHealth_V2, or MechRobotBossModel_V2.")]
        public GameObject prefab;
    }

    /// <summary>One row written to TSV/JSON after a case completes.</summary>
    [Serializable]
    public sealed class CombatMatrixRowResult
    {
        public string weapon;
        public string enemy;
        public string expectation;
        public bool fired;
        public bool hit;
        public bool damaged;
        public bool killed;
        public float timeToDamage;
        public float timeToKill;
        public string result;
    }

    [Serializable]
    public sealed class CombatMatrixJsonExport
    {
        public string suite = "CombatMatrixIntegrationTest_V2";
        public string timestampUtc;
        public bool allPassed;
        public CombatMatrixRowResult[] rows;
    }
}
