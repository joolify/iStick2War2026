using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * Binds txt_shop_stat_* TMP rows to the selected ShopOfferConfig_V2 and applies tier colors.
     */
    public sealed class ShopOfferStatsPresenter_V2
    {
        private sealed class StatRow
        {
            public string LabelObjectName;
            public string ValueObjectName;
            public string[] LabelAlternateNames;
            public string[] ValueAlternateNames;
            public TMP_Text Label;
            public TMP_Text Value;
            public GameObject LabelRoot;
            public GameObject ValueRoot;
        }

        private readonly ShopStatTierResolver_V2 _tierResolver = new ShopStatTierResolver_V2();
        private readonly StatRow _damage = CreateStatRow(
            "txt_shop_stat_damage_label",
            "txt_shop_stat_damage_value",
            valueAlternates: new[] { "txt_shop_stat_damage" });
        private readonly StatRow _fireRate = CreateStatRow(
            "txt_shop_stat_fire_rate_label",
            "txt_shop_stat_fire_rate_value");
        private readonly StatRow _magazine = CreateStatRow(
            "txt_shop_stat_magazine_label",
            "txt_shop_stat_magazine_value",
            labelAlternates: new[] { "txt_shop_state_magazine_label" },
            valueAlternates: new[]
            {
                "txt_shop_state_magazine",
                "txt_shop_state_magazine_value",
                "txt_shop_stat_magazine",
            });
        private readonly StatRow _reload = CreateStatRow(
            "txt_shop_stat_reload_label",
            "txt_shop_stat_reload_value");
        private readonly StatRow _armorPen = CreateStatRow(
            "txt_shop_stat_armor_pen_label",
            "txt_shop_stat_armor_pen_value",
            labelAlternates: new[]
            {
                "txt_shop_stat_armor_pen_labael",
                "txt_shop_stat_armor_pen_labeal",
                "txt_shop_stat_armor_pe n_label",
                "txt_shop_stat_armor_pe_n_label",
                "txt_shop_stat_armor pen_label",
            });
        private readonly StatRow _health = CreateStatRow(
            "txt_shop_stat_health_label",
            "txt_shop_stat_health_value");
        private readonly StatRow _bunkerMax = CreateStatRow(
            "txt_shop_stat_bunker_max_label",
            "txt_shop_stat_bunker_max_value");
        private readonly StatRow _bunkerRepair = CreateStatRow(
            "txt_shop_stat_bunker_repair_label",
            "txt_shop_stat_bunker_repair_value");
        private readonly StatRow _heroHealth = CreateStatRow(
            "txt_shop_stat_hero_health_label",
            "txt_shop_stat_hero_health_value");
        private readonly StatRow _ammo = CreateStatRow(
            "txt_shop_stat_ammo_label",
            "txt_shop_stat_ammo_value");

        private TMP_Text _costText;
        private GameObject _statsPanelRoot;
        private bool _bindingsResolved;

        public void ResolveBindings(
            Func<string, TMP_Text> findLabelText,
            Func<string, TMP_Text> findValueText,
            Func<string, GameObject> findObject,
            Func<TMP_Text, string, string[], TMP_Text> findLabelNearValue = null)
        {
            if (findLabelText == null || findValueText == null)
            {
                return;
            }

            ResolveRow(_damage, findLabelText, findValueText, findLabelNearValue);
            ResolveRow(_fireRate, findLabelText, findValueText, findLabelNearValue);
            ResolveRow(_magazine, findLabelText, findValueText, findLabelNearValue);
            ResolveRow(_reload, findLabelText, findValueText, findLabelNearValue);
            ResolveRow(_armorPen, findLabelText, findValueText, findLabelNearValue);
            ResolveRow(_health, findLabelText, findValueText, findLabelNearValue);
            ResolveRow(_bunkerMax, findLabelText, findValueText, findLabelNearValue);
            ResolveRow(_bunkerRepair, findLabelText, findValueText, findLabelNearValue);
            ResolveRow(_heroHealth, findLabelText, findValueText, findLabelNearValue);
            ResolveRow(_ammo, findLabelText, findValueText, findLabelNearValue);

            _costText = findLabelText("txt_shop_cost");
            _statsPanelRoot = findObject != null ? findObject("panel_shop_stats") : null;

            _bindingsResolved = true;
        }

        public void RebuildBaselines(IReadOnlyList<ShopOfferConfig_V2> offers, WaveManager_V2 waveManager)
        {
            _tierResolver.RebuildFromOffers(offers, waveManager);
        }

        public void Refresh(ShopOfferConfig_V2 offer, WaveManager_V2 waveManager)
        {
            if (!_bindingsResolved)
            {
                return;
            }

            HideAllRows();

            if (offer == null || waveManager == null)
            {
                SetStatsPanelVisible(false);
                return;
            }

            SetStatsPanelVisible(true);
            int cost = waveManager.GetOfferEffectiveCost(offer);
            SetPlainText(_costText, $"Cost: {cost}");

            switch (offer.Kind)
            {
                case ShopOfferKind_V2.WeaponUnlock:
                    RefreshWeaponStats(offer.Weapon, showAmmoRow: false);
                    break;

                case ShopOfferKind_V2.AmmoRefill:
                    RefreshWeaponStats(offer.Weapon, showAmmoRow: true);
                    break;

                case ShopOfferKind_V2.HealthPack:
                    RefreshHealthPackStats(offer, waveManager);
                    break;

                case ShopOfferKind_V2.BunkerMaxUpgrade:
                    RefreshBunkerMaxStats(offer, waveManager);
                    break;

                case ShopOfferKind_V2.BunkerRepair:
                    RefreshBunkerRepairStats(offer, waveManager);
                    break;
            }
        }

        public void HideAll()
        {
            HideAllRows();
            SetStatsPanelVisible(false);
            SetPlainText(_costText, string.Empty);
        }

        private void RefreshWeaponStats(HeroWeaponDefinition_V2 weapon, bool showAmmoRow)
        {
            if (weapon == null)
            {
                return;
            }

            float shotsPerSecond = 1f / weapon.FireRate;
            float armorPen = ShopStatTierResolver_V2.ComputeArmorPenPercent(weapon);

            ShowWeaponRow(
                _damage,
                "Damage",
                weapon.BaseDamage.ToString("0"),
                _tierResolver.GetDamageTier(weapon.BaseDamage));
            ShowWeaponRow(
                _fireRate,
                "Fire Rate",
                $"{shotsPerSecond:0.#}/s",
                _tierResolver.GetFireRateTier(shotsPerSecond));
            ShowWeaponRow(
                _magazine,
                "Magazine",
                weapon.MaxAmmo.ToString(),
                _tierResolver.GetMagazineTier(weapon.MaxAmmo));
            ShowWeaponRow(
                _reload,
                "Reload",
                $"{weapon.ReloadDuration:0.#}s",
                _tierResolver.GetReloadTier(weapon.ReloadDuration));

            if (armorPen > 0.5f)
            {
                ShowWeaponRow(
                    _armorPen,
                    "Armor Pen.",
                    $"+{armorPen:0}%",
                    _tierResolver.GetArmorPenTier(armorPen));
            }

            if (showAmmoRow)
            {
                ShowWeaponRow(
                    _ammo,
                    "Ammo",
                    $"{weapon.MaxAmmo} / {weapon.MaxReserveAmmo}",
                    _tierResolver.GetAmmoReserveTier(weapon.MaxReserveAmmo));
            }
        }

        private void RefreshHealthPackStats(ShopOfferConfig_V2 offer, WaveManager_V2 waveManager)
        {
            int heal = offer.HealthAmount > 0 ? offer.HealthAmount : waveManager.DefaultHealthPackHealAmount;
            Hero_V2 hero = waveManager.Hero;
            if (hero != null)
            {
                int current = hero.GetCurrentHealth();
                int max = hero.GetMaxHealth();
                int after = Mathf.Min(max, current + heal);
                ShowRow(
                    _heroHealth,
                    "Health",
                    $"{current} -> {after}",
                    _tierResolver.GetHealthHealTier(heal));
            }

            ShowRow(
                _health,
                "Health",
                $"+{heal} HP",
                _tierResolver.GetHealthHealTier(heal));
        }

        private void RefreshBunkerMaxStats(ShopOfferConfig_V2 offer, WaveManager_V2 waveManager)
        {
            int delta = offer.BunkerMaxIncrease > 0 ? offer.BunkerMaxIncrease : waveManager.DefaultBunkerMaxUpgradeAmount;
            int before = waveManager.BunkerMaxHealth;
            int after = before + delta;
            ShowRow(
                _bunkerMax,
                "Bunker Max HP",
                $"{before} -> {after}",
                _tierResolver.GetBunkerMaxTier(delta));
        }

        private void RefreshBunkerRepairStats(ShopOfferConfig_V2 offer, WaveManager_V2 waveManager)
        {
            int repair = offer.BunkerRepairAmount > 0 ? offer.BunkerRepairAmount : waveManager.DefaultBunkerRepairAmount;
            int before = waveManager.BunkerHealth;
            int after = Mathf.Min(waveManager.BunkerMaxHealth, before + repair);
            ShowRow(
                _bunkerRepair,
                "Bunker Repair",
                $"{before} -> {after}",
                _tierResolver.GetBunkerRepairTier(repair));
        }

        private void ShowWeaponRow(StatRow row, string label, string value, ShopStatTier_V2 tier)
        {
            ShowRow(row, label, value, tier);
        }

        private static void ShowRow(StatRow row, string label, string value, ShopStatTier_V2 tier)
        {
            if (row == null || !RowHasBinding(row))
            {
                return;
            }

            SetRowActive(row, true);

            if (row.Value != null && row.Label != null)
            {
                SetPlainText(row.Label, label);
                SetTierValue(row.Value, value, tier);
                return;
            }

            if (row.Value != null)
            {
                SetTierValue(row.Value, $"{label}: {value}", tier);
                return;
            }

            if (row.Label != null)
            {
                SetTierValue(row.Label, $"{label}: {value}", tier);
            }
        }

        private void HideAllRows()
        {
            SetRowActive(_damage, false);
            SetRowActive(_fireRate, false);
            SetRowActive(_magazine, false);
            SetRowActive(_reload, false);
            SetRowActive(_armorPen, false);
            SetRowActive(_health, false);
            SetRowActive(_bunkerMax, false);
            SetRowActive(_bunkerRepair, false);
            SetRowActive(_heroHealth, false);
            SetRowActive(_ammo, false);
        }

        private void SetStatsPanelVisible(bool visible)
        {
            if (_statsPanelRoot != null)
            {
                _statsPanelRoot.SetActive(visible);
            }
        }

        private static StatRow CreateStatRow(
            string labelObjectName,
            string valueObjectName,
            string[] labelAlternates = null,
            string[] valueAlternates = null)
        {
            return new StatRow
            {
                LabelObjectName = labelObjectName,
                ValueObjectName = valueObjectName,
                LabelAlternateNames = labelAlternates,
                ValueAlternateNames = valueAlternates,
            };
        }

        private static bool RowHasBinding(StatRow row)
        {
            return row != null && (row.Label != null || row.Value != null);
        }

        private static void ResolveRow(
            StatRow row,
            Func<string, TMP_Text> findLabelText,
            Func<string, TMP_Text> findValueText,
            Func<TMP_Text, string, string[], TMP_Text> findLabelNearValue)
        {
            if (row == null || findLabelText == null || findValueText == null)
            {
                return;
            }

            row.Label = FindText(findLabelText, row.LabelObjectName, row.LabelAlternateNames);
            row.Value = FindText(findValueText, row.ValueObjectName, row.ValueAlternateNames);

            if (row.Label == null && row.Value != null && findLabelNearValue != null)
            {
                row.Label = findLabelNearValue(row.Value, row.LabelObjectName, row.LabelAlternateNames);
            }

            if (row.Label != null && row.Value == row.Label)
            {
                row.Value = null;
            }

            row.LabelRoot = row.Label != null ? row.Label.gameObject : null;
            row.ValueRoot = row.Value != null ? row.Value.gameObject : null;
        }

        private static TMP_Text FindText(Func<string, TMP_Text> findText, string primaryName, string[] alternateNames)
        {
            TMP_Text match = findText(primaryName);
            if (match != null)
            {
                return match;
            }

            if (alternateNames == null)
            {
                return null;
            }

            for (int i = 0; i < alternateNames.Length; i++)
            {
                string alternateName = alternateNames[i];
                if (string.IsNullOrWhiteSpace(alternateName))
                {
                    continue;
                }

                match = findText(alternateName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void SetRowActive(StatRow row, bool active)
        {
            if (row == null)
            {
                return;
            }

            if (row.LabelRoot != null)
            {
                row.LabelRoot.SetActive(active);
            }

            if (row.ValueRoot != null)
            {
                row.ValueRoot.SetActive(active);
            }
        }

        private static void SetPlainText(TMP_Text textField, string value)
        {
            if (textField != null)
            {
                textField.text = value ?? string.Empty;
            }
        }

        private static void SetTierValue(TMP_Text valueField, string value, ShopStatTier_V2 tier)
        {
            if (valueField == null)
            {
                return;
            }

            valueField.text = value ?? string.Empty;
            valueField.color = ShopStatTierColors_V2.GetColor(tier);
        }
    }
}
