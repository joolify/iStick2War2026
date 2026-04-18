using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;

namespace iStick2War.Tests.EditMode
{
    public sealed class ShopOfferConfig_V2Tests
    {
        [Test]
        public void DisplayName_UsesSerializedName_WhenNonEmpty()
        {
            var offer = new ShopOfferConfig_V2();
            EditModeTestHelpers.SetPrivateField(offer, "_displayName", "  Medkit  ");
            EditModeTestHelpers.SetPrivateField(offer, "_kind", ShopOfferKind_V2.HealthPack);

            Assert.That(offer.DisplayName, Is.EqualTo("  Medkit  "));
        }

        [Test]
        public void DisplayName_FallsBackToWeaponDisplayName_ForWeaponUnlock()
        {
            HeroWeaponDefinition_V2 weapon = ScriptableObject.CreateInstance<HeroWeaponDefinition_V2>();
            try
            {
                EditModeTestHelpers.SetPrivateField(weapon, "_displayName", "Bazooka Mk1");
                var offer = new ShopOfferConfig_V2();
                EditModeTestHelpers.SetPrivateField(offer, "_displayName", "");
                EditModeTestHelpers.SetPrivateField(offer, "_kind", ShopOfferKind_V2.WeaponUnlock);
                EditModeTestHelpers.SetPrivateField(offer, "_weapon", weapon);

                Assert.That(offer.DisplayName, Is.EqualTo("Bazooka Mk1"));
            }
            finally
            {
                Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void DisplayName_FallsBackToKindName_WhenNoOverride()
        {
            var offer = new ShopOfferConfig_V2();
            EditModeTestHelpers.SetPrivateField(offer, "_displayName", "");
            EditModeTestHelpers.SetPrivateField(offer, "_kind", ShopOfferKind_V2.BunkerRepair);
            EditModeTestHelpers.SetPrivateField<HeroWeaponDefinition_V2>(offer, "_weapon", null);

            Assert.That(offer.DisplayName, Is.EqualTo(nameof(ShopOfferKind_V2.BunkerRepair)));
        }

        [Test]
        public void Cost_IsNotNegative()
        {
            var offer = new ShopOfferConfig_V2();
            EditModeTestHelpers.SetPrivateField(offer, "_cost", -25);

            Assert.That(offer.Cost, Is.EqualTo(0));
        }

        [Test]
        public void HealthAndBunkerAmounts_AreNotNegative()
        {
            var offer = new ShopOfferConfig_V2();
            EditModeTestHelpers.SetPrivateField(offer, "_healthAmount", -5);
            EditModeTestHelpers.SetPrivateField(offer, "_bunkerRepairAmount", -7);
            EditModeTestHelpers.SetPrivateField(offer, "_bunkerMaxIncrease", -1);

            Assert.That(offer.HealthAmount, Is.EqualTo(0));
            Assert.That(offer.BunkerRepairAmount, Is.EqualTo(0));
            Assert.That(offer.BunkerMaxIncrease, Is.EqualTo(0));
        }
    }
}
