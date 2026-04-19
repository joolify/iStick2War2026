using UnityEngine;
using iStick2War;
using System.Collections.Generic;

namespace iStick2War_V2
{
    /*
 * HeroWeaponSystem_V2 (Combat Rules, Not Presentation)
 *
 * PURPOSE
 * - Owns weapon behavior rules: fire gating, ammo, reload, and hit evaluation.
 * - Executes weapon actions when requested by the controller.
 *
 * DOES NOT
 * - Read input directly.
 * - Play animations or VFX.
 * - Change locomotion or state-machine decisions directly.
 *
 * INPUTS
 * - HeroModel_V2 (ammo, fire rate, dead flag).
 * - Aim/shoot context from caller (origin, direction, layer mask, damage).
 *
 * OUTPUTS
 * - Applies ammo/time changes to model.
 * - Produces shot result data (hit/miss, hit point, target) for visual/audio layers.
 *
 * INVARIANTS
 * - No shooting when disabled, dead, out of ammo, or on cooldown.
 * - Reload never exceeds max ammo.
 * - Shooting path should have a single entry point (avoid Shoot/TryShoot divergence).
 *
 * UNITY/SCENE REQUIREMENTS
 * - Raycast LayerMask must include EnemyBodyPart.
 * - Target hitboxes need Collider2D + ParatrooperBodyPart_V2.
 *
 * STATUS (WIP MIGRATION)
 * - TODO(hero-v2): implement Shoot() raycast flow and unify with TryShoot().
 * - TODO(hero-v2): expose shot result event/data for line renderer and muzzle VFX.
 */
    public class HeroWeaponSystem_V2 
    {
        private static readonly bool DebugWeaponLogs = false;
        private readonly HeroModel_V2 _model;
        private readonly HeroWeaponInventory_V2 _inventory = new HeroWeaponInventory_V2();

        private bool isDisabled;

        // Timing
        private float lastShootTime;
        private float _reloadEndTime;
        private bool _isReloading;

        /// <summary>weaponType, isProjectile, rayHit (meaningless when isProjectile).</summary>
        public event System.Action<WeaponType, bool, bool> OnCommittedAttack;

        public event System.Action<WeaponType> OnReloadCompleted;
        private readonly HeroShotResolver_V2 _shotResolver = new HeroShotResolver_V2();

        public HeroWeaponSystem_V2(
            HeroModel_V2 model,
            IEnumerable<HeroWeaponDefinition_V2> initialLoadout,
            WeaponType startingWeapon)
        {
            _model = model;
            InitializeInventory(initialLoadout, startingWeapon);
        }

        // -------------------------
        // SHOOT CHECK
        // -------------------------
        public bool CanShoot()
        {
            if (_inventory.ActiveWeapon == null) return false;
            if (isDisabled) return false;
            if (_model.isDead) return false;
            if (_isReloading) return false;
            if (_model.currentAmmo <= 0) return false;

            float timeSinceLastShot = Time.time - lastShootTime;
            return timeSinceLastShot >= _model.fireRate;
        }

        // -------------------------
        // SHOOT EXECUTION
        // -------------------------
        public void TryShoot()
        {
            if (!CanShoot()) return;

            lastShootTime = Time.time;

            ConsumeAmmo(1);

            // IMPORTANT:
            // här kan du senare trigga events:
            // - recoil
            // - bullet spawn
            // - hit detection
        }

        public bool Shoot(HeroShotContext_V2 shotContext, out HeroShotResult_V2 shotResult)
        {
            shotResult = default;

            if (!CanShoot())
            {
                LogWeapon($"[HeroWeaponSystem_V2] Shoot blocked. disabled={isDisabled}, dead={_model.isDead}, ammo={_model.currentAmmo}/{_model.maxAmmo}, fireRate={_model.fireRate}, sinceLastShot={Time.time - lastShootTime:0.000}");
                return false;
            }

            lastShootTime = Time.time;
            ConsumeAmmo(1);

            shotResult = _shotResolver.ResolveShot(shotContext);
            OnCommittedAttack?.Invoke(_model.currentWeaponType, false, shotResult.DidHit);
            LogWeapon($"[HeroWeaponSystem_V2] Shoot OK. didHit={shotResult.DidHit}, finalPos={shotResult.FinalPos}, ammoLeft={_model.currentAmmo}");
            return true;
        }

