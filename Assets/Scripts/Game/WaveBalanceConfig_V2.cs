using System;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// One row of global balance multipliers; row index <c>i</c> applies to wave number <c>i + 1</c>.
    /// If the run has more waves than rows, the last row is repeated.
    /// </summary>
    [Serializable]
    public struct WaveBalanceWaveRow
    {
        [Min(0.01f)]
        public float enemyHpMultiplier;

        [Min(0.01f)]
        public float enemyDamageMultiplier;

        [Tooltip("Values above 1 spawn faster (spawn interval is divided by this).")]
        [Min(0.01f)]
        public float spawnRateMultiplier;

        [Min(0f)]
        public float waveRewardMultiplier;

        public static WaveBalanceWaveRow Identity => new WaveBalanceWaveRow
        {
            enemyHpMultiplier = 1f,
            enemyDamageMultiplier = 1f,
            spawnRateMultiplier = 1f,
            waveRewardMultiplier = 1f
        };
    }

    /// <summary>
    /// Global per-wave balance layer multiplied onto each <see cref="WaveConfig_V2"/> asset.
    /// Single source for A/B runs: assign one asset on <see cref="WaveManager_V2"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WaveBalanceConfig_V2",
        menuName = "iStick2War/Waves/Wave Balance Config V2")]
    public sealed class WaveBalanceConfig_V2 : ScriptableObject
    {
        [SerializeField]
        private string _scalingVersion = "default";

        [Tooltip("Row[i] applies to wave i+1. Extra waves repeat the last row. Empty list = all multipliers 1.")]
        [SerializeField]
        private List<WaveBalanceWaveRow> _rows = new List<WaveBalanceWaveRow>();

        public string ScalingVersion =>
            string.IsNullOrWhiteSpace(_scalingVersion) ? "default" : _scalingVersion.Trim();

        public WaveBalanceWaveRow ResolveRowForWave(int waveNumberOneBased)
        {
            int wave = Mathf.Max(1, waveNumberOneBased);
            if (_rows == null || _rows.Count == 0)
            {
                return WaveBalanceWaveRow.Identity;
            }

            int idx = Mathf.Min(wave - 1, _rows.Count - 1);
            WaveBalanceWaveRow src = _rows[idx];
            return new WaveBalanceWaveRow
            {
                enemyHpMultiplier = Mathf.Max(0.01f, src.enemyHpMultiplier),
                enemyDamageMultiplier = Mathf.Max(0.01f, src.enemyDamageMultiplier),
                spawnRateMultiplier = Mathf.Max(0.01f, src.spawnRateMultiplier),
                waveRewardMultiplier = Mathf.Max(0f, src.waveRewardMultiplier)
            };
        }
    }

    /// <summary>
    /// Effective combat + reward tuning for a wave (WaveConfig × balance row), for telemetry and debugging.
    /// </summary>
    public readonly struct WaveRunScalingSnapshot
    {
        public string ScalingVersion { get; }
        public float BalanceEnemyHpMultiplier { get; }
        public float BalanceEnemyDamageMultiplier { get; }
        public float BalanceSpawnRateMultiplier { get; }
        public float BalanceWaveRewardMultiplier { get; }
        public float ConfigEnemyHpMultiplier { get; }
        public float ConfigEnemyDamageMultiplier { get; }
        public float ConfigSpawnIntervalSeconds { get; }
        public int ConfigWaveRewardCurrency { get; }
        public float EffectiveEnemyHpMultiplier { get; }
        public float EffectiveEnemyDamageMultiplier { get; }
        public float EffectiveSpawnIntervalSeconds { get; }
        public int EffectiveWaveRewardCurrency { get; }

        public WaveRunScalingSnapshot(
            string scalingVersion,
            float balanceEnemyHpMultiplier,
            float balanceEnemyDamageMultiplier,
            float balanceSpawnRateMultiplier,
            float balanceWaveRewardMultiplier,
            float configEnemyHpMultiplier,
            float configEnemyDamageMultiplier,
            float configSpawnIntervalSeconds,
            int configWaveRewardCurrency,
            float effectiveEnemyHpMultiplier,
            float effectiveEnemyDamageMultiplier,
            float effectiveSpawnIntervalSeconds,
            int effectiveWaveRewardCurrency)
        {
            ScalingVersion = scalingVersion ?? "";
            BalanceEnemyHpMultiplier = balanceEnemyHpMultiplier;
            BalanceEnemyDamageMultiplier = balanceEnemyDamageMultiplier;
            BalanceSpawnRateMultiplier = balanceSpawnRateMultiplier;
            BalanceWaveRewardMultiplier = balanceWaveRewardMultiplier;
            ConfigEnemyHpMultiplier = configEnemyHpMultiplier;
            ConfigEnemyDamageMultiplier = configEnemyDamageMultiplier;
            ConfigSpawnIntervalSeconds = configSpawnIntervalSeconds;
            ConfigWaveRewardCurrency = configWaveRewardCurrency;
            EffectiveEnemyHpMultiplier = effectiveEnemyHpMultiplier;
            EffectiveEnemyDamageMultiplier = effectiveEnemyDamageMultiplier;
            EffectiveSpawnIntervalSeconds = effectiveSpawnIntervalSeconds;
            EffectiveWaveRewardCurrency = effectiveWaveRewardCurrency;
        }
    }
}
