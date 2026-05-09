using UnityEngine;

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
 * - directionX, started, timers (nextDropAt, expireAt)
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
    public sealed class BombPlaneModel_V2 : MonoBehaviour
    {
        [HideInInspector] public BombPlaneState_V2 currentState = BombPlaneState_V2.Idle;
        [HideInInspector] public float directionX = 1f;
        [HideInInspector] public float expireAt;
        [HideInInspector] public float nextDropAt;
        [HideInInspector] public int bombsDropped;
        [HideInInspector] public bool started;
        [HideInInspector] public bool frozenForCombatMatrixHarness;
    }
}
