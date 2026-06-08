using UnityEngine;
using UnityEngine.SceneManagement;

namespace iStick2War_V2
{
    /*
     * LifeOverRuntimeLayout_V2 (LifeOver V2 — optional auto UI layout)
     *
     * PURPOSE:
     * When enabled, WaveManager and LifeOverUiFactory may create/repair labels, reparent canvases,
     * and apply runtime canvas layout. Disable on LifeOver V2 to keep Inspector-authored layout.
     *
     * ---------------------------------------------------------
     * NAVIGATION (Game_V2)
     *
     * LifeOver flow → WaveManager_V2.cs
     * Label factory → LifeOverUiFactory_V2.cs
     */
    [DisallowMultipleComponent]
    public sealed class LifeOverRuntimeLayout_V2 : MonoBehaviour
    {
        private const string LifeOverChromeRootName = "LifeOver V2";

        private static readonly string[] SuppressedChromeRootsWhenLifeOverShows =
        {
            "GameOver",
            "GameOver V2",
            "LifeOver V2 old",
        };

        public static void SuppressGameOverChrome()
        {
            for (int i = 0; i < SuppressedChromeRootsWhenLifeOverShows.Length; i++)
            {
                GameObject chromeRoot = FindSceneRootByName(SuppressedChromeRootsWhenLifeOverShows[i]);
                if (chromeRoot != null)
                {
                    chromeRoot.SetActive(false);
                }
            }
        }

        public static bool IsInspectorLayoutAuthoritative(Transform lifeOverRoot = null)
        {
            if (lifeOverRoot == null)
            {
                lifeOverRoot = FindLifeOverChromeRoot();
            }

            if (lifeOverRoot == null)
            {
                return false;
            }

            LifeOverRuntimeLayout_V2 layout = lifeOverRoot.GetComponent<LifeOverRuntimeLayout_V2>();
            return layout != null && !layout.isActiveAndEnabled;
        }

        public static bool IsInspectorLayoutAuthoritativeForCanvas(GameObject canvasGo)
        {
            if (canvasGo == null)
            {
                return false;
            }

            Transform walk = canvasGo.transform;
            while (walk != null)
            {
                if (walk.name.Equals(LifeOverChromeRootName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return IsInspectorLayoutAuthoritative(walk);
                }

                walk = walk.parent;
            }

            return false;
        }

        private static Transform FindLifeOverChromeRoot()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] transforms = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate != null &&
                        candidate.name.Equals(LifeOverChromeRootName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static GameObject FindSceneRootByName(string exactName)
        {
            if (string.IsNullOrEmpty(exactName))
            {
                return null;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] transforms = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate != null &&
                        candidate.name.Equals(exactName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate.gameObject;
                    }
                }
            }

            return null;
        }
    }
}
