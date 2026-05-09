using UnityEngine;

namespace iStick2War_V2
{
    /*
 * AircraftFlyAcrossScreen_V2 (Orthographic fly-by motion)
 *
 * PURPOSE:
 * Moves a kinematic Rigidbody2D horizontally: +X when spawned from the left anchor, -X from the right, using
 * MovePosition so triggers stay aligned when Physics2D Auto Sync Transforms is off. Destroys the GameObject once
 * it clears the camera frustum plus margin, or when lifetime expires.
 *
 * ---------------------------------------------------------
 * INPUT SOURCES
 *
 * - BeginFlight(...) from spawners (EnemySpawner_V2 / prefab harness) with speed, camera, margin, invert flags.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Drop paratroopers or bombs (HelicopterCarrier_V2 / BombPlaneController_V2 own payloads).
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Reusable horizontal transit helper for simple aircraft props without a full AI stack.
 */
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

        // True if this aircraft came from the left spawn side.
        // Flip travel direction if the sprite faces the opposite way to the default mapping.
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

        // Stops horizontal flight and disables lifetime / off-screen despawn from this component
        // (used by combat matrix / automation harness).
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
