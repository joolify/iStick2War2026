using Assets.Scripts.Components;
using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * MechRobotBossBodyPart_V2 (Hitbox relay)
 *
 * PURPOSE:
 * Marks colliders with BodyPartType for hero hit-scan, sets EnemyBodyPart layer when available, and forwards
 * OnHit to MechRobotBossDamageReceiver_V2 with the same layering contract as ParatrooperBodyPart_V2.
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
 * Thin relay on child colliders so hero weapons can raycast a consistent enemy body contract.
 */
    public sealed class MechRobotBossBodyPart_V2 : MonoBehaviour
    {
        public BodyPartType bodyPart = BodyPartType.Torso;

        private MechRobotBossDamageReceiver_V2 _damageReceiver;
        private MechRobotBossModel_V2 _model;

        private void Awake()
        {
            _damageReceiver = GetComponentInParent<MechRobotBossDamageReceiver_V2>();
            _model = GetComponentInParent<MechRobotBossModel_V2>();

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
    }
}
