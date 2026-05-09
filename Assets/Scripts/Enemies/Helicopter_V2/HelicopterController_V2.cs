using Assets.Scripts.Components;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * HelicopterController_V2 (Helicopter gameplay coordinator)
 *
 * PURPOSE:
 * Holds HelicopterStateMachine_V2 and advances flight state: StartFlight() enters Fly, OnDestroyed() enters Die,
 * and OnAnimationEvent() reacts to Spine-derived AnimationEventType values from HelicopterSpineEventForwarder_V2
 * (e.g. DeployStarted → Fly).
 *
 * ---------------------------------------------------------
 * INPUT SOURCES
 *
 * - Helicopter_V2 composition root (Initialize after sibling components exist).
 * - Animation events from HelicopterSpineEventForwarder_V2 (timing only, no combat math).
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Own spawn placement or carrier drop cadence (EnemySpawner_V2 / HelicopterCarrier_V2).
 * - Drive Spine playback or track selection (HelicopterView_V2).
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Thin bridge between animator timing and HelicopterStateMachine_V2, same role shape as other *_Controller_V2 units.
 */
    public sealed class HelicopterController_V2 : MonoBehaviour
    {
        private HelicopterStateMachine_V2 _stateMachine;

        public void Initialize(
            HelicopterModel_V2 model,
            HelicopterStateMachine_V2 stateMachine)
        {
            _stateMachine = stateMachine;
        }

        public void StartFlight()
        {
            _stateMachine?.ChangeState(HelicopterState_V2.Fly);
        }

        public void OnDestroyed()
        {
            _stateMachine?.ChangeState(HelicopterState_V2.Die);
        }

        public void OnAnimationEvent(AnimationEventType eventType)
        {
            if (_stateMachine == null)
            {
                return;
            }

            if (eventType == AnimationEventType.DeployStarted)
            {
                _stateMachine.ChangeState(HelicopterState_V2.Fly);
            }
        }
    }
}
