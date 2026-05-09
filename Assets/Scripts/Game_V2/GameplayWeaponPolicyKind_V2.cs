namespace iStick2War_V2
{
    /*
 * GameplayWeaponPolicyKind_V2 (Weapon / shop policy for a scene run)
 *
 * PURPOSE:
 * Declares how shop weapon offers and AutoHero weapon switching behave: full vanilla progression, block only
 * weapon unlock purchases, or Colt-only runs that strip other guns and restrict ammo refills.
 *
 * ---------------------------------------------------------
 * CONSUMERS
 *
 * - GameplaySceneProfile_V2 / GameplaySceneRules_V2, ShopPanel_V2 offer filtering, Hero_V2 inventory sync, AutoHero_V2.
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Enum stays int-backed for telemetry JSON and switch statements in static rule helpers.
 */
    public enum GameplayWeaponPolicyKind_V2
    {
        // Vanilla: all shop weapon offers and AutoHero weapon logic.
        FullProgression = 0,

        // Block ShopOfferKind_V2.WeaponUnlock purchases; starting loadout unchanged.
        BlockShopWeaponUnlocks = 1,

        // Colt 45 only: strip other guns after load, block unlocks and non-colt ammo refills; AutoHero stays on Colt.
        ColtOnly = 2,
    }
}
