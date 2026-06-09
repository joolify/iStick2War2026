using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SwedishPlanePowerUp_V2 (Survival parachute drop — composition root)
     *
     * NAVIGATION: SwedishPlaneController_V2.cs → SwedishPlanePowerUpController_V2.cs
     */
    [DisallowMultipleComponent]
    [AddComponentMenu("iStick2War/Swedish Plane PowerUp V2")]
    public sealed class SwedishPlanePowerUp_V2 : MonoBehaviour
    {
        private SwedishPlanePowerUpModel_V2 _model;
        private SwedishPlanePowerUpStateMachine_V2 _stateMachine;
        private SwedishPlanePowerUpController_V2 _controller;
        private SwedishPlanePowerUpView_V2 _view;
        private SwedishPlanePowerUpRewardPreview_V2 _rewardPreview;
        private bool _initialized;

        public void InitializeForSpawn()
        {
            EnsureReferences();
            _stateMachine.Initialize(_model);
            _controller.Initialize(_model, _stateMachine, _view, _rewardPreview);

            SkeletonAnimation skeletonAnimation = _view != null ? _view.SkeletonAnimation : null;
            ForceSpineMeshRebuild(skeletonAnimation);

            _view.Initialize(_stateMachine);
            _view.ResetVisualStateForSpawn();
            _rewardPreview?.ClearForSpawn();
            _initialized = true;
        }

        public void BeginDrop(SurvivalPowerUpOffer_V2 offer)
        {
            BeginDrop(offer, null, null);
        }

        public void BeginDrop(SurvivalPowerUpOffer_V2 offer, WaveManager_V2 waveManager, Hero_V2 hero)
        {
            EnsureReferences();
            if (!_initialized)
            {
                InitializeForSpawn();
            }

            _controller.BeginDrop(offer, waveManager, hero);
        }

        public bool IsReadyForHeroPickup()
        {
            EnsureReferences();
            return _model != null &&
                   _model.pickupEnabled &&
                   _stateMachine != null &&
                   _stateMachine.CurrentState == SwedishPlanePowerUpState_V2.Land;
        }

        public Vector3 GetPickupWorldCenter()
        {
            EnsureReferences();
            return _view != null ? _view.GetPickupWorldCenter() : transform.position;
        }

        private void EnsureReferences()
        {
            _model = GetComponent<SwedishPlanePowerUpModel_V2>();
            if (_model == null)
            {
                _model = gameObject.AddComponent<SwedishPlanePowerUpModel_V2>();
            }

            _stateMachine = GetComponent<SwedishPlanePowerUpStateMachine_V2>();
            if (_stateMachine == null)
            {
                _stateMachine = gameObject.AddComponent<SwedishPlanePowerUpStateMachine_V2>();
            }

            _controller = GetComponent<SwedishPlanePowerUpController_V2>();
            if (_controller == null)
            {
                _controller = gameObject.AddComponent<SwedishPlanePowerUpController_V2>();
            }

            _view = GetComponent<SwedishPlanePowerUpView_V2>();
            if (_view == null)
            {
                _view = GetComponentInChildren<SwedishPlanePowerUpView_V2>(true);
            }

            if (_view == null)
            {
                _view = gameObject.AddComponent<SwedishPlanePowerUpView_V2>();
            }

            _rewardPreview = GetComponentInChildren<SwedishPlanePowerUpRewardPreview_V2>(true);
            if (_rewardPreview == null)
            {
                _rewardPreview = GetComponent<SwedishPlanePowerUpRewardPreview_V2>();
            }

            if (_initialized && _controller != null)
            {
                _controller.BindRewardPreview(_rewardPreview);
            }
        }

        private static void ForceSpineMeshRebuild(SkeletonAnimation skeletonAnimation)
        {
            if (skeletonAnimation == null)
            {
                return;
            }

            skeletonAnimation.Initialize(overwrite: true);
            MeshRenderer meshRenderer = skeletonAnimation.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }

            skeletonAnimation.UpdateMode = UpdateMode.FullUpdate;
            skeletonAnimation.Update(0f);
            skeletonAnimation.LateUpdate();
        }
    }
}
