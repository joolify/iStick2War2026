using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Per-wave tuning for <see cref="WaveManager_V2"/> / <see cref="EnemySpawner_V2"/>.
    /// </summary>
    /// <remarks>
    /// Early air campaign (waves ~1–10) design intent:
    /// <list type="bullet">
    /// <item><description>Waves 1–3: helicopter + paratroopers only, low density.</description></item>
    /// <item><description>Waves 4–7: ramp counts/tempo; first bomber passes when BomberPassCount is above zero.</description></item>
    /// <item><description>Waves 8–10: heavier air; more bomber passes once bomber spawning is implemented.</description></item>
    /// </list>
    /// <see cref="EnemyCount"/> is still “drops per wave” (one helicopter approach per count with current spawner).
    /// </remarks>
    [CreateAssetMenu(
        fileName = "WaveConfig_V2",
        menuName = "iStick2War/Waves/Wave Config V2")]
    public sealed class WaveConfig_V2 : ScriptableObject
    {
        [Header("Spawn")]
        [SerializeField] private int _enemyCount = 6;
        [SerializeField] private float _waveDurationSeconds = 25f;
        [SerializeField] private float _spawnIntervalSeconds = 1.6f;

        [Header("Difficulty Multipliers")]
        [SerializeField] private float _enemyHealthMultiplier = 1f;
        [SerializeField] private float _enemyDamageMultiplier = 1f;

        [Header("Air threats (bomber is optional)")]
        [Tooltip(
            "How many bomber flyovers to schedule this wave. Independent of EnemyCount (helicopter paratrooper drops). " +
            "EnemySpawner_V2 spawns bombers only when a bomber prefab is assigned there.")]
        [SerializeField] private int _bomberPassCount;

        [Header("Economy Reward")]
        [SerializeField] private int _waveRewardCurrency = 80;

        public int EnemyCount => Mathf.Max(0, _enemyCount);
        public float WaveDurationSeconds => Mathf.Max(1f, _waveDurationSeconds);
        public float SpawnIntervalSeconds => Mathf.Max(0.1f, _spawnIntervalSeconds);
        public float EnemyHealthMultiplier => Mathf.Max(0.1f, _enemyHealthMultiplier);
        public float EnemyDamageMultiplier => Mathf.Max(0.1f, _enemyDamageMultiplier);
        public int BomberPassCount => Mathf.Max(0, _bomberPassCount);
        public int WaveRewardCurrency => Mathf.Max(0, _waveRewardCurrency);
    }
}
