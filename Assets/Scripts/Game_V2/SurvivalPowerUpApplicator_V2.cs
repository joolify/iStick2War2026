using UnityEngine;

namespace iStick2War_V2
{
    // Applies a rolled survival powerup to hero / bunker / loadout.
    public static class SurvivalPowerUpApplicator_V2
    {
        public static bool TryApply(WaveManager_V2 waveManager, Hero_V2 hero, SurvivalPowerUpOffer_V2 offer)
        {
            if (offer == null)
            {
                return false;
            }

            switch (offer.kind)
            {
                case SurvivalPowerUpKind_V2.HealthPack:
                    return TryApplyHealthPack(hero, offer.healthAmount);
                case SurvivalPowerUpKind_V2.BunkerRepair:
                    return TryApplyBunkerRepair(waveManager, offer.bunkerRepairAmount);
                case SurvivalPowerUpKind_V2.WeaponUnlock:
                    return TryApplyWeaponUnlock(hero, offer.weaponDefinition, autoEquip: true);
                case SurvivalPowerUpKind_V2.AmmoRefill:
                    return TryApplyAmmoRefill(hero);
                default:
                    return false;
            }
        }

        // Pickup path: apply rolled offer, then sensible fallbacks so the crate always clears when touched.
        public static bool TryApplyForPickup(WaveManager_V2 waveManager, Hero_V2 hero, SurvivalPowerUpOffer_V2 offer)
        {
            if (hero == null || hero.IsDead())
            {
                return false;
            }

            if (offer != null && TryApply(waveManager, hero, offer))
            {
                return true;
            }

            if (TryApplyAmmoRefill(hero))
            {
                return true;
            }

            if (offer != null && offer.kind == SurvivalPowerUpKind_V2.WeaponUnlock &&
                offer.weaponDefinition != null &&
                hero.HasWeaponUnlocked(offer.weaponDefinition))
            {
                return hero.TryRefillWeaponMagazine(offer.weaponDefinition);
            }

            if (TryApplyBunkerRepair(waveManager, offer != null ? offer.bunkerRepairAmount : 25))
            {
                return true;
            }

            if (TryApplyHealthPack(hero, offer != null ? offer.healthAmount : 25))
            {
                return true;
            }

            // Nothing left to grant — still consume the pickup so it does not block the lane.
            return offer != null;
        }

        private static bool TryApplyHealthPack(Hero_V2 hero, int amount)
        {
            if (hero == null || hero.IsDead() || hero.IsHealthFull())
            {
                return false;
            }

            hero.Heal(Mathf.Max(1, amount));
            return true;
        }

        private static bool TryApplyBunkerRepair(WaveManager_V2 waveManager, int amount)
        {
            if (waveManager == null || waveManager.IsBunkerFullHealth())
            {
                return false;
            }

            return waveManager.ApplySurvivalBunkerRepair(Mathf.Max(1, amount));
        }

        private static bool TryApplyWeaponUnlock(Hero_V2 hero, HeroWeaponDefinition_V2 definition, bool autoEquip)
        {
            if (hero == null || definition == null)
            {
                return false;
            }

            if (hero.HasWeaponUnlocked(definition))
            {
                return hero.TryRefillWeaponMagazine(definition);
            }

            return hero.UnlockWeapon(definition, autoEquip);
        }

        private static bool TryApplyAmmoRefill(Hero_V2 hero)
        {
            if (hero == null)
            {
                return false;
            }

            HeroWeaponDefinition_V2 active = hero.GetActiveWeaponDefinition();
            if (active == null)
            {
                return false;
            }

            return hero.TryRefillWeaponMagazine(active);
        }
    }
}
