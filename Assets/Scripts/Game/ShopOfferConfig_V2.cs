using UnityEngine;

namespace iStick2War_V2
{
    public enum ShopOfferKind_V2
    {
        HealthPack,
        BunkerRepair,
        WeaponUnlock,
        AmmoRefill
    }

    /// <summary>
    /// One row in the shop carousel. Configure in Inspector on ShopPanel_V2.
    /// </summary>
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
        [Tooltip("Required for WeaponUnlock and AmmoRefill.")]
        [SerializeField] private HeroWeaponDefinition_V2 _weapon;
        [Tooltip("Optional: GameObject in the scene (e.g. weapon silhouette). Only the active offer's object is enabled.")]
        [SerializeField] private GameObject _previewObject;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_displayName))
                {
                    return _displayName;
                }

                if (_weapon != null && !string.IsNullOrWhiteSpace(_weapon.DisplayName))
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
        public HeroWeaponDefinition_V2 Weapon => _weapon;
        public GameObject PreviewObject => _previewObject;
    }
}
