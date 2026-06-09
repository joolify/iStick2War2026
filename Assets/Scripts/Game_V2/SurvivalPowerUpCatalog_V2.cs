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
        }

        [SerializeField] private Entry[] _entries =
        {
            new Entry { kind = SurvivalPowerUpKind_V2.HealthPack, weight = 30, healthAmount = 50 },
            new Entry { kind = SurvivalPowerUpKind_V2.BunkerRepair, weight = 25, bunkerRepairAmount = 25 },
            new Entry { kind = SurvivalPowerUpKind_V2.AmmoRefill, weight = 25 },
            new Entry { kind = SurvivalPowerUpKind_V2.WeaponUnlock, weight = 20 }
        };

        public bool TryRollOffer(out SurvivalPowerUpOffer_V2 offer)
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
                if (!IsEntryRollable(entry))
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
                if (!IsEntryRollable(entry))
                {
                    continue;
                }

                cumulative += entry.weight;
                if (roll >= cumulative)
                {
                    continue;
                }

                if (!IsEntryRollable(entry))
                {
                    continue;
                }

                offer = new SurvivalPowerUpOffer_V2
                {
                    kind = entry.kind,
                    healthAmount = Mathf.Max(1, entry.healthAmount),
                    bunkerRepairAmount = Mathf.Max(1, entry.bunkerRepairAmount),
                    weaponDefinition = entry.weaponDefinition
                };
                return true;
            }

            return false;
        }

        private static bool IsEntryRollable(Entry entry)
        {
            if (entry == null || entry.weight <= 0)
            {
                return false;
            }

            if (entry.kind == SurvivalPowerUpKind_V2.WeaponUnlock && entry.weaponDefinition == null)
            {
                return false;
            }

            return true;
        }
    }
}
