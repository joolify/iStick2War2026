using iStick2War;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    [CreateAssetMenu(
        fileName = "HeroWeaponDefinition_V2",
        menuName = "iStick2War/Hero V2/Weapon Definition")]
    public sealed class HeroWeaponDefinition_V2 : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private WeaponType _weaponType = WeaponType.Thompson;
        [SerializeField] private string _displayName = "Thompson";

        [Header("Combat")]
        [SerializeField] private int _maxAmmo = 30;
        [SerializeField] private float _fireRate = 0.1f;
        [SerializeField] private float _reloadDuration = 0.5f;
        [SerializeField] private float _baseDamage = 30f;
        [SerializeField] private float _range = 100f;
        [SerializeField] private bool _debugDrawShotRay = true;

        public WeaponType WeaponType => _weaponType;
        public string DisplayName => _displayName;
        public int MaxAmmo => Mathf.Max(1, _maxAmmo);
        public float FireRate => Mathf.Max(0.01f, _fireRate);
        public float ReloadDuration => Mathf.Max(0.01f, _reloadDuration);
        public float BaseDamage => Mathf.Max(0f, _baseDamage);
        public float Range => Mathf.Max(1f, _range);
        public bool DebugDrawShotRay => _debugDrawShotRay;
    }
}
