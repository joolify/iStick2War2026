using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;

namespace iStick2War.Tests.EditMode
{
    public sealed class HeroMovementSystem_V2Tests
    {
        private GameObject _go;
        private HeroModel_V2 _model;
        private HeroMovementSystem_V2 _movement;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HeroMovementSystem_V2Tests");
            _model = _go.AddComponent<HeroModel_V2>();
            _go.AddComponent<BoxCollider2D>();
            _movement = new HeroMovementSystem_V2(_model);
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
        public void Move_WithoutRigidbody_TranslatesWhenEnabled()
        {
            Vector3 before = _go.transform.position;

            _movement.Move(Vector2.right, 0.25f);

            Assert.That(_go.transform.position.x, Is.GreaterThan(before.x));
        }

        [Test]
        public void Move_WhenDisabled_DoesNotTranslate()
        {
            _movement.Disable();
            Vector3 before = _go.transform.position;

            _movement.Move(Vector2.right, 0.5f);

            Assert.That(_go.transform.position, Is.EqualTo(before));
        }

        [Test]
        public void Disable_ZerosModelVelocity()
        {
            _movement.Move(Vector2.right, 0.1f);
            _movement.Disable();

            Assert.That(_model.velocity, Is.EqualTo(Vector2.zero));
        }
    }
}
