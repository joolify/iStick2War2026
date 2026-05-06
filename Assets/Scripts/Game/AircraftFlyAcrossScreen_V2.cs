using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Simple 2D fly-by: moves along +X when spawned from the left approach, along -X from the right.
    /// Destroys the GameObject once it passes outside the orthographic camera frustum (plus margin).
    /// Uses a kinematic <see cref="Rigidbody2D"/> and <see cref="Rigidbody2D.MovePosition"/> so colliders stay
    /// in sync with physics when project Auto Sync Transforms is off (otherwise fast projectiles pass through).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class AircraftFlyAcrossScreen_V2 : MonoBehaviour
    {
        private float _dirX;
        private float _speed;
        private Camera _cam;
        private float _halfWidth;
        private float _halfHeight;
        private float _offscreenMargin;
        private float _expireTime;
        private bool _flightActive;
        private Rigidbody2D _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        /// <param name="spawnedFromLeftAnchor">True if this aircraft came from the left spawn side.</param>
        /// <param name="invertFlightDirectionX">Flip travel direction if the sprite faces the opposite way to the default mapping.</param>
        public void BeginFlight(
            bool spawnedFromLeftAnchor,
            float speedWorldUnitsPerSecond,
            Camera cam,
            float offscreenMarginWorld,
            float maxLifetimeSeconds,
            bool invertFlightDirectionX = false)
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
            _rb.useFullKinematicContacts = true;
            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            _speed = Mathf.Max(0.01f, speedWorldUnitsPerSecond);
            float baseDir = spawnedFromLeftAnchor ? 1f : -1f;
            _dirX = invertFlightDirectionX ? -baseDir : baseDir;
            _cam = cam != null ? cam : Camera.main;
            _offscreenMargin = Mathf.Max(0.5f, offscreenMarginWorld);
            _expireTime = Time.time + Mathf.Max(1f, maxLifetimeSeconds);
            _flightActive = true;

            if (_cam != null && _cam.orthographic)
            {
                _halfHeight = _cam.orthographicSize;
                _halfWidth = _halfHeight * _cam.aspect;
            }
        }

        /// <summary>
        /// Stops horizontal flight and disables lifetime / off-screen despawn from this component
        /// (used by combat matrix / automation harness).
        /// </summary>
        public void FreezeForCombatMatrixHarness()
        {
            _flightActive = false;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
        }

        private void FixedUpdate()
        {
            if (!_flightActive || _rb == null)
            {
                return;
            }

            Vector2 delta = new Vector2(_dirX * _speed * Time.fixedDeltaTime, 0f);
            _rb.MovePosition(_rb.position + delta);
        }

        private void Update()
        {
            if (!_flightActive)
            {
                return;
            }

            if (Time.time >= _expireTime)
            {
                Destroy(gameObject);
                return;
            }

            if (_cam == null || !_cam.orthographic)
            {
                return;
            }

            Vector3 c = _cam.transform.position;
            float x = transform.position.x;
            float leftBound = c.x - _halfWidth - _offscreenMargin;
            float rightBound = c.x + _halfWidth + _offscreenMargin;

            if (_dirX > 0f && x > rightBound)
            {
                Destroy(gameObject);
            }
            else if (_dirX < 0f && x < leftBound)
            {
                Destroy(gameObject);
            }
        }
    }
}
