using System;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SwedishPlanePowerUpView_V2 — Deploy one-shot (parachute open), Land one-shot (touchdown), then hold last frame.
     * Hides the Land slot during Deploy and the Deploy slot during Land / pickup idle.
     */
    public sealed class SwedishPlanePowerUpView_V2 : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [SerializeField] private string _deployAnim = "Deploy";
        [SerializeField] private string _landAnim = "Land";
        [SerializeField] private string _deploySlotName = "Deploy";
        [SerializeField] private string _landSlotName = "Land";
        [SerializeField] private string _landHoldAttachmentName = "land8";

        private IAircraftStateChangedSource_V2<SwedishPlanePowerUpState_V2> _stateMachine;
        private Action<SwedishPlanePowerUpState_V2, SwedishPlanePowerUpState_V2> _stateChangedHandler;
        private Action _deployClipCompleted;
        private Action _landClipCompleted;

        public SkeletonAnimation SkeletonAnimation => _skeletonAnimation;

        public event Action DeployClipCompleted
        {
            add => _deployClipCompleted += value;
            remove => _deployClipCompleted -= value;
        }

        public event Action LandClipCompleted
        {
            add => _landClipCompleted += value;
            remove => _landClipCompleted -= value;
        }

        public void Initialize(IAircraftStateChangedSource_V2<SwedishPlanePowerUpState_V2> stateMachine)
        {
            if (_stateMachine != null && _stateChangedHandler != null)
            {
                _stateMachine.OnStateChanged -= _stateChangedHandler;
            }

            _stateMachine = stateMachine;

            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponent<SkeletonAnimation>();
                if (_skeletonAnimation == null)
                {
                    _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
                }
            }

            _stateChangedHandler = HandleStateChanged;
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged += _stateChangedHandler;
            }

            PlayForState(_stateMachine != null ? _stateMachine.CurrentState : SwedishPlanePowerUpState_V2.Idle);
        }

        public void ResetVisualStateForSpawn()
        {
            ClearAnimationTracks();
            ApplySlotVisibility(showDeploy: false, showLand: false, presetLandAttachment: false);
        }

        private void OnDestroy()
        {
            if (_stateMachine != null && _stateChangedHandler != null)
            {
                _stateMachine.OnStateChanged -= _stateChangedHandler;
            }
        }

        private void HandleStateChanged(SwedishPlanePowerUpState_V2 from, SwedishPlanePowerUpState_V2 to)
        {
            PlayForState(to);
        }

        private void PlayForState(SwedishPlanePowerUpState_V2 state)
        {
            if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null)
            {
                return;
            }

            if (state == SwedishPlanePowerUpState_V2.Deploy && !string.IsNullOrWhiteSpace(_deployAnim))
            {
                ApplySlotVisibility(showDeploy: true, showLand: false, presetLandAttachment: false);
                TrackEntry entry = _skeletonAnimation.AnimationState.SetAnimation(0, _deployAnim, false);
                if (entry != null)
                {
                    entry.Complete -= HandleDeployClipComplete;
                    entry.Complete += HandleDeployClipComplete;
                }

                return;
            }

            if (state == SwedishPlanePowerUpState_V2.Land && !string.IsNullOrWhiteSpace(_landAnim))
            {
                ApplySlotVisibility(showDeploy: false, showLand: true, presetLandAttachment: false);
                TrackEntry entry = _skeletonAnimation.AnimationState.SetAnimation(0, _landAnim, false);
                if (entry != null)
                {
                    entry.Complete -= HandleLandClipComplete;
                    entry.Complete += HandleLandClipComplete;
                }

                return;
            }

            if (state == SwedishPlanePowerUpState_V2.PickedUp)
            {
                HoldLandPickupPose();
            }
        }

        public void HoldLandPickupPose()
        {
            ApplySlotVisibility(showDeploy: false, showLand: true, presetLandAttachment: true);
            ClearAnimationTracks();
        }

        // World position near the visible crate (Spine root is offset from gameplay ground).
        public Vector3 GetPickupWorldCenter()
        {
            if (_skeletonAnimation == null)
            {
                return transform.position;
            }

            _skeletonAnimation.LateUpdate();
            Skeleton skeleton = _skeletonAnimation.Skeleton;
            if (skeleton == null)
            {
                return transform.position;
            }

            skeleton.UpdateWorldTransform(Skeleton.Physics.Update);
            Bone rootBone = skeleton.RootBone;
            if (rootBone == null)
            {
                return transform.position;
            }

            Vector3 local = new Vector3(rootBone.WorldX, rootBone.WorldY, 0f);
            return _skeletonAnimation.transform.TransformPoint(local);
        }

        private void HandleDeployClipComplete(TrackEntry trackEntry)
        {
            if (trackEntry != null)
            {
                trackEntry.Complete -= HandleDeployClipComplete;
            }

            _deployClipCompleted?.Invoke();
        }

        private void HandleLandClipComplete(TrackEntry trackEntry)
        {
            if (trackEntry != null)
            {
                trackEntry.Complete -= HandleLandClipComplete;
            }

            HoldLandPickupPose();
            _landClipCompleted?.Invoke();
        }

        private void ApplySlotVisibility(bool showDeploy, bool showLand, bool presetLandAttachment)
        {
            if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null)
            {
                return;
            }

            Skeleton skeleton = _skeletonAnimation.Skeleton;
            skeleton.SetSlotsToSetupPose();

            if (!showDeploy && !string.IsNullOrWhiteSpace(_deploySlotName))
            {
                skeleton.SetAttachment(_deploySlotName, null);
            }

            if (!showLand && !string.IsNullOrWhiteSpace(_landSlotName))
            {
                skeleton.SetAttachment(_landSlotName, null);
            }
            else if (presetLandAttachment && showLand && !string.IsNullOrWhiteSpace(_landSlotName) &&
                     !string.IsNullOrWhiteSpace(_landHoldAttachmentName))
            {
                skeleton.SetAttachment(_landSlotName, _landHoldAttachmentName);
            }

            _skeletonAnimation.LateUpdate();
        }

        private void ClearAnimationTracks()
        {
            if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null)
            {
                return;
            }

            _skeletonAnimation.AnimationState.ClearTracks();
        }
    }
}
