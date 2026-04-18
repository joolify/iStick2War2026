using System.Collections.Generic;
using iStick2War;
using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;

namespace iStick2War.Tests.EditMode
{
    public sealed class HeroWeaponSystem_V2Tests
    {
        private readonly List<Object> _destroyList = new List<Object>();

        private GameObject _heroGo;
        private HeroModel_V2 _model;
        private HeroWeaponDefinition_V2 _definition;
        private HeroWeaponSystem_V2 _system;

        [SetUp]
        public void SetUp()
        {
            _destroyList.Clear();

            _heroGo = new GameObject("HeroWeaponSystem_V2Tests_Hero");
            _destroyList.Add(_heroGo);

            _model = _heroGo.AddComponent<HeroModel_V2>();
            _definition = ScriptableObject.CreateInstance<HeroWeaponDefinition_V2>();
            _destroyList.Add(_definition);

            _system = new HeroWeaponSystem_V2(_model, new[] { _definition }, WeaponType.Thompson);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _destroyList.Count - 1; i >= 0; i--)
            {
                Object obj = _destroyList[i];
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _destroyList.Clear();
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

        [Test]
        public void CanReload_IsTrue_WhenMagazineNotFull_AndReserveAvailable()
        {
            _model.ConsumeAmmo(1);

            Assert.That(_system.CanReload(), Is.True);
        }

        [Test]
        public void CanReload_IsFalse_WhenReserveIsZero()
        {
            _model.SetAmmoState(Mathf.Max(1, _model.maxAmmo / 2), 0);

            Assert.That(_system.CanReload(), Is.False);
        }

        [Test]
        public void AfterDisable_CannotShootOrReload()
        {
            _model.ConsumeAmmo(1);
            _system.Disable();

            Assert.That(_system.CanShoot(), Is.False);
            Assert.That(_system.CanReload(), Is.False);
        }

        [Test]
        public void StartReload_SetsReloading_WhenAllowed()
        {
            _model.SetAmmoState(5, 40);

            Assert.That(_system.StartReload(), Is.True);
            Assert.That(_system.IsReloading(), Is.True);
        }

        [Test]
        public void TrySwitchToNextWeapon_ReturnsFalse_WhenSingleWeaponInLoadout()
        {
            Assert.That(_system.TrySwitchToNextWeapon(), Is.False);
        }

        [Test]
        public void TrySwitchToNextWeapon_SwitchesModelWeapon_WhenMultipleInLoadout()
        {
            HeroWeaponDefinition_V2 thompson = EditModeTestHelpers.CreateWeaponDefinition(WeaponType.Thompson);
            HeroWeaponDefinition_V2 mp40 = EditModeTestHelpers.CreateWeaponDefinition(WeaponType.MP40);
            _destroyList.Add(thompson);
            _destroyList.Add(mp40);

            _system = new HeroWeaponSystem_V2(_model, new[] { thompson, mp40 }, WeaponType.Thompson);

            Assert.That(_model.currentWeaponType, Is.EqualTo(WeaponType.Thompson));
            Assert.That(_system.TrySwitchToNextWeapon(), Is.True);
            Assert.That(_model.currentWeaponType, Is.EqualTo(WeaponType.MP40));
        }

        [Test]
        public void UnlockWeapon_ReturnsTrue_WhenNew_AndFalse_WhenAlreadyPresent()
        {
            HeroWeaponDefinition_V2 mp40 = EditModeTestHelpers.CreateWeaponDefinition(WeaponType.MP40);
            _destroyList.Add(mp40);

            Assert.That(_system.UnlockWeapon(mp40), Is.True);
            Assert.That(_system.HasWeaponUnlocked(mp40), Is.True);
            Assert.That(_system.UnlockWeapon(mp40), Is.False);
        }

        [Test]
        public void EmptyLoadout_CannotShoot()
        {
            var empty = System.Array.Empty<HeroWeaponDefinition_V2>();
            var system = new HeroWeaponSystem_V2(_model, empty, WeaponType.Thompson);

            Assert.That(system.CanShoot(), Is.False);
        }

        [Test]
        public void TryRefillMagazineForWeapon_ReturnsFalse_WhenMagazineAlreadyFull()
        {
            Assert.That(_system.TryRefillMagazineForWeapon(_definition), Is.False);
        }

        [Test]
        public void UnlockWeapon_WithAutoEquip_SwitchesActiveWeapon()
        {
            HeroWeaponDefinition_V2 mp40 = EditModeTestHelpers.CreateWeaponDefinition(WeaponType.MP40);
            _destroyList.Add(mp40);

            Assert.That(_system.UnlockWeapon(mp40, autoEquip: true), Is.True);
            Assert.That(_model.currentWeaponType, Is.EqualTo(WeaponType.MP40));
        }

        [Test]
        public void TrySwitchToPreviousWeapon_CyclesBack_WhenMultipleInLoadout()
        {
            HeroWeaponDefinition_V2 thompson = EditModeTestHelpers.CreateWeaponDefinition(WeaponType.Thompson);
            HeroWeaponDefinition_V2 mp40 = EditModeTestHelpers.CreateWeaponDefinition(WeaponType.MP40);
            _destroyList.Add(thompson);
            _destroyList.Add(mp40);

            _system = new HeroWeaponSystem_V2(_model, new[] { thompson, mp40 }, WeaponType.Thompson);

            Assert.That(_system.TrySwitchToNextWeapon(), Is.True);
            Assert.That(_model.currentWeaponType, Is.EqualTo(WeaponType.MP40));
            Assert.That(_system.TrySwitchToPreviousWeapon(), Is.True);
            Assert.That(_model.currentWeaponType, Is.EqualTo(WeaponType.Thompson));
        }
    }
}
