using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombDrone_V2 Architecture Principle
 *
 * BombDrone_V2 acts as the COMPOSITION ROOT (entry point) of the bomb-drone stack.
 *
 * ❌ It MUST NOT implement flight, bunker targeting, bomb drops, or Spine playback:
 * - Horizontal movement integration
 * - BunkerHitbox_V2 alignment / drop gating
 * - Projectile spawn / pooling
 * - Animation track selection
 *
 * ✅ It is ONLY responsible for:
 * - Ensuring Model / StateMachine / Controller / View / Spine forwarder (and health) exist
 * - Wiring lifecycle: InitializeForSpawn, BeginRun → Controller.StartFlight
 * - Subscribing AircraftHealth_V2.OnDestroyed → Controller.OnDestroyed
 * - FreezeForCombatMatrixHarness passthrough
 *
 * ---------------------------------------------------------
 * ARCHITECTURE MODEL
 *
 * BombDrone_V2     = Composition Root (bootstrap + external API for spawners)
 * Controller        = Brain (Update: fly, bunker drop, lifetime / camera despawn)
 * StateMachine      = Rules (state + events for the View)
 * Model             = DNA (started, direction, single bomb flag, timers, harness freeze)
 * View              = Body (Spine fly / dropBomb clips from state changes)
 *
 * Carried payload + drop timing + physics → BombDroneController_V2 (hierarchy on BombDrone V2.prefab).
 *
 * ---------------------------------------------------------
 * DESIGN GOAL
 *
 * Keep this MonoBehaviour thin and stable: one place to wire subsystems,
 * mirroring Bombplane_V2 / Hero_V2 composition-root discipline.
 */
    [DisallowMultipleComponent]
    public sealed class BombDrone_V2 : AircraftHealthCompositionRootBase_V2
    {
        private BombDroneModel_V2 _model;
        private BombDroneStateMachine_V2 _stateMachine;
        private BombDroneController_V2 _controller;
        private BombDroneView_V2 _view;
        private BombDroneSpineEventForwarder_V2 _spineEventForwarder;

        public void BeginRun()
        {
            if (!_initialized)
            {
                InitializeForSpawn();
            }

            _controller?.StartFlight();
        }

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
            SubscribeHealthDestroyed(HandleDestroyed);

            _initialized = true;
        }

        // Stops horizontal movement, bomb drops, and off-screen / lifetime despawn (combat matrix harness).
        public void FreezeForCombatMatrixHarness()
        {
            _controller?.FreezeForCombatMatrixHarness();
        }

        private void OnDestroy()
        {
            UnsubscribeHealthDestroyed(HandleDestroyed);
        }

        private void HandleDestroyed(AircraftHealth_V2 aircraft)
        {
            _controller?.OnDestroyed();
        }

        private void EnsureReferences()
        {
            _model = GetComponent<BombDroneModel_V2>();
            if (_model == null)
            {
                _model = gameObject.AddComponent<BombDroneModel_V2>();
            }

            _stateMachine = GetComponent<BombDroneStateMachine_V2>();
            if (_stateMachine == null)
            {
                _stateMachine = gameObject.AddComponent<BombDroneStateMachine_V2>();
            }

            _controller = GetComponent<BombDroneController_V2>();
            if (_controller == null)
            {
                _controller = gameObject.AddComponent<BombDroneController_V2>();
            }

            _view = GetComponent<BombDroneView_V2>();
            if (_view == null)
            {
                _view = GetComponentInChildren<BombDroneView_V2>(true);
            }

            if (_view == null)
            {
                _view = gameObject.AddComponent<BombDroneView_V2>();
            }

            _spineEventForwarder = GetComponent<BombDroneSpineEventForwarder_V2>();
            if (_spineEventForwarder == null)
            {
                _spineEventForwarder = GetComponentInChildren<BombDroneSpineEventForwarder_V2>(true);
            }

            if (_spineEventForwarder == null)
            {
                _spineEventForwarder = gameObject.AddComponent<BombDroneSpineEventForwarder_V2>();
            }
        }
    }
}
