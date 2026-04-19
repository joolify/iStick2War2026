using System.Collections.Generic;
using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Session-wide rules selected by <see cref="GameplaySceneProfileApplier_V2"/> for shop, bot weapon choice, and telemetry.
    /// </summary>
    public static class GameplaySceneRules_V2
    {
        private static bool _active;
        private static string _profileId = "";
        private static GameplayWeaponPolicyKind_V2 _weaponPolicy = GameplayWeaponPolicyKind_V2.FullProgression;
        private static bool _overrideAutoHero;
        private static AutoHeroTestProfileKind_V2 _autoHeroProfile = AutoHeroTestProfileKind_V2.Perfect;

        public static bool IsActive => _active;
        public static string ProfileId => _profileId;
        public static GameplayWeaponPolicyKind_V2 WeaponPolicy => _weaponPolicy;

        public static void Clear()
        {
            _active = false;
            _profileId = "";
            _weaponPolicy = GameplayWeaponPolicyKind_V2.FullProgression;
            _overrideAutoHero = false;
            _autoHeroProfile = AutoHeroTestProfileKind_V2.Perfect;
        }

        public static void ApplyFromAsset(GameplaySceneProfile_V2 asset)
        {
            if (asset == null)
            {
                Clear();
                return;
            }

            _active = true;
            _profileId = asset.ProfileId;
            _weaponPolicy = asset.WeaponPolicy;
            _overrideAutoHero = asset.OverrideAutoHeroTestProfile;
            _autoHeroProfile = asset.AutoHeroTestProfile;
        }

        public static void ApplyBuiltin(GameplayBuiltinScenePreset_V2 preset)
        {
            if (preset == GameplayBuiltinScenePreset_V2.None)
            {
                Clear();
                return;
            }

            _active = true;
            switch (preset)
            {
                case GameplayBuiltinScenePreset_V2.FullProgression:
                    _profileId = "builtin_full_progression";
                    _weaponPolicy = GameplayWeaponPolicyKind_V2.FullProgression;
                    _overrideAutoHero = false;
                    _autoHeroProfile = AutoHeroTestProfileKind_V2.Perfect;
                    break;
                case GameplayBuiltinScenePreset_V2.ColtOnly_AimBenchmark:
                    _profileId = "builtin_colt_only_aim_benchmark";
                    _weaponPolicy = GameplayWeaponPolicyKind_V2.ColtOnly;
                    _overrideAutoHero = true;
                    _autoHeroProfile = AutoHeroTestProfileKind_V2.HumanLike;
                    break;
                case GameplayBuiltinScenePreset_V2.BlockWeaponUnlocks_DefaultBot:
                    _profileId = "builtin_block_weapon_unlocks";
                    _weaponPolicy = GameplayWeaponPolicyKind_V2.BlockShopWeaponUnlocks;
                    _overrideAutoHero = false;
                    _autoHeroProfile = AutoHeroTestProfileKind_V2.Perfect;
                    break;
                case GameplayBuiltinScenePreset_V2.ColtOnly_Struggling:
                    _profileId = "builtin_colt_only_struggling";
                    _weaponPolicy = GameplayWeaponPolicyKind_V2.ColtOnly;
                    _overrideAutoHero = true;
                    _autoHeroProfile = AutoHeroTestProfileKind_V2.Struggling;
                    break;
                case GameplayBuiltinScenePreset_V2.FullShop_StrugglingBot:
                    _profileId = "builtin_full_shop_struggling_bot";
                    _weaponPolicy = GameplayWeaponPolicyKind_V2.FullProgression;
                    _overrideAutoHero = true;
                    _autoHeroProfile = AutoHeroTestProfileKind_V2.Struggling;
                    break;
                default:
                    Clear();
                    break;
            }
        }

        public static bool TryGetAutoHeroOverride(out AutoHeroTestProfileKind_V2 profile)
        {
            profile = _autoHeroProfile;
            return _active && _overrideAutoHero;
        }

        public static bool IsShopOfferBlocked(ShopOfferConfig_V2 offer)
        {
            if (!_active || offer == null)
            {
                return false;
            }

            return BlockShopWeaponUnlock(offer) || BlockShopAmmoRefill(offer);
        }

        public static bool IsColtOnlyRun()
        {
            return _active && _weaponPolicy == GameplayWeaponPolicyKind_V2.ColtOnly;
        }

        public static bool BlockShopWeaponUnlock(ShopOfferConfig_V2 offer)
        {
            if (!_active || offer == null || offer.Kind != ShopOfferKind_V2.WeaponUnlock)
            {
                return false;
            }

            return _weaponPolicy == GameplayWeaponPolicyKind_V2.BlockShopWeaponUnlocks ||
                   _weaponPolicy == GameplayWeaponPolicyKind_V2.ColtOnly;
        }

        public static bool BlockShopAmmoRefill(ShopOfferConfig_V2 offer)
        {
            if (!_active || offer == null || offer.Kind != ShopOfferKind_V2.AmmoRefill || offer.Weapon == null)
            {
                return false;
            }

            if (_weaponPolicy != GameplayWeaponPolicyKind_V2.ColtOnly)
            {
                return false;
            }

            return offer.Weapon.WeaponType != WeaponType.Colt45;
        }

        /// <summary>Weapon types AutoHero may switch toward during combat (subset of unlocked).</summary>
        public static bool AutoHeroMayConsiderWeaponType(WeaponType type)
        {
            if (!_active || _weaponPolicy != GameplayWeaponPolicyKind_V2.ColtOnly)
            {
                return true;
            }

            return type == WeaponType.Colt45;
        }

        public static IReadOnlyList<WeaponType> GetColtOnlyAllowlist()
        {
            return ColtOnlyAllowlist;
        }

        private static readonly WeaponType[] ColtOnlyAllowlist = { WeaponType.Colt45 };
    }

    /// <summary>In-scene preset when no <see cref="GameplaySceneProfile_V2"/> asset is assigned.</summary>
    public enum GameplayBuiltinScenePreset_V2
    {
        None = 0,
        FullProgression = 1,
        ColtOnly_AimBenchmark = 2,
        BlockWeaponUnlocks_DefaultBot = 3,
        ColtOnly_Struggling = 4,
        FullShop_StrugglingBot = 5,
    }
}
