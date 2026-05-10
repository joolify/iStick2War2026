using UnityEngine;

namespace iStick2War_V2
{
    /*
 * AircraftMotionModelBase_V2
 *
 * Shared horizontal-pass fields for bomb drone / bomb plane style aircraft.
 * State-only stacks (kamikaze, helicopter) use AircraftVisualStateModelBase_V2 instead.
 */
    public abstract class AircraftMotionModelBase_V2 : MonoBehaviour
    {
        [HideInInspector] public float directionX = 1f;
        [HideInInspector] public float expireAt;
        [HideInInspector] public bool started;
        [HideInInspector] public bool frozenForCombatMatrixHarness;
    }
}
