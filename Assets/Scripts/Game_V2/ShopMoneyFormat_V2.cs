using System.Globalization;

namespace iStick2War_V2
{
    /*
     * Formats shop currency as whole-dollar USD strings for TMP labels (e.g. $899).
     */
    public static class ShopMoneyFormat_V2
    {
        private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

        public static string Format(int amount)
        {
            int dollars = amount < 0 ? 0 : amount;
            return dollars.ToString("C0", UsCulture);
        }

        public static string FormatCost(int amount)
        {
            return $"Cost: {Format(amount)}";
        }
    }
}
