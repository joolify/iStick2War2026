using Assets.Scripts.Components;
using iStick2War;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * MechRobotBossBodyPart_V2 (Hitbox relay)
 *
 * PURPOSE:
 * Marks Spine BoundingBoxFollower polygon colliders with BodyPartType for hero hit-scan, sets EnemyBodyPart
 * layer when available, and forwards OnHit to MechRobotBossDamageReceiver_V2.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Apply armor math itself (MechRobotBossDamageReceiver_V2 owns multipliers)
 * - Drive boss AI or animation
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Thin relay on child BoundingBox hitboxes; mirrors ParatrooperBodyPart_V2 collider stability without hit-reaction bones.
 */
    public sealed class MechRobotBossBodyPart_V2 : MonoBehaviour
    {
        public BodyPartType bodyPart = BodyPartType.Torso;

        [SerializeField] private bool _freezeBoundingBoxFollowerAtStartup = true;
        [SerializeField] private bool _enableColliderWatchdog = true;
        [SerializeField] private float _watchdogIntervalSeconds = 1f;

        private MechRobotBossDamageReceiver_V2 _damageReceiver;
        private MechRobotBossModel_V2 _model;
        private BoundingBoxFollower _boundingBoxFollower;
        private float _nextWatchdogTime;
        private bool _watchdogMissingLogged;

        private void Awake()
        {
            _damageReceiver = GetComponentInParent<MechRobotBossDamageReceiver_V2>();
            _model = GetComponentInParent<MechRobotBossModel_V2>();
            StabilizeBoundingBoxFollowerCollider();

            int enemyBodyPartLayer = LayerMask.NameToLayer("EnemyBodyPart");
            if (enemyBodyPartLayer >= 0)
            {
                gameObject.layer = enemyBodyPartLayer;
            }

            if (GetComponent<Collider2D>() == null)
            {
                Debug.LogWarning($"[MechRobotBossBodyPart_V2] No Collider2D on '{gameObject.name}'.");
            }
        }

        public void PrepareForSpawn()
        {
            if (_model == null)
            {
                _model = GetComponentInParent<MechRobotBossModel_V2>();
            }

            if (_damageReceiver == null)
            {
                _damageReceiver = GetComponentInParent<MechRobotBossDamageReceiver_V2>();
            }

            int enemyBodyPartLayer = LayerMask.NameToLayer("EnemyBodyPart");
            if (enemyBodyPartLayer >= 0)
            {
                gameObject.layer = enemyBodyPartLayer;
            }

            StabilizeBoundingBoxFollowerCollider();
            _watchdogMissingLogged = false;
        }

        public bool IsLivingCharacterForTargeting()
        {
            if (_model == null)
            {
                _model = GetComponentInParent<MechRobotBossModel_V2>();
            }

            return _model != null && !_model.IsDead() && _model.currentState != MechRobotBossBodyState.Die;
        }

        public void OnHit(DamageInfo info)
        {
            info.BodyPart = bodyPart;
            _damageReceiver?.TakeDamage(info);
        }

        private void Update()
        {
            if (_enableColliderWatchdog && Time.time >= _nextWatchdogTime)
            {
                _nextWatchdogTime = Time.time + Mathf.Max(0.1f, _watchdogIntervalSeconds);
                RunColliderWatchdog();
            }
        }

        private void StabilizeBoundingBoxFollowerCollider()
        {
            if (!_freezeBoundingBoxFollowerAtStartup)
            {
                return;
            }

            _boundingBoxFollower = GetComponent<BoundingBoxFollower>();
            if (_boundingBoxFollower == null)
            {
                return;
            }

            // BoundingBoxFollower can disable colliders when slot attachments change; freeze after first init.
            _boundingBoxFollower.Initialize(true);
            _boundingBoxFollower.clearStateOnDisable = false;

            PolygonCollider2D[] polygonColliders = GetComponents<PolygonCollider2D>();
            for (int i = 0; i < polygonColliders.Length; i++)
            {
                if (polygonColliders[i] != null)
                {
                    polygonColliders[i].enabled = true;
                }
            }

            _boundingBoxFollower.enabled = false;
        }

        private void RunColliderWatchdog()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            bool anyEnabledCollider = false;
            PolygonCollider2D[] polygonColliders = GetComponents<PolygonCollider2D>();
            for (int i = 0; i < polygonColliders.Length; i++)
            {
                if (polygonColliders[i] != null && polygonColliders[i].enabled)
                {
                    anyEnabledCollider = true;
                    break;
                }
            }

            if (anyEnabledCollider)
            {
                _watchdogMissingLogged = false;
                return;
            }

            if (!_watchdogMissingLogged)
            {
                _watchdogMissingLogged = true;
                Debug.LogWarning(
                    $"[MechRobotBossBodyPart_V2] Collider watchdog: '{name}' has no enabled PolygonCollider2D. Trying recovery.");
            }

            TryRecoverColliderFromBoundingBoxFollower();
        }

        private void TryRecoverColliderFromBoundingBoxFollower()
        {
            if (_boundingBoxFollower == null)
            {
                _boundingBoxFollower = GetComponent<BoundingBoxFollower>();
            }

            if (_boundingBoxFollower == null)
            {
                return;
            }

            bool wasEnabled = _boundingBoxFollower.enabled;
            _boundingBoxFollower.enabled = true;
            _boundingBoxFollower.Initialize(true);

            PolygonCollider2D[] recoveredColliders = GetComponents<PolygonCollider2D>();
            for (int i = 0; i < recoveredColliders.Length; i++)
            {
                if (recoveredColliders[i] != null)
                {
                    recoveredColliders[i].enabled = true;
                }
            }

            if (_freezeBoundingBoxFollowerAtStartup)
            {
                _boundingBoxFollower.clearStateOnDisable = false;
                _boundingBoxFollower.enabled = false;
            }
            else
            {
                _boundingBoxFollower.enabled = wasEnabled;
            }
        }
    }
}
