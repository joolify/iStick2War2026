using UnityEngine;

namespace iStick2War_V2
{
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
