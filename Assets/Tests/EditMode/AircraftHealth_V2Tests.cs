using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;

namespace iStick2War.Tests.EditMode
{
    public sealed class AircraftHealth_V2Tests
    {
        private GameObject _go;
        private AircraftHealth_V2 _health;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AircraftHealth_V2Tests");
            _health = _go.AddComponent<AircraftHealth_V2>();
            EditModeTestHelpers.SetPrivateField(_health, "_destroyRootWhenDead", false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void ApplyDamage_IgnoresNonPositive()
        {
            float before = EditModeTestHelpers.GetPrivateField<float>(_health, "_currentHealth");

            _health.ApplyDamage(0f);
            _health.ApplyDamage(-10f);

            Assert.That(EditModeTestHelpers.GetPrivateField<float>(_health, "_currentHealth"), Is.EqualTo(before));
        }

        [Test]
        public void ApplyDamage_ReducesCurrentHealth()
        {
            EditModeTestHelpers.SetPrivateField(_health, "_currentHealth", 100f);

            _health.ApplyDamage(30f);

            Assert.That(EditModeTestHelpers.GetPrivateField<float>(_health, "_currentHealth"), Is.EqualTo(70f));
        }

        [Test]
        public void ApplyDamage_WhenAlreadyDead_DoesNothing()
        {
            EditModeTestHelpers.SetPrivateField(_health, "_currentHealth", 0f);

            _health.ApplyDamage(50f);

            Assert.That(EditModeTestHelpers.GetPrivateField<float>(_health, "_currentHealth"), Is.EqualTo(0f));
        }
    }
}
