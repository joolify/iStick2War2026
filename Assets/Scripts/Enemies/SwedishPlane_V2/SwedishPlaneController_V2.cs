using System;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SwedishPlaneController_V2 — neutral horizontal supply pass.
     * Flies across the playfield and drops parachute powerups over/near the bunker lane.
     */
    public sealed class SwedishPlaneController_V2 : AircraftHorizontalFlightControllerBase_V2
    {
        [Header("Flight")]
        [SerializeField] private float _horizontalFlySpeed = 5.5f;
        [SerializeField] private float _flightOffscreenMarginWorld = 4f;
        [SerializeField] private float _maxLifetimeSeconds = 40f;
        // Swedish plane art faces right when scale.x is positive (unlike bomb-plane body).
        [SerializeField] private bool _spriteFacesRightWhenScaleXPositive = true;
        [SerializeField] private bool _invertFlightDirectionX;

        [Header("Drops")]
        [SerializeField] private float _dropToleranceX = 1.2f;
        [SerializeField] private int _defaultDropsPerPass = 1;
        [SerializeField] private float _minSecondsBetweenDrops = 0.35f;

        private SwedishPlaneModel_V2 _model;
        private SwedishPlaneStateMachine_V2 _stateMachine;
        private Rigidbody2D _rb;
        private Camera _cam;
        private BunkerHitbox_V2 _bunkerHitbox;
        private SwedishPlaneRunConfig_V2 _runConfig;
        private bool _hasSpawnSide;
        private bool _spawnedFromLeft;
        private float _nextDropAllowedAt;
        private int _dropsBudget;
        private Action _onPassComplete;

        public void Initialize(SwedishPlaneModel_V2 model, SwedishPlaneStateMachine_V2 stateMachine)
        {
            _model = model;
            _stateMachine = stateMachine;
            _rb = GetComponent<Rigidbody2D>();
        }

        public void BeginSupplyRun(SwedishPlaneRunConfig_V2 config)
        {
            _runConfig = config;
            _onPassComplete = config?.onPassComplete;
            _hasSpawnSide = config != null;
            _spawnedFromLeft = config != null && config.spawnedFromLeft;
            _cam = config != null && config.gameplayCamera != null ? config.gameplayCamera : Camera.main;
            _bunkerHitbox = FindAnyObjectByType<BunkerHitbox_V2>(FindObjectsInactive.Include);
            _dropsBudget = Mathf.Max(1, config != null ? config.dropsThisPass : _defaultDropsPerPass);
            _nextDropAllowedAt = 0f;

            if (_model == null || _stateMachine == null)
            {
                return;
            }

            _model.expireAt = Time.time + Mathf.Max(4f, _maxLifetimeSeconds);
            _model.started = true;
            _model.dropsReleased = 0;
            _model.passCompleteSignaled = false;
            _model.directionX = ResolveInitialDirectionX();
            ApplyFlightFacingForDirection(_model.directionX);
            EnsureFlightRigidbodyReady();
            _stateMachine.ChangeState(SwedishPlaneState_V2.Fly);
        }

        private void ApplyFlightFacingForDirection(float directionX)
        {
            if (Mathf.Approximately(directionX, 0f))
            {
                return;
            }

            bool shouldFaceRight = directionX > 0f;
            bool usePositiveScaleX = shouldFaceRight == _spriteFacesRightWhenScaleXPositive;
            Vector3 scale = transform.localScale;
            scale.x = usePositiveScaleX ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        private void Update()
        {
            if (_model == null || _stateMachine == null || !_model.started || _model.frozenForCombatMatrixHarness)
            {
                return;
            }

            SwedishPlaneState_V2 state = _stateMachine.CurrentState;
            if (state == SwedishPlaneState_V2.Idle || state == SwedishPlaneState_V2.Complete)
            {
                return;
            }

            if (Time.time >= _model.expireAt)
            {
                CompletePassAndDespawn();
                return;
            }

            IntegrateHorizontalFlight();
            TryReleasePowerUpDrop();
            TryDespawnWhenPastCameraBounds();
        }

        private void IntegrateHorizontalFlight()
        {
            EnsureFlightRigidbodyReady();
            float speed = Mathf.Max(0.1f, _horizontalFlySpeed);
            Vector2 delta = Vector2.right * (_model.directionX * speed * Time.deltaTime);
            if (_rb != null)
            {
                _rb.MovePosition(_rb.position + delta);
            }
            else
            {
                transform.position += (Vector3)delta;
            }
        }

        private void TryReleasePowerUpDrop()
        {
            if (_model.dropsReleased >= _dropsBudget || Time.time < _nextDropAllowedAt)
            {
                return;
            }

            if (_bunkerHitbox == null)
            {
                _bunkerHitbox = FindAnyObjectByType<BunkerHitbox_V2>(FindObjectsInactive.Include);
            }

            if (_bunkerHitbox == null)
            {
                return;
            }

            float bunkerX = _bunkerHitbox.transform.position.x;
            float dx = Mathf.Abs(transform.position.x - bunkerX);
            if (dx > Mathf.Max(0.2f, _dropToleranceX))
            {
                return;
            }

            ReleasePowerUpAt(transform.position);
            _model.dropsReleased++;
            _nextDropAllowedAt = Time.time + Mathf.Max(0.05f, _minSecondsBetweenDrops);
        }

        private void ReleasePowerUpAt(Vector3 worldPos)
        {
            SwedishPlanePowerUp_V2 prefab = _runConfig != null ? _runConfig.powerUpPrefab : null;
            if (prefab == null)
            {
                return;
            }

            SurvivalPowerUpCatalog_V2 catalog = _runConfig != null ? _runConfig.catalog : null;
            if (catalog == null || !catalog.TryRollOffer(out SurvivalPowerUpOffer_V2 offer))
            {
                return;
            }

            SwedishPlanePowerUp_V2 instance = SimplePrefabPool_V2.Spawn(prefab, worldPos, Quaternion.identity);
            if (instance == null)
            {
                return;
            }

            instance.InitializeForSpawn();
            if (_runConfig != null && _runConfig.survivalCoordinator != null)
            {
                _runConfig.survivalCoordinator.BindRuntimeContextToPowerUp(instance, offer);
            }
            else
            {
                instance.BeginDrop(offer);
            }
        }

        private void TryDespawnWhenPastCameraBounds()
        {
            RefreshCameraIfNeeded();
            if (_cam == null || !_cam.orthographic)
            {
                return;
            }

            if (!TryGetOrthographicCameraHorizontalBounds(
                    _cam,
                    _flightOffscreenMarginWorld,
                    out float leftBound,
                    out float rightBound))
            {
                return;
            }

            if (_model.dropsReleased >= _dropsBudget &&
                IsPastHorizontalFlyBounds(transform.position.x, _model.directionX, leftBound, rightBound))
            {
                CompletePassAndDespawn();
            }
        }

        private void CompletePassAndDespawn()
        {
            if (_model != null && !_model.passCompleteSignaled)
            {
                _model.passCompleteSignaled = true;
                _stateMachine?.ChangeState(SwedishPlaneState_V2.Complete);
                _onPassComplete?.Invoke();
            }

            DespawnSelfViaPool(gameObject);
        }

        private float ResolveInitialDirectionX()
        {
            if (_hasSpawnSide)
            {
                float dir = _spawnedFromLeft ? 1f : -1f;
                return _invertFlightDirectionX ? -dir : dir;
            }

            return ResolveInitialDirectionXTowardBunkerOrFacing(
                _bunkerHitbox,
                transform,
                _spriteFacesRightWhenScaleXPositive) * (_invertFlightDirectionX ? -1f : 1f);
        }

        private void EnsureFlightRigidbodyReady()
        {
            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody2D>();
            }

            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody2D>();
            }

            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.simulated = true;
            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        private void RefreshCameraIfNeeded()
        {
            if (_cam == null || !_cam.isActiveAndEnabled)
            {
                _cam = _runConfig != null && _runConfig.gameplayCamera != null
                    ? _runConfig.gameplayCamera
                    : Camera.main;
            }
        }
    }
}
