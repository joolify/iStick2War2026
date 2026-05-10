using UnityEngine;

namespace iStick2War_V2
{
    /*
 * AircraftHealthCompositionRootBase_V2
 *
 * Shared composition-root helpers for aircraft that subscribe to AircraftHealth_V2.OnDestroyed
 * (bomb drone, kamikaze, helicopter — not bomb plane, which uses this project’s separate root type).
 */
    public abstract class AircraftHealthCompositionRootBase_V2 : MonoBehaviour
    {
        protected bool _initialized;
        protected AircraftHealth_V2 _health;

        protected void ResolveHealthFromHierarchy()
        {
            _health = GetComponent<AircraftHealth_V2>();
            if (_health == null)
            {
                _health = GetComponentInChildren<AircraftHealth_V2>(true);
            }
        }

        protected void SubscribeHealthDestroyed(System.Action<AircraftHealth_V2> handler)
        {
            if (_health != null)
            {
                _health.OnDestroyed -= handler;
                _health.OnDestroyed += handler;
            }
        }

        protected void UnsubscribeHealthDestroyed(System.Action<AircraftHealth_V2> handler)
        {
            if (_health != null)
            {
                _health.OnDestroyed -= handler;
            }
        }
    }
}
