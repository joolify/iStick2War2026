using Assets.Scripts.Components;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * KamikazeDroneController_V2 (Thin Brain / State Bridge)
 *
 * PURPOSE:
 * Small bridge between spawn lifecycle, health death, and optional Spine animation events.
 * It advances KamikazeDroneStateMachine_V2 so the View can react (single fly loop today).
 *
 * Responsibilities:
 * - StartFlight → state Fly (called from KamikazeDrone_V2.BeginFlight after InitializeForSpawn)
 * - OnDestroyed → state Die (AircraftHealth_V2)
 * - OnAnimationEvent(DeployStarted) → Fly when designers map a Spine “fly started” event
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * Intentionally minimal: all heavy kamikaze behaviour lives on KamikazeDroneDriver_V2.
 * This controller exists for parity with other V2 aircraft stacks and future Spine-driven cues.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Move transform, query BunkerHitbox_V2, or apply explosion damage
 */
    public sealed class KamikazeDroneController_V2 : MonoBehaviour, IAircraftSpineAnimationCommandReceiver_V2
    {
        private KamikazeDroneStateMachine_V2 _stateMachine;

        public void Initialize(KamikazeDroneModel_V2 model, KamikazeDroneStateMachine_V2 stateMachine)
        {
            _stateMachine = stateMachine;
        }

        public void StartFlight()
        {
            _stateMachine?.ChangeState(KamikazeDroneState_V2.Fly);
        }

        public void OnDestroyed()
        {
            _stateMachine?.ChangeState(KamikazeDroneState_V2.Die);
        }

        public void OnAnimationEvent(AnimationEventType eventType)
        {
            if (_stateMachine == null)
            {
                return;
            }

            if (eventType == AnimationEventType.DeployStarted)
            {
                _stateMachine.ChangeState(KamikazeDroneState_V2.Fly);
            }
        }
    }
}
