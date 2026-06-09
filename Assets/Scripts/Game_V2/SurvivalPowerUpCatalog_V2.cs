using System;
using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SurvivalPowerUpCatalog_V2 — weighted random survival powerup table.
     * Assign on SwedishPlaneSurvivalCoordinator_V2 or as a Resources asset.
     */
    [CreateAssetMenu(fileName = "SurvivalPowerUpCatalog_V2", menuName = "iStick2War/Survival PowerUp Catalog V2")]
    public sealed class SurvivalPowerUpCatalog_V2 : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SurvivalPowerUpKind_V2 kind = SurvivalPowerUpKind_V2.HealthPack;
            [Min(1)] public int weight = 1;
            [Min(1)] public int healthAmount = 50;
            [Min(1)] public int bunkerRepairAmount = 25;
            public HeroWeaponDefinition_V2 weaponDefinition;
            // Optional UI override; empty displayName uses kind-based fallback at roll time.
            public string displayName;
            public string pickupTitle;
            public Sprite previewSprite;
            [Tooltip("Optional shop_* prefab or sprite root used for PowerUpImage on the crate.")]
            public GameObject previewObject;
        }

        [Header("Default previews when an entry leaves preview empty")]
        [SerializeField] private GameObject _defaultHealthPreviewObject;
        [SerializeField] private GameObject _defaultBunkerRepairPreviewObject;
        [SerializeField] private GameObject _defaultAmmoRefillPreviewObject;

        [SerializeField] private Entry[] _entries =
        {
            new Entry { kind = SurvivalPowerUpKind_V2.HealthPack, weight = 30, healthAmount = 50 },
            new Entry { kind = SurvivalPowerUpKind_V2.BunkerRepair, weight = 25, bunkerRepairAmount = 25 },
            new Entry { kind = SurvivalPowerUpKind_V2.AmmoRefill, weight = 25 },
            new Entry { kind = SurvivalPowerUpKind_V2.WeaponUnlock, weight = 20 }
        };

        public bool TryRollOffer(out SurvivalPowerUpOffer_V2 offer)
        {
            return TryRollOffer(hero: null, out offer);
        }

        public bool TryRollOffer(Hero_V2 hero, out SurvivalPowerUpOffer_V2 offer)
        {
            offer = null;
            if (_entries == null || _entries.Length == 0)
            {
                return false;
            }

            int totalWeight = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                if (!IsEntryRollable(entry, hero))
                {
                    continue;
                }

                totalWeight += entry.weight;
            }

            if (totalWeight <= 0)
            {
                return false;
            }

            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                if (!IsEntryRollable(entry, hero))
                {
                    continue;
                }

                cumulative += entry.weight;
                if (roll >= cumulative)
                {
                    continue;
                }

                offer = new SurvivalPowerUpOffer_V2
                {
                    kind = entry.kind,
                    healthAmount = Mathf.Max(1, entry.healthAmount),
                    bunkerRepairAmount = Mathf.Max(1, entry.bunkerRepairAmount),
                    weaponDefinition = entry.weaponDefinition,
                    displayName = ResolveDisplayName(entry),
                    pickupTitle = entry.pickupTitle,
                    previewSprite = entry.previewSprite,
                    previewObject = ResolvePreviewObject(entry)
                };
                return true;
            }

            return false;
        }

        private string ResolveDisplayName(Entry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(entry.displayName))
            {
                return entry.displayName;
            }

            var rolled = new SurvivalPowerUpOffer_V2
            {
                kind = entry.kind,
                healthAmount = entry.healthAmount,
                bunkerRepairAmount = entry.bunkerRepairAmount,
                weaponDefinition = entry.weaponDefinition
            };
            return SurvivalPowerUpPreviewResolver_V2.ResolveDisplayName(rolled);
        }

        private GameObject ResolvePreviewObject(Entry entry)
        {
            if (entry == null)
            {
                return null;
            }

            if (entry.previewObject != null)
            {
                return entry.previewObject;
            }

            switch (entry.kind)
            {
                case SurvivalPowerUpKind_V2.HealthPack:
                    return _defaultHealthPreviewObject;
                case SurvivalPowerUpKind_V2.BunkerRepair:
                    return _defaultBunkerRepairPreviewObject;
                case SurvivalPowerUpKind_V2.AmmoRefill:
                    return _defaultAmmoRefillPreviewObject;
                default:
                    return null;
            }
        }

        private static bool IsEntryRollable(Entry entry, Hero_V2 hero)
        {
            if (entry == null || entry.weight <= 0)
            {
                return false;
            }

            if (entry.kind == SurvivalPowerUpKind_V2.WeaponUnlock && entry.weaponDefinition == null)
            {
                return false;
            }

            if (entry.kind == SurvivalPowerUpKind_V2.AmmoRefill)
            {
                if (entry.weaponDefinition == null)
                {
                    return false;
                }

                if (hero != null && !hero.HasWeaponUnlocked(entry.weaponDefinition))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
