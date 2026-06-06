using Assets.Scripts.Components;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombDroneController_V2 (Critical Component / Brain)
 *
 * Gameplay driver for the bomb drone: fly horizontally toward the bunker, pause over it, then play the
 * dropBomb Spine phase while spawning the configured payload (visual prefab and/or BombProjectile_V2).
 *
 * Responsibilities:
 * - StartFlight: seed Model (timers, direction toward bunker when available)
 * - Update: horizontal motion in Fly and DropBomb (exit continues during drop clip); HoverOverBunker holds position
 * - TryBeginHoverOverBunker: X-alignment vs BunkerHitbox_V2 → HoverOverBunker for hold duration
 * - After hold: spawn payload at mount, enter DropBomb for View, then return to Fly
 * - Camera-bounded despawn and max lifetime despawn via SimplePrefabPool_V2
 * - Freeze / death hooks for harness and AircraftHealth_V2
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLES
 *
 * - Serialized tuning lives on this MonoBehaviour (unlike BombPlane’s separate Config struct)
 * - Reads/writes BombDroneModel_V2; nudges BombDroneStateMachine_V2 for View sync
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Act as composition root (BombDrone_V2 wires references and BeginRun)
 * - Select Spine clips directly (BombDroneView_V2 subscribes to state changes)
 *
 * ---------------------------------------------------------
 * Spine events:
 *
 * OnAnimationEvent exists for forwarder wiring; mapping is intentionally empty until designers add events.
 */
    public sealed class BombDroneController_V2 : AircraftHorizontalFlightControllerBase_V2,
        IAircraftSpineAnimationCommandReceiver_V2
    {
        [Header("Flight")]
        [SerializeField] private float _horizontalFlySpeed = 6.2f;
        [SerializeField] private float _flightOffscreenMarginWorld = 4f;
        [SerializeField] private float _maxLifetimeSeconds = 30f;
        [SerializeField] private bool _spriteFacesRightWhenScaleXPositive = true;
        [SerializeField] private bool _invertFlightDirectionX = false;

        [Header("Bombing")]
        [Tooltip("When set, spawned after the hover hold instead of (or in addition to) BombProjectile_V2.")]
        [SerializeField] private GameObject _droppedBombPrefab;
        [Tooltip("Optional child transform carried under the drone (often parented under the Spine view so it follows fly animation). Detached at drop. Recreated from _droppedBombPrefab on the next StartFlight when missing.")]
        [SerializeField] private Transform _attachedPayloadBomb;
        [SerializeField] private Vector3 _attachedPayloadLocalPosition = new Vector3(0f, -1.33f, 0f);
        [SerializeField] private Vector3 _attachedPayloadLocalScale = new Vector3(2f, 2f, 1f);
        [SerializeField] private BombProjectile_V2 _bombProjectilePrefab;
        [SerializeField] private Transform _bombDropMount;
        [SerializeField] private int _bombDamage = 24;
        [SerializeField] private float _bombExplosionRadius = 1.75f;
        [SerializeField] private float _dropToleranceX = 0.7f;
        [SerializeField] private float _dropBombStateDuration = 0.35f;
        [Tooltip("Seconds to hold still over the bunker before dropBomb + payload.")]
        [SerializeField] private float _holdOverBunkerSeconds = 1f;
        [Tooltip("Gravity for pooled/instantiated dropped bomb prefab when it has no Rigidbody2D yet.")]
        [SerializeField] private float _droppedBombGravityScale = 2.4f;

        private BombDroneModel_V2 _model;
        private BombDroneStateMachine_V2 _stateMachine;
        private Rigidbody2D _rb;
        private Camera _cam;
        private BunkerHitbox_V2 _bunkerHitbox;
        private float _returnToFlyAfterDropAt;
        private float _hoverEndAt;
        private float _approachStartedAt;
        private bool _hasEnteredViewportHorizontally;
        private bool _hasSpawnSide;
        private bool _spawnedFromLeft;
        private Camera _spawnCameraOverride;
        private bool _pendingFlightIntegration;

        public void Initialize(BombDroneModel_V2 model, BombDroneStateMachine_V2 stateMachine)
        {
            _model = model;
            _stateMachine = stateMachine;
            _rb = GetComponent<Rigidbody2D>();
        }

        public void ConfigureSpawnContext(bool spawnedFromLeft, Camera gameplayCamera)
        {
            _hasSpawnSide = true;
            _spawnedFromLeft = spawnedFromLeft;
            _spawnCameraOverride = gameplayCamera;
        }

        // Pre–controller-split prefabs (bombDrone.prefab) kept tuning on BombDrone_V2; copy onto runtime-added Controller fields.
        internal void AdoptLegacyCompositionRootTuningIfUnset(
            BombProjectile_V2 bombProjectilePrefab,
            Transform bombDropMount,
            GameObject droppedBombPrefab,
            Transform attachedPayloadBomb,
            float horizontalFlySpeed,
            float flightOffscreenMarginWorld,
            float maxLifetimeSeconds,
            bool spriteFacesRightWhenScaleXPositive,
            bool invertFlightDirectionX,
            int bombDamage,
            float bombExplosionRadius,
            float dropToleranceX)
        {
            if (_bombProjectilePrefab == null && bombProjectilePrefab != null)
            {
                _bombProjectilePrefab = bombProjectilePrefab;
            }

            if (_bombDropMount == null && bombDropMount != null)
            {
                _bombDropMount = bombDropMount;
            }

            if (_droppedBombPrefab == null && droppedBombPrefab != null)
            {
                _droppedBombPrefab = droppedBombPrefab;
            }

            if (_attachedPayloadBomb == null && attachedPayloadBomb != null)
            {
                _attachedPayloadBomb = attachedPayloadBomb;
            }

            if (_bombProjectilePrefab == null && _droppedBombPrefab == null && attachedPayloadBomb == null)
            {
                return;
            }

            if (horizontalFlySpeed > 0f)
            {
                _horizontalFlySpeed = horizontalFlySpeed;
            }

            if (flightOffscreenMarginWorld > 0f)
            {
                _flightOffscreenMarginWorld = flightOffscreenMarginWorld;
            }

            if (maxLifetimeSeconds > 0f)
            {
                _maxLifetimeSeconds = maxLifetimeSeconds;
            }

            _spriteFacesRightWhenScaleXPositive = spriteFacesRightWhenScaleXPositive;
            _invertFlightDirectionX = invertFlightDirectionX;

            if (bombDamage > 0)
            {
                _bombDamage = bombDamage;
            }

            if (bombExplosionRadius > 0f)
            {
                _bombExplosionRadius = bombExplosionRadius;
            }

            if (dropToleranceX > 0f)
            {
                _dropToleranceX = dropToleranceX;
            }
        }

        public void StartFlight()
        {
            if (_model == null || _stateMachine == null)
            {
                return;
            }

            _cam = ResolveGameplayCamera();
            _bunkerHitbox = FindAnyObjectByType<BunkerHitbox_V2>(FindObjectsInactive.Include);
            _model.expireAt = Time.time + Mathf.Max(2f, _maxLifetimeSeconds);
            _model.bombDropped = false;
            _model.started = true;
            _model.directionX = ResolveAndSanitizeInitialDirectionX();
            _approachStartedAt = Time.time;
            _hasEnteredViewportHorizontally = false;
            _pendingFlightIntegration = false;
            EnsureFlightRigidbodyReady();

            _returnToFlyAfterDropAt = 0f;
            _hoverEndAt = 0f;
            _stateMachine.ChangeState(BombDroneState_V2.Fly);

            EnsureAttachedPayloadForSpawn();
            SyncAttachedPayloadSorting();
        }

        public void FreezeForCombatMatrixHarness()
        {
            if (_model != null)
            {
                _model.frozenForCombatMatrixHarness = true;
            }
        }

        public void OnDestroyed()
        {
            _stateMachine?.ChangeState(BombDroneState_V2.Die);
        }

        public void OnAnimationEvent(AnimationEventType eventType)
        {
            // No Spine events wired yet for BombDrone.
        }

        private void Update()
        {
            if (_model == null || _stateMachine == null || !_model.started || _model.frozenForCombatMatrixHarness)
            {
                return;
            }

            BombDroneState_V2 state = _stateMachine.CurrentState;
            if (Time.time >= _model.expireAt)
            {
                DespawnSelfViaPool(gameObject);
                return;
            }

            if (state == BombDroneState_V2.Idle || state == BombDroneState_V2.Die)
            {
                return;
            }

            TickStateTimers(state);

            if (ShouldIntegrateHorizontalFlight(state))
            {
                _pendingFlightIntegration = true;
            }

            TrackViewportEntryIfNeeded();
            CorrectApproachDirectionIfDriftingOffscreen(state);
            ForceApproachTowardPlayfieldWhenOutsideViewport(state);

            TryDespawnWhenPastCameraBounds(state);
        }

        private void FixedUpdate()
        {
            if (_model == null || _stateMachine == null || !_model.started || _model.frozenForCombatMatrixHarness)
            {
                return;
            }

            if (!_pendingFlightIntegration)
            {
                return;
            }

            _pendingFlightIntegration = false;
            BombDroneState_V2 state = _stateMachine.CurrentState;
            if (state == BombDroneState_V2.Idle || state == BombDroneState_V2.Die)
            {
                return;
            }

            IntegrateHorizontalFlight(state);
        }

        private static bool ShouldIntegrateHorizontalFlight(BombDroneState_V2 state)
        {
            // DropBomb follows hover release; keep flying out while the one-shot drop clip plays.
            return state == BombDroneState_V2.Fly || state == BombDroneState_V2.DropBomb;
        }

        private void IntegrateHorizontalFlight(BombDroneState_V2 state)
        {
            EnsureFlightRigidbodyReady();
            float speed = Mathf.Max(0.1f, _horizontalFlySpeed);
            Vector2 delta = Vector2.right * (_model.directionX * speed * Time.fixedDeltaTime);
            if (_rb != null)
            {
                _rb.MovePosition(_rb.position + delta);
            }
            else
            {
                transform.position += (Vector3)delta;
                Physics2D.SyncTransforms();
            }

            if (!_model.bombDropped && state == BombDroneState_V2.Fly)
            {
                TryBeginHoverOverBunker();
            }
        }

        private void EnsureFlightRigidbodyReady()
        {
            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody2D>();
            }

            if (_rb == null)
            {
                return;
            }

            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.simulated = true;
            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.WakeUp();
        }

        private void TickStateTimers(BombDroneState_V2 state)
        {
            if (state == BombDroneState_V2.HoverOverBunker)
            {
                // Failsafe if hover end time was cleared while still hovering (pool edge cases).
                if (_hoverEndAt <= 0f)
                {
                    _hoverEndAt = Time.time;
                }

                if (Time.time >= _hoverEndAt)
                {
                    CompleteHoverAndBeginDrop();
                }
            }
            else if (state == BombDroneState_V2.DropBomb)
            {
                if (_returnToFlyAfterDropAt <= 0f)
                {
                    _returnToFlyAfterDropAt = Time.time + Mathf.Max(0.05f, _dropBombStateDuration);
                }

                if (Time.time >= _returnToFlyAfterDropAt)
                {
                    _returnToFlyAfterDropAt = 0f;
                    _stateMachine.ChangeState(BombDroneState_V2.Fly);
                }
            }
        }

        private void TryDespawnWhenPastCameraBounds(BombDroneState_V2 state)
        {
            RefreshFlightCameraIfNeeded();

            if (_model.bombDropped && IsPastExitViewportHorizontalEdges(_cam, 0.02f))
            {
                DespawnSelfViaPool(gameObject);
                return;
            }

            if (!TryGetOrthographicCameraHorizontalBounds(_cam, _flightOffscreenMarginWorld, out float left, out float right))
            {
                TryApproachStuckOutsideViewportFailsafe(state);
                return;
            }

            float x = GetFlightWorldPositionX();
            if (_model.bombDropped)
            {
                if (x > right || x < left)
                {
                    DespawnSelfViaPool(gameObject);
                }

                return;
            }

            if (IsActiveFlightState(state) &&
                IsPastHorizontalFlyBounds(x, _model.directionX, left, right))
            {
                DespawnSelfViaPool(gameObject);
                return;
            }

            if (IsActiveFlightState(state) &&
                (x > right || x < left) &&
                !_hasEnteredViewportHorizontally &&
                Time.time - _approachStartedAt >= 3f)
            {
                DespawnSelfViaPool(gameObject);
                return;
            }

            if (state == BombDroneState_V2.HoverOverBunker &&
                !IsHorizontallyInsideViewport(0.12f) &&
                Time.time >= _hoverEndAt + 0.75f)
            {
                DespawnSelfViaPool(gameObject);
                return;
            }

            TryApproachStuckOutsideViewportFailsafe(state);
        }

        private static bool IsActiveFlightState(BombDroneState_V2 state)
        {
            return state == BombDroneState_V2.Fly ||
                   state == BombDroneState_V2.HoverOverBunker ||
                   state == BombDroneState_V2.DropBomb;
        }

        private void RefreshFlightCameraIfNeeded()
        {
            if (_spawnCameraOverride != null && _spawnCameraOverride.isActiveAndEnabled)
            {
                _cam = _spawnCameraOverride;
                return;
            }

            if (_cam == null || !_cam.isActiveAndEnabled)
            {
                _cam = ResolveGameplayCamera();
            }
        }

        private Camera ResolveGameplayCamera()
        {
            if (_spawnCameraOverride != null && _spawnCameraOverride.isActiveAndEnabled)
            {
                return _spawnCameraOverride;
            }

            EnemySpawner_V2 spawner = FindAnyObjectByType<EnemySpawner_V2>(FindObjectsInactive.Include);
            if (spawner != null)
            {
                Camera spawnerCamera = spawner.GetSpawnCamera();
                if (spawnerCamera != null)
                {
                    return spawnerCamera;
                }
            }

            return Camera.main;
        }

        private float ResolveAndSanitizeInitialDirectionX()
        {
            float directionX;
            if (_hasSpawnSide)
            {
                float travelDir = _spawnedFromLeft ? 1f : -1f;
                directionX = _invertFlightDirectionX ? -travelDir : travelDir;
            }
            else
            {
                directionX = ResolveInitialDirectionXTowardBunkerOrFacing(
                    _bunkerHitbox,
                    transform,
                    _spriteFacesRightWhenScaleXPositive);
                if (_invertFlightDirectionX)
                {
                    directionX *= -1f;
                }
            }

            return SanitizeApproachDirectionTowardPlayfield(directionX);
        }

        private void ForceApproachTowardPlayfieldWhenOutsideViewport(BombDroneState_V2 state)
        {
            if (_model.bombDropped || state == BombDroneState_V2.HoverOverBunker)
            {
                return;
            }

            if (IsHorizontallyInsideViewport(0.04f))
            {
                return;
            }

            RefreshFlightCameraIfNeeded();
            if (_cam == null)
            {
                return;
            }

            float cameraX = _cam.transform.position.x;
            float worldX = GetFlightWorldPositionX();
            float towardCenter = cameraX - worldX;
            if (Mathf.Abs(towardCenter) <= 0.05f)
            {
                return;
            }

            _model.directionX = Mathf.Sign(towardCenter);
        }

        private float GetFlightWorldPositionX()
        {
            if (_rb != null)
            {
                return _rb.position.x;
            }

            return transform.position.x;
        }

        private float SanitizeApproachDirectionTowardPlayfield(float directionX)
        {
            RefreshFlightCameraIfNeeded();
            if (_cam == null)
            {
                return directionX;
            }

            float worldX = GetFlightWorldPositionX();
            float cameraX = _cam.transform.position.x;
            if (worldX < cameraX - 0.05f && directionX < 0f)
            {
                return 1f;
            }

            if (worldX > cameraX + 0.05f && directionX > 0f)
            {
                return -1f;
            }

            return directionX;
        }

        private void TrackViewportEntryIfNeeded()
        {
            if (_hasEnteredViewportHorizontally || _model.bombDropped)
            {
                return;
            }

            if (IsHorizontallyInsideViewport(0.02f))
            {
                _hasEnteredViewportHorizontally = true;
            }
        }

        private void CorrectApproachDirectionIfDriftingOffscreen(BombDroneState_V2 state)
        {
            if (_model.bombDropped || state == BombDroneState_V2.HoverOverBunker)
            {
                return;
            }

            _model.directionX = SanitizeApproachDirectionTowardPlayfield(_model.directionX);
        }

        private void TryApproachStuckOutsideViewportFailsafe(BombDroneState_V2 state)
        {
            if (_model.bombDropped || !IsActiveFlightState(state) || _hasEnteredViewportHorizontally)
            {
                return;
            }

            float elapsed = Time.time - _approachStartedAt;
            if (elapsed < 5f || IsHorizontallyInsideViewport(0.12f))
            {
                return;
            }

            if (elapsed < 10f)
            {
                ForceApproachTowardPlayfieldWhenOutsideViewport(state);
                return;
            }

            DespawnSelfViaPool(gameObject);
        }

        private bool IsHorizontallyInsideViewport(float viewportMargin)
        {
            RefreshFlightCameraIfNeeded();
            if (_cam == null)
            {
                return false;
            }

            Vector3 viewport = _cam.WorldToViewportPoint(transform.position);
            if (viewport.z <= 0f)
            {
                return false;
            }

            float margin = Mathf.Max(0f, viewportMargin);
            return viewport.x >= -margin && viewport.x <= 1f + margin;
        }

        private bool IsPastExitViewportHorizontalEdges(Camera cam, float viewportMargin)
        {
            if (cam == null)
            {
                return false;
            }

            Vector3 viewport = cam.WorldToViewportPoint(transform.position);
            if (viewport.z <= 0f)
            {
                return false;
            }

            float margin = Mathf.Max(0f, viewportMargin);
            return viewport.x < -margin || viewport.x > 1f + margin;
        }

        private void TryBeginHoverOverBunker()
        {
            if (_stateMachine.CurrentState != BombDroneState_V2.Fly || _model.bombDropped)
            {
                return;
            }

            if (!IsHorizontallyInsideViewport(0.08f))
            {
                return;
            }

            if (_bunkerHitbox == null)
            {
                _bunkerHitbox = FindAnyObjectByType<BunkerHitbox_V2>(FindObjectsInactive.Include);
                if (_bunkerHitbox == null)
                {
                    return;
                }
            }

            float tolerance = Mathf.Max(0.1f, _dropToleranceX);
            float bunkerX = ResolveBunkerDropAlignWorldX(_bunkerHitbox);
            float dx = Mathf.Abs(GetFlightWorldPositionX() - bunkerX);
            if (dx > tolerance)
            {
                return;
            }

            _hoverEndAt = Time.time + Mathf.Max(0.05f, _holdOverBunkerSeconds);
            _stateMachine.ChangeState(BombDroneState_V2.HoverOverBunker);
        }

        private static float ResolveBunkerDropAlignWorldX(BunkerHitbox_V2 bunkerHitbox)
        {
            if (bunkerHitbox == null)
            {
                return 0f;
            }

            Collider2D col = bunkerHitbox.GetComponent<Collider2D>();
            if (col != null)
            {
                return col.bounds.center.x;
            }

            return bunkerHitbox.transform.position.x;
        }

        private Vector3 ResolveBombDropWorldPosition()
        {
            Transform attached = _attachedPayloadBomb;
            if (attached != null && attached && attached.gameObject.activeInHierarchy)
            {
                return attached.position;
            }

            if (_bombDropMount != null)
            {
                return _bombDropMount.position;
            }

            return transform.position;
        }

        private void CompleteHoverAndBeginDrop()
        {
            Vector3 dropPos = ResolveBombDropWorldPosition();
            dropPos.x = CombatBand_V2.ClampBomberReleaseWorldX(dropPos.x);

            _model.bombDropped = true;
            _returnToFlyAfterDropAt = Time.time + Mathf.Max(0.05f, _dropBombStateDuration);
            _stateMachine.ChangeState(BombDroneState_V2.DropBomb);

            bool gameplayBombSpawned = TrySpawnGameplayBomb(dropPos);
            ReleaseOrHideAttachedPayload(dropPos, gameplayBombSpawned);

            if (!gameplayBombSpawned && _droppedBombPrefab != null)
            {
                GameObject dropped = SimplePrefabPool_V2.Spawn(_droppedBombPrefab, dropPos, Quaternion.identity);
                ApplyDroppedBombPresentation(dropped);
                ApplyDroppedBombPhysics(dropped);
            }
        }

        private bool TrySpawnGameplayBomb(Vector3 dropPos)
        {
            if (_bombProjectilePrefab == null)
            {
                return false;
            }

            BombProjectile_V2 bomb = SimplePrefabPool_V2.Spawn(_bombProjectilePrefab, dropPos, Quaternion.identity);
            if (bomb == null)
            {
                return false;
            }

            Vector2 inherited = _rb != null ? _rb.linearVelocity : Vector2.zero;
            bomb.Initialize(inherited, Mathf.Max(1, _bombDamage), Mathf.Max(0.2f, _bombExplosionRadius));
            ApplyDroppedBombPresentation(bomb.gameObject);
            return true;
        }

        private void SyncAttachedPayloadSorting()
        {
            Transform attached = _attachedPayloadBomb;
            if (attached == null || !attached)
            {
                return;
            }

            SpriteRenderer payloadRenderer = attached.GetComponent<SpriteRenderer>();
            if (payloadRenderer == null)
            {
                return;
            }

            MeshRenderer droneBody = GetComponentInChildren<MeshRenderer>(true);
            if (droneBody == null)
            {
                return;
            }

            payloadRenderer.sortingLayerID = droneBody.sortingLayerID;
            payloadRenderer.sortingOrder = droneBody.sortingOrder - 1;
        }

        private void ApplyDroppedBombPresentation(GameObject dropped)
        {
            if (dropped == null)
            {
                return;
            }

            SpriteRenderer droppedRenderer = dropped.GetComponent<SpriteRenderer>();
            if (droppedRenderer == null)
            {
                return;
            }

            Transform attached = _attachedPayloadBomb;
            SpriteRenderer payloadRenderer = attached != null && attached
                ? attached.GetComponent<SpriteRenderer>()
                : null;

            if (payloadRenderer != null)
            {
                droppedRenderer.sortingLayerID = payloadRenderer.sortingLayerID;
                droppedRenderer.sortingOrder = payloadRenderer.sortingOrder;
                dropped.transform.localScale = attached.lossyScale;
                return;
            }

            MeshRenderer droneBody = GetComponentInChildren<MeshRenderer>(true);
            if (droneBody != null)
            {
                droppedRenderer.sortingLayerID = droneBody.sortingLayerID;
                droppedRenderer.sortingOrder = droneBody.sortingOrder - 1;
            }
        }

        private void ReleaseOrHideAttachedPayload(Vector3 dropPos, bool gameplayBombSpawned)
        {
            Transform attached = _attachedPayloadBomb;
            if (attached == null || !attached || !attached.IsChildOf(transform))
            {
                return;
            }

            if (gameplayBombSpawned)
            {
                // Cosmetic bomb5 sprite on the drone; pooled BombProjectile_V2 owns fall + explosion.
                attached.gameObject.SetActive(false);
                return;
            }

            attached.SetParent(null, true);
            attached.position = dropPos;
            ApplyDroppedBombPhysics(attached.gameObject);
        }

        private void EnsureAttachedPayloadForSpawn()
        {
            Transform attached = _attachedPayloadBomb;
            if (attached != null && attached && attached.IsChildOf(transform))
            {
                if (!attached.gameObject.activeSelf)
                {
                    attached.gameObject.SetActive(true);
                }

                return;
            }

            if (_droppedBombPrefab == null)
            {
                return;
            }

            Transform carryParent = ResolvePayloadCarryParent();
            GameObject instance = Instantiate(_droppedBombPrefab, carryParent);
            instance.name = "bomb5@4K";
            DisableCarriedPayloadGameplay(instance);
            Transform t = instance.transform;
            t.localPosition = _attachedPayloadLocalPosition;
            t.localRotation = Quaternion.identity;
            t.localScale = _attachedPayloadLocalScale;
            _attachedPayloadBomb = t;
        }

        private static void DisableCarriedPayloadGameplay(GameObject payload)
        {
            if (payload == null)
            {
                return;
            }

            BombProjectile_V2 projectile = payload.GetComponent<BombProjectile_V2>();
            if (projectile != null)
            {
                projectile.enabled = false;
            }

            Collider2D col = payload.GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = false;
            }

            Rigidbody2D rb = payload.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = false;
            }
        }

        private Transform ResolvePayloadCarryParent()
        {
            BombDroneView_V2 view = GetComponentInChildren<BombDroneView_V2>(true);
            return view != null ? view.transform : transform;
        }

        private void ApplyDroppedBombPhysics(GameObject dropped)
        {
            if (dropped == null)
            {
                return;
            }

            Rigidbody2D dropRb = dropped.GetComponent<Rigidbody2D>();
            if (dropRb == null)
            {
                dropRb = dropped.AddComponent<Rigidbody2D>();
            }

            dropRb.bodyType = RigidbodyType2D.Dynamic;
            dropRb.gravityScale = _droppedBombGravityScale;
            Vector2 inherited = _rb != null ? _rb.linearVelocity : Vector2.zero;
            dropRb.linearVelocity = inherited;
        }

        private void OnDisable()
        {
            if (_model == null)
            {
                return;
            }

            _model.started = false;
            _model.bombDropped = false;
            _model.expireAt = 0f;
            _model.frozenForCombatMatrixHarness = false;
            _returnToFlyAfterDropAt = 0f;
            _hoverEndAt = 0f;
            _approachStartedAt = 0f;
            _hasEnteredViewportHorizontally = false;
            _hasSpawnSide = false;
            _spawnedFromLeft = false;
            _spawnCameraOverride = null;
            _pendingFlightIntegration = false;
        }
    }
}
