using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class MechRobotBossView_V2 : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [SerializeField] private string _idleAnim = "idle";
        [SerializeField] private string _walkAnim = "walk";
        [SerializeField] private string _runAnim = "run";
        [SerializeField] private string _aimAnim = "aim";
        [SerializeField] private string _shootAnim = "shoot";
        [Tooltip("Optional Spine clips for attack pattern (leave empty to use shoot for all).")]
        [SerializeField] private string _shootMachineGunAnim;
        [SerializeField] private string _shootCannonAnim;
        [SerializeField] private string _shootMissileAnim;
        [SerializeField] private MechRobotBossWeaponSystem_V2 _weaponSystem;
        [Tooltip("Optional: leave empty if skeleton has no dedicated death clip (boss despawns after delay).")]
        [SerializeField] private string _dieAnim = "";

        private MechRobotBossStateMachine_V2 _stateMachine;

        public void Initialize(MechRobotBossStateMachine_V2 stateMachine)
        {
            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponent<SkeletonAnimation>();
                if (_skeletonAnimation == null)
                {
                    _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
                }
            }

            if (_weaponSystem == null)
            {
                _weaponSystem = GetComponent<MechRobotBossWeaponSystem_V2>();
                if (_weaponSystem == null)
                {
                    _weaponSystem = GetComponentInParent<MechRobotBossWeaponSystem_V2>();
                }
            }

            _stateMachine = stateMachine;
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
                _stateMachine.OnStateChanged += HandleStateChanged;
            }

            PlayForState(_stateMachine != null ? _stateMachine.CurrentState : MechRobotBossBodyState.Idle);
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
            }
        }

        public void ResetVisualStateForSpawn()
        {
            if (_skeletonAnimation != null && _skeletonAnimation.AnimationState != null)
            {
                PlayForState(MechRobotBossBodyState.Idle);
            }
        }

        private void HandleStateChanged(MechRobotBossBodyState from, MechRobotBossBodyState to)
        {
            PlayForState(to);
        }

        private void PlayForState(MechRobotBossBodyState state)
        {
            if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null)
            {
                return;
            }

            string name = ResolveAnimationNameForPlayback(state);
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            bool loop = state == MechRobotBossBodyState.Idle ||
                        state == MechRobotBossBodyState.Walk ||
                        state == MechRobotBossBodyState.Run ||
                        state == MechRobotBossBodyState.Aim;

            TrackEntry entry = _skeletonAnimation.AnimationState.SetAnimation(0, name, loop);
            if (entry != null && (state == MechRobotBossBodyState.Shoot || state == MechRobotBossBodyState.Aim))
            {
                entry.ResetRotationDirections();
            }
        }

        private string ResolveAnimationNameForPlayback(MechRobotBossBodyState state)
        {
            if (state == MechRobotBossBodyState.Shoot)
            {
                return ResolveShootAnimationName();
            }

            return ResolveLocomotionOrAimName(state);
        }

        private string ResolveShootAnimationName()
        {
            if (_weaponSystem == null || !_weaponSystem.AttackPatternEnabled)
            {
                return _shootAnim;
            }

            switch (_weaponSystem.CurrentShootPresentation)
            {
                case MechRobotBossShootPresentation.MachineGun:
                    return !string.IsNullOrEmpty(_shootMachineGunAnim) ? _shootMachineGunAnim : _shootAnim;
                case MechRobotBossShootPresentation.Cannon:
                    return !string.IsNullOrEmpty(_shootCannonAnim) ? _shootCannonAnim : _shootAnim;
                case MechRobotBossShootPresentation.Missile:
                    return !string.IsNullOrEmpty(_shootMissileAnim) ? _shootMissileAnim : _shootAnim;
                default:
                    return _shootAnim;
            }
        }

        private string ResolveLocomotionOrAimName(MechRobotBossBodyState state)
        {
            switch (state)
            {
                case MechRobotBossBodyState.Idle:
                    return _idleAnim;
                case MechRobotBossBodyState.Walk:
                    return _walkAnim;
                case MechRobotBossBodyState.Run:
                    return _runAnim;
                case MechRobotBossBodyState.Aim:
                    return _aimAnim;
                case MechRobotBossBodyState.Shoot:
                    return _shootAnim;
                case MechRobotBossBodyState.Die:
                    return string.IsNullOrEmpty(_dieAnim) ? _idleAnim : _dieAnim;
                default:
                    return _idleAnim;
            }
        }
    }
}
