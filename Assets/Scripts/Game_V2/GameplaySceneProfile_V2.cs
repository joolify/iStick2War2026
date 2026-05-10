using UnityEngine;

namespace iStick2War_V2{
    /*
 * GameplaySceneProfile_V2 (Scene benchmark / policy asset)
 *
 * PURPOSE:
 * ScriptableObject carrying ProfileId string, GameplayWeaponPolicyKind_V2, and optional AutoHero test profile override.
 * GameplaySceneProfileApplier_V2 reads it in Awake to populate GameplaySceneRules_V2 static state for the session.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Mutate hero inventory itself (applier + Hero_V2 deferred passes perform stripping).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Applies profile into statics → GameplaySceneProfileApplier_V2.cs → GameplaySceneRules_V2.cs
 * Weapon policy enum → GameplayWeaponPolicyKind_V2.cs
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Keeps Colt-only and bot benchmark presets data-driven instead of hardcoding scene-specific branches.
 */
    [CreateAssetMenu(menuName = "iStick2War V2/Gameplay Scene Profile", fileName = "GameplaySceneProfile")]
    public sealed class GameplaySceneProfile_V2 : ScriptableObject
    {
        [SerializeField] private string _profileId = "custom";
        [SerializeField] private GameplayWeaponPolicyKind_V2 _weaponPolicy = GameplayWeaponPolicyKind_V2.FullProgression;

        [Header("AutoHero (optional)")]
        [SerializeField] private bool _overrideAutoHeroTestProfile;
        [SerializeField] private AutoHeroTestProfileKind_V2 _autoHeroTestProfile = AutoHeroTestProfileKind_V2.Perfect;

        public string ProfileId => string.IsNullOrWhiteSpace(_profileId) ? "custom" : _profileId.Trim();
        public GameplayWeaponPolicyKind_V2 WeaponPolicy => _weaponPolicy;
        public bool OverrideAutoHeroTestProfile => _overrideAutoHeroTestProfile;
        public AutoHeroTestProfileKind_V2 AutoHeroTestProfile => _autoHeroTestProfile;
    }
}
