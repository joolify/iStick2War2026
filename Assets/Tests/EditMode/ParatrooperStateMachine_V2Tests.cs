using iStick2War;
using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;

namespace iStick2War.Tests.EditMode
{
    public sealed class ParatrooperStateMachine_V2Tests
    {
        private GameObject _go;
        private ParatrooperModel_V2 _model;
        private ParatrooperStateMachine_V2 _stateMachine;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ParatrooperStateMachine_V2Tests");
            _model = _go.AddComponent<ParatrooperModel_V2>();
            _stateMachine = _go.AddComponent<ParatrooperStateMachine_V2>();
            _stateMachine.Initialize(_model);
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
        public void Initialize_SetsIdle_OnModelAndMachine()
        {
            Assert.That(_stateMachine.CurrentState, Is.EqualTo(StickmanBodyState.Idle));
            Assert.That(_model.currentState, Is.EqualTo(StickmanBodyState.Idle));
        }

        [Test]
        public void ChangeState_UpdatesCurrentState()
        {
            _stateMachine.ChangeState(StickmanBodyState.Run);

            Assert.That(_stateMachine.CurrentState, Is.EqualTo(StickmanBodyState.Run));
            Assert.That(_model.currentState, Is.EqualTo(StickmanBodyState.Run));
        }

        [Test]
        public void ChangeState_FromDie_IsBlocked()
        {
            _stateMachine.ChangeState(StickmanBodyState.Die);
            _stateMachine.ChangeState(StickmanBodyState.Run);

            Assert.That(_stateMachine.CurrentState, Is.EqualTo(StickmanBodyState.Die));
        }

        [Test]
        public void OnStateChanged_FiresOnTransition()
        {
            int count = 0;
            _stateMachine.OnStateChanged += (_, __) => count++;

            _stateMachine.ChangeState(StickmanBodyState.Run);

            Assert.That(count, Is.EqualTo(1));
        }
    }
}
