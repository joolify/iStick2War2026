using iStick2War;
using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;

namespace iStick2War.Tests.EditMode
{
    public sealed class HeroWeaponSystem_V2Tests
    {
        private GameObject _heroGo;
        private HeroModel_V2 _model;
        private HeroWeaponDefinition_V2 _definition;
        private HeroWeaponSystem_V2 _system;

        [SetUp]
        public void SetUp()
        {
            _heroGo = new GameObject("HeroWeaponSystem_V2Tests_Hero");
            _model = _heroGo.AddComponent<HeroModel_V2>();
            _definition = ScriptableObject.CreateInstance<HeroWeaponDefinition_V2>();
            _system = new HeroWeaponSystem_V2(_model, new[] { _definition }, WeaponType.Thompson);
        }

        [TearDown]
        public void TearDown()
        {
            if (_heroGo != null)
            {
                Object.DestroyImmediate(_heroGo);
            }

            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
            }
        }

        [Test]
        public void CanShoot_IsFalse_WhenHeroDead()
        {
            _model.SetDead();

            Assert.That(_system.CanShoot(), Is.False);
        }

        [Test]
        public void CanReload_IsFalse_WhenMagazineFullAndReserveAvailable()
        {
            Assert.That(_model.currentAmmo, Is.EqualTo(_model.maxAmmo));
            Assert.That(_system.CanReload(), Is.False);
        }

        [Test]
        public void CanShoot_IsFalse_WhenMagazineEmpty()
        {
            _model.ConsumeAmmo(_model.currentAmmo);

            Assert.That(_system.CanShoot(), Is.False);
        }
    }
}
