using UnityEngine;

namespace iStick2War_V2
{
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

        private float _nextDropTime;
        private int _bombsDropped;
        private bool _hasStarted;
        private Rigidbody2D _rigidbody2D;
        private Camera _flightCamera;
        private float _flightDirectionX;
        private float _expireAt;
        private bool _frozenForCombatMatrixHarness;

        /// <summary>
        /// Starts a pass using sprite scale to guess travel direction (scene-placed planes only).
        /// Prefer <see cref="BeginBombRun(bool)"/> from spawners so direction matches spawn side.
        /// </summary>
        public void BeginBombRun()
        {
            BeginBombRun(spawnedFromLeft: null);
        }

        /// <param name="spawnedFromLeft">
        /// Same as aircraft spawn: true = entered from left, should fly toward +X (before <see cref="_invertFlightDirectionX"/>).
        /// </param>
        public void BeginBombRun(bool spawnedFromLeft)
        {
            BeginBombRun(spawnedFromLeft: (bool?)spawnedFromLeft);
        }

        private void BeginBombRun(bool? spawnedFromLeft)
        {
            EnsureHitboxIfMissing();

            _hasStarted = true;
            _nextDropTime = Time.time + Mathf.Max(0.2f, _bombDropIntervalSeconds);
            _bombsDropped = 0;
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _flightCamera = Camera.main;
            _expireAt = Time.time + Mathf.Max(1f, _flightMaxLifetimeSeconds);

            if (spawnedFromLeft.HasValue)
            {
                float baseDir = spawnedFromLeft.Value ? 1f : -1f;
                _flightDirectionX = _invertFlightDirectionX ? -baseDir : baseDir;
            }
            else
            {
                bool positiveScaleMeansFacingRight = _spriteFacesRightWhenScaleXPositive;
                bool facingRight = transform.lossyScale.x >= 0f
                    ? positiveScaleMeansFacingRight
                    : !positiveScaleMeansFacingRight;
                _flightDirectionX = facingRight ? 1f : -1f;
                if (_invertFlightDirectionX)
                {
                    _flightDirectionX *= -1f;
                }
            }
        }

        /// <summary>
        /// Holds position and skips bombing / flight / despawn logic (combat matrix harness).
        /// </summary>
        public void FreezeForCombatMatrixHarness()
        {
            _frozenForCombatMatrixHarness = true;
        }

        private void Start()
        {
            if (!_hasStarted)
            {
                BeginBombRun();
            }
        }

        private void Update()
        {
            if (!_hasStarted)
            {
                return;
            }

            if (_frozenForCombatMatrixHarness)
            {
                return;
            }

            // Drop before movement/despawn so a pass cannot exit the same frame without attempting a release.
            if (_bombProjectilePrefab != null && _bombsDropped < Mathf.Max(1, _maxBombsPerPass) && Time.time >= _nextDropTime)
            {
                DropBomb();
                _nextDropTime = Time.time + Mathf.Max(0.2f, _bombDropIntervalSeconds);
            }

            TickFlight();
        }

        private void DropBomb()
        {
            Vector3 dropPos = _bombDropMount != null ? _bombDropMount.position : transform.position;
            BombProjectile_V2 bomb = SimplePrefabPool_V2.Spawn(_bombProjectilePrefab, dropPos, Quaternion.identity);
            if (bomb != null)
            {
                Vector2 inherited = _rigidbody2D != null ? _rigidbody2D.linearVelocity : Vector2.zero;
                bomb.Initialize(inherited, _bombDamage, _bombExplosionRadius);
                _bombsDropped++;
                if (_debugBombLogs)
                {
                    Debug.Log($"[Bombplane_V2] Dropped bomb {_bombsDropped}/{_maxBombsPerPass} at {dropPos}");
                }
            }
        }

        private void TickFlight()
        {
            if (!_enableHorizontalFlight)
            {
                return;
            }

            float speed = Mathf.Max(0.01f, _horizontalFlySpeed);
            transform.position += Vector3.right * (_flightDirectionX * speed * Time.deltaTime);
            Physics2D.SyncTransforms();

            if (Time.time >= _expireAt)
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
            float margin = Mathf.Max(0.5f, _flightOffscreenMarginWorld);
            float camX = _flightCamera.transform.position.x;
            float leftBound = camX - halfWidth - margin;
            float rightBound = camX + halfWidth + margin;
            float x = transform.position.x;

            if ((_flightDirectionX > 0f && x > rightBound) || (_flightDirectionX < 0f && x < leftBound))
            {
                DespawnSelf();
            }
        }

        private void OnDisable()
        {
            _hasStarted = false;
            _bombsDropped = 0;
            _nextDropTime = 0f;
            _expireAt = 0f;
        }

        private void DespawnSelf()
        {
            SimplePrefabPool_V2.Despawn(gameObject);
        }

        /// <summary>
        /// World velocity used for flight (transform-based). Exposed for AA lead / intercept aim (e.g. AutoHero bazooka).
        /// </summary>
        public Vector2 GetHorizontalFlightVelocityWorld()
        {
            if (!_hasStarted || !_enableHorizontalFlight)
            {
                return Vector2.zero;
            }

            float speed = Mathf.Max(0.01f, _horizontalFlySpeed);
            return new Vector2(_flightDirectionX * speed, 0f);
        }

        private void EnsureHitboxIfMissing()
        {
            if (!_ensureHitboxFromSpriteIfMissing)
            {
                return;
            }

            Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
            if (cols != null && cols.Length > 0)
            {
                return;
            }

            var box = gameObject.AddComponent<BoxCollider2D>();
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Bounds b = sr.sprite.bounds;
                box.size = new Vector2(b.size.x, b.size.y);
                box.offset = new Vector2(b.center.x, b.center.y);
            }
            else
            {
                box.size = _fallbackHitboxSize;
                box.offset = Vector2.zero;
            }
        }
    }
}
