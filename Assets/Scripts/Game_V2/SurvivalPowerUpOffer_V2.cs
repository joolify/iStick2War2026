using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    // Rolled offer applied when the hero picks up a SwedishPlane powerup.
    public sealed class SurvivalPowerUpOffer_V2
    {
        public SurvivalPowerUpKind_V2 kind;
        public int healthAmount;
        public int bunkerRepairAmount;
        public HeroWeaponDefinition_V2 weaponDefinition;
        // Optional presentation copied from SurvivalPowerUpCatalog_V2 at roll time.
        public string displayName;
        public string pickupTitle;
        public Sprite previewSprite;
        public GameObject previewObject;
    }
}
