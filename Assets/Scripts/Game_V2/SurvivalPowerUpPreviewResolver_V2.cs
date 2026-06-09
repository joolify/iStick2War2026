using UnityEngine;
using UnityEngine.UI;

namespace iStick2War_V2
{
    // Resolves survival powerup reward label + preview sprite for world/UI pickup chrome.
    public static class SurvivalPowerUpPreviewResolver_V2
    {
        public static string ResolveDisplayName(SurvivalPowerUpOffer_V2 offer)
        {
            if (offer == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(offer.displayName))
            {
                return offer.displayName;
            }

            switch (offer.kind)
            {
                case SurvivalPowerUpKind_V2.HealthPack:
                    return $"+{Mathf.Max(1, offer.healthAmount)} HP";
                case SurvivalPowerUpKind_V2.BunkerRepair:
                    return $"+{Mathf.Max(1, offer.bunkerRepairAmount)} Bunker";
                case SurvivalPowerUpKind_V2.WeaponUnlock:
                    return offer.weaponDefinition != null && !string.IsNullOrWhiteSpace(offer.weaponDefinition.DisplayName)
                        ? offer.weaponDefinition.DisplayName
                        : "Weapon";
                case SurvivalPowerUpKind_V2.AmmoRefill:
                    if (offer.weaponDefinition != null && !string.IsNullOrWhiteSpace(offer.weaponDefinition.DisplayName))
                    {
                        return $"{offer.weaponDefinition.DisplayName} ammo";
                    }

                    return "Ammo refill";
                default:
                    return offer.kind.ToString();
            }
        }

        public static string ResolvePickupTitle(SurvivalPowerUpOffer_V2 offer)
        {
            if (offer == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(offer.pickupTitle))
            {
                return offer.pickupTitle;
            }

            switch (offer.kind)
            {
                case SurvivalPowerUpKind_V2.HealthPack:
                    return "Health restored";
                case SurvivalPowerUpKind_V2.BunkerRepair:
                    return "Bunker repaired";
                case SurvivalPowerUpKind_V2.WeaponUnlock:
                    return "Weapon unlocked";
                case SurvivalPowerUpKind_V2.AmmoRefill:
                    return "Ammo refilled";
                default:
                    return "Power-up acquired";
            }
        }

        public static Sprite ResolvePreviewSprite(SurvivalPowerUpOffer_V2 offer)
        {
            if (offer == null)
            {
                return null;
            }

            if (offer.previewSprite != null)
            {
                return offer.previewSprite;
            }

            return ResolvePreviewSprite(offer.previewObject);
        }

        public static Sprite ResolvePreviewSprite(GameObject previewRoot)
        {
            if (previewRoot == null)
            {
                return null;
            }

            SpriteRenderer spriteRenderer = previewRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                return spriteRenderer.sprite;
            }

            Image image = previewRoot.GetComponentInChildren<Image>(true);
            return image != null ? image.sprite : null;
        }
    }
}
