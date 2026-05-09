using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombPlaneController_V2 (Critical Component / Brain)
 *
 * This acts as the gameplay driver for the bomb plane prefab.
 *
 * Responsibilities:
 * - Apply serialized Config each pass (SetConfig / StartBombRun)
 * - Horizontal flight along X when enabled (camera-bounded despawn)
 * - Timed bomb drops via SimplePrefabPool_V2 + BombProjectile_V2.Initialize
 * - Resolve drop world position: Spine bone (optional) → mount → root
 * - Optional hitbox creation when the prefab has no Collider2D
 * - Pool despawn when lifetime or off-screen rules fire
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLES
 *
 * - Reads/writes BombPlaneModel_V2; drives BombPlaneStateMachine_V2 for View sync
 * - Does not own inspector tuning; Bombplane_V2 builds Config from serialized fields
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Act as the composition root (use Bombplane_V2 for wiring)
 * - Play Spine clips directly (delegated to BombPlaneView_V2 via state events)
 *
 * ---------------------------------------------------------
 * Config struct:
 *
 * Immutable-ish snapshot of tuning for one pass (flight, bombs, debug flags).
 */
    public sealed class BombPlaneController_V2 : MonoBehaviour
    {
        [System.Serializable]
        public struct Config
        {
            public bool enableHorizontalFlight;
            public float horizontalFlySpeed;
            public float flightOffscreenMarginWorld;
            public float flightMaxLifetimeSeconds;
            public bool spriteFacesRightWhenScaleXPositive;
            public bool invertFlightDirectionX;
            public BombProjectile_V2 bombProjectilePrefab;
            public Transform bombDropMount;
            public string bombSpawnBoneName;
            public BombPlaneView_V2 bombSpawnView;
            public float bombDropIntervalSeconds;
            public int maxBombsPerPass;
            public int bombDamage;
            public float bombExplosionRadius;
            public bool debugBombLogs;
            public bool ensureHitboxFromSpriteIfMissing;
            public Vector2 fallbackHitboxSize;
        }

        private BombPlaneModel_V2 _model;
        private BombPlaneStateMachine_V2 _stateMachine;
        private Rigidbody2D _rigidbody2D;
        private Camera _flightCamera;
        private Config _config;

        public void Initialize(BombPlaneModel_V2 model, BombPlaneStateMachine_V2 stateMachine)
        {
            _model = model;
            _stateMachine = stateMachine;
            _rigidbody2D = GetComponent<Rigidbody2D>();
        }

        public void SetConfig(Config config)
        {
            _config = config;
        }

        public void StartBombRun(bool? spawnedFromLeft)
        {
            if (_model == null || _stateMachine == null)
            {
                return;
            }

            EnsureHitboxIfMissing();

            _model.started = true;
            _model.bombsDropped = 0;
            _model.nextDropAt = Time.time + Mathf.Max(0.2f, _config.bombDropIntervalSeconds);
            _model.expireAt = Time.time + Mathf.Max(1f, _config.flightMaxLifetimeSeconds);
            _flightCamera = Camera.main;

            if (spawnedFromLeft.HasValue)
            {
                float baseDir = spawnedFromLeft.Value ? 1f : -1f;
                _model.directionX = _config.invertFlightDirectionX ? -baseDir : baseDir;
            }
            else
            {
                bool positiveScaleMeansFacingRight = _config.spriteFacesRightWhenScaleXPositive;
                bool facingRight = transform.lossyScale.x >= 0f
                    ? positiveScaleMeansFacingRight
                    : !positiveScaleMeansFacingRight;
                _model.directionX = facingRight ? 1f : -1f;
                if (_config.invertFlightDirectionX)
                {
                    _model.directionX *= -1f;
                }
            }

            _stateMachine.ChangeState(BombPlaneState_V2.Fly);
        }

        public void FreezeForCombatMatrixHarness()
        {
            if (_model != null)
            {
                _model.frozenForCombatMatrixHarness = true;
            }
        }

        public Vector2 GetHorizontalFlightVelocityWorld()
        {
            if (_model == null || !_model.started || !_config.enableHorizontalFlight)
            {
                return Vector2.zero;
            }

            float speed = Mathf.Max(0.01f, _config.horizontalFlySpeed);
            return new Vector2(_model.directionX * speed, 0f);
        }

        private void Update()
        {
            if (_model == null || _stateMachine == null || !_model.started || _model.frozenForCombatMatrixHarness)
            {
                return;
            }

            if (_config.bombProjectilePrefab != null &&
                _model.bombsDropped < Mathf.Max(1, _config.maxBombsPerPass) &&
                Time.time >= _model.nextDropAt)
            {
                DropBomb();
                _model.nextDropAt = Time.time + Mathf.Max(0.2f, _config.bombDropIntervalSeconds);
                _stateMachine.ChangeState(BombPlaneState_V2.DropBomb);
                _stateMachine.ChangeState(BombPlaneState_V2.Fly);
            }

            TickFlight();
        }

        private void DropBomb()
        {
            Vector3 dropPos = ResolveBombDropWorldPosition();
            BombProjectile_V2 bomb = SimplePrefabPool_V2.Spawn(_config.bombProjectilePrefab, dropPos, Quaternion.identity);
            if (bomb == null)
            {
                return;
            }

            Vector2 inherited = _rigidbody2D != null ? _rigidbody2D.linearVelocity : Vector2.zero;
            bomb.Initialize(inherited, _config.bombDamage, _config.bombExplosionRadius);
            _model.bombsDropped++;
            if (_config.debugBombLogs)
            {
                Debug.Log($"[BombPlaneController_V2] Dropped bomb {_model.bombsDropped}/{_config.maxBombsPerPass} at {dropPos}");
            }
        }

        private Vector3 ResolveBombDropWorldPosition()
        {
            if (!string.IsNullOrWhiteSpace(_config.bombSpawnBoneName) &&
                _config.bombSpawnView != null &&
                _config.bombSpawnView.TryGetBoneWorldPosition(_config.bombSpawnBoneName, out Vector3 fromBone))
            {
                return fromBone;
            }

            if (_config.bombDropMount != null)
            {
                return _config.bombDropMount.position;
            }

            return transform.position;
        }

        private void TickFlight()
        {
            if (!_config.enableHorizontalFlight)
            {
                return;
            }

            float speed = Mathf.Max(0.01f, _config.horizontalFlySpeed);
            transform.position += Vector3.right * (_model.directionX * speed * Time.deltaTime);
            Physics2D.SyncTransforms();

            if (Time.time >= _model.expireAt)
            {
                DespawnSelf();
                return;
            }

            if (_flightCamera == null || !_flightCamera.orthographic)
            {
                return;
            }

            float halfHeight = _flightCamera.orthographicSize;
            float halfWidth = halfHeight * _flightCamera.aspect;
            float margin = Mathf.Max(0.5f, _config.flightOffscreenMarginWorld);
            float camX = _flightCamera.transform.position.x;
            float leftBound = camX - halfWidth - margin;
            float rightBound = camX + halfWidth + margin;
            float x = transform.position.x;

            if ((_model.directionX > 0f && x > rightBound) || (_model.directionX < 0f && x < leftBound))
            {
                DespawnSelf();
            }
        }

        private void OnDisable()
        {
            if (_model == null)
            {
                return;
            }

            _model.started = false;
            _model.bombsDropped = 0;
            _model.nextDropAt = 0f;
            _model.expireAt = 0f;
            _model.frozenForCombatMatrixHarness = false;
        }

        private void DespawnSelf()
        {
            _stateMachine?.ChangeState(BombPlaneState_V2.Die);
            SimplePrefabPool_V2.Despawn(gameObject);
        }

        private void EnsureHitboxIfMissing()
        {
            if (!_config.ensureHitboxFromSpriteIfMissing)
            {
                return;
            }

            Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
            if (cols != null && cols.Length > 0)
            {
                return;
            }

            BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Bounds b = sr.sprite.bounds;
                box.size = new Vector2(b.size.x, b.size.y);
                box.offset = new Vector2(b.center.x, b.center.y);
            }
            else
            {
                box.size = _config.fallbackHitboxSize;
                box.offset = Vector2.zero;
            }
        }
    }
}
