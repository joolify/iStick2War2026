namespace iStick2War_V2
{
    /// <summary>
    /// How shop and bot weapon choices behave for a scene / benchmark run.
    /// </summary>
    public enum GameplayWeaponPolicyKind_V2
    {
        /// <summary>Vanilla: all shop weapon offers and AutoHero weapon logic.</summary>
        FullProgression = 0,

        /// <summary>Block only <see cref="ShopOfferKind_V2.WeaponUnlock"/> purchases; starting loadout unchanged.</summary>
        BlockShopWeaponUnlocks = 1,

        /// <summary>
        /// Colt 45 only: strip other guns from inventory after load, block weapon unlocks and non-colt ammo refills,
        /// AutoHero never switches to Carbine/Bazooka.
        /// </summary>
        ColtOnly = 2,
    }
}
