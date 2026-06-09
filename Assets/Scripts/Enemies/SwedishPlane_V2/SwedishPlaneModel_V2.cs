using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SwedishPlaneModel_V2 — runtime pass data for the neutral Swedish supply plane.
     */
    public sealed class SwedishPlaneModel_V2 : AircraftMotionModelBase_V2, IAircraftStateMirror_V2<SwedishPlaneState_V2>
    {
        [HideInInspector] [SerializeField] private SwedishPlaneState_V2 _currentState = SwedishPlaneState_V2.Idle;

        public SwedishPlaneState_V2 currentState
        {
            get => _currentState;
            set => _currentState = value;
        }

        [HideInInspector] public int dropsReleased;
        [HideInInspector] public bool passCompleteSignaled;
    }
}
