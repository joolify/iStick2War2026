using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * Helicopter_V2 (Helicopter entity composition root)
 *
 * PURPOSE:
 * Ensures and wires HelicopterModel_V2, HelicopterStateMachine_V2, HelicopterController_V2,
 * HelicopterView_V2, HelicopterSpineEventForwarder_V2, and AircraftHealth_V2 on this GameObject.
 * Exposes InitializeForSpawn() and BeginFlight() as the spawn-time lifecycle for one helicopter flight.
 *
 * ---------------------------------------------------------
 * TYPICAL CALL FLOW
 *
 * EnemySpawner_V2 (or scene setup) → InitializeForSpawn() → BeginFlight() → controller / state machine / view.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Own paratrooper drop cadence (HelicopterCarrier_V2 sequences drops for a carrier flight).
 * - Implement infantry or weapon combat rules (Paratrooper_* / hero systems).
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Thin composition root: resolve or add sibling components, forward lifecycle, subscribe AircraftHealth_V2.OnDestroyed.
 */
    [DisallowMultipleComponent]
    public sealed class Helicopter_V2 : AircraftHealthCompositionRootBase_V2
    {
        private HelicopterModel_V2 _model;
        private HelicopterStateMachine_V2 _stateMachine;
        private HelicopterController_V2 _controller;
        private HelicopterView_V2 _view;
        private HelicopterSpineEventForwarder_V2 _spineEventForwarder;

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

            ResolveHealthFromHierarchy();
            SubscribeHealthDestroyed(HandleAircraftDestroyed);

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
            UnsubscribeHealthDestroyed(HandleAircraftDestroyed);
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
        }
    }
}
