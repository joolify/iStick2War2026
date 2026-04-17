using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Simple 2D fly-by: moves along +X when spawned from the left approach, along -X from the right.
    /// Destroys the GameObject once it passes outside the orthographic camera frustum (plus margin).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AircraftFlyAcrossScreen_V2 : MonoBehaviour
    {
        private float _dirX;
        private float _speed;
        private Camera _cam;
        private float _halfWidth;
        private float _halfHeight;
        private float _offscreenMargin;
        private float _expireTime;

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
            _speed = Mathf.Max(0.01f, speedWorldUnitsPerSecond);
            float baseDir = spawnedFromLeftAnchor ? 1f : -1f;
            _dirX = invertFlightDirectionX ? -baseDir : baseDir;
            _cam = cam != null ? cam : Camera.main;
            _offscreenMargin = Mathf.Max(0.5f, offscreenMarginWorld);
            _expireTime = Time.time + Mathf.Max(1f, maxLifetimeSeconds);

            if (_cam != null && _cam.orthographic)
            {
                _halfHeight = _cam.orthographicSize;
                _halfWidth = _halfHeight * _cam.aspect;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            transform.position += new Vector3(_dirX * _speed * dt, 0f, 0f);

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
