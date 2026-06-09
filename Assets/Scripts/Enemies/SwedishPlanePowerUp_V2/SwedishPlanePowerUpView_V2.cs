using System;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SwedishPlanePowerUpView_V2 — Deploy one-shot (parachute open), Land one-shot (touchdown), then pickup idle.
     * Hides Deploy during Land; hides Land after Land completes; pickup idle uses powerup_no_parachute.
     * Deploy + Land (until clip end) use powerup-slot attachment "powerup"; Land clip switches to no_parachute on last frames.
     */
    public sealed class SwedishPlanePowerUpView_V2 : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [SerializeField] private string _deployAnim = "Deploy";
        [SerializeField] private string _landAnim = "Land";
        [SerializeField] private string _deploySlotName = "Deploy";
        [SerializeField] private string _landSlotName = "Land";
        [SerializeField] private string _powerupSlotName = "powerup";
        [SerializeField] private string _powerupParachuteAttachmentName = "powerup";
        [SerializeField] private string _powerupIdleAttachmentName = "powerup_no_parachute";
        [SerializeField] private string _pickupBoundingBoxSlotName = "powerup-bb";

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
            ApplySlotVisibility(showDeploy: false, showLand: false);
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
                ApplySlotVisibility(showDeploy: true, showLand: false);
                TrackEntry entry = _skeletonAnimation.AnimationState.SetAnimation(0, _deployAnim, false);
                ApplyPowerupParachuteAttachment();
                if (entry != null)
                {
                    entry.Complete -= HandleDeployClipComplete;
                    entry.Complete += HandleDeployClipComplete;
                }

                return;
            }

            if (state == SwedishPlanePowerUpState_V2.Land && !string.IsNullOrWhiteSpace(_landAnim))
            {
                ApplySlotVisibility(showDeploy: false, showLand: true);
                TrackEntry entry = _skeletonAnimation.AnimationState.SetAnimation(0, _landAnim, false);
                ApplyPowerupParachuteAttachment();
                if (entry != null)
                {
                    entry.Complete -= HandleLandClipComplete;
                    entry.Complete += HandleLandClipComplete;
                }

                return;
            }

            if (state == SwedishPlanePowerUpState_V2.PickedUp)
            {
                ShowPickupIdlePose();
            }
        }

        public void ShowPickupIdlePose()
        {
            ApplySlotVisibility(showDeploy: false, showLand: false);
            ApplyPowerupIdleAttachment();
            ClearAnimationTracks();
        }

        // Deploy slots visible for bbox sampling before Deploy clip starts (BeginDrop).
        public void PrepareDeployPoseForLandingSample()
        {
            ApplySlotVisibility(showDeploy: true, showLand: false);
            ApplyPowerupParachuteAttachment();
        }

        // Read from controller LateUpdate; do not call SkeletonAnimation.LateUpdate here (double-advance breaks sync).
        public float GetDeployClipProgress01()
        {
            if (_skeletonAnimation == null)
            {
                return 0f;
            }

            if (_skeletonAnimation.AnimationState == null)
            {
                return 0f;
            }

            TrackEntry entry = _skeletonAnimation.AnimationState.GetCurrent(0);
            if (entry == null || entry.Animation == null)
            {
                return 0f;
            }

            if (!string.IsNullOrWhiteSpace(_deployAnim) && entry.Animation.Name != _deployAnim)
            {
                return 0f;
            }

            float duration = entry.Animation.Duration;
            if (duration <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(entry.TrackTime / duration);
        }

        public float GetDeployClipDurationSeconds()
        {
            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponent<SkeletonAnimation>();
                if (_skeletonAnimation == null)
                {
                    _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
                }
            }

            if (_skeletonAnimation == null || _skeletonAnimation.SkeletonDataAsset == null)
            {
                return 1.4f;
            }

            SkeletonData data = _skeletonAnimation.SkeletonDataAsset.GetSkeletonData(false);
            if (data == null || string.IsNullOrWhiteSpace(_deployAnim))
            {
                return 1.4f;
            }

            Spine.Animation deploy = data.FindAnimation(_deployAnim);
            return deploy != null && deploy.Duration > 0f ? deploy.Duration : 1.4f;
        }

        public bool IsCrateBottomNearSurface(
            float surfaceWorldY,
            Collider2D[] pickupColliders,
            bool useIdlePoseForMeasure,
            float epsilon = 0.05f)
        {
            ApplyPowerupAttachmentForMeasure(useIdlePoseForMeasure);

            _skeletonAnimation?.LateUpdate();
            Physics2D.SyncTransforms();

            if (!TryGetMeasureBottomWorldY(pickupColliders, useIdlePoseForMeasure, out float bottomWorldY))
            {
                return false;
            }

            return bottomWorldY >= surfaceWorldY - Mathf.Max(0.01f, epsilon) &&
                   bottomWorldY <= surfaceWorldY + 0.35f;
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

        // Align crate bottom to probed ground: landingRootY = surfaceY - (bottomY - rootY).
        public bool TrySampleLandingRootY(
            Transform compositionRoot,
            float probedSurfaceY,
            Collider2D[] pickupColliders,
            out float landingRootY,
            bool useIdlePoseForMeasure = true)
        {
            landingRootY = probedSurfaceY;
            if (compositionRoot == null || _skeletonAnimation == null)
            {
                return false;
            }

            ApplyPowerupAttachmentForMeasure(useIdlePoseForMeasure);

            _skeletonAnimation.LateUpdate();

            if (!TryGetMeasureBottomWorldY(pickupColliders, useIdlePoseForMeasure, out float bottomWorldY))
            {
                return false;
            }

            float bottomOffsetFromRoot = bottomWorldY - compositionRoot.position.y;
            landingRootY = probedSurfaceY - bottomOffsetFromRoot;

            // Never place the crate bottom below the probed ground surface.
            float predictedBottom = landingRootY + bottomOffsetFromRoot;
            if (predictedBottom < probedSurfaceY)
            {
                landingRootY += probedSurfaceY - predictedBottom;
            }

            return true;
        }

        // Lowest world Y for deploy descent: pickup bbox plus live Deploy parachute attachment (extends below powerup-bb).
        public bool TryGetMinimumRootYForDescentVisualOnSurface(
            Transform compositionRoot,
            float surfaceWorldY,
            Collider2D[] pickupColliders,
            out float minimumRootY)
        {
            minimumRootY = compositionRoot != null ? compositionRoot.position.y : 0f;
            if (compositionRoot == null || _skeletonAnimation == null)
            {
                return false;
            }

            _skeletonAnimation.LateUpdate();
            Physics2D.SyncTransforms();

            if (!TryGetDescentVisualBottomWorldY(pickupColliders, out float bottomWorldY))
            {
                return false;
            }

            float liftNeeded = surfaceWorldY - bottomWorldY;
            minimumRootY = liftNeeded > 0f
                ? compositionRoot.position.y + liftNeeded
                : compositionRoot.position.y;
            return true;
        }

        public void AlignRootSoCrateBottomOnSurface(
            Transform compositionRoot,
            float surfaceWorldY,
            Collider2D[] pickupColliders,
            bool useIdlePoseForMeasure = true)
        {
            if (compositionRoot == null || _skeletonAnimation == null)
            {
                return;
            }

            ApplyPowerupAttachmentForMeasure(useIdlePoseForMeasure);

            for (int i = 0; i < 12; i++)
            {
                _skeletonAnimation.LateUpdate();
                Physics2D.SyncTransforms();

                if (!TryGetMeasureBottomWorldY(pickupColliders, useIdlePoseForMeasure, out float bottomWorldY))
                {
                    return;
                }

                float lift = surfaceWorldY - bottomWorldY;
                if (Mathf.Abs(lift) < 0.015f)
                {
                    break;
                }

                compositionRoot.position += new Vector3(0f, lift, 0f);
            }
        }

        // Single-step lift when the measured crate bottom is below the probed ground surface.
        public bool TryLiftRootSoBottomOnSurface(
            Transform compositionRoot,
            float surfaceWorldY,
            Collider2D[] pickupColliders,
            bool useIdlePoseForMeasure,
            float epsilon = 0.02f)
        {
            if (compositionRoot == null || _skeletonAnimation == null)
            {
                return false;
            }

            ApplyPowerupAttachmentForMeasure(useIdlePoseForMeasure);

            _skeletonAnimation.LateUpdate();
            Physics2D.SyncTransforms();

            if (!TryGetMeasureBottomWorldY(pickupColliders, useIdlePoseForMeasure, out float bottomWorldY))
            {
                return false;
            }

            if (bottomWorldY >= surfaceWorldY - Mathf.Max(0.01f, epsilon))
            {
                return false;
            }

            compositionRoot.position += new Vector3(0f, surfaceWorldY - bottomWorldY, 0f);
            return true;
        }

        private void ApplyPowerupAttachmentForMeasure(bool useIdlePoseForMeasure)
        {
            if (useIdlePoseForMeasure)
            {
                ApplyPowerupIdleAttachment();
                return;
            }

            ApplyPowerupParachuteAttachment();
        }

        private void ApplyPowerupParachuteAttachment()
        {
            if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_powerupSlotName) || string.IsNullOrWhiteSpace(_powerupParachuteAttachmentName))
            {
                return;
            }

            _skeletonAnimation.Skeleton.SetAttachment(_powerupSlotName, _powerupParachuteAttachmentName);
            _skeletonAnimation.LateUpdate();
        }

        private bool TryGetMeasureBottomWorldY(
            Collider2D[] pickupColliders,
            bool useIdlePoseForMeasure,
            out float bottomWorldY)
        {
            return useIdlePoseForMeasure
                ? TryGetCrateBottomWorldY(pickupColliders, out bottomWorldY)
                : TryGetDescentVisualBottomWorldY(pickupColliders, out bottomWorldY);
        }

        private bool TryGetDescentVisualBottomWorldY(Collider2D[] pickupColliders, out float bottomWorldY)
        {
            bottomWorldY = 0f;
            bool hasBottom = false;

            if (TryGetCrateBottomWorldY(pickupColliders, out float crateBottom))
            {
                bottomWorldY = crateBottom;
                hasBottom = true;
            }

            if (TryGetSlotAttachmentBottomWorldY(_deploySlotName, out float deployBottom) &&
                (!hasBottom || deployBottom < bottomWorldY))
            {
                bottomWorldY = deployBottom;
                hasBottom = true;
            }

            return hasBottom;
        }

        private bool TryGetSlotAttachmentBottomWorldY(string slotName, out float bottomWorldY)
        {
            bottomWorldY = 0f;
            Skeleton skeleton = _skeletonAnimation != null ? _skeletonAnimation.Skeleton : null;
            if (skeleton == null || string.IsNullOrWhiteSpace(slotName))
            {
                return false;
            }

            skeleton.UpdateWorldTransform(Skeleton.Physics.Update);
            Slot slot = skeleton.FindSlot(slotName);
            if (slot == null || slot.Attachment == null)
            {
                return false;
            }

            Transform skeletonTransform = _skeletonAnimation.transform;
            float minY = float.MaxValue;

            if (slot.Attachment is RegionAttachment regionAttachment)
            {
                var worldVertices = new float[8];
                regionAttachment.ComputeWorldVertices(slot, worldVertices, 0, 2);
                for (int i = 0; i < worldVertices.Length; i += 2)
                {
                    Vector3 worldPoint = skeletonTransform.TransformPoint(
                        new Vector3(worldVertices[i], worldVertices[i + 1], 0f));
                    minY = Mathf.Min(minY, worldPoint.y);
                }
            }
            else if (slot.Attachment is MeshAttachment meshAttachment)
            {
                int vertexCount = meshAttachment.WorldVerticesLength;
                var worldVertices = new float[vertexCount];
                meshAttachment.ComputeWorldVertices(slot, 0, vertexCount, worldVertices, 0, 2);
                for (int i = 0; i < worldVertices.Length; i += 2)
                {
                    Vector3 worldPoint = skeletonTransform.TransformPoint(
                        new Vector3(worldVertices[i], worldVertices[i + 1], 0f));
                    minY = Mathf.Min(minY, worldPoint.y);
                }
            }
            else if (slot.Attachment is BoundingBoxAttachment boundingBox)
            {
                var worldVertices = boundingBox.GetWorldVertices(slot, null);
                for (int i = 0; i < worldVertices.Length; i++)
                {
                    Vector3 worldPoint = skeletonTransform.TransformPoint(
                        new Vector3(worldVertices[i].x, worldVertices[i].y, 0f));
                    minY = Mathf.Min(minY, worldPoint.y);
                }
            }
            else
            {
                return false;
            }

            if (minY >= float.MaxValue)
            {
                return false;
            }

            bottomWorldY = minY;
            return true;
        }

        private bool TryGetCrateBottomWorldY(Collider2D[] pickupColliders, out float bottomWorldY)
        {
            bottomWorldY = 0f;
            bool hasBottom = false;

            if (pickupColliders != null)
            {
                for (int i = 0; i < pickupColliders.Length; i++)
                {
                    Collider2D pickupCollider = pickupColliders[i];
                    if (pickupCollider == null || !pickupCollider.enabled)
                    {
                        continue;
                    }

                    float minY = pickupCollider.bounds.min.y;
                    if (!hasBottom || minY < bottomWorldY)
                    {
                        bottomWorldY = minY;
                        hasBottom = true;
                    }
                }
            }

            if (hasBottom)
            {
                return true;
            }

            return TryGetBoundingBoxBottomWorldY(out bottomWorldY);
        }

        private bool TryGetBoundingBoxBottomWorldY(out float bottomWorldY)
        {
            bottomWorldY = 0f;
            Skeleton skeleton = _skeletonAnimation != null ? _skeletonAnimation.Skeleton : null;
            if (skeleton == null || string.IsNullOrWhiteSpace(_pickupBoundingBoxSlotName))
            {
                return false;
            }

            skeleton.UpdateWorldTransform(Skeleton.Physics.Update);
            Slot slot = skeleton.FindSlot(_pickupBoundingBoxSlotName);
            if (slot == null || slot.Attachment is not BoundingBoxAttachment boundingBox)
            {
                return false;
            }

            var worldVertices = boundingBox.GetWorldVertices(slot, null);
            Transform skeletonTransform = _skeletonAnimation.transform;
            float minY = float.MaxValue;
            for (int i = 0; i < worldVertices.Length; i++)
            {
                Vector3 worldPoint = skeletonTransform.TransformPoint(
                    new Vector3(worldVertices[i].x, worldVertices[i].y, 0f));
                minY = Mathf.Min(minY, worldPoint.y);
            }

            if (minY >= float.MaxValue)
            {
                return false;
            }

            bottomWorldY = minY;
            return true;
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

            ShowPickupIdlePose();
            _landClipCompleted?.Invoke();
        }

        private void ApplyPowerupIdleAttachment()
        {
            if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_powerupSlotName) || string.IsNullOrWhiteSpace(_powerupIdleAttachmentName))
            {
                return;
            }

            _skeletonAnimation.Skeleton.SetAttachment(_powerupSlotName, _powerupIdleAttachmentName);
            _skeletonAnimation.LateUpdate();
        }

        private void ApplySlotVisibility(bool showDeploy, bool showLand)
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
