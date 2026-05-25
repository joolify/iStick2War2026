using UnityEngine;

namespace iStick2War_V2
{
    /*
 * ShellCasing_V2 (Short-lived pooled casing physics)
 *
 * PURPOSE:
 * Runtime helper for tiny weapon casing props. HeroView_V2 spawns the prefab through SimplePrefabPool_V2,
 * then arms this component with velocity, spin, and lifetime. On timeout it returns the instance to the pool.
 *
 * ---------------------------------------------------------
 * MUST NOT
 *
 * - Decide when weapons fire.
 * - Apply damage, muzzle flashes, audio, or gameplay collision.
 */
    [DisallowMultipleComponent]
    public sealed class ShellCasing_V2 : MonoBehaviour
    {
        private Rigidbody2D _rigidbody2D;
        private float _despawnAtUnscaledTime;
        private bool _armed;

        public void Arm(Vector2 velocity, float angularVelocity, float lifetimeSeconds)
        {
            EnsureRigidbody();

            _armed = true;
            _despawnAtUnscaledTime = Time.unscaledTime + Mathf.Max(0.05f, lifetimeSeconds);

            if (_rigidbody2D != null)
            {
                _rigidbody2D.simulated = true;
                if (_rigidbody2D.bodyType == RigidbodyType2D.Static)
                {
                    _rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
                }

                _rigidbody2D.linearVelocity = velocity;
                _rigidbody2D.angularVelocity = angularVelocity;
                _rigidbody2D.WakeUp();
            }
        }

        private void Awake()
        {
            EnsureRigidbody();
        }

        private void OnEnable()
        {
            _armed = false;
        }

        private void Update()
        {
            if (!_armed || Time.unscaledTime < _despawnAtUnscaledTime)
            {
                return;
            }

            _armed = false;
            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.angularVelocity = 0f;
                _rigidbody2D.Sleep();
            }

            SimplePrefabPool_V2.Despawn(gameObject);
        }

        private void EnsureRigidbody()
        {
            if (_rigidbody2D != null)
            {
                return;
            }

            _rigidbody2D = GetComponent<Rigidbody2D>();
            if (_rigidbody2D == null)
            {
                _rigidbody2D = gameObject.AddComponent<Rigidbody2D>();
            }
        }
    }
}
