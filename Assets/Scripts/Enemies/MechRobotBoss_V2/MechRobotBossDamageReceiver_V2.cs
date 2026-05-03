using Assets.Scripts.Components;
using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class MechRobotBossDamageReceiver_V2 : MonoBehaviour
    {
        [SerializeField] private float _armorMultiplier = 0.85f;

        private MechRobotBossModel_V2 _model;
        private MechRobotBossStateMachine_V2 _stateMachine;
        private MechRobotBossDeathHandler_V2 _deathHandler;
        private bool _deathStateSent;

        private void OnEnable()
        {
            _deathStateSent = false;
        }

        private void Awake()
        {
            _model = GetComponentInParent<MechRobotBossModel_V2>();
            _stateMachine = GetComponentInParent<MechRobotBossStateMachine_V2>();
            _deathHandler = GetComponentInParent<MechRobotBossDeathHandler_V2>();
        }

        public void TakeDamage(DamageInfo info)
        {
            if (_model == null || _stateMachine == null)
            {
                Debug.LogWarning("[MechRobotBossDamageReceiver_V2] TakeDamage skipped: missing model or state machine.");
                return;
            }

            if (_model.IsDead() || _deathStateSent)
            {
                return;
            }

            float mult = Mathf.Clamp(_armorMultiplier, 0.05f, 2f) * GetBodyPartMultiplier(info.BodyPart);

            float finalDamage = Mathf.Max(0f, info.BaseDamage * mult);
            _model.ApplyDamage(finalDamage);
            bool isDead = _model.IsDead();

            if (isDead && !_deathStateSent)
            {
                _deathStateSent = true;
                _stateMachine.ChangeState(MechRobotBossBodyState.Die);
            }
        }

        private static float GetBodyPartMultiplier(BodyPartType part)
        {
            switch (part)
            {
                case BodyPartType.Head:
                    return 1.35f;
                default:
                    return 1f;
            }
        }
    }
}
