using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SwedishPlanePowerUpPickupTrigger_V2 — forwards Spine bounding-box trigger hits to the powerup controller.
     */
    [DisallowMultipleComponent]
    public sealed class SwedishPlanePowerUpPickupTrigger_V2 : MonoBehaviour
    {
        private SwedishPlanePowerUpController_V2 _controller;

        public void Bind(SwedishPlanePowerUpController_V2 controller)
        {
            _controller = controller;
        }

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponentInParent<SwedishPlanePowerUpController_V2>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            _controller?.NotifyPickupTrigger(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            _controller?.NotifyPickupTrigger(other);
        }
    }
}
