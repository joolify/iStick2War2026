using UnityEngine;
using UnityEngine.Serialization;

namespace iStick2War_V2
{
    /*
 * KamikazeDroneModel_V2 (Pure Data Layer)
 *
 * RESPONSIBILITY:
 * Minimal runtime state for the kamikaze stack. Currently mirrors the state machine’s
 * KamikazeDroneState_V2 for debugging and future expansion.
 *
 * ---------------------------------------------------------
 * STORED DATA:
 *
 * - currentState
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT CONTAIN:
 *
 * - Update loops, physics, bunker queries, or explosion logic (see KamikazeDroneDriver_V2)
 * - Spine access
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE:
 *
 * Keep the model trivial until gameplay state needs to move off KamikazeDroneDriver_V2 only.
 */
    public sealed class KamikazeDroneModel_V2 : MonoBehaviour, IAircraftStateMirror_V2<KamikazeDroneState_V2>
    {
        [FormerlySerializedAs("currentState")]
        [SerializeField]
        private KamikazeDroneState_V2 _currentState = KamikazeDroneState_V2.Idle;

        public KamikazeDroneState_V2 currentState
        {
            get => _currentState;
            set => _currentState = value;
        }
    }
}
