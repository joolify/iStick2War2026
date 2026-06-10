using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace iStick2War_V2
{
    /*
     * LegacyUiEventSystemUtility_V2 - ensures UI clicks work when Player Settings use Input Manager (Old) only.
     * InputSystemUIInputModule does not receive mouse/touch in that configuration; StandaloneInputModule does.
     */
    public static class LegacyUiEventSystemUtility_V2
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAfterSceneLoad()
        {
            EnsureLegacyUiInputModuleInScene();
        }

        public static void EnsureLegacyUiInputModuleInScene()
        {
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                return;
            }

            EnsureLegacyUiInputModule(eventSystem.gameObject);
        }

        public static void EnsureLegacyUiInputModule(GameObject eventSystemGo)
        {
            if (eventSystemGo == null)
            {
                return;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputSystemModule = eventSystemGo.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule != null)
            {
                inputSystemModule.enabled = false;
            }
#endif

            StandaloneInputModule legacyModule = eventSystemGo.GetComponent<StandaloneInputModule>();
            if (legacyModule == null)
            {
                legacyModule = eventSystemGo.AddComponent<StandaloneInputModule>();
            }

            legacyModule.enabled = true;

            EventSystem eventSystem = eventSystemGo.GetComponent<EventSystem>();
            if (eventSystem != null)
            {
                eventSystem.enabled = false;
                eventSystem.enabled = true;
            }
#endif
        }
    }
}
