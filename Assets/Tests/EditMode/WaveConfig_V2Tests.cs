using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;

namespace iStick2War.Tests.EditMode
{
    public sealed class WaveConfig_V2Tests
    {
        private WaveConfig_V2 _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<WaveConfig_V2>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        [Test]
        public void EnemyCount_IsNotNegative()
        {
            EditModeTestHelpers.SetPrivateField(_config, "_enemyCount", -10);

            Assert.That(_config.EnemyCount, Is.EqualTo(0));
        }

        [Test]
        public void WaveDurationSeconds_IsAtLeastOne()
        {
            EditModeTestHelpers.SetPrivateField(_config, "_waveDurationSeconds", 0.2f);

            Assert.That(_config.WaveDurationSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void SpawnIntervalSeconds_IsAtLeastPointOne()
        {
            EditModeTestHelpers.SetPrivateField(_config, "_spawnIntervalSeconds", 0.01f);

            Assert.That(_config.SpawnIntervalSeconds, Is.EqualTo(0.1f));
        }

        [Test]
        public void EnemyMultipliers_AreClampedLow()
        {
            EditModeTestHelpers.SetPrivateField(_config, "_enemyHealthMultiplier", 0.01f);
            EditModeTestHelpers.SetPrivateField(_config, "_enemyDamageMultiplier", 0.01f);

            Assert.That(_config.EnemyHealthMultiplier, Is.EqualTo(0.1f));
            Assert.That(_config.EnemyDamageMultiplier, Is.EqualTo(0.1f));
        }

        [Test]
        public void BomberPassCount_IsNotNegative()
        {
            EditModeTestHelpers.SetPrivateField(_config, "_bomberPassCount", -3);

            Assert.That(_config.BomberPassCount, Is.EqualTo(0));
        }

        [Test]
        public void WaveRewardCurrency_IsNotNegative()
        {
            EditModeTestHelpers.SetPrivateField(_config, "_waveRewardCurrency", -50);

            Assert.That(_config.WaveRewardCurrency, Is.EqualTo(0));
        }
    }
}
