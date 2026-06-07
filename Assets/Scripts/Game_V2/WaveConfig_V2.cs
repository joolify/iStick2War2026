using UnityEngine;

namespace iStick2War_V2
{
    /*
 * WaveConfig_V2 (Per-wave ScriptableObject tuning)
 *
 * PURPOSE:
 * Data-only asset consumed by WaveManager_V2 and EnemySpawner_V2: paratrooper/helicopter counts, groundtrooper
 * counts, spawn pacing, difficulty multipliers, optional bomber / mech boss knobs, and wave reward currency.
 * No runtime behaviour here.
 *
 * ---------------------------------------------------------
 * DESIGN INTENT (early air campaign, waves ~1–15)
 *
 * - Waves 1–3: helicopter + paratroopers only, low density.
 * - Waves 4–7: ramp counts/tempo; first bomb-plane passes when BomberPassCount is above zero.
 * - Waves 8–12: heavier air; more bomb-plane passes when BomberPassCount is raised (spawned by EnemySpawner_V2).
 * - Waves 13–15: late-run pressure; tune per-wave assets under Assets/Data/WaveManager/Waves01to15/.
 * - EnemyCount (paratrooper drops per wave) remains “drops per wave” (one helicopter approach per count with the current spawner).
 * - GroundTrooperCount spawns the same Paratrooper_V2 prefab from off-screen ground without Deploy / Glide.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Execute spawn logic or read scene transforms (EnemySpawner_V2 owns instantiation).
 * - Mutate global economy directly (WaveManager_V2 applies rewards from config values).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Consumers → WaveManager_V2.cs, EnemySpawner_V2.cs
 * Global scaling curves → WaveBalanceConfig_V2.cs (optional)
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Authoritative tuning snapshot per wave index; designers duplicate assets per wave row in the WaveManager list.
 */
    [CreateAssetMenu(
        fileName = "WaveConfig_V2",
        menuName = "iStick2War/Waves/Wave Config V2")]
    public sealed class WaveConfig_V2 : ScriptableObject
    {
        [Header("Paratrooper Spawn")]
        [InspectorName("Paratrooper Count")]
        [SerializeField] private int _enemyCount = 6;
        [SerializeField] private float _waveDurationSeconds = 25f;
        [SerializeField] private float _spawnIntervalSeconds = 1.6f;

        [Header("Groundtrooper Spawn")]
        [InspectorName("Groundtrooper Count")]
        [SerializeField] private int _groundTrooperCount;
        [SerializeField] private float _groundTrooperSpawnIntervalSeconds = 1.8f;

        [Header("Difficulty Multipliers")]
        [SerializeField] private float _enemyHealthMultiplier = 1f;
        [SerializeField] private float _enemyDamageMultiplier = 1f;

        [Header("Air threats (BombPlane optional)")]
        [InspectorName("BombPlane Count")]
        [Tooltip(
            "How many bomb-plane flyovers to schedule this wave. Independent of paratrooper helicopter drops (EnemyCount). " +
            "EnemySpawner_V2 spawns bombers only when a bomber prefab is assigned there.")]
        [SerializeField] private int _bomberPassCount;
        [Tooltip(
            "How many enemy kamikaze drones to schedule this wave. " +
            "EnemySpawner_V2 spawns them only when a kamikaze drone prefab is assigned.")]
        [SerializeField] private int _kamikazeDroneCount;
        [Tooltip(
            "How many enemy bomb drones to schedule this wave. " +
            "Requires the Bomb Drone prefab slot on EnemySpawner_V2 in the scene (same pattern as bomber / kamikaze); " +
            "this count alone does not spawn anything if that reference is empty.")]
        [SerializeField] private int _bombDroneCount;

        [Header("Boss (ground)")]
        [Tooltip(
            "Mech Robot Boss units spawned after the helicopter paratrooper schedule completes (runs even when Paratrooper Count is 0). " +
            "Assign the MechRobotBoss prefab (e.g. Mech Robot V2) on EnemySpawner_V2 — otherwise the count is ignored.")]
        [SerializeField] private int _mechRobotBossCount;

        [Header("Economy Reward")]
        [SerializeField] private int _waveRewardCurrency = 80;

        [Header("Dev shortcut")]
        [InspectorName("Start At Main Menu On Scene Load")]
        [Tooltip(
            "When this row is the active wave at gameplay scene boot (wave index 0), load MainMenuScene " +
            "instead of starting Prepare/InWave. Main menu Play loads the gameplay scene and skips this once. " +
            "Useful when pressing Play in the editor on SampleScene. Ignored when Open Shop Directly or Open LifeOver Directly is on. Turn off before shipping.")]
        [SerializeField] private bool _startAtMainMenuOnSceneLoad;
        [InspectorName("Open Shop Directly")]
        [Tooltip(
            "Skip playing this wave: after Prepare, open the shop immediately (wave reward still granted). " +
            "Useful for testing shop UI on the first wave row. Turn off before shipping.")]
        [SerializeField] private bool _openShopDirectly;
        [InspectorName("Open LifeOver Directly")]
        [Tooltip(
            "Skip playing this wave: after Prepare, open the LifeOver menu immediately (one life consumed, bunker restored). " +
            "Useful for testing LifeOver UI. Turn off before shipping.")]
        [SerializeField] private bool _openLifeOverDirectly;

        public int EnemyCount => Mathf.Max(0, _enemyCount);
        public int GroundTrooperCount => Mathf.Max(0, _groundTrooperCount);
        public float WaveDurationSeconds => Mathf.Max(1f, _waveDurationSeconds);
        public float SpawnIntervalSeconds => Mathf.Max(0.1f, _spawnIntervalSeconds);
        public float GroundTrooperSpawnIntervalSeconds => Mathf.Max(0.1f, _groundTrooperSpawnIntervalSeconds);
        public float EnemyHealthMultiplier => Mathf.Max(0.1f, _enemyHealthMultiplier);
        public float EnemyDamageMultiplier => Mathf.Max(0.1f, _enemyDamageMultiplier);
        public int BomberPassCount => Mathf.Max(0, _bomberPassCount);
        public int KamikazeDroneCount => Mathf.Max(0, _kamikazeDroneCount);
        public int BombDroneCount => Mathf.Max(0, _bombDroneCount);
        public int MechRobotBossCount => Mathf.Max(0, _mechRobotBossCount);
        public int WaveRewardCurrency => Mathf.Max(0, _waveRewardCurrency);
        public bool StartAtMainMenuOnSceneLoad => _startAtMainMenuOnSceneLoad;
        public bool OpenShopDirectly => _openShopDirectly;
        public bool OpenLifeOverDirectly => _openLifeOverDirectly;
    }
}
