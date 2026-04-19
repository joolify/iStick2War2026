using System.Collections;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Place one instance in a gameplay scene (any active GameObject). Applies <see cref="GameplaySceneRules_V2"/> in
    /// <see cref="Awake"/> and clears on destroy. Colt-only profiles strip extra weapons one frame after load so
    /// <see cref="Hero_V2"/> finishes <see cref="Hero_V2.Awake"/> first.
    /// </summary>
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
