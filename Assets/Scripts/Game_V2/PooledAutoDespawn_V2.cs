using UnityEngine;

namespace iStick2War_V2
{
    /*
 * PooledAutoDespawn_V2 (Timed return-to-pool helper)
 *
 * PURPOSE:
 * Arms on spawn with Arm(seconds); each Update compares Time.time and calls SimplePrefabPool_V2.Despawn when due.
 * Typical use: explosion chunks or muzzle flashes that should not leak instances during long runs.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Decide gameplay outcomes (callers arm after VFX spawn only).
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Tiny MonoBehaviour add-on so pooled prefabs stay self-scheduling without bespoke coroutines everywhere.
 */
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
