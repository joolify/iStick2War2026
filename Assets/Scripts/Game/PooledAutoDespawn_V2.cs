using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Utility timer for pooled transient objects (e.g. explosion effects).
    /// </summary>
    public sealed class PooledAutoDespawn_V2 : MonoBehaviour
    {
        private float _despawnAt;
        private bool _armed;

        public void Arm(float seconds)
        {
            _despawnAt = Time.time + Mathf.Max(0.01f, seconds);
            _armed = true;
        }

        private void OnEnable()
        {
            // Waiting for explicit Arm() on each spawn.
            _armed = false;
            _despawnAt = 0f;
        }

        private void Update()
        {
            if (!_armed)
            {
                return;
            }

            if (Time.time >= _despawnAt)
            {
                _armed = false;
                SimplePrefabPool_V2.Despawn(gameObject);
            }
        }
    }
}
