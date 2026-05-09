using System.Collections;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * MechRobotBossDeathHandler_V2 (Death / despawn)
 *
 * PURPOSE:
 * When Die is invoked, raises OnDeathStarted, waits a configurable delay, then returns the boss instance to
 * SimplePrefabPool_V2. Supports ForceDespawnImmediately for watchdog / error paths.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Apply incoming damage (MechRobotBossDamageReceiver_V2)
 * - Change locomotion or animation (controller / view)
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Small coroutine-based teardown; composition root may also react to state machine Die for extra wiring.
 */
    public sealed class MechRobotBossDeathHandler_V2 : MonoBehaviour
    {
        [SerializeField] private float _despawnDelaySeconds = 2.5f;

        private bool _isDying;
        public event System.Action<MechRobotBossDeathHandler_V2> OnDeathStarted;

        private void OnEnable()
        {
            _isDying = false;
            StopAllCoroutines();
        }

        public void Die()
        {
            if (_isDying)
            {
                return;
            }

            _isDying = true;
            OnDeathStarted?.Invoke(this);
            StartCoroutine(DeathRoutine());
        }

        public void ForceDespawnImmediately(string reason = null)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                Debug.LogWarning($"[MechRobotBossDeathHandler_V2] ForceDespawnImmediately: {reason}");
            }

            _isDying = true;
            StopAllCoroutines();
            OnDeathStarted?.Invoke(this);
            SimplePrefabPool_V2.Despawn(gameObject);
        }

        private IEnumerator DeathRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, _despawnDelaySeconds));
            SimplePrefabPool_V2.Despawn(gameObject);
        }
    }
}
