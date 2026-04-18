using iStick2War;
using NUnit.Framework;

namespace iStick2War.Tests.EditMode
{
    public sealed class HealthStatsTests
    {
        [Test]
        public void Init_SetsCurrentToMax()
        {
            var stats = new HealthStats { maxHealth = 50f };
            stats.Init();

            Assert.That(stats.curHealth, Is.EqualTo(50f));
        }

        [Test]
        public void CurHealth_ClampsToMax()
        {
            var stats = new HealthStats { maxHealth = 40f };
            stats.Init();
            stats.curHealth = 999f;

            Assert.That(stats.curHealth, Is.EqualTo(40f));
        }

        [Test]
        public void CurHealth_DoesNotGoBelowZero()
        {
            var stats = new HealthStats { maxHealth = 30f };
            stats.Init();
            stats.curHealth = -10f;

            Assert.That(stats.curHealth, Is.EqualTo(0f));
        }
    }
}
