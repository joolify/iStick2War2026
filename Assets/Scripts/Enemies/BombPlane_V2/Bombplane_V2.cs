using UnityEngine;

namespace iStick2War_V2
{
    /*
 * Bombplane_V2 Architecture Principle
 *
 * Bombplane_V2 acts as the COMPOSITION ROOT (entry point) of the bomb-plane stack.
 *
 * ❌ It MUST NOT contain bomb-drop simulation, flight integration, or Spine playback:
 * - Horizontal flight stepping
 * - Bomb spawn timing / pooling
 * - Animation track selection
 * - State transition rules
 *
 * ✅ It is ONLY responsible for:
 * - Collecting serialized tuning (flight, bombing, hitbox fallback)
 * - Ensuring Model / StateMachine / Controller / View / Spine forwarder exist
 * - Wiring dependencies (Initialize, SetConfig, BeginBombRun)
 * - Exposing a small public API for spawners (BeginBombRun fromLeft, freeze harness)
 *
 * ---------------------------------------------------------
 * ARCHITECTURE MODEL
 *
 * Bombplane_V2                        = Composition root (bootstrap + inspector-facing config)
 * BombPlaneController_V2             = Brain (Update: drops, flight, despawn, hitbox ensure; Config per pass)
 * BombPlaneStateMachine_V2           = Rules (state + events for the View)
 * BombPlaneModel_V2                 = DNA (runtime pass data: started, timers, bomb count, direction)
 * BombPlaneView_V2                  = Body (Spine clips, bone world sampling for drop origin)
 * BombPlaneSpineEventForwarder_V2   = Spine → controller animation events (timing only)
 *
 * ---------------------------------------------------------
 * WHERE TO READ NEXT (navigation)
 *
 * State enum + terminal state → BombPlaneState_V2.cs
 * State transitions + model mirror → BombPlaneStateMachine_V2.cs
 * Drop interval, flight, pooling, Config struct → BombPlaneController_V2.cs
 * Spine → controller animation events → BombPlaneSpineEventForwarder_V2.cs
 * Fly / dropBomb Spine tracks → BombPlaneView_V2.cs
 * Pooled bomb flight + explosion → BombProjectile_V2.cs
 * Wave bomber spawn + facing / BeginBombRun(fromLeft) → EnemySpawner_V2.cs
 *
 * ---------------------------------------------------------
 * TYPICAL CALL FLOW
 *
 * EnemySpawner_V2 (or scene Start) → InitializeForSpawn() → BeginBombRun(fromLeft?) → BombPlaneController_V2.SetConfig + StartBombRun.
 *
 * ---------------------------------------------------------
 * DESIGN GOAL
 *
 * Bombplane_V2 stays thin: one place for serialized fields and lifecycle glue,
 * mirroring Hero_V2’s separation between root and systems.
 */
    [DisallowMultipleComponent]
    public sealed class Bombplane_V2 : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] private bool _enableHorizontalFlight = true;
        [SerializeField] private float _horizontalFlySpeed = 4f;
        [SerializeField] private float _flightOffscreenMarginWorld = 4f;
        [SerializeField] private float _flightMaxLifetimeSeconds = 45f;
        [Tooltip("If true: +scaleX means plane nose points right. Disable if sprite faces left at +scaleX.")]
        [SerializeField] private bool _spriteFacesRightWhenScaleXPositive = false;
        [Tooltip("Must match EnemySpawner_V2 invert for bomber passes when using BeginBombRun(fromLeft).")]
        [SerializeField] private bool _invertFlightDirectionX = false;

        [Header("Bombing")]
        [SerializeField] private BombProjectile_V2 _bombProjectilePrefab;
        [SerializeField] private Transform _bombDropMount;
        [Tooltip("Spine bone on the plane skeleton (e.g. bombSpawnPoint). Empty = spawn from bomb drop mount / root.")]
        [SerializeField] private string _bombSpawnBoneName = "";
        [SerializeField] private float _bombDropIntervalSeconds = 1.25f;
        [SerializeField] private int _maxBombsPerPass = 4;
        [SerializeField] private int _bombDamage = 30;
        [SerializeField] private float _bombExplosionRadius = 2f;
        [SerializeField] private bool _debugBombLogs;

        [Header("Combat / targeting")]
        [Tooltip(
            "Prefabs without any Collider2D cannot be aimed at by AutoHero or hit by rockets. When enabled, a BoxCollider2D " +
            "is added from the SpriteRenderer bounds if none exists.")]
        [SerializeField] private bool _ensureHitboxFromSpriteIfMissing = true;
        [SerializeField] private Vector2 _fallbackHitboxSize = new Vector2(4f, 1.25f);

        private BombPlaneModel_V2 _model;
        private BombPlaneStateMachine_V2 _stateMachine;
        private BombPlaneController_V2 _controller;
        private BombPlaneView_V2 _view;
        private BombPlaneSpineEventForwarder_V2 _spineEventForwarder;
        private bool _initialized;

        // Starts a pass using sprite scale to guess travel direction (scene-placed planes only).
        // Prefer BeginBombRun(bool fromLeft) from spawners so direction matches spawn side.
        public void BeginBombRun()
        {
            BeginBombRun(spawnedFromLeft: (bool?)null);
        }

        // Same as aircraft spawn: true = entered from left, should fly toward +X (before ).
        public void BeginBombRun(bool spawnedFromLeft)
        {
            BeginBombRun(spawnedFromLeft: (bool?) spawnedFromLeft);
        }

        private void BeginBombRun(bool? spawnedFromLeft)
        {
            if (!_initialized)
            {
                InitializeForSpawn();
            }

            if (_controller == null)
            {
                return;
            }

            _controller.SetConfig(BuildConfig());
            _controller.StartBombRun(spawnedFromLeft);
        }

        // Holds position and skips bombing / flight / despawn logic (combat matrix harness).
        public void FreezeForCombatMatrixHarness()
        {
            _controller?.FreezeForCombatMatrixHarness();
        }

        private void Start()
        {
            if (!_initialized)
            {
                InitializeForSpawn();
            }

            if (!_model.started)
            {
                BeginBombRun();
            }
        }

        private void InitializeForSpawn()
        {
            EnsureReferences();
            _stateMachine.Initialize(_model);
            _controller.Initialize(_model, _stateMachine);
            _controller.SetConfig(BuildConfig());
            _view.Initialize(_stateMachine);
            _view.ResetVisualStateForSpawn();

            Spine.Unity.SkeletonAnimation skeletonAnimation = _view.SkeletonAnimation;
            if (skeletonAnimation != null && _spineEventForwarder != null)
            {
                _spineEventForwarder.Init(_controller, skeletonAnimation);
            }

            _initialized = true;
        }

        private void EnsureReferences()
        {
            _model = GetComponent<BombPlaneModel_V2>();
            if (_model == null)
            {
                _model = gameObject.AddComponent<BombPlaneModel_V2>();
            }

            _stateMachine = GetComponent<BombPlaneStateMachine_V2>();
            if (_stateMachine == null)
            {
                _stateMachine = gameObject.AddComponent<BombPlaneStateMachine_V2>();
            }

            _controller = GetComponent<BombPlaneController_V2>();
            if (_controller == null)
            {
                _controller = gameObject.AddComponent<BombPlaneController_V2>();
            }

            _view = GetComponent<BombPlaneView_V2>();
            if (_view == null)
            {
                _view = GetComponentInChildren<BombPlaneView_V2>(true);
            }

            if (_view == null)
            {
                _view = gameObject.AddComponent<BombPlaneView_V2>();
            }

            _spineEventForwarder = GetComponent<BombPlaneSpineEventForwarder_V2>();
            if (_spineEventForwarder == null)
            {
                _spineEventForwarder = gameObject.AddComponent<BombPlaneSpineEventForwarder_V2>();
            }
        }

        // World velocity used for flight (transform-based). Exposed for AA lead / intercept aim (e.g. AutoHero bazooka).
        public Vector2 GetHorizontalFlightVelocityWorld()
        {
            return _controller != null ? _controller.GetHorizontalFlightVelocityWorld() : Vector2.zero;
        }

        private BombPlaneController_V2.Config BuildConfig()
        {
            return new BombPlaneController_V2.Config
            {
                enableHorizontalFlight = _enableHorizontalFlight,
                horizontalFlySpeed = _horizontalFlySpeed,
                flightOffscreenMarginWorld = _flightOffscreenMarginWorld,
                flightMaxLifetimeSeconds = _flightMaxLifetimeSeconds,
                spriteFacesRightWhenScaleXPositive = _spriteFacesRightWhenScaleXPositive,
                invertFlightDirectionX = _invertFlightDirectionX,
                bombProjectilePrefab = _bombProjectilePrefab,
                bombDropMount = _bombDropMount,
                bombSpawnBoneName = _bombSpawnBoneName,
                bombSpawnView = _view,
                bombDropIntervalSeconds = _bombDropIntervalSeconds,
                maxBombsPerPass = _maxBombsPerPass,
                bombDamage = _bombDamage,
                bombExplosionRadius = _bombExplosionRadius,
                debugBombLogs = _debugBombLogs,
                ensureHitboxFromSpriteIfMissing = _ensureHitboxFromSpriteIfMissing,
                fallbackHitboxSize = _fallbackHitboxSize
            };
        }
    }
}
