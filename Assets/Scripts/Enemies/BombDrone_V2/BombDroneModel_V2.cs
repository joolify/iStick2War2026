using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombDroneModel_V2 (Pure Data Layer)
 *
 * RESPONSIBILITY:
 * Holds mutable runtime state for one bomb-drone sortie (single bomb over bunker design).
 * Fields are hidden in the inspector; BombDroneController_V2 owns writes during the pass.
 *
 * ---------------------------------------------------------
 * STORED DATA (examples):
 *
 * - currentState (mirrors state machine)
 * - directionX, started, expireAt (AircraftMotionModelBase_V2)
 * - bombDropped (at most one bomb per pass when rules fire)
 * - frozenForCombatMatrixHarness (base)
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT CONTAIN:
 *
 * - Unity frame callbacks (Update, etc.)
 * - Bunker lookup, camera bounds, or pooling
 * - Spine or animation access
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE:
 *
 * Passive “DNA” mutated by the Controller; safe for tests and debugging snapshots.
 */
    public sealed class BombDroneModel_V2 : AircraftMotionModelBase_V2, IAircraftStateMirror_V2<BombDroneState_V2>
    {
        [HideInInspector] [SerializeField] private BombDroneState_V2 _currentState = BombDroneState_V2.Idle;

        public BombDroneState_V2 currentState
        {
            get => _currentState;
            set => _currentState = value;
        }

        [HideInInspector] public bool bombDropped;
    }
}
