using UnityEngine;

namespace iStick2War_V2
{
    public sealed class BombDroneModel_V2 : MonoBehaviour
    {
        [HideInInspector] public BombDroneState_V2 currentState = BombDroneState_V2.Idle;
        [HideInInspector] public float directionX = 1f;
        [HideInInspector] public float expireAt;
        [HideInInspector] public bool bombDropped;
        [HideInInspector] public bool started;
        [HideInInspector] public bool frozenForCombatMatrixHarness;
    }
}
