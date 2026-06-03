using UnityEngine;

namespace iStick2War_V2
{
    /*
 * ShopOfferKind_V2 (Shop offer category discriminator)
 *
 * PURPOSE:
 * Identifies which WaveManager_V2 purchase path executes for a carousel row (health, bunker repair, weapon unlock,
 * ammo refill, bunker max upgrade).
 *
 * NAVIGATION: rows live on ShopPanel_V2; purchases execute in WaveManager_V2.
 */
    public enum ShopOfferKind_V2
    {
        HealthPack,
        BunkerRepair,
        WeaponUnlock,
        AmmoRefill,
        BunkerMaxUpgrade
    }

    /*
 * ShopOfferConfig_V2 (Serializable shop carousel row)
 *
 * PURPOSE:
 * Holds display string, ShopOfferKind_V2, cost, per-kind payload fields (heal/repair amounts, weapon references),
 * and optional preview GameObject. ShopPanel_V2 reads these entries to populate UI and invoke purchases.
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Data-only serializable class so designers edit offers as list elements without custom editors.
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * UI host → ShopPanel_V2.cs | Economy execution → WaveManager_V2.cs
 */
    [System.Serializable]
    public sealed class ShopOfferConfig_V2
    {
        [SerializeField] private string _displayName = "";
        [SerializeField] private ShopOfferKind_V2 _kind = ShopOfferKind_V2.WeaponUnlock;
        [SerializeField] private int _cost = 50;
        [Tooltip("Health pack heal amount. 0 = use WaveManager default.")]
        [SerializeField] private int _healthAmount;
        [Tooltip("Bunker repair amount. 0 = use WaveManager default.")]
        [SerializeField] private int _bunkerRepairAmount;
        [Tooltip("Bunker max HP increase (run-persistent). 0 = use WaveManager default.")]
        [SerializeField] private int _bunkerMaxIncrease;
        [Tooltip("Only used for WeaponUnlock / AmmoRefill. Ignored for bunker and health offers.")]
        [SerializeField] private HeroWeaponDefinition_V2 _weapon;
        [Tooltip("Optional shop preview (scene instance under Items/Weapons, or shop_* prefab). Shown when this offer is selected.")]
        [SerializeField] private GameObject _previewObject;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_displayName))
                {
                    return _displayName;
                }

                if (_kind is ShopOfferKind_V2.WeaponUnlock or ShopOfferKind_V2.AmmoRefill &&
                    _weapon != null &&
                    !string.IsNullOrWhiteSpace(_weapon.DisplayName))
                {
                    return _weapon.DisplayName;
                }

                return _kind.ToString();
            }
        }

        public ShopOfferKind_V2 Kind => _kind;
        public int Cost => Mathf.Max(0, _cost);
        public int HealthAmount => Mathf.Max(0, _healthAmount);
        public int BunkerRepairAmount => Mathf.Max(0, _bunkerRepairAmount);
        public int BunkerMaxIncrease => Mathf.Max(0, _bunkerMaxIncrease);
        public HeroWeaponDefinition_V2 Weapon => _weapon;
        public GameObject PreviewObject => _previewObject;
    }
}
