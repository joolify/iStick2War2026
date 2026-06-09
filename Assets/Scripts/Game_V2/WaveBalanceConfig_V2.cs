using System;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * WaveBalanceWaveRow (Global balance row for one wave number)
 *
 * PURPOSE:
 * Holds per-wave multipliers for enemy HP, enemy damage, spawn rate, and wave reward currency. Row index i applies
 * to wave number i + 1; if the run exceeds configured rows, the last row repeats.
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Serializable struct so WaveBalanceConfig_V2 can edit curves in Inspector without custom property drawers.
 *
 * NAVIGATION: consumed only via WaveBalanceConfig_V2 → WaveManager_V2.cs.
 */
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

    /*
 * WaveBalanceConfig_V2 (Global balance overlay asset)
 *
 * PURPOSE:
 * Provides WaveBalanceWaveRow scaling layered on top of each WaveConfig_V2 for retention tuning and A/B tests.
 * WaveManager_V2 resolves the active row per one-based wave number; optional built-in curve applies when rows empty.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Spawn enemies or change loop state (WaveManager_V2 / EnemySpawner_V2).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Row struct → WaveBalanceWaveRow (this file) | Per-wave base → WaveConfig_V2.cs | Consumer → WaveManager_V2.cs
 * Telemetry snapshot struct → WaveRunScalingSnapshot (this file) + WaveRunTelemetry_V2.cs
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Single optional asset reference on WaveManager_V2 keeps production wave assets stable while balance iterates globally.
 */
    [CreateAssetMenu(
        fileName = "WaveBalanceConfig_V2",
        menuName = "iStick2War/Waves/Wave Balance Config V2")]
    public sealed class WaveBalanceConfig_V2 : ScriptableObject
    {
        [SerializeField]
        private string _scalingVersion = "default";

        [Tooltip("When _rows is empty, resolve waves 1-15 from a built-in retention-focused curve.")]
        [SerializeField]
        private bool _useBuiltInDefaultCurveWhenRowsEmpty = true;

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
                return _useBuiltInDefaultCurveWhenRowsEmpty
                    ? ResolveBuiltInCurveWaveRow(wave)
                    : WaveBalanceWaveRow.Identity;
            }

            int idx = Mathf.Min(wave - 1, _rows.Count - 1);
            WaveBalanceWaveRow src = _rows[idx];
            float hp = Mathf.Max(0.01f, src.enemyHpMultiplier);
            float dmg = Mathf.Max(0.01f, src.enemyDamageMultiplier);
            float spawn = Mathf.Max(0.01f, src.spawnRateMultiplier);
            float reward = Mathf.Max(0f, src.waveRewardMultiplier);

            if (wave > _rows.Count)
            {
                int extraWaves = wave - _rows.Count;
                float postCapRamp = 1f + extraWaves * 0.04f;
                hp *= postCapRamp;
                dmg *= 1f + extraWaves * 0.035f;
                spawn *= 1f + extraWaves * 0.025f;
                reward *= 1f + extraWaves * 0.02f;
            }

            return new WaveBalanceWaveRow
            {
                enemyHpMultiplier = hp,
                enemyDamageMultiplier = dmg,
                spawnRateMultiplier = spawn,
                waveRewardMultiplier = reward
            };
        }

        private static WaveBalanceWaveRow ResolveBuiltInCurveWaveRow(int wave)
        {
            // Mild ramp for waves 1-3, steeper pressure through wave 15 with matching reward.
            WaveBalanceWaveRow capped = ResolveBuiltInCurveWaveRowCappedAt15(wave);
            if (wave <= 15)
            {
                return capped;
            }

            // Survival endless: extrapolate beyond campaign cap (~4% pressure per extra wave).
            int extraWaves = wave - 15;
            float postCapRamp = 1f + extraWaves * 0.04f;
            return new WaveBalanceWaveRow
            {
                enemyHpMultiplier = capped.enemyHpMultiplier * postCapRamp,
                enemyDamageMultiplier = capped.enemyDamageMultiplier * (1f + extraWaves * 0.035f),
                spawnRateMultiplier = capped.spawnRateMultiplier * (1f + extraWaves * 0.025f),
                waveRewardMultiplier = capped.waveRewardMultiplier * (1f + extraWaves * 0.02f)
            };
        }

        private static WaveBalanceWaveRow ResolveBuiltInCurveWaveRowCappedAt15(int wave)
        {
            float t = Mathf.Clamp01((Mathf.Min(wave, 15) - 1f) / 14f);
            return new WaveBalanceWaveRow
            {
                enemyHpMultiplier = Mathf.Lerp(1f, 1.42f, t),
                enemyDamageMultiplier = Mathf.Lerp(1f, 1.36f, t),
                spawnRateMultiplier = Mathf.Lerp(1f, 1.52f, t),
                waveRewardMultiplier = Mathf.Lerp(1f, 1.34f, t)
            };
        }
    }

    /*
 * WaveRunScalingSnapshot (Effective multipliers for one resolved wave)
 *
 * PURPOSE:
 * Immutable snapshot combining WaveBalanceConfig_V2 row values with the active WaveConfig_V2 numbers so
 * WaveRunTelemetry_V2 and debug HUD can log what actually applied after multiplication.
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Read-only struct created by WaveManager_V2 when a wave starts; avoids recomputing curves during combat frames.
 *
 * NAVIGATION: logged by WaveRunTelemetry_V2; built in WaveManager_V2.
 */
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
