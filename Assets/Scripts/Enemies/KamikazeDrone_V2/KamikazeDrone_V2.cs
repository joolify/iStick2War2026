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
 * - Those live on KamikazeDroneDriver_V2 (parallel gameplay driver on the same prefab)
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
 * KamikazeDroneDriver_V2      = Separate driver: cruise, dive, overlap explode, bunker/hero damage
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

            // Pooled drones keep SkeletonAnimation.valid across Disable; Initialize(false) then no-ops and the
            // mesh pipeline can skip rebuilding for this frame — only the spawner-added CircleCollider2D is visible.
            // Full overwrite + an immediate mesh tick matches a fresh prefab dropped into the scene.
            SkeletonAnimation skeletonAnimation = _view != null ? _view.SkeletonAnimation : null;
            ForceSpineMeshRebuild(skeletonAnimation);

            _view.Initialize(_stateMachine);
            _view.ResetVisualStateForSpawn();

            skeletonAnimation = _view.SkeletonAnimation;
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

            // Prefab places KamikazeDroneView_V2 + SpineEventForwarder on the child with SkeletonAnimation.
            // GetComponent on root would miss them and AddComponent would add a duplicate root View without
            // serialized Spine refs — animations never wire up after pool spawn (manual prefab-in-scene still works).
            _view = null;
            SkeletonAnimation[] skeletonAnimations = GetComponentsInChildren<SkeletonAnimation>(true);
            for (int i = 0; i < skeletonAnimations.Length; i++)
            {
                SkeletonAnimation sk = skeletonAnimations[i];
                if (sk == null)
                {
                    continue;
                }

                KamikazeDroneView_V2 onSkelGo = sk.GetComponent<KamikazeDroneView_V2>();
                if (onSkelGo != null)
                {
                    _view = onSkelGo;
                    break;
                }
            }

            if (_view == null)
            {
                _view = GetComponent<KamikazeDroneView_V2>();
            }

            if (_view == null)
            {
                _view = gameObject.AddComponent<KamikazeDroneView_V2>();
            }

            _spineEventForwarder = null;
            for (int i = 0; i < skeletonAnimations.Length; i++)
            {
                SkeletonAnimation sk = skeletonAnimations[i];
                if (sk == null)
                {
                    continue;
                }

                KamikazeDroneSpineEventForwarder_V2 forwarder = sk.GetComponent<KamikazeDroneSpineEventForwarder_V2>();
                if (forwarder != null)
                {
                    _spineEventForwarder = forwarder;
                    break;
                }
            }

            if (_spineEventForwarder == null)
            {
                _spineEventForwarder = GetComponent<KamikazeDroneSpineEventForwarder_V2>();
            }

            if (_spineEventForwarder == null)
            {
                _spineEventForwarder = gameObject.AddComponent<KamikazeDroneSpineEventForwarder_V2>();
            }

            // Older spawns / pooled instances could have duplicate View or Forwarder on the root; remove them
            // when the authoritative components live under the SkeletonAnimation child (prefab layout).
            if (_view != null && _view.transform != transform)
            {
                KamikazeDroneView_V2 orphanView = GetComponent<KamikazeDroneView_V2>();
                if (orphanView != null)
                {
                    Object.Destroy(orphanView);
                }
            }

            if (_spineEventForwarder != null && _spineEventForwarder.transform != transform)
            {
                KamikazeDroneSpineEventForwarder_V2 orphanForwarder = GetComponent<KamikazeDroneSpineEventForwarder_V2>();
                if (orphanForwarder != null)
                {
                    Object.Destroy(orphanForwarder);
                }
            }

            _health = GetComponent<AircraftHealth_V2>();
            if (_health == null)
            {
                _health = GetComponentInChildren<AircraftHealth_V2>(true);
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
