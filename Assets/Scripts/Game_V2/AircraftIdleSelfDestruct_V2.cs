using UnityEngine;

namespace iStick2War_V2
{
    /*
 * AircraftIdleSelfDestruct_V2 (Stuck-aircraft cleanup)
 *
 * PURPOSE:
 * Despawns pooled aircraft when their root (or Rigidbody2D) has not moved for a configured duration.
 * Catches edge cases where camera-bounds despawn fails after bomb drops or off-screen flight.
 *
 * ---------------------------------------------------------
 * INPUT SOURCES
 *
 * - EnsureAndBeginMonitoring from EnemySpawner_V2 on helicopter, bomber, bomb drone, kamikaze, and generic air threats.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Drive flight AI or bomb/paratrooper payloads.
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2 + aircraft)
 *
 * Spawn wiring → EnemySpawner_V2.cs | Pool return → SimplePrefabPool_V2.cs
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Small add-on component so every aircraft type shares one idle-stuck failsafe without duplicating timers in each controller.
 */
    [DisallowMultipleComponent]
    public sealed class AircraftIdleSelfDestruct_V2 : MonoBehaviour
    {
        [SerializeField] private float _idleSecondsBeforeDespawn = 5f;
        [SerializeField] private float _movementThresholdWorld = 0.04f;

        private bool _monitoring;
        private bool _frozenForHarness;
        private Vector3 _lastSampledPosition;
        private float _lastMovementTime;
        private Rigidbody2D _rb;

        public static AircraftIdleSelfDestruct_V2 EnsureAndBeginMonitoring(GameObject root, float idleSeconds = 5f)
        {
            if (root == null)
            {
                return null;
            }

            AircraftIdleSelfDestruct_V2 monitor = root.GetComponent<AircraftIdleSelfDestruct_V2>();
            if (monitor == null)
            {
                monitor = root.AddComponent<AircraftIdleSelfDestruct_V2>();
            }

            monitor.BeginMonitoring(idleSeconds);
            return monitor;
        }

        public void BeginMonitoring(float idleSeconds = -1f)
        {
            if (idleSeconds > 0f)
            {
                _idleSecondsBeforeDespawn = idleSeconds;
            }

            _rb = GetComponent<Rigidbody2D>();
            _frozenForHarness = false;
            _monitoring = true;
            _lastSampledPosition = ReadTrackedPosition();
            _lastMovementTime = Time.time;
        }

        public void FreezeForCombatMatrixHarness()
        {
            _frozenForHarness = true;
        }

        private void OnDisable()
        {
            _monitoring = false;
            _frozenForHarness = false;
        }

        private void Update()
        {
            if (!_monitoring || _frozenForHarness)
            {
                return;
            }

            Vector3 pos = ReadTrackedPosition();
            float thresholdSq = _movementThresholdWorld * _movementThresholdWorld;
            if ((pos - _lastSampledPosition).sqrMagnitude > thresholdSq)
            {
                _lastSampledPosition = pos;
                _lastMovementTime = Time.time;
                return;
            }

            if (Time.time - _lastMovementTime >= _idleSecondsBeforeDespawn)
            {
                _monitoring = false;
                SimplePrefabPool_V2.Despawn(gameObject);
            }
        }

        private Vector3 ReadTrackedPosition()
        {
            if (_rb != null && _rb.simulated)
            {
                return _rb.position;
            }

            return transform.position;
        }
    }
}
