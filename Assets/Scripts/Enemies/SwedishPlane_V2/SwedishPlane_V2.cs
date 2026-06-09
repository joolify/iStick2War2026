using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SwedishPlane_V2 (neutral Survival supply aircraft — composition root)
     *
     * Friendly fly-by that drops parachute powerups instead of bombs. No AircraftHealth_V2.
     * NAVIGATION: SwedishPlaneSurvivalCoordinator_V2.cs → SwedishPlaneController_V2.cs
     */
    [DisallowMultipleComponent]
    [AddComponentMenu("iStick2War/Swedish Plane V2")]
    public sealed class SwedishPlane_V2 : MonoBehaviour
    {
        private SwedishPlaneModel_V2 _model;
        private SwedishPlaneStateMachine_V2 _stateMachine;
        private SwedishPlaneController_V2 _controller;
        private SwedishPlaneView_V2 _view;
        private SwedishPlaneSpineEventForwarder_V2 _spineEventForwarder;
        private bool _initialized;

        public void InitializeForSpawn()
        {
            EnsureReferences();
            _stateMachine.Initialize(_model);
            _controller.Initialize(_model, _stateMachine);

            SkeletonAnimation skeletonAnimation = _view != null ? _view.SkeletonAnimation : null;
            ForceSpineMeshRebuild(skeletonAnimation);

            _view.Initialize(_stateMachine);
            _view.ResetVisualStateForSpawn();

            skeletonAnimation = _view != null ? _view.SkeletonAnimation : null;
            if (skeletonAnimation != null && _spineEventForwarder != null)
            {
                _spineEventForwarder.Init(skeletonAnimation);
            }

            _initialized = true;
        }

        public void BeginSupplyRun(SwedishPlaneRunConfig_V2 config)
        {
            if (!_initialized)
            {
                InitializeForSpawn();
            }

            _controller.BeginSupplyRun(config);
        }

        private void EnsureReferences()
        {
            _model = GetComponent<SwedishPlaneModel_V2>();
            if (_model == null)
            {
                _model = gameObject.AddComponent<SwedishPlaneModel_V2>();
            }

            _stateMachine = GetComponent<SwedishPlaneStateMachine_V2>();
            if (_stateMachine == null)
            {
                _stateMachine = gameObject.AddComponent<SwedishPlaneStateMachine_V2>();
            }

            _controller = GetComponent<SwedishPlaneController_V2>();
            if (_controller == null)
            {
                _controller = gameObject.AddComponent<SwedishPlaneController_V2>();
            }

            ResolveViewAndForwarderFromSkeleton();
        }

        private void ResolveViewAndForwarderFromSkeleton()
        {
            _view = null;
            _spineEventForwarder = null;
            SkeletonAnimation[] skeletonAnimations = GetComponentsInChildren<SkeletonAnimation>(true);
            for (int i = 0; i < skeletonAnimations.Length; i++)
            {
                SkeletonAnimation sk = skeletonAnimations[i];
                if (sk == null)
                {
                    continue;
                }

                if (_view == null)
                {
                    SwedishPlaneView_V2 onSkel = sk.GetComponent<SwedishPlaneView_V2>();
                    if (onSkel != null)
                    {
                        _view = onSkel;
                    }
                }

                if (_spineEventForwarder == null)
                {
                    SwedishPlaneSpineEventForwarder_V2 forwarder = sk.GetComponent<SwedishPlaneSpineEventForwarder_V2>();
                    if (forwarder != null)
                    {
                        _spineEventForwarder = forwarder;
                    }
                }
            }

            if (_view == null)
            {
                _view = GetComponent<SwedishPlaneView_V2>();
            }

            if (_view == null)
            {
                _view = gameObject.AddComponent<SwedishPlaneView_V2>();
            }

            if (_spineEventForwarder == null)
            {
                _spineEventForwarder = GetComponentInChildren<SwedishPlaneSpineEventForwarder_V2>(true);
            }

            if (_spineEventForwarder == null)
            {
                _spineEventForwarder = gameObject.AddComponent<SwedishPlaneSpineEventForwarder_V2>();
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
