using UnityEngine;

namespace iStick2War_V2
{
    public sealed class SwedishPlanePowerUpModel_V2 : MonoBehaviour, IAircraftStateMirror_V2<SwedishPlanePowerUpState_V2>
    {
        [HideInInspector] [SerializeField] private SwedishPlanePowerUpState_V2 _currentState = SwedishPlanePowerUpState_V2.Idle;

        public SwedishPlanePowerUpState_V2 currentState
        {
            get => _currentState;
            set => _currentState = value;
        }

        [HideInInspector] public SurvivalPowerUpOffer_V2 rolledOffer;
        [HideInInspector] public bool pickupEnabled;
    }
}
