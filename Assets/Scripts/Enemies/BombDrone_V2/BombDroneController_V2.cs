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
 * - Update: horizontal motion only in Fly; HoverOverBunker holds position; DropBomb holds until clip timer
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

        public void Initialize(BombDroneModel_V2 model, BombDroneStateMachine_V2 stateMachine)
        {
            _model = model;
            _stateMachine = stateMachine;
            _rb = GetComponent<Rigidbody2D>();
        }

        public void StartFlight()
        {
            if (_model == null || _stateMachine == null)
            {
                return;
            }

            _cam = Camera.main;
            _bunkerHitbox = FindAnyObjectByType<BunkerHitbox_V2>(FindObjectsInactive.Include);
            _model.expireAt = Time.time + Mathf.Max(2f, _maxLifetimeSeconds);
            _model.bombDropped = false;
            _model.started = true;
            _model.directionX = ResolveInitialDirectionXTowardBunkerOrFacing(
                _bunkerHitbox,
                transform,
                _spriteFacesRightWhenScaleXPositive);
            if (_invertFlightDirectionX)
            {
                _model.directionX *= -1f;
            }

            _returnToFlyAfterDropAt = 0f;
            _hoverEndAt = 0f;
            _stateMachine.ChangeState(BombDroneState_V2.Fly);

            EnsureAttachedPayloadForSpawn();
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

            if (state == BombDroneState_V2.Fly)
            {
                float speed = Mathf.Max(0.1f, _horizontalFlySpeed);
                transform.position += Vector3.right * (_model.directionX * speed * Time.deltaTime);
                Physics2D.SyncTransforms();

                if (!_model.bombDropped)
                {
                    TryBeginHoverOverBunker();
                }
            }
            else if (state == BombDroneState_V2.HoverOverBunker)
            {
                if (Time.time >= _hoverEndAt)
                {
                    CompleteHoverAndBeginDrop();
                }
            }
            else if (state == BombDroneState_V2.DropBomb)
            {
                if (_returnToFlyAfterDropAt > 0f && Time.time >= _returnToFlyAfterDropAt)
                {
                    _returnToFlyAfterDropAt = 0f;
                    _stateMachine.ChangeState(BombDroneState_V2.Fly);
                }
            }

            if (Time.time >= _model.expireAt)
            {
                DespawnSelfViaPool(gameObject);
                return;
            }

            if (state == BombDroneState_V2.Fly &&
                TryGetOrthographicCameraHorizontalBounds(_cam, _flightOffscreenMarginWorld, out float left, out float right))
            {
                float x = transform.position.x;
                if (IsPastHorizontalFlyBounds(x, _model.directionX, left, right))
                {
                    DespawnSelfViaPool(gameObject);
                }
            }
        }

        private void TryBeginHoverOverBunker()
        {
            if (_stateMachine.CurrentState != BombDroneState_V2.Fly || _model.bombDropped)
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
            float dx = Mathf.Abs(transform.position.x - _bunkerHitbox.transform.position.x);
            if (dx > tolerance)
            {
                return;
            }

            _hoverEndAt = Time.time + Mathf.Max(0.05f, _holdOverBunkerSeconds);
            _stateMachine.ChangeState(BombDroneState_V2.HoverOverBunker);
        }

        private void CompleteHoverAndBeginDrop()
        {
            Vector3 dropPos = _bombDropMount != null ? _bombDropMount.position : transform.position;

            _model.bombDropped = true;
            _returnToFlyAfterDropAt = Time.time + Mathf.Max(0.05f, _dropBombStateDuration);
            _stateMachine.ChangeState(BombDroneState_V2.DropBomb);

            Transform attached = _attachedPayloadBomb;
            if (attached != null && attached && attached.IsChildOf(transform))
            {
                attached.SetParent(null, true);
                attached.position = dropPos;
                ApplyDroppedBombPhysics(attached.gameObject);
                return;
            }

            if (_droppedBombPrefab != null)
            {
                GameObject dropped = SimplePrefabPool_V2.Spawn(_droppedBombPrefab, dropPos, Quaternion.identity);
                ApplyDroppedBombPhysics(dropped);
            }
            else if (_bombProjectilePrefab != null)
            {
                BombProjectile_V2 bomb = SimplePrefabPool_V2.Spawn(_bombProjectilePrefab, dropPos, Quaternion.identity);
                if (bomb != null)
                {
                    Vector2 inherited = _rb != null ? _rb.linearVelocity : Vector2.zero;
                    bomb.Initialize(inherited, Mathf.Max(1, _bombDamage), Mathf.Max(0.2f, _bombExplosionRadius));
                }
            }
        }

        private void EnsureAttachedPayloadForSpawn()
        {
            Transform attached = _attachedPayloadBomb;
            if (attached != null && attached && attached.IsChildOf(transform))
            {
                return;
            }

            if (_droppedBombPrefab == null)
            {
                return;
            }

            Transform carryParent = ResolvePayloadCarryParent();
            GameObject instance = Instantiate(_droppedBombPrefab, carryParent);
            instance.name = "bomb5@4K";
            Transform t = instance.transform;
            t.localPosition = _attachedPayloadLocalPosition;
            t.localRotation = Quaternion.identity;
            t.localScale = _attachedPayloadLocalScale;
            _attachedPayloadBomb = t;
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
        }
    }
}
