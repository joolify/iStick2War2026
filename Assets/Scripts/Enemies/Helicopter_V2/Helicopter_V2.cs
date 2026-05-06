using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    [DisallowMultipleComponent]
    public sealed class Helicopter_V2 : MonoBehaviour
    {
        private HelicopterModel_V2 _model;
        private HelicopterStateMachine_V2 _stateMachine;
        private HelicopterController_V2 _controller;
        private HelicopterView_V2 _view;
        private HelicopterSpineEventForwarder_V2 _spineEventForwarder;
        private AircraftHealth_V2 _health;
        private bool _initialized;

        public void InitializeForSpawn()
        {
            EnsureReferences();

            _stateMachine.Initialize(_model);
            _controller.Initialize(_model, _stateMachine);
            _view.Initialize(_stateMachine);
            _view.ResetVisualStateForSpawn();

            SkeletonAnimation skeletonAnimation = _view.SkeletonAnimation;
            if (skeletonAnimation != null && _spineEventForwarder != null)
            {
                _spineEventForwarder.Init(_controller, skeletonAnimation);
            }

            if (_health != null)
            {
                _health.OnDestroyed -= HandleAircraftDestroyed;
                _health.OnDestroyed += HandleAircraftDestroyed;
            }

            _initialized = true;
        }

        public void BeginFlight()
        {
            if (!_initialized)
            {
                InitializeForSpawn();
            }

            _controller.StartFlight();
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnDestroyed -= HandleAircraftDestroyed;
            }
        }

        private void HandleAircraftDestroyed(AircraftHealth_V2 aircraft)
        {
            _controller.OnDestroyed();
        }

        private void EnsureReferences()
        {
            _model = GetComponent<HelicopterModel_V2>();
            if (_model == null)
            {
                _model = gameObject.AddComponent<HelicopterModel_V2>();
            }

            _stateMachine = GetComponent<HelicopterStateMachine_V2>();
            if (_stateMachine == null)
            {
                _stateMachine = gameObject.AddComponent<HelicopterStateMachine_V2>();
            }

            _controller = GetComponent<HelicopterController_V2>();
            if (_controller == null)
            {
                _controller = gameObject.AddComponent<HelicopterController_V2>();
            }

            _view = GetComponent<HelicopterView_V2>();
            if (_view == null)
            {
                _view = gameObject.AddComponent<HelicopterView_V2>();
            }

            _spineEventForwarder = GetComponent<HelicopterSpineEventForwarder_V2>();
            if (_spineEventForwarder == null)
            {
                _spineEventForwarder = gameObject.AddComponent<HelicopterSpineEventForwarder_V2>();
            }

            _health = GetComponent<AircraftHealth_V2>();
        }
    }
}
