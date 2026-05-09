using System.Collections;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * GameplaySceneProfileApplier_V2 (Scene hook: apply profile rules)
 *
 * PURPOSE:
 * Runs in Awake on any active GameObject: applies GameplaySceneProfile_V2 asset or GameplayBuiltinScenePreset_V2 to
 * GameplaySceneRules_V2 statics, clears on destroy. Colt-only paths defer weapon stripping one frame so Hero_V2 Awake completes.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Own combat simulation (Hero / Wave systems consume rules only).
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Single scene-placed MonoBehaviour keeps benchmark scenes declarative without custom scene controllers.
 */
    [DefaultExecutionOrder(-200)]
    public sealed class GameplaySceneProfileApplier_V2 : MonoBehaviour
    {
        [Tooltip("If set, overrides Built-in preset.")]
        [SerializeField] private GameplaySceneProfile_V2 _customProfile;

        [SerializeField] private GameplayBuiltinScenePreset_V2 _builtinPreset = GameplayBuiltinScenePreset_V2.None;

        private void Awake()
        {
            if (_customProfile != null)
            {
                GameplaySceneRules_V2.ApplyFromAsset(_customProfile);
            }
            else if (_builtinPreset != GameplayBuiltinScenePreset_V2.None)
            {
                GameplaySceneRules_V2.ApplyBuiltin(_builtinPreset);
            }
            else
            {
                GameplaySceneRules_V2.Clear();
            }
        }

        private void OnDestroy()
        {
            GameplaySceneRules_V2.Clear();
        }

        private IEnumerator Start()
        {
            yield return null;
            if (!GameplaySceneRules_V2.IsColtOnlyRun())
            {
                yield break;
            }

            Hero_V2 hero = FindAnyObjectByType<Hero_V2>(FindObjectsInactive.Include);
            if (hero != null)
            {
                hero.ApplySceneWeaponAllowlist(GameplaySceneRules_V2.GetColtOnlyAllowlist());
            }
        }
    }
}
