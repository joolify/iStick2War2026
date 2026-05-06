using Assets.Scripts.Components;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class BombDroneController_V2 : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] private float _horizontalFlySpeed = 6.2f;
        [SerializeField] private float _flightOffscreenMarginWorld = 4f;
        [SerializeField] private float _maxLifetimeSeconds = 30f;
        [SerializeField] private bool _spriteFacesRightWhenScaleXPositive = true;
        [SerializeField] private bool _invertFlightDirectionX = false;

        [Header("Bombing")]
        [SerializeField] private BombProjectile_V2 _bombProjectilePrefab;
        [SerializeField] private Transform _bombDropMount;
        [SerializeField] private int _bombDamage = 24;
        [SerializeField] private float _bombExplosionRadius = 1.75f;
        [SerializeField] private float _dropToleranceX = 0.7f;
        [SerializeField] private float _dropBombStateDuration = 0.35f;

        private BombDroneModel_V2 _model;
        private BombDroneStateMachine_V2 _stateMachine;
        private Rigidbody2D _rb;
        private Camera _cam;
        private BunkerHitbox_V2 _bunkerHitbox;
        private float _returnToFlyAfterDropAt;

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
            _model.directionX = ResolveInitialDirectionX();
            if (_invertFlightDirectionX)
            {
                _model.directionX *= -1f;
            }

            _returnToFlyAfterDropAt = 0f;
            _stateMachine.ChangeState(BombDroneState_V2.Fly);
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

            float speed = Mathf.Max(0.1f, _horizontalFlySpeed);
            transform.position += Vector3.right * (_model.directionX * speed * Time.deltaTime);
            Physics2D.SyncTransforms();

            if (_stateMachine.CurrentState == BombDroneState_V2.DropBomb &&
                _returnToFlyAfterDropAt > 0f &&
                Time.time >= _returnToFlyAfterDropAt)
            {
                _returnToFlyAfterDropAt = 0f;
                _stateMachine.ChangeState(BombDroneState_V2.Fly);
            }

            if (!_model.bombDropped && _bombProjectilePrefab != null)
            {
                TryDropBombOverBunker();
            }

            if (Time.time >= _model.expireAt)
            {
                DespawnSelf();
                return;
            }

            if (_cam == null || !_cam.orthographic)
            {
                return;
            }

            float halfHeight = _cam.orthographicSize;
            float halfWidth = halfHeight * _cam.aspect;
            float camX = _cam.transform.position.x;
            float margin = Mathf.Max(0.5f, _flightOffscreenMarginWorld);
            float leftBound = camX - halfWidth - margin;
            float rightBound = camX + halfWidth + margin;
            float x = transform.position.x;
            if ((_model.directionX > 0f && x > rightBound) || (_model.directionX < 0f && x < leftBound))
            {
                DespawnSelf();
            }
        }

        private float ResolveInitialDirectionX()
        {
            if (_bunkerHitbox != null)
            {
                float dx = _bunkerHitbox.transform.position.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.05f)
                {
                    return Mathf.Sign(dx);
                }
            }

            bool positiveScaleMeansFacingRight = _spriteFacesRightWhenScaleXPositive;
            bool facingRight = transform.lossyScale.x >= 0f
                ? positiveScaleMeansFacingRight
                : !positiveScaleMeansFacingRight;
            return facingRight ? 1f : -1f;
        }

        private void TryDropBombOverBunker()
        {
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

            Vector3 dropPos = _bombDropMount != null ? _bombDropMount.position : transform.position;
            BombProjectile_V2 bomb = SimplePrefabPool_V2.Spawn(_bombProjectilePrefab, dropPos, Quaternion.identity);
            if (bomb == null)
            {
                return;
            }

            Vector2 inherited = _rb != null ? _rb.linearVelocity : Vector2.zero;
            bomb.Initialize(inherited, Mathf.Max(1, _bombDamage), Mathf.Max(0.2f, _bombExplosionRadius));
            _model.bombDropped = true;
            _returnToFlyAfterDropAt = Time.time + Mathf.Max(0.05f, _dropBombStateDuration);
            _stateMachine.ChangeState(BombDroneState_V2.DropBomb);
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
        }

        private void DespawnSelf()
        {
            SimplePrefabPool_V2.Despawn(gameObject);
        }
    }
}
