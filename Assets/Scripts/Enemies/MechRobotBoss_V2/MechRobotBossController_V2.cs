using Assets.Scripts.Components;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class MechRobotBossController_V2 : MonoBehaviour
    {
        private MechRobotBossModel_V2 _model;
        private MechRobotBossStateMachine_V2 _stateMachine;
        private MechRobotBossWeaponSystem_V2 _weaponSystem;
        private Rigidbody2D _rigidbody2D;

        [Header("Movement")]
        [SerializeField] private float _walkSpeed = 2.1f;
        [SerializeField] private float _runSpeed = 4.2f;
        [SerializeField] private float _runEnterDistance = 9f;
        [Header("Combat")]
        [SerializeField] private float _attackMaxDistance = 14f;
        [Tooltip("When enabled, weapon system runs MG / cannon / missile loop; Aim↔Shoot follows telegraph + bursts.")]
        [SerializeField] private bool _useAttackPattern = true;
        [Header("Spine")]
        [Tooltip("When true, MP40-style: only deal damage while ShootStarted event keeps the window open. " +
                 "When false, fires on weapon cooldown for the whole Shoot state (works if skeleton lacks shoot events).")]
        [SerializeField] private bool _useSpineShootWindow;
        [Tooltip("When shoot events are off, time spent in Shoot before returning to Aim (lets non-loop shoot clip play).")]
        [SerializeField] private float _shootStateHoldSecondsNoEvents = 0.65f;

        private bool _shootWindowOpen;
        private bool _noEventShootCycleActive;
        private float _noEventShootEndTime;

        public void Initialize(
            MechRobotBossModel_V2 model,
            MechRobotBossStateMachine_V2 stateMachine,
            MechRobotBossWeaponSystem_V2 weaponSystem)
        {
            _model = model;
            _stateMachine = stateMachine;
            _weaponSystem = weaponSystem;
            if (_rigidbody2D == null)
            {
                _rigidbody2D = GetComponent<Rigidbody2D>();
            }

            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleMachineStateChanged;
                _stateMachine.OnStateChanged += HandleMachineStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleMachineStateChanged;
            }
        }

        private void HandleMachineStateChanged(MechRobotBossBodyState from, MechRobotBossBodyState to)
        {
            if (to == MechRobotBossBodyState.Shoot && !_useSpineShootWindow)
            {
                _noEventShootCycleActive = true;
                _noEventShootEndTime = Time.time + Mathf.Max(0.12f, _shootStateHoldSecondsNoEvents);
            }
            else if (to != MechRobotBossBodyState.Shoot)
            {
                _noEventShootCycleActive = false;
            }
        }

        public void StartGame()
        {
            _shootWindowOpen = false;
            _noEventShootCycleActive = false;
        }

        public void ResetForSpawn()
        {
            _shootWindowOpen = false;
            _noEventShootCycleActive = false;
        }

        public void OnAnimationEvent(AnimationEventType eventName)
        {
            if (_stateMachine == null || _model == null || _model.IsDead())
            {
                return;
            }

            if (_useAttackPattern &&
                _weaponSystem != null &&
                _weaponSystem.AttackPatternEnabled &&
                eventName == AnimationEventType.ShootFinished)
            {
                return;
            }

            switch (eventName)
            {
                case AnimationEventType.ShootStarted:
                    if (_stateMachine.CurrentState == MechRobotBossBodyState.Shoot)
                    {
                        _shootWindowOpen = true;
                    }

                    break;
                case AnimationEventType.ShootFinished:
                    _shootWindowOpen = false;
                    if (_stateMachine.CurrentState == MechRobotBossBodyState.Shoot && !_model.IsDead())
                    {
                        _stateMachine.ChangeState(MechRobotBossBodyState.Aim);
                    }

                    break;
            }
        }

        private void FixedUpdate()
        {
            if (_model == null || _stateMachine == null || _weaponSystem == null)
            {
                return;
            }

            if (_model.IsDead() || _stateMachine.CurrentState == MechRobotBossBodyState.Die)
            {
                if (_rigidbody2D != null)
                {
                    _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
                }

                return;
            }

            Hero_V2 hero = FindAnyObjectByType<Hero_V2>();
            if (hero == null)
            {
                _stateMachine.ChangeState(MechRobotBossBodyState.Idle);
                StopHorizontal();
                return;
            }

            Vector2 heroPos = hero.transform.position;
            Vector2 pos = transform.position;
            float dx = heroPos.x - pos.x;
            FaceToward(dx);

            float absDx = Mathf.Abs(dx);
            bool inCombatRange = absDx <= _attackMaxDistance;

            if (_weaponSystem != null && _useAttackPattern && _weaponSystem.AttackPatternEnabled)
            {
                _weaponSystem.TickAttackPattern(inCombatRange);
            }

            if (_noEventShootCycleActive &&
                !_useSpineShootWindow &&
                _stateMachine.CurrentState == MechRobotBossBodyState.Shoot &&
                Time.time >= _noEventShootEndTime)
            {
                _noEventShootCycleActive = false;
                _stateMachine.ChangeState(MechRobotBossBodyState.Aim);
            }

            bool mayFire =
                !_useSpineShootWindow
                    ? _stateMachine.CurrentState == MechRobotBossBodyState.Shoot
                    : _shootWindowOpen && _stateMachine.CurrentState == MechRobotBossBodyState.Shoot;

            if (_weaponSystem != null && _useAttackPattern && _weaponSystem.AttackPatternEnabled && inCombatRange)
            {
                bool wantShoot = _weaponSystem.ShouldUseShootTrack;
                if (wantShoot && _stateMachine.CurrentState != MechRobotBossBodyState.Shoot)
                {
                    _stateMachine.ChangeState(MechRobotBossBodyState.Shoot);
                }
                else if (!wantShoot && _stateMachine.CurrentState == MechRobotBossBodyState.Shoot)
                {
                    _stateMachine.ChangeState(MechRobotBossBodyState.Aim);
                }

                StopHorizontal();
                return;
            }

            if (mayFire)
            {
                _weaponSystem.TryAutoShootAtHero();
                StopHorizontal();
                return;
            }

            if (inCombatRange && _weaponSystem.CanShoot())
            {
                if (_stateMachine.CurrentState != MechRobotBossBodyState.Shoot)
                {
                    if (_stateMachine.CurrentState != MechRobotBossBodyState.Aim)
                    {
                        _stateMachine.ChangeState(MechRobotBossBodyState.Aim);
                    }
                    else
                    {
                        _stateMachine.ChangeState(MechRobotBossBodyState.Shoot);
                    }
                }

                StopHorizontal();
                return;
            }

            if (inCombatRange)
            {
                StopHorizontal();
                _stateMachine.ChangeState(MechRobotBossBodyState.Aim);
                return;
            }

            float speed = absDx >= _runEnterDistance ? _runSpeed : _walkSpeed;
            float dir = dx >= 0f ? 1f : -1f;
            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = new Vector2(dir * speed, _rigidbody2D.linearVelocity.y);
            }
            else
            {
                transform.position += new Vector3(dir * speed * Time.fixedDeltaTime, 0f, 0f);
            }

            _stateMachine.ChangeState(absDx >= _runEnterDistance ? MechRobotBossBodyState.Run : MechRobotBossBodyState.Walk);
        }

        private void StopHorizontal()
        {
            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
            }
        }

        private void FaceToward(float deltaXFromHero)
        {
            if (Mathf.Abs(deltaXFromHero) < 0.05f)
            {
                return;
            }

            bool faceRight = deltaXFromHero > 0f;
            Vector3 s = transform.localScale;
            float ax = Mathf.Abs(s.x);
            s.x = faceRight ? ax : -ax;
            transform.localScale = s;
        }
    }
}