        // -------------------------
        // RELOAD CHECK
        // -------------------------
        public bool CanReload()
        {
            if (_inventory.ActiveWeapon == null) return false;
            if (isDisabled) return false;
            if (_model.isDead) return false;
            if (_isReloading) return false;
            if (_model.currentAmmo == _model.maxAmmo) return false;
            if (_model.currentReserveAmmo <= 0) return false;

            return true;
        }

        // -------------------------
        // RELOAD EXECUTION
        // -------------------------
        public bool StartReload()
        {
            if (!CanReload()) return false;

            _isReloading = true;
            _reloadEndTime = Time.time + _model.reloadDuration;
            return true;
        }

        public void Tick()
        {
            if (!_isReloading)
            {
                return;
            }

            if (Time.time < _reloadEndTime)
            {
                return;
            }

            WeaponType weaponForReload = _model.currentWeaponType;
            _isReloading = false;
            RefillAmmo();
            OnReloadCompleted?.Invoke(weaponForReload);
            LogWeapon($"[HeroWeaponSystem_V2] Reload complete. ammo={_model.currentAmmo}/{_model.maxAmmo}");
        }

        public bool IsReloading()
        {
            return _isReloading;
        }

        // -------------------------
        // DISABLE SYSTEM
        // -------------------------
        public void Disable()
        {
            isDisabled = true;
            _isReloading = false;
        }

        internal void Shoot()
        {
            // Backwards-compatible entry point while caller migration is in progress.
            TryShoot();
        }

        public HeroShotContext_V2 CreateShotContext(Vector2 origin, Vector2 direction, bool defaultDebugDrawShotRay)
        {
            HeroWeaponRuntimeState_V2 activeWeapon = _inventory.ActiveWeapon;
            float range = activeWeapon != null ? activeWeapon.Definition.Range : 100f;
            float baseDamage = activeWeapon != null ? activeWeapon.Definition.BaseDamage : 30f;
            float aircraftDamage = activeWeapon != null ? activeWeapon.Definition.DamageVsAircraft : baseDamage;
            bool debugRay = activeWeapon != null ? activeWeapon.Definition.DebugDrawShotRay : defaultDebugDrawShotRay;

            return new HeroShotContext_V2
            {
                Origin = origin,
                Direction = direction,
                Range = range,
                WhatToHit = LayerMask.GetMask("EnemyBodyPart"),
                BaseDamage = baseDamage,
                AircraftDamage = aircraftDamage,
                DebugDrawShotRay = debugRay
            };
        }

        public bool ActiveWeaponUsesProjectile()
        {
            return TryGetActiveWeaponDefinition(out HeroWeaponDefinition_V2 definition) &&
                   ShouldUseProjectile(definition);
        }

        public bool ShootProjectile(Vector2 origin, Vector2 direction)
        {
            if (!TryGetActiveWeaponDefinition(out HeroWeaponDefinition_V2 definition))
            {
                return false;
            }

            if (!ShouldUseProjectile(definition))
            {
                return false;
            }

            if (definition.ProjectilePrefab == null)
            {
                Debug.LogWarning($"[HeroWeaponSystem_V2] Projectile shot blocked: weapon '{definition.WeaponType}' has no ProjectilePrefab assigned.");
                return false;
            }

            if (!CanShoot())
            {
                return false;
            }

            lastShootTime = Time.time;
            ConsumeAmmo(1);

            GameObject projectileObject = Object.Instantiate(definition.ProjectilePrefab, origin, Quaternion.identity);
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            projectileObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            HeroRocketProjectile_V2 rocket = projectileObject.GetComponent<HeroRocketProjectile_V2>();
            if (rocket != null)
            {
                rocket.Initialize(
                    dir,
                    definition.ProjectileSpeed,
                    definition.ProjectileLifetime,
                    definition.BaseDamage,
                    definition.DamageVsAircraft);
            }
            else
            {
                Rigidbody2D rb = projectileObject.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = dir * definition.ProjectileSpeed;
                }
                Object.Destroy(projectileObject, definition.ProjectileLifetime);
            }

