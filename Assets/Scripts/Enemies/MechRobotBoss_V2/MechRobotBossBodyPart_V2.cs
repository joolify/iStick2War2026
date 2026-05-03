using Assets.Scripts.Components;
using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>EnemyBodyPart relay for hero hitscan (same layering contract as <see cref="ParatrooperBodyPart_V2"/>).</summary>
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
