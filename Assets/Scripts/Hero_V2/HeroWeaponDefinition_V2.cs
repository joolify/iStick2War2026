using iStick2War;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
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

        [Header("Projectile (optional)")]
        [SerializeField] private bool _useProjectile = false;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private float _projectileSpeed = 14f;
        [SerializeField] private float _projectileLifetime = 5f;

        [Header("Presentation")]
        [SerializeField] private AnimationReferenceAsset _idleAnimation;
        [SerializeField] private AnimationReferenceAsset _aimAnimation;
        [SerializeField] private AnimationReferenceAsset _shootAnimation;
        [SerializeField] private AnimationReferenceAsset _runAnimation;
        [SerializeField] private AnimationReferenceAsset _jumpAnimation;
        [SerializeField] private AnimationReferenceAsset _reloadAnimation;
        [SerializeField] private AnimationReferenceAsset _dryFireAnimation;

        public WeaponType WeaponType => _weaponType;
        public string DisplayName => _displayName;
        public int MaxAmmo => Mathf.Max(1, _maxAmmo);
        public float FireRate => Mathf.Max(0.01f, _fireRate);
        public float ReloadDuration => Mathf.Max(0.01f, _reloadDuration);
        public float BaseDamage => Mathf.Max(0f, _baseDamage);
        public float Range => Mathf.Max(1f, _range);
        public bool DebugDrawShotRay => _debugDrawShotRay;
        public bool UseProjectile => _useProjectile;
        public GameObject ProjectilePrefab => _projectilePrefab;
        public float ProjectileSpeed => Mathf.Max(0.1f, _projectileSpeed);
        public float ProjectileLifetime => Mathf.Max(0.1f, _projectileLifetime);
        public AnimationReferenceAsset IdleAnimation => _idleAnimation;
        public AnimationReferenceAsset AimAnimation => _aimAnimation;
        public AnimationReferenceAsset ShootAnimation => _shootAnimation;
        public AnimationReferenceAsset RunAnimation => _runAnimation;
        public AnimationReferenceAsset JumpAnimation => _jumpAnimation;
        public AnimationReferenceAsset ReloadAnimation => _reloadAnimation;
        public AnimationReferenceAsset DryFireAnimation => _dryFireAnimation;
    }
}
