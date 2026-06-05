using Assets.Scripts.Components;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * MechRobotBossController_V2 (Critical Component / Brain)
 *
 * PURPOSE:
 * Drives mech boss gameplay each frame: walk/run toward the hero, enter Aim/Shoot when in attack range,
 * coordinates with MechRobotBossWeaponSystem_V2 for MG / cannon / missile attack patterns, and reacts to
 * Spine shoot-window events from MechRobotBossSpineEventForwarder_V2 when enabled.
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLES
 *
 * - Reads MechRobotBossModel_V2 and drives MechRobotBossStateMachine_V2 for View sync
 * - Uses Rigidbody2D (self or child) for horizontal movement; does not replace composition root wiring
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Act as composition root (MechRobotBoss / MechRobotBoss_V2.cs owns bootstrap)
 * - Play Spine clips directly (MechRobotBossView_V2 owns track selection)
 * - Apply raw damage to the model without going through MechRobotBossDamageReceiver_V2 / body parts
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Same separation as BombPlaneController_V2: orchestration and rules live here; presentation and hit pipeline live elsewhere.
 */
    public sealed class MechRobotBossController_V2 : MonoBehaviour
    {
        private MechRobotBossModel_V2 _model;
        private MechRobotBossStateMachine_V2 _stateMachine;
        private MechRobotBossWeaponSystem_V2 _weaponSystem;
        private Rigidbody2D _rigidbody2D;
        private SkeletonAnimation _skeletonAnimation;

        [Header("Movement")]
        [SerializeField] private float _walkSpeed = 2.1f;
        [SerializeField] private float _runSpeed = 4.2f;
        [SerializeField] private float _runEnterDistance = 9f;
        [Header("Combat")]
        [SerializeField] private float _attackMaxDistance = 14f;
        [Tooltip("Prevents off-screen ground spawns from starting their attack loop before entering the Game view.")]
        [SerializeField] private bool _requireInsideCameraBeforeCombat = true;
        [Tooltip("Extra inset from the camera left/right edge before the mech is allowed to stop and attack.")]
        [SerializeField] private float _cameraCombatEntryInsetWorld = 0.35f;
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
                if (_rigidbody2D == null)
                {
                    _rigidbody2D = GetComponentInChildren<Rigidbody2D>(true);
                }
            }

            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
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
                    _rigidbody2D.linearVelocity = Vector2.zero;
                    _rigidbody2D.angularVelocity = 0f;
                }

                return;
            }

            StabilizeBossPhysicsPose();

            Hero_V2 hero = FindAnyObjectByType<Hero_V2>();
            if (hero == null)
            {
                _stateMachine.ChangeState(MechRobotBossBodyState.Idle);
                StopHorizontal();
                return;
            }

            Vector2 heroPos = hero.transform.position;
            Vector2 pos = _rigidbody2D != null ? _rigidbody2D.position : (Vector2)transform.position;
            float dx = heroPos.x - pos.x;
            FaceToward(dx);

            float absDx = Mathf.Abs(dx);
            bool inCombatRange = absDx <= _attackMaxDistance && IsInsideCameraCombatBand(pos);

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
                inCombatRange &&
                (!_useSpineShootWindow
                    ? _stateMachine.CurrentState == MechRobotBossBodyState.Shoot
                    : _shootWindowOpen && _stateMachine.CurrentState == MechRobotBossBodyState.Shoot);

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
                _rigidbody2D.linearVelocity = new Vector2(dir * speed, 0f);
            }
            else
            {
                transform.position += new Vector3(dir * speed * Time.fixedDeltaTime, 0f, 0f);
            }

            _stateMachine.ChangeState(absDx >= _runEnterDistance ? MechRobotBossBodyState.Run : MechRobotBossBodyState.Walk);
        }

        private bool IsInsideCameraCombatBand(Vector2 position)
        {
            if (!_requireInsideCameraBeforeCombat)
            {
                return true;
            }

            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return true;
            }

            float halfWidth = cam.orthographicSize * cam.aspect;
            float inset = Mathf.Max(0f, _cameraCombatEntryInsetWorld);
            float minX = cam.transform.position.x - halfWidth + inset;
            float maxX = cam.transform.position.x + halfWidth - inset;
            return position.x >= minX && position.x <= maxX;
        }

        private void StopHorizontal()
        {
            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.angularVelocity = 0f;
            }
        }

        private void StabilizeBossPhysicsPose()
        {
            if (_rigidbody2D == null)
            {
                return;
            }

            _rigidbody2D.angularVelocity = 0f;
            _rigidbody2D.rotation = 0f;
            Vector2 velocity = _rigidbody2D.linearVelocity;
            if (Mathf.Abs(velocity.y) > 0.0001f)
            {
                _rigidbody2D.linearVelocity = new Vector2(velocity.x, 0f);
            }
        }

        private void FaceToward(float deltaXFromHero)
        {
            if (Mathf.Abs(deltaXFromHero) < 0.05f)
            {
                return;
            }

            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
            }

            if (_skeletonAnimation != null && _skeletonAnimation.Skeleton != null)
            {
                bool faceRight = deltaXFromHero > 0f;
                float absScale = Mathf.Abs(_skeletonAnimation.Skeleton.ScaleX);
                if (absScale < 0.001f)
                {
                    absScale = 1f;
                }

                _skeletonAnimation.Skeleton.ScaleX = faceRight ? absScale : -absScale;
                return;
            }

            // Fallback only when skeleton is missing: keep root scale positive to avoid child RB mirror bugs.
            bool faceRightFallback = deltaXFromHero > 0f;
            Vector3 s = transform.localScale;
            float ax = Mathf.Abs(s.x);
            s.x = faceRightFallback ? ax : -ax;
            transform.localScale = s;
        }
    }
}
