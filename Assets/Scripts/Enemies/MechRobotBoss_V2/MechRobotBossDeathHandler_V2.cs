using System.Collections;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * MechRobotBossDeathHandler_V2 (Death / despawn)
 *
 * PURPOSE:
 * When Die is invoked, plays the same helicopter-style aircraft explosion VFX, hides the boss mesh,
 * raises OnDeathStarted, waits a configurable delay, then returns the boss instance to SimplePrefabPool_V2.
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
        [Tooltip("Uses AircraftExplosionVfxDefaults_V2 helicopterOrGenericAircraft (same prefab as Helicopter V2 death).")]
        [SerializeField] private bool _spawnHelicopterStyleExplosion = true;

        private bool _isDying;
        private GameObject _hiddenViewRoot;
        public event System.Action<MechRobotBossDeathHandler_V2> OnDeathStarted;

        private void OnEnable()
        {
            _isDying = false;
            StopAllCoroutines();
            RestoreHiddenViewForSpawn();
        }

        public void Die()
        {
            if (_isDying)
            {
                return;
            }

            _isDying = true;
            PlayDeathExplosion();
            HideBossPresentation();
            GetComponent<MechRobotBoss>()?.HideRuntimeHealthBarForDeath();
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

        private void PlayDeathExplosion()
        {
            if (!_spawnHelicopterStyleExplosion)
            {
                return;
            }

            Vector3 explosionPoint = ResolveExplosionWorldPoint();
            AircraftExplosionVfx_V2.TrySpawnKind(
                explosionPoint,
                AircraftExplosionVfxKind_V2.HelicopterOrGenericAircraft);
        }

        private Vector3 ResolveExplosionWorldPoint()
        {
            MechRobotBoss boss = GetComponent<MechRobotBoss>();
            if (boss != null && boss.TryGetHealthBarAnchorWorld(out Vector3 anchor))
            {
                return anchor;
            }

            Bounds? merged = null;
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!merged.HasValue)
                {
                    merged = col.bounds;
                }
                else
                {
                    Bounds combined = merged.Value;
                    combined.Encapsulate(col.bounds);
                    merged = combined;
                }
            }

            if (merged.HasValue)
            {
                return merged.Value.center;
            }

            SkeletonAnimation skeleton = GetComponentInChildren<SkeletonAnimation>(true);
            if (skeleton != null)
            {
                return skeleton.transform.position;
            }

            return transform.position;
        }

        private void HideBossPresentation()
        {
            Rigidbody2D rb = GetComponentInChildren<Rigidbody2D>(true);
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
                if (_hiddenViewRoot == null)
                {
                    _hiddenViewRoot = rb.gameObject;
                    _hiddenViewRoot.SetActive(false);
                }
            }
        }

        private void RestoreHiddenViewForSpawn()
        {
            if (_hiddenViewRoot != null)
            {
                _hiddenViewRoot.SetActive(true);
                Rigidbody2D rb = _hiddenViewRoot.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.simulated = true;
                }
            }

            _hiddenViewRoot = null;
        }

        private IEnumerator DeathRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, _despawnDelaySeconds));
            SimplePrefabPool_V2.Despawn(gameObject);
        }
    }
}
