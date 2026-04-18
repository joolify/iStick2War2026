using iStick2War;
using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;

namespace iStick2War.Tests.EditMode
{
    public sealed class HeroModel_V2Tests
    {
        private GameObject _heroGo;
        private HeroModel_V2 _model;

        [SetUp]
        public void SetUp()
        {
            _heroGo = new GameObject("HeroModel_V2Tests_Hero");
            _model = _heroGo.AddComponent<HeroModel_V2>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_heroGo != null)
            {
                Object.DestroyImmediate(_heroGo);
            }
        }

        [Test]
        public void TakeDamage_ReducesCurrentHealth()
        {
            int before = _model.currentHealth;
            _model.TakeDamage(12);

            Assert.That(_model.currentHealth, Is.EqualTo(before - 12));
            Assert.That(_model.isDead, Is.False);
        }

        [Test]
        public void TakeDamage_ReachingZero_SetsDead_AndClampsHealth()
        {
            _model.TakeDamage(_model.currentHealth + 50);

            Assert.That(_model.currentHealth, Is.EqualTo(0));
            Assert.That(_model.isDead, Is.True);
        }

        [Test]
        public void TakeDamage_DoesNothing_WhenAlreadyDead()
        {
            _model.SetDead();
            int healthBeforeSecondHit = _model.currentHealth;
            _model.TakeDamage(10);

            Assert.That(_model.currentHealth, Is.EqualTo(healthBeforeSecondHit));
        }

        [Test]
        public void Heal_IncreasesHealth_UpToMax()
        {
            _model.TakeDamage(40);
            _model.Heal(1000);

            Assert.That(_model.currentHealth, Is.EqualTo(_model.maxHealth));
        }

        [Test]
        public void Heal_DoesNothing_WhenDead()
        {
            _model.TakeDamage(_model.maxHealth);
            int healthAfterDeath = _model.currentHealth;
            _model.Heal(50);

            Assert.That(_model.currentHealth, Is.EqualTo(healthAfterDeath));
        }

        [Test]
        public void ConsumeAmmo_ClampsAtZero()
        {
            _model.ConsumeAmmo(_model.currentAmmo + 10);

            Assert.That(_model.currentAmmo, Is.EqualTo(0));
        }

        [Test]
        public void SetAmmoState_ClampsToMaxValues()
        {
            var def = ScriptableObject.CreateInstance<HeroWeaponDefinition_V2>();
            try
            {
                _model.ConfigureWeaponState(
                    def,
                    WeaponType.Thompson,
                    weaponMaxAmmo: 10,
                    weaponCurrentAmmo: 100,
                    weaponMaxReserveAmmo: 20,
                    weaponCurrentReserveAmmo: 999,
                    weaponFireRate: 0.2f,
                    weaponReloadDuration: 1f);

                Assert.That(_model.currentAmmo, Is.EqualTo(10));
                Assert.That(_model.currentReserveAmmo, Is.EqualTo(20));
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }
    }
}
