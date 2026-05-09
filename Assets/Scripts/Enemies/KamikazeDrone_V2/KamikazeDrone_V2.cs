using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * KamikazeDrone_V2 Architecture Principle
 *
 * KamikazeDrone_V2 acts as the COMPOSITION ROOT for the lightweight “aircraft stack” on the
 * kamikaze prefab: Model, StateMachine, Controller, View, Spine forwarder, plus health wiring.
 *
 * ❌ It MUST NOT implement bunker approach, dive phases, explosions, or damage:
 * - Those live on EnemyKamikazeDrone_V2 (parallel gameplay driver on the same prefab)
 *
 * ✅ It is ONLY responsible for:
 * - Ensuring subsystems exist and InitializeForSpawn wires them
 * - Exposing BeginFlight → KamikazeDroneController_V2.StartFlight (state → Fly for visuals)
 * - Forwarding AircraftHealth_V2.OnDestroyed → Controller.OnDestroyed (state → Die)
 * - Optional Spine event path via KamikazeDroneSpineEventForwarder_V2
 *
 * ---------------------------------------------------------
 * ARCHITECTURE MODEL
 *
 * KamikazeDrone_V2            = Composition Root (bootstrap + thin lifecycle API)
 * KamikazeDroneController_V2 = Minimal brain (Fly / Die + optional DeployStarted from Spine)
 * KamikazeDroneStateMachine  = Rules + View notifications
 * KamikazeDroneModel_V2      = DNA (currently mirrors currentState)
 * KamikazeDroneView_V2       = Body (single looping Spine clip)
 * EnemyKamikazeDrone_V2      = Separate driver: cruise, dive, overlap explode, bunker/hero damage
 *
 * ---------------------------------------------------------
 * DESIGN GOAL
 *
 * Mirror Helicopter/BombDrone composition discipline while keeping heavy movement off this root.
 * EnemySpawner_V2 adds this component at runtime if missing, then calls InitializeForSpawn / BeginFlight.
 */
    [DisallowMultipleComponent]
    public sealed class KamikazeDrone_V2 : MonoBehaviour
    {
        private KamikazeDroneModel_V2 _model;
        private KamikazeDroneStateMachine_V2 _stateMachine;
        private KamikazeDroneController_V2 _controller;
        private KamikazeDroneView_V2 _view;
        private KamikazeDroneSpineEventForwarder_V2 _spineEventForwarder;
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
                _health.OnDestroyed -= HandleDestroyed;
                _health.OnDestroyed += HandleDestroyed;
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
                _health.OnDestroyed -= HandleDestroyed;
            }
        }

        private void HandleDestroyed(AircraftHealth_V2 aircraft)
        {
            _controller.OnDestroyed();
        }

        private void EnsureReferences()
        {
            _model = GetComponent<KamikazeDroneModel_V2>();
            if (_model == null)
            {
                _model = gameObject.AddComponent<KamikazeDroneModel_V2>();
            }

            _stateMachine = GetComponent<KamikazeDroneStateMachine_V2>();
            if (_stateMachine == null)
            {
                _stateMachine = gameObject.AddComponent<KamikazeDroneStateMachine_V2>();
            }

            _controller = GetComponent<KamikazeDroneController_V2>();
            if (_controller == null)
            {
                _controller = gameObject.AddComponent<KamikazeDroneController_V2>();
            }

            _view = GetComponent<KamikazeDroneView_V2>();
            if (_view == null)
            {
                _view = gameObject.AddComponent<KamikazeDroneView_V2>();
            }

            _spineEventForwarder = GetComponent<KamikazeDroneSpineEventForwarder_V2>();
            if (_spineEventForwarder == null)
            {
                _spineEventForwarder = gameObject.AddComponent<KamikazeDroneSpineEventForwarder_V2>();
            }

            _health = GetComponent<AircraftHealth_V2>();
            if (_health == null)
            {
                _health = GetComponentInChildren<AircraftHealth_V2>(true);
            }
        }
    }
}
