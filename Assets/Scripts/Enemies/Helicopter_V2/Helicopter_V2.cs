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
 * ARCHITECTURE MODEL
 *
 * Helicopter_V2              = Composition root (bootstrap + spawner-facing API)
 * HelicopterController_V2    = Brain (StartFlight / OnDestroyed / Spine command bridge)
 * HelicopterStateMachine_V2  = Rules (state enum + transitions; mirrors into HelicopterModel_V2)
 * HelicopterModel_V2         = DNA (currentState mirror for views / tooling)
 * HelicopterView_V2          = Body (Spine clip selection from state changes)
 * HelicopterSpineEventForwarder_V2 = Spine → controller animation events (timing only)
 *
 * ---------------------------------------------------------
 * WHERE TO READ NEXT (navigation)
 *
 * State enum + terminal state → HelicopterState_V2.cs
 * Flight entry + Die + animation commands → HelicopterController_V2.cs
 * Fly / deploy Spine tracks → HelicopterView_V2.cs
 * Paratrooper drop cadence + bone-side triggers → HelicopterCarrier_V2.cs (carrier prefabs; configured from EnemySpawner_V2)
 * Horizontal “fly across” without carrier logic → AircraftFlyAcrossScreen_V2.cs (other aircraft; not this stack’s core)
 *
 * ---------------------------------------------------------
 * TYPICAL CALL FLOW
 *
 * EnemySpawner_V2 (or scene setup) → InitializeForSpawn() → BeginFlight() → HelicopterController_V2.StartFlight().
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
