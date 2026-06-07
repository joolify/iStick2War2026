using System;
using System.Collections.Generic;
using System.Text;
using iStick2War;
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
        private TMP_Text _warningText;
        private Color _warningTextColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        private GameObject _statsPanelRoot;
        private IReadOnlyList<ShopOfferConfig_V2> _configuredOffers;
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
            _warningText = FindText(
                findLabelText,
                "txt_shop_stat_warningText_label",
                new[]
                {
                    "txt_shop_stat_warning_text_label",
                    "txt_shop_stat_warning_text",
                    "txt_shop_stat_warning",
                });
            if (findObject != null)
            {
                _statsPanelRoot = findObject("panel_shop_stats") ?? findObject("ShopStatsContainer");
            }

            _bindingsResolved = true;
        }

        public void SetWarningText(TMP_Text warningText, Color color)
        {
            if (warningText != null)
            {
                _warningText = warningText;
            }

            _warningTextColor = color;
            ApplyWarningTextColor();
        }

        public void SetWarningTextColor(Color color)
        {
            _warningTextColor = color;
            ApplyWarningTextColor();
        }

        public void RebuildBaselines(IReadOnlyList<ShopOfferConfig_V2> offers, WaveManager_V2 waveManager)
        {
            _configuredOffers = offers;
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
                RefreshRunWarnings(waveManager, null);
                return;
            }

            SetStatsPanelVisible(true);
            int cost = waveManager.GetOfferEffectiveCost(offer);
            SetPlainText(_costText, ShopMoneyFormat_V2.FormatCost(cost));
            bool weaponOwned = offer.Weapon != null && waveManager.IsWeaponOwned(offer.Weapon);

            switch (offer.Kind)
            {
                case ShopOfferKind_V2.WeaponUnlock:
                    RefreshWeaponStats(offer.Weapon, waveManager, showAmmoRow: weaponOwned);
                    break;

                case ShopOfferKind_V2.AmmoRefill:
                    RefreshWeaponStats(offer.Weapon, waveManager, showAmmoRow: weaponOwned);
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

            RefreshRunWarnings(waveManager, offer);
        }

        public void RefreshRunWarnings(WaveManager_V2 waveManager, ShopOfferConfig_V2 selectedOffer)
        {
            if (!_bindingsResolved || _warningText == null)
            {
                return;
            }

            string warningText = BuildRunWarningText(waveManager, selectedOffer);
            if (string.IsNullOrEmpty(warningText))
            {
                SetPlainText(_warningText, string.Empty);
                _warningText.gameObject.SetActive(false);
                return;
            }

            _warningText.gameObject.SetActive(true);
            ApplyWarningTextColor();
            SetPlainText(_warningText, warningText);
        }

        // TMP multiplies Vertex Color by material Face Color; black Face makes any vertex color look black.
        private void ApplyWarningTextColor()
        {
            if (_warningText == null)
            {
                return;
            }

            _warningText.color = _warningTextColor;
            Material material = _warningText.fontMaterial;
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(ShaderUtilities.ID_FaceColor))
            {
                material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
            }
        }

        public void HideAll()
        {
            HideAllRows();
            SetStatsPanelVisible(false);
            SetPlainText(_costText, string.Empty);
            RefreshRunWarnings(null, null);
        }

        private string BuildRunWarningText(WaveManager_V2 waveManager, ShopOfferConfig_V2 selectedOffer)
        {
            if (waveManager == null || selectedOffer == null)
            {
                return string.Empty;
            }

            var lines = new List<string>(3);
            Hero_V2 hero = waveManager.Hero;

            switch (selectedOffer.Kind)
            {
                case ShopOfferKind_V2.HealthPack:
                    if (hero != null && !waveManager.IsHeroHealthFull())
                    {
                        lines.Add(
                            $"Warning: Hero HP below max ({hero.GetCurrentHealth()}/{hero.GetMaxHealth()})");
                    }

                    break;

                case ShopOfferKind_V2.BunkerRepair:
                case ShopOfferKind_V2.BunkerMaxUpgrade:
                    if (!waveManager.IsBunkerFullHealth())
                    {
                        lines.Add(
                            $"Warning: Bunker HP below max ({waveManager.BunkerHealth}/{waveManager.BunkerMaxHealth})");
                    }

                    break;

                case ShopOfferKind_V2.WeaponUnlock:
                case ShopOfferKind_V2.AmmoRefill:
                    TryAddWeaponAmmoWarningForOffer(waveManager, selectedOffer, lines);
                    break;
            }

            if (lines.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(lines[i]);
            }

            return builder.ToString();
        }

        private static void TryAddWeaponAmmoWarningForOffer(
            WaveManager_V2 waveManager,
            ShopOfferConfig_V2 offer,
            List<string> lines)
        {
            if (waveManager == null || offer == null || lines == null || offer.Weapon == null)
            {
                return;
            }

            if (AutoHero_V2.WeaponTestLockShowsInfiniteAmmoOnTopBar)
            {
                return;
            }

            Hero_V2 hero = waveManager.Hero;
            if (hero == null || !waveManager.IsWeaponOwned(offer.Weapon) || waveManager.IsWeaponAmmoFull(offer.Weapon))
            {
                return;
            }

            if (!hero.TryGetOwnedWeaponAmmo(
                    offer.Weapon,
                    out int mag,
                    out int maxMag,
                    out int reserve,
                    out int maxReserve))
            {
                return;
            }

            HeroWeaponDefinition_V2 weapon = offer.Weapon;
            string weaponName = string.IsNullOrWhiteSpace(weapon.DisplayName)
                ? weapon.WeaponType.ToString()
                : weapon.DisplayName;
            string summary = FormatOwnedWeaponAmmoDisplay(weapon.WeaponType, mag, maxMag, reserve, maxReserve);
            lines.Add($"Warning: Low ammo ({weaponName} {summary})");
        }

        public static string FormatOwnedWeaponAmmoDisplay(
            WeaponType weaponType,
            int mag,
            int maxMag,
            int reserve,
            int maxReserve)
        {
            if (HeroWeaponAmmoRules_V2.HasInfiniteReserveAmmo(weaponType))
            {
                return $"{mag}/{maxMag} (∞)";
            }

            if (weaponType == WeaponType.Bazooka)
            {
                return $"{mag} mag, {reserve} rkt ({mag + reserve}/{maxMag + maxReserve})";
            }

            return $"{mag} mag + {reserve} rsv ({mag + reserve}/{maxMag + maxReserve})";
        }

        private static ShopStatTier_V2 GetAmmoFillTier(int currentTotal, int maxTotal)
        {
            if (maxTotal <= 0)
            {
                return ShopStatTier_V2.Bad;
            }

            float ratio = (float)currentTotal / maxTotal;
            if (ratio >= 0.99f)
            {
                return ShopStatTier_V2.Good;
            }

            if (ratio >= 0.45f)
            {
                return ShopStatTier_V2.Normal;
            }

            return ShopStatTier_V2.Bad;
        }

        private static bool TryGetOwnedAmmoDisplay(
            HeroWeaponDefinition_V2 weapon,
            WaveManager_V2 waveManager,
            out string display,
            out ShopStatTier_V2 tier)
        {
            display = string.Empty;
            tier = ShopStatTier_V2.Normal;
            if (weapon == null || waveManager == null)
            {
                return false;
            }

            Hero_V2 hero = waveManager.Hero;
            if (hero == null || !hero.TryGetOwnedWeaponAmmo(
                    weapon,
                    out int mag,
                    out int maxMag,
                    out int reserve,
                    out int maxReserve))
            {
                return false;
            }

            display = FormatOwnedWeaponAmmoDisplay(weapon.WeaponType, mag, maxMag, reserve, maxReserve);
            int currentTotal = mag + reserve;
            int maxTotal = maxMag + maxReserve;
            if (weapon.WeaponType == WeaponType.Bazooka)
            {
                currentTotal = mag + reserve;
                maxTotal = maxMag + maxReserve;
            }

            tier = GetAmmoFillTier(currentTotal, maxTotal);
            return true;
        }

        private void RefreshWeaponStats(HeroWeaponDefinition_V2 weapon, WaveManager_V2 waveManager, bool showAmmoRow)
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

            if (showAmmoRow && TryGetOwnedAmmoDisplay(weapon, waveManager, out string ammoValue, out ShopStatTier_V2 ammoTier))
            {
                ShowWeaponRow(_ammo, "Your ammo", ammoValue, ammoTier);
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
                ShowInfoRow(
                    _heroHealth,
                    "Health",
                    $"{current} > {after}");
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
                $"{before} > {after}",
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
                $"{before} > {after}",
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
                SetLabelText(row.Label, label);
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
                // No value TMP in scene: keep the label word black, tier-color only the stat value.
                SetCombinedLabelValueText(row.Label, label, value, tier);
            }
        }

        // Informational stat rows (hero HP before/after): black label + black value, no tier tint.
        private static void ShowInfoRow(StatRow row, string label, string value)
        {
            if (row == null || !RowHasBinding(row))
            {
                return;
            }

            SetRowActive(row, true);

            if (row.Value != null && row.Label != null)
            {
                SetLabelText(row.Label, label);
                SetLabelText(row.Value, value);
                return;
            }

            if (row.Value != null)
            {
                SetLabelText(row.Value, $"{label}: {value}");
                return;
            }

            if (row.Label != null)
            {
                SetLabelText(row.Label, $"{label}: {value}");
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

        private static readonly Color StatLabelColor = Color.black;

        private static void SetLabelText(TMP_Text textField, string value)
        {
            if (textField == null)
            {
                return;
            }

            textField.text = value ?? string.Empty;
            textField.color = StatLabelColor;
            EnsureTmpFaceColorWhite(textField);
        }

        private static void SetCombinedLabelValueText(
            TMP_Text textField,
            string label,
            string value,
            ShopStatTier_V2 tier)
        {
            if (textField == null)
            {
                return;
            }

            Color tierColor = ShopStatTierColors_V2.GetColor(tier);
            string tierHex = ColorUtility.ToHtmlStringRGB(tierColor);
            textField.text = $"<color=#000000>{label}:</color> <color=#{tierHex}>{value}</color>";
            textField.color = Color.white;
            EnsureTmpFaceColorWhite(textField);
        }

        private static void EnsureTmpFaceColorWhite(TMP_Text textField)
        {
            Material material = textField.fontMaterial;
            if (material != null && material.HasProperty(ShaderUtilities.ID_FaceColor))
            {
                material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
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
