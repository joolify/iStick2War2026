using UnityEngine;
using UnityEngine.Serialization;

namespace iStick2War_V2
{
    /*
 * BombPlaneModel_V2 (Pure Data Layer)
 *
 * RESPONSIBILITY:
 * Holds the mutable runtime state for a single bomb-plane pass.
 * Inspector fields are hidden; the Controller and composition root write values.
 *
 * ---------------------------------------------------------
 * STORED DATA (examples):
 *
 * - currentState (mirrors state machine for debugging / consumers)
 * - directionX, started, timers (nextDropAt, expireAt) — motion on AircraftMotionModelBase_V2
 * - bombsDropped, frozenForCombatMatrixHarness
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT CONTAIN:
 *
 * - Update / FixedUpdate or other Unity frame callbacks
 * - Spawn logic, pooling, or physics
 * - Animation or Spine access
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE:
 *
 * Treat as passive state (“DNA”) mutated by BombPlaneController_V2 and read by tooling/tests.
 */
    public sealed class BombPlaneModel_V2 : AircraftMotionModelBase_V2, IAircraftStateMirror_V2<BombPlaneState_V2>
    {
        [HideInInspector]
        [FormerlySerializedAs("currentState")]
        [SerializeField]
        private BombPlaneState_V2 _currentState = BombPlaneState_V2.Idle;

        public BombPlaneState_V2 currentState
        {
            get => _currentState;
            set => _currentState = value;
        }

        [HideInInspector] public float nextDropAt;
        [HideInInspector] public int bombsDropped;
    }
}
