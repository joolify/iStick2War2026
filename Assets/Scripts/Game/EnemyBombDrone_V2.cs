using UnityEngine;

namespace iStick2War_V2
{
    [DisallowMultipleComponent]
    public sealed class EnemyBombDrone_V2 : MonoBehaviour
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

        private Rigidbody2D _rb;
        private Camera _cam;
        private BunkerHitbox_V2 _bunkerHitbox;
        private float _directionX;
        private float _expireAt;
        private bool _bombDropped;
        private bool _started;

        public void BeginRun()
        {
            _rb = GetComponent<Rigidbody2D>();
            _cam = Camera.main;
            _bunkerHitbox = FindAnyObjectByType<BunkerHitbox_V2>(FindObjectsInactive.Include);
            _expireAt = Time.time + Mathf.Max(2f, _maxLifetimeSeconds);
            _bombDropped = false;
            _started = true;

            bool positiveScaleMeansFacingRight = _spriteFacesRightWhenScaleXPositive;
            bool facingRight = transform.lossyScale.x >= 0f
                ? positiveScaleMeansFacingRight
                : !positiveScaleMeansFacingRight;
            _directionX = facingRight ? 1f : -1f;
            if (_invertFlightDirectionX)
            {
                _directionX *= -1f;
            }
        }

        private void Start()
        {
            if (!_started)
            {
                BeginRun();
            }
        }

        private void Update()
        {
            if (!_started)
            {
                return;
            }

            float speed = Mathf.Max(0.1f, _horizontalFlySpeed);
            transform.position += Vector3.right * (_directionX * speed * Time.deltaTime);

            if (!_bombDropped && _bombProjectilePrefab != null)
            {
                TryDropBombOverBunker();
            }

            if (Time.time >= _expireAt)
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
            if ((_directionX > 0f && x > rightBound) || (_directionX < 0f && x < leftBound))
            {
                DespawnSelf();
            }
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
            if (bomb != null)
            {
                Vector2 inherited = _rb != null ? _rb.linearVelocity : Vector2.zero;
                bomb.Initialize(inherited, Mathf.Max(1, _bombDamage), Mathf.Max(0.2f, _bombExplosionRadius));
                _bombDropped = true;
            }
        }

        private void OnDisable()
        {
            _started = false;
            _bombDropped = false;
            _expireAt = 0f;
        }

        private void DespawnSelf()
        {
            OnDisable();
            SimplePrefabPool_V2.Despawn(gameObject);
        }
    }
}
