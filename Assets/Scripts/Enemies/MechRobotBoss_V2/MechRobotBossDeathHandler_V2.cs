using System.Collections;
using UnityEngine;

namespace iStick2War_V2
{
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
