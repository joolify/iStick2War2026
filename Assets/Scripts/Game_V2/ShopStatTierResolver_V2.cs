using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * Builds percentile baselines from shop offers and maps numeric stat samples to ShopStatTier_V2.
     */
    public sealed class ShopStatTierResolver_V2
    {
        private readonly List<float> _damageSamples = new List<float>();
        private readonly List<float> _fireRateSamples = new List<float>();
        private readonly List<float> _magazineSamples = new List<float>();
        private readonly List<float> _reloadSamples = new List<float>();
        private readonly List<float> _armorPenSamples = new List<float>();
        private readonly List<float> _healthHealSamples = new List<float>();
        private readonly List<float> _bunkerMaxSamples = new List<float>();
        private readonly List<float> _bunkerRepairSamples = new List<float>();
        private readonly List<float> _ammoReserveSamples = new List<float>();

        public void RebuildFromOffers(
            IReadOnlyList<ShopOfferConfig_V2> offers,
            WaveManager_V2 waveManager)
        {
            ClearSamples();

            if (offers == null)
            {
                return;
            }

            for (int i = 0; i < offers.Count; i++)
            {
                ShopOfferConfig_V2 offer = offers[i];
                if (offer == null)
                {
                    continue;
                }

                switch (offer.Kind)
                {
                    case ShopOfferKind_V2.WeaponUnlock:
                    case ShopOfferKind_V2.AmmoRefill:
                        AddWeaponSamples(offer.Weapon);
                        if (offer.Kind == ShopOfferKind_V2.AmmoRefill && offer.Weapon != null)
                        {
                            _ammoReserveSamples.Add(offer.Weapon.MaxReserveAmmo);
                        }

                        break;

                    case ShopOfferKind_V2.HealthPack:
                        _healthHealSamples.Add(ResolveHealthHealAmount(offer, waveManager));
                        break;

                    case ShopOfferKind_V2.BunkerMaxUpgrade:
                        _bunkerMaxSamples.Add(ResolveBunkerMaxIncrease(offer, waveManager));
                        break;

                    case ShopOfferKind_V2.BunkerRepair:
                        _bunkerRepairSamples.Add(ResolveBunkerRepairAmount(offer, waveManager));
                        break;
                }
            }

            SortSamples(_damageSamples);
            SortSamples(_fireRateSamples);
            SortSamples(_magazineSamples);
            SortSamples(_reloadSamples);
            SortSamples(_armorPenSamples);
            SortSamples(_healthHealSamples);
            SortSamples(_bunkerMaxSamples);
            SortSamples(_bunkerRepairSamples);
            SortSamples(_ammoReserveSamples);
        }

        public ShopStatTier_V2 GetDamageTier(float damage) => ResolveHigherIsBetter(damage, _damageSamples);

        public ShopStatTier_V2 GetFireRateTier(float shotsPerSecond) =>
            ResolveHigherIsBetter(shotsPerSecond, _fireRateSamples);

        public ShopStatTier_V2 GetMagazineTier(float magazine) =>
            ResolveHigherIsBetter(magazine, _magazineSamples);

        // Lower reload seconds is better.
        public ShopStatTier_V2 GetReloadTier(float reloadSeconds) =>
            ResolveLowerIsBetter(reloadSeconds, _reloadSamples);

        public ShopStatTier_V2 GetArmorPenTier(float armorPenPercent) =>
            ResolveHigherIsBetter(armorPenPercent, _armorPenSamples);

        public ShopStatTier_V2 GetHealthHealTier(float healAmount) =>
            ResolveHigherIsBetter(healAmount, _healthHealSamples);

        public ShopStatTier_V2 GetBunkerMaxTier(float maxIncrease) =>
            ResolveHigherIsBetter(maxIncrease, _bunkerMaxSamples);

        public ShopStatTier_V2 GetBunkerRepairTier(float repairAmount) =>
            ResolveHigherIsBetter(repairAmount, _bunkerRepairSamples);

        public ShopStatTier_V2 GetAmmoReserveTier(float reserveAmmo) =>
            ResolveHigherIsBetter(reserveAmmo, _ammoReserveSamples);

        private void ClearSamples()
        {
            _damageSamples.Clear();
            _fireRateSamples.Clear();
            _magazineSamples.Clear();
            _reloadSamples.Clear();
            _armorPenSamples.Clear();
            _healthHealSamples.Clear();
            _bunkerMaxSamples.Clear();
            _bunkerRepairSamples.Clear();
            _ammoReserveSamples.Clear();
        }

        private void AddWeaponSamples(HeroWeaponDefinition_V2 weapon)
        {
            if (weapon == null)
            {
                return;
            }

            _damageSamples.Add(weapon.BaseDamage);
            _fireRateSamples.Add(1f / weapon.FireRate);
            _magazineSamples.Add(weapon.MaxAmmo);
            _reloadSamples.Add(weapon.ReloadDuration);
            _armorPenSamples.Add(ComputeArmorPenPercent(weapon));
        }

        public static float ComputeArmorPenPercent(HeroWeaponDefinition_V2 weapon)
        {
            if (weapon == null || weapon.BaseDamage <= 0f)
            {
                return 0f;
            }

            float bonus = (weapon.DamageVsAircraft / weapon.BaseDamage) - 1f;
            return Mathf.Max(0f, bonus * 100f);
        }

        private static int ResolveHealthHealAmount(ShopOfferConfig_V2 offer, WaveManager_V2 waveManager)
        {
            if (offer.HealthAmount > 0)
            {
                return offer.HealthAmount;
            }

            return waveManager != null ? waveManager.DefaultHealthPackHealAmount : 25;
        }

        private static int ResolveBunkerMaxIncrease(ShopOfferConfig_V2 offer, WaveManager_V2 waveManager)
        {
            if (offer.BunkerMaxIncrease > 0)
            {
                return offer.BunkerMaxIncrease;
            }

            return waveManager != null ? waveManager.DefaultBunkerMaxUpgradeAmount : 25;
        }

        private static int ResolveBunkerRepairAmount(ShopOfferConfig_V2 offer, WaveManager_V2 waveManager)
        {
            if (offer.BunkerRepairAmount > 0)
            {
                return offer.BunkerRepairAmount;
            }

            return waveManager != null ? waveManager.DefaultBunkerRepairAmount : 25;
        }

        private static void SortSamples(List<float> samples)
        {
            if (samples.Count > 1)
            {
                samples.Sort();
            }
        }

        private static ShopStatTier_V2 ResolveHigherIsBetter(float value, List<float> sortedSamples)
        {
            if (sortedSamples == null || sortedSamples.Count == 0)
            {
                return ShopStatTier_V2.Normal;
            }

            if (sortedSamples.Count == 1)
            {
                return ShopStatTier_V2.Normal;
            }

            float percentile = GetPercentileRank(value, sortedSamples);
            return MapPercentileToTier(percentile);
        }

        private static ShopStatTier_V2 ResolveLowerIsBetter(float value, List<float> sortedSamples)
        {
            if (sortedSamples == null || sortedSamples.Count == 0)
            {
                return ShopStatTier_V2.Normal;
            }

            if (sortedSamples.Count == 1)
            {
                return ShopStatTier_V2.Normal;
            }

            float percentile = 1f - GetPercentileRank(value, sortedSamples);
            return MapPercentileToTier(percentile);
        }

        private static float GetPercentileRank(float value, List<float> sortedSamples)
        {
            int lessOrEqual = 0;
            for (int i = 0; i < sortedSamples.Count; i++)
            {
                if (value >= sortedSamples[i])
                {
                    lessOrEqual++;
                }
            }

            return lessOrEqual / (float)sortedSamples.Count;
        }

        private static ShopStatTier_V2 MapPercentileToTier(float percentile01)
        {
            if (percentile01 < 0.2f)
            {
                return ShopStatTier_V2.Bad;
            }

            if (percentile01 < 0.5f)
            {
                return ShopStatTier_V2.Normal;
            }

            if (percentile01 < 0.8f)
            {
                return ShopStatTier_V2.Good;
            }

            if (percentile01 < 0.95f)
            {
                return ShopStatTier_V2.Epic;
            }

            return ShopStatTier_V2.Legendary;
        }
    }
}
