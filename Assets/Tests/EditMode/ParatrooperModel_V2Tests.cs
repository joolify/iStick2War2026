using iStick2War;
using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;

namespace iStick2War.Tests.EditMode
{
    public sealed class ParatrooperModel_V2Tests
    {
        private GameObject _go;
        private ParatrooperModel_V2 _model;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ParatrooperModel_V2Tests");
            _model = _go.AddComponent<ParatrooperModel_V2>();
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
        public void ApplyWaveHealthMultiplier_IgnoresNonPositiveAndOne()
        {
            _model.health = 40f;
            _model.ApplyWaveHealthMultiplier(0f);
            _model.ApplyWaveHealthMultiplier(-1f);
            _model.ApplyWaveHealthMultiplier(1f);

            Assert.That(_model.health, Is.EqualTo(40f));
        }

        [Test]
        public void ApplyWaveHealthMultiplier_ScalesHealth()
        {
            _model.health = 50f;
            _model.ApplyWaveHealthMultiplier(2f);

            Assert.That(_model.health, Is.EqualTo(100f));
        }

        [Test]
        public void ApplyDamage_ReducesHealth_AndClampsAtZero()
        {
            _model.health = 30f;

            Assert.That(_model.ApplyDamage(25f), Is.EqualTo(5f));
            Assert.That(_model.ApplyDamage(100f), Is.EqualTo(0f));
            Assert.That(_model.health, Is.EqualTo(0f));
        }

        [Test]
        public void IsDead_WhenHealthZeroOrLess()
        {
            _model.health = 0f;
            Assert.That(_model.IsDead(), Is.True);

            _model.health = -1f;
            Assert.That(_model.IsDead(), Is.True);

            _model.health = 0.01f;
            Assert.That(_model.IsDead(), Is.False);
        }

        [Test]
        public void GetMultiplier_ReturnsOne_WhenPartNotInTable()
        {
            _model.damageMultipliers = null;

            Assert.That(_model.GetMultiplier(BodyPartType.Head), Is.EqualTo(1f));
        }

        [Test]
        public void GetMultiplier_UsesDamageProfile_ForHead()
        {
            _model.ApplyDamageProfile();

            Assert.That(_model.GetMultiplier(BodyPartType.Head), Is.GreaterThan(1f));
        }
    }
}