            OnCommittedAttack?.Invoke(_model.currentWeaponType, true, false);
            LogWeapon($"[HeroWeaponSystem_V2] Projectile shot. weapon={definition.WeaponType}, ammoLeft={_model.currentAmmo}");
            return true;
        }

        public bool TrySwitchToNextWeapon()
        {
            if (isDisabled || _model.isDead) return false;
            return TrySwitchActiveWeapon(_inventory.SwitchNext);
        }

        public bool TrySwitchToPreviousWeapon()
        {
            if (isDisabled || _model.isDead) return false;
            return TrySwitchActiveWeapon(_inventory.SwitchPrevious);
        }

        public bool TrySwitchToSlot(int slotIndex)
        {
            if (isDisabled || _model.isDead) return false;
            return TrySwitchActiveWeapon(() => _inventory.SetActiveBySlot(slotIndex));
        }

        public bool TrySwitchToWeaponType(WeaponType weaponType)
        {
            if (isDisabled || _model.isDead)
            {
                return false;
            }

            return TrySwitchActiveWeapon(() => _inventory.SetActiveByType(weaponType));
        }

        public bool HasUnlockedWeaponOfType(WeaponType weaponType)
        {
            return !isDisabled && !_model.isDead && _inventory.ContainsWeaponType(weaponType);
        }

        /// <summary>True if that weapon is in the loadout and has rounds in mag or reserve (reload possible).</summary>
        public bool HasUsableAmmoForWeaponType(WeaponType weaponType)
        {
            if (isDisabled || _model.isDead || !_inventory.TryGetWeaponStateByType(weaponType, out HeroWeaponRuntimeState_V2 state))
            {
                return false;
            }

            return state.CurrentAmmo > 0 || state.CurrentReserveAmmo > 0;
        }

        /// <summary>Switches to the first unlocked weapon that still has ammo (loadout order).</summary>
        public bool TrySwitchToAnyWeaponWithAmmo()
        {
            if (isDisabled || _model.isDead)
            {
                return false;
            }

            if (!_inventory.TryGetFirstWeaponIndexWithAmmo(out int idx))
            {
                return false;
            }

            return TrySwitchActiveWeapon(() => _inventory.SetActiveBySlot(idx));
        }

        public bool UnlockWeapon(HeroWeaponDefinition_V2 definition, bool autoEquip = false)
        {
            if (definition == null)
            {
                return false;
            }

            int beforeCount = _inventory.Count;
            _inventory.AddIfMissing(definition);
            bool added = _inventory.Count > beforeCount;

            if (autoEquip)
            {
                bool switched = _inventory.SetActiveByType(definition.WeaponType);
                if (switched)
                {
                    _isReloading = false;
                    ApplyActiveWeaponToModel();
                }
            }

            return added;
        }

        public bool HasWeaponUnlocked(HeroWeaponDefinition_V2 definition)
        {
            return definition != null && _inventory.HasWeapon(definition);
        }

        public bool IsMagazineFullForWeapon(HeroWeaponDefinition_V2 definition)
        {
            if (definition == null || !_inventory.TryGetWeaponState(definition, out HeroWeaponRuntimeState_V2 state))
            {
                return false;
            }

            return state.Definition != null &&
                   state.CurrentAmmo >= state.Definition.MaxAmmo &&
                   state.CurrentReserveAmmo >= state.Definition.MaxReserveAmmo;
        }

