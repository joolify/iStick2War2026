using UnityEngine;

namespace iStick2War_V2{
    /// <summary>
    /// Optional reusable profile asset (assign on <see cref="GameplaySceneProfileApplier_V2"/>). When null, the applier
    /// can use a built-in preset enum instead.
    /// </summary>
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
