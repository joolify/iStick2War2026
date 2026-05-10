using UnityEngine;
using UnityEngine.Serialization;

namespace iStick2War_V2
{
    public sealed class HelicopterModel_V2 : MonoBehaviour, IAircraftStateMirror_V2<HelicopterState_V2>
    {
        [FormerlySerializedAs("currentState")]
        [SerializeField]
        private HelicopterState_V2 _currentState = HelicopterState_V2.Idle;

        public HelicopterState_V2 currentState
        {
            get => _currentState;
            set => _currentState = value;
        }
    }
}
