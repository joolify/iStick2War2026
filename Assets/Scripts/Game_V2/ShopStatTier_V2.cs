using UnityEngine;

namespace iStick2War_V2
{
    /*
     * Shop stat quality tiers (Diablo-style) for carousel stat values.
     * Percentile bands are resolved in ShopStatTierResolver_V2 from shop weapon rows.
     */
    public enum ShopStatTier_V2
    {
        Bad = 0,
        Normal = 1,
        Good = 2,
        Epic = 3,
        Legendary = 4,
    }

    public static class ShopStatTierColors_V2
    {
        // Bad / Normal / Good / Epic / Legendary
        private static readonly Color Bad = new Color(0.8f, 0.2f, 0.2f, 1f);
        private static readonly Color Normal = new Color(0.91f, 0.78f, 0.36f, 1f);
        private static readonly Color Good = new Color(0.3f, 0.69f, 0.31f, 1f);
        private static readonly Color Epic = new Color(0.29f, 0.56f, 0.85f, 1f);
        private static readonly Color Legendary = new Color(0.64f, 0.21f, 0.93f, 1f);

        public static Color GetColor(ShopStatTier_V2 tier)
        {
            switch (tier)
            {
                case ShopStatTier_V2.Bad:
                    return Bad;
                case ShopStatTier_V2.Good:
                    return Good;
                case ShopStatTier_V2.Epic:
                    return Epic;
                case ShopStatTier_V2.Legendary:
                    return Legendary;
                default:
                    return Normal;
            }
        }
    }
}
