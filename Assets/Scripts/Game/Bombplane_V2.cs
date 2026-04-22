using UnityEngine;

namespace iStick2War_V2
{
    [DisallowMultipleComponent]
    public sealed class Bombplane_V2 : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] private bool _enableHorizontalFlight = true;
        [SerializeField] private float _horizontalFlySpeed = 5.5f;
        [SerializeField] private float _flightOffscreenMarginWorld = 4f;
        [SerializeField] private float _flightMaxLifetimeSeconds = 45f;
        [Tooltip("If true: +scaleX means plane nose points right. Disable if sprite faces left at +scaleX.")]
        [SerializeField] private bool _spriteFacesRightWhenScaleXPositive = false;
        [SerializeField] private bool _invertFlightDirectionX = false;

        [Header("Bombing")]
        [SerializeField] private BombProjectile_V2 _bombProjectilePrefab;
        [SerializeField] private Transform _bombDropMount;
        [SerializeField] private float _bombDropIntervalSeconds = 1.25f;
        [SerializeField] private int _maxBombsPerPass = 4;
        [SerializeField] private int _bombDamage = 30;
        [SerializeField] private float _bombExplosionRadius = 2f;
        [SerializeField] private bool _debugBombLogs;

        private float _nextDropTime;
        private int _bombsDropped;
        private bool _hasStarted;
        private Rigidbody2D _rigidbody2D;
        private Camera _flightCamera;
        private float _flightDirectionX;
        private float _expireAt;

        public void BeginBombRun()
        {
            _hasStarted = true;
            _nextDropTime = Time.time + Mathf.Max(0.2f, _bombDropIntervalSeconds);
            _bombsDropped = 0;
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _flightCamera = Camera.main;
            _expireAt = Time.time + Mathf.Max(1f, _flightMaxLifetimeSeconds);

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

            TickFlight();

            if (_bombProjectilePrefab == null || _bombsDropped >= Mathf.Max(1, _maxBombsPerPass))
            {
                return;
            }

            if (Time.time < _nextDropTime)
            {
                return;
            }

            DropBomb();
            _nextDropTime = Time.time + Mathf.Max(0.2f, _bombDropIntervalSeconds);
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
    }
}
