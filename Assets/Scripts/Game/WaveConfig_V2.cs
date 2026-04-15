using UnityEngine;

namespace iStick2War_V2
{
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

        [Header("Economy Reward")]
        [SerializeField] private int _waveRewardCurrency = 80;

        public int EnemyCount => Mathf.Max(1, _enemyCount);
        public float WaveDurationSeconds => Mathf.Max(1f, _waveDurationSeconds);
        public float SpawnIntervalSeconds => Mathf.Max(0.1f, _spawnIntervalSeconds);
        public float EnemyHealthMultiplier => Mathf.Max(0.1f, _enemyHealthMultiplier);
        public float EnemyDamageMultiplier => Mathf.Max(0.1f, _enemyDamageMultiplier);
        public int WaveRewardCurrency => Mathf.Max(0, _waveRewardCurrency);
    }
}
