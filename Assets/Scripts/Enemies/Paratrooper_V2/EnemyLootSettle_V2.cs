using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * Ground loot (mp40, naziHelmet): ignore other loot colliders and cancel ground bounce so props
     * land and rest instead of ping-ponging off the floor.
     */
    [DisallowMultipleComponent]
    public sealed class EnemyLootSettle_V2 : MonoBehaviour
    {
        private static readonly List<Collider2D> s_lootColliders = new List<Collider2D>();
        private static int s_groundLayer = -1;
        private static int s_landingPointLayer = -1;

        [SerializeField] private float _groundVelocityRetain = 0.12f;
        [SerializeField] private float _groundAngularRetain = 0.08f;
        [SerializeField] private float _sleepBelowSpeed = 0.2f;

        private Rigidbody2D _rb;
        private Collider2D[] _colliders;
        private int _groundContactCount;
        private bool _gentleGroundPlacement;
        private bool _gentleSimulationDeferred;

        public void ConfigureGentleGroundPlacement()
        {
            _gentleGroundPlacement = true;
        }

        private void Start()
        {
            if (!_gentleGroundPlacement || _rb == null || _gentleSimulationDeferred)
            {
                return;
            }

            _gentleSimulationDeferred = true;
            _rb.simulated = false;
            StartCoroutine(EnableSimulationNextFixedUpdate());
        }

        private IEnumerator EnableSimulationNextFixedUpdate()
        {
            yield return new WaitForFixedUpdate();
            if (_rb == null)
            {
                yield break;
            }

            _rb.simulated = true;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            Physics2D.SyncTransforms();
            TrySleepOnGround();
        }

        private void OnEnable()
        {
            _rb = GetComponent<Rigidbody2D>();
            _colliders = GetComponentsInChildren<Collider2D>(true);
            _groundContactCount = 0;
            RegisterLootColliders();
        }

        private void OnDisable()
        {
            UnregisterLootColliders();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_rb == null || collision == null || !IsGroundCollision(collision))
            {
                return;
            }

            _groundContactCount++;
            CancelGroundBounce(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (_rb == null || collision == null || !IsGroundCollision(collision))
            {
                return;
            }

            if (_rb.linearVelocity.sqrMagnitude > _sleepBelowSpeed * _sleepBelowSpeed * 4f)
            {
                CancelGroundBounce(collision);
                return;
            }

            TrySleepOnGround();
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision == null || !IsGroundCollision(collision))
            {
                return;
            }

            _groundContactCount = Mathf.Max(0, _groundContactCount - 1);
        }

        private void CancelGroundBounce(Collision2D collision)
        {
            Vector2 velocity = _rb.linearVelocity;
            int contactCount = collision.contactCount;
            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                float separationSpeed = Vector2.Dot(velocity, contact.normal);
                if (separationSpeed > 0f)
                {
                    velocity -= separationSpeed * contact.normal;
                }
            }

            velocity *= _groundVelocityRetain;
            _rb.linearVelocity = velocity;
            _rb.angularVelocity *= _groundAngularRetain;
            TrySleepOnGround();
        }

        private void TrySleepOnGround()
        {
            if (_groundContactCount <= 0)
            {
                return;
            }

            if (_rb.linearVelocity.sqrMagnitude > _sleepBelowSpeed * _sleepBelowSpeed &&
                Mathf.Abs(_rb.angularVelocity) > _sleepBelowSpeed)
            {
                return;
            }

            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.Sleep();
        }

        private static bool IsGroundCollision(Collision2D collision)
        {
            EnsureGroundLayerIds();
            Collider2D other = collision.collider;
            if (other == null)
            {
                return false;
            }

            int layer = other.gameObject.layer;
            return layer == s_groundLayer || layer == s_landingPointLayer;
        }

        private static void EnsureGroundLayerIds()
        {
            if (s_groundLayer < 0)
            {
                s_groundLayer = LayerMask.NameToLayer("Ground");
            }

            if (s_landingPointLayer < 0)
            {
                s_landingPointLayer = LayerMask.NameToLayer("LandingPoint");
            }
        }

        private void RegisterLootColliders()
        {
            if (_colliders == null || _colliders.Length == 0)
            {
                return;
            }

            PruneNullLootColliders();

            for (int i = 0; i < s_lootColliders.Count; i++)
            {
                Collider2D existing = s_lootColliders[i];
                if (existing == null)
                {
                    continue;
                }

                for (int j = 0; j < _colliders.Length; j++)
                {
                    Collider2D ours = _colliders[j];
                    if (ours != null)
                    {
                        Physics2D.IgnoreCollision(ours, existing, true);
                    }
                }
            }

            for (int j = 0; j < _colliders.Length; j++)
            {
                Collider2D ours = _colliders[j];
                if (ours != null)
                {
                    s_lootColliders.Add(ours);
                }
            }
        }

        private void UnregisterLootColliders()
        {
            if (_colliders == null)
            {
                return;
            }

            for (int j = 0; j < _colliders.Length; j++)
            {
                Collider2D ours = _colliders[j];
                if (ours != null)
                {
                    s_lootColliders.Remove(ours);
                }
            }
        }

        private static void PruneNullLootColliders()
        {
            for (int i = s_lootColliders.Count - 1; i >= 0; i--)
            {
                if (s_lootColliders[i] == null)
                {
                    s_lootColliders.RemoveAt(i);
                }
            }
        }
    }
}
