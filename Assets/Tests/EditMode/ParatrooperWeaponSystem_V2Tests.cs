using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;

namespace iStick2War.Tests.EditMode
{
    public sealed class ParatrooperWeaponSystem_V2Tests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void ApplyWaveDamageMultiplier_IgnoresNonPositive()
        {
            _go = new GameObject("ParatrooperWeaponTest");
            var system = _go.AddComponent<ParatrooperWeaponSystem_V2>();
            EditModeTestHelpers.SetPrivateField(system, "_baseDamage", 12);

            system.ApplyWaveDamageMultiplier(0f);
            system.ApplyWaveDamageMultiplier(-2f);

            Assert.That(EditModeTestHelpers.GetPrivateField<int>(system, "_baseDamage"), Is.EqualTo(12));
        }

        [Test]
        public void ApplyWaveDamageMultiplier_IgnoresApproximatelyOne()
        {
            _go = new GameObject("ParatrooperWeaponTest");
            var system = _go.AddComponent<ParatrooperWeaponSystem_V2>();
            EditModeTestHelpers.SetPrivateField(system, "_baseDamage", 12);

            system.ApplyWaveDamageMultiplier(1f);

            Assert.That(EditModeTestHelpers.GetPrivateField<int>(system, "_baseDamage"), Is.EqualTo(12));
        }

        [Test]
        public void ApplyWaveDamageMultiplier_ScalesAndRounds_AndFloorsAtOne()
        {
            _go = new GameObject("ParatrooperWeaponTest");
            var system = _go.AddComponent<ParatrooperWeaponSystem_V2>();
            EditModeTestHelpers.SetPrivateField(system, "_baseDamage", 10);

            system.ApplyWaveDamageMultiplier(2.2f);

            Assert.That(EditModeTestHelpers.GetPrivateField<int>(system, "_baseDamage"), Is.EqualTo(22));

            system.ApplyWaveDamageMultiplier(0.01f);

            Assert.That(EditModeTestHelpers.GetPrivateField<int>(system, "_baseDamage"), Is.GreaterThanOrEqualTo(1));
        }
    }
}