        public bool TryRefillMagazineForWeapon(HeroWeaponDefinition_V2 definition)
        {
            if (definition == null || isDisabled || _model.isDead)
            {
                return false;
            }

            if (!_inventory.TryGetWeaponState(definition, out HeroWeaponRuntimeState_V2 state))
            {
                return false;
            }

            if (state.Definition == null)
            {
                return false;
            }

            bool alreadyFull =
                state.CurrentAmmo >= state.Definition.MaxAmmo &&
                state.CurrentReserveAmmo >= state.Definition.MaxReserveAmmo;
            if (alreadyFull)
            {
                return false;
            }

            state.CurrentAmmo = state.Definition.MaxAmmo;
            state.CurrentReserveAmmo = state.Definition.MaxReserveAmmo;

            HeroWeaponRuntimeState_V2 active = _inventory.ActiveWeapon;
            if (active != null &&
                active.Definition != null &&
                active.Definition.WeaponType == definition.WeaponType)
            {
                _isReloading = false;
                ApplyActiveWeaponToModel();
            }

            return true;
        }

        private void InitializeInventory(IEnumerable<HeroWeaponDefinition_V2> initialLoadout, WeaponType startingWeapon)
        {
            if (initialLoadout != null)
            {
                foreach (HeroWeaponDefinition_V2 def in initialLoadout)
                {
                    _inventory.AddIfMissing(def);
                }
            }

            if (_inventory.Count == 0)
            {
                LogWeapon("[HeroWeaponSystem_V2] No loadout assigned. Weapon switching disabled until weapons are added.");
                return;
            }

            if (!_inventory.SetActiveByType(startingWeapon))
            {
                _inventory.SetActiveBySlot(0);
            }

            ApplyActiveWeaponToModel();
        }

        private void ApplyActiveWeaponToModel()
        {
            HeroWeaponRuntimeState_V2 active = _inventory.ActiveWeapon;
            if (active == null || active.Definition == null)
            {
                return;
            }

            _model.ConfigureWeaponState(
                active.Definition,
                active.Definition.WeaponType,
                active.Definition.MaxAmmo,
                active.CurrentAmmo,
                active.Definition.MaxReserveAmmo,
                active.CurrentReserveAmmo,
                active.Definition.FireRate,
                active.Definition.ReloadDuration);
            LogWeapon($"[HeroWeaponSystem_V2] Active weapon: {active.Definition.WeaponType} ({active.CurrentAmmo}/{active.Definition.MaxAmmo}).");
        }

        private void ConsumeAmmo(int amount)
        {
            HeroWeaponRuntimeState_V2 active = _inventory.ActiveWeapon;
            if (active != null)
            {
                active.CurrentAmmo = Mathf.Max(0, active.CurrentAmmo - amount);
            }

            _model.ConsumeAmmo(amount);
        }

        private void RefillAmmo()
        {
            HeroWeaponRuntimeState_V2 active = _inventory.ActiveWeapon;
            if (active != null && active.Definition != null)
            {
                int needed = Mathf.Max(0, active.Definition.MaxAmmo - active.CurrentAmmo);
                int toLoad = Mathf.Min(needed, Mathf.Max(0, active.CurrentReserveAmmo));
                active.CurrentAmmo += toLoad;
                active.CurrentReserveAmmo = Mathf.Max(0, active.CurrentReserveAmmo - toLoad);
                _model.SetAmmoState(active.CurrentAmmo, active.CurrentReserveAmmo);
                return;
            }

            _model.SetAmmoState(_model.currentAmmo, 0);
        }

        private bool TryGetActiveWeaponDefinition(out HeroWeaponDefinition_V2 definition)
        {
            definition = _inventory.ActiveWeapon != null ? _inventory.ActiveWeapon.Definition : null;
            return definition != null;
        }

        private static bool ShouldUseProjectile(HeroWeaponDefinition_V2 definition)
        {
            return definition != null &&
                   (definition.WeaponType == WeaponType.Bazooka || definition.UseProjectile);
        }

        private bool TrySwitchActiveWeapon(System.Func<bool> switchAction)
        {
            if (switchAction == null || !switchAction())
            {
                return false;
            }

            _isReloading = false;
            ApplyActiveWeaponToModel();
            return true;
        }

        private static void LogWeapon(string message)
        {
            if (DebugWeaponLogs)
            {
                Debug.Log(message);
            }
        }
    }
}
