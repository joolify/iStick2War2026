using iStick2War_V2;
using NUnit.Framework;

namespace iStick2War.Tests.EditMode
{
    public sealed class HeroStateMachine_V2Tests
    {
        private HeroStateMachine_V2 _sm;

        [SetUp]
        public void SetUp()
        {
            _sm = new HeroStateMachine_V2();
        }

        [Test]
        public void StartsInIdle()
        {
            Assert.That(_sm.CurrentState, Is.EqualTo(HeroState.Idle));
        }

        [Test]
        public void ChangeState_UpdatesCurrentState()
        {
            _sm.ChangeState(HeroState.Moving);

            Assert.That(_sm.CurrentState, Is.EqualTo(HeroState.Moving));
        }

        [Test]
        public void ChangeState_SameState_DoesNothing()
        {
            int fired = 0;
            _sm.OnStateChanged += (_, __) => fired++;

            _sm.ChangeState(HeroState.Idle);

            Assert.That(fired, Is.EqualTo(0));
            Assert.That(_sm.CurrentState, Is.EqualTo(HeroState.Idle));
        }

        [Test]
        public void ChangeState_FromDead_IsBlocked()
        {
            _sm.ChangeState(HeroState.Dead);
            _sm.ChangeState(HeroState.Moving);

            Assert.That(_sm.CurrentState, Is.EqualTo(HeroState.Dead));
        }

        [Test]
        public void ChangeState_ToDead_IsAlwaysAllowed()
        {
            _sm.ChangeState(HeroState.Reloading);
            _sm.ChangeState(HeroState.Dead);

            Assert.That(_sm.CurrentState, Is.EqualTo(HeroState.Dead));
        }

        [Test]
        public void ChangeState_ReloadingToShooting_IsBlocked()
        {
            _sm.ChangeState(HeroState.Reloading);
            _sm.ChangeState(HeroState.Shooting);

            Assert.That(_sm.CurrentState, Is.EqualTo(HeroState.Reloading));
        }

        [Test]
        public void ChangeState_ShootingToReloading_IsAllowed()
        {
            _sm.ChangeState(HeroState.Shooting);
            _sm.ChangeState(HeroState.Reloading);

            Assert.That(_sm.CurrentState, Is.EqualTo(HeroState.Reloading));
        }

        [Test]
        public void ForceState_BypassesTransitionRules()
        {
            _sm.ChangeState(HeroState.Dead);
            _sm.ForceState(HeroState.Idle);

            Assert.That(_sm.CurrentState, Is.EqualTo(HeroState.Idle));
        }

        [Test]
        public void OnStateChanged_FiresWithPreviousAndNew()
        {
            HeroState? prev = null;
            HeroState? next = null;
            _sm.OnStateChanged += (from, to) =>
            {
                prev = from;
                next = to;
            };

            _sm.ChangeState(HeroState.Jumping);

            Assert.That(prev, Is.EqualTo(HeroState.Idle));
            Assert.That(next, Is.EqualTo(HeroState.Jumping));
        }
    }
}
