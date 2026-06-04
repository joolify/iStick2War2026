using System.Collections.Generic;
using UnityEngine;
using iStick2War;

namespace iStick2War_V2
{
    // Per-pellet data for Ithaca shotgun VFX (line trails) after the latest Shoot().
    public readonly struct IthacaPelletVisualSnapshot_V2
    {
        public readonly Vector2 Direction;
        public readonly Vector2 FinalPos;
        public readonly bool DidHit;
        public readonly RaycastHit2D Hit;

        public IthacaPelletVisualSnapshot_V2(Vector2 direction, Vector2 finalPos, bool didHit, RaycastHit2D hit)
        {
            Direction = direction;
            FinalPos = finalPos;
            DidHit = didHit;
            Hit = hit;
        }
    }

    /*
 * HeroWeaponSystem_V2 (Combat Rules, Not Presentation)
 *
 * PURPOSE:
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
 * - Raycast LayerMask must include EnemyBodyPart (infantry) and Aircraft (AircraftHealth_V2 hitboxes).
 * - Infantry: Collider2D + ParatrooperBodyPart_V2. Aircraft: Collider2D + AircraftHealth_V2 on same hierarchy.
 *
 * STATUS (partial migration)
 * - Hit-scan path: Shoot(HeroShotContext_V2, out HeroShotResult_V2) uses HeroShotResolver_V2.
 * - TryShoot() / internal Shoot() still exist as a lighter path without raycast; callers should
 *   prefer the context-based Shoot for combat. TODO: fold legacy callers so one entry wins.
 * - TODO(hero-v2): richer shot result events/data for line renderer and muzzle VFX if needed.
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
        private float _minigunHeat01;
        private float _lastMinigunHeatUpdateTime;

        private const float MinigunHeatPerShot = 0.105f;
        private const float MinigunCooldownPerSecond = 0.42f;
        private const float MinigunOverheatBlockThreshold = 1f;
        private const float MinigunOverheatRecoverThreshold = 0.35f;
        private const float MinigunMaxSpreadDegreesAtFullHeat = 7f;
        public const int IthacaPelletCount = 5;
        private const float IthacaPelletSpreadDegrees = 14f;
        private const float IthacaFarDamageMultiplier = 0.25f;
        // At or above this distance falloff multiplier, Ithaca triggers paratrooper explosive gib (bazooka-style).
        public const float IthacaExplosiveGibMinDistanceMultiplier = 0.85f;
        private readonly IthacaPelletVisualSnapshot_V2[] _ithacaPelletSnapshots =
            new IthacaPelletVisualSnapshot_V2[IthacaPelletCount];

        /// <summary>weaponType, isProjectile, rayHit (meaningless when isProjectile).</summary>
        public event System.Action<WeaponType, bool, bool> OnCommittedAttack;

        public event System.Action<WeaponType> OnReloadCompleted;
        private readonly HeroShotResolver_V2 _shotResolver = new HeroShotResolver_V2();

        public IthacaPelletVisualSnapshot_V2 GetIthacaPelletVisual(int index)
        {
            return _ithacaPelletSnapshots[Mathf.Clamp(index, 0, IthacaPelletCount - 1)];
        }

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
            TickMinigunHeat();

            if (_inventory.ActiveWeapon == null) return false;
            if (isDisabled) return false;
            if (_model.isDead) return false;
            if (_isReloading) return false;
            if (_model.currentAmmo <= 0)
            {
                return false;
            }
            if (_model.currentWeaponType == WeaponType.Minigun &&
                _minigunHeat01 >= MinigunOverheatBlockThreshold)
            {
                return false;
            }

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
            AddWorldShakeForCommittedHitscanShot(_model.currentWeaponType);

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
            AccumulateMinigunHeatFromShot();

            shotResult = shotContext.WeaponType == WeaponType.Ithaca
                ? ResolveIthacaPelletShot(shotContext)
                : _shotResolver.ResolveShot(shotContext);
            OnCommittedAttack?.Invoke(_model.currentWeaponType, false, shotResult.DidHit);
            AddWorldShakeForCommittedHitscanShot(_model.currentWeaponType);
            LogWeapon($"[HeroWeaponSystem_V2] Shoot OK. didHit={shotResult.DidHit}, finalPos={shotResult.FinalPos}, ammoLeft={_model.currentAmmo}");
            return true;
        }

        private static void AddWorldShakeForCommittedHitscanShot(WeaponType weaponType)
        {
            switch (weaponType)
            {
                case WeaponType.Colt45:
                    WorldShake_V2.AddImpulse(WorldShakeImpulseKind_V2.Colt45Shot);
                    break;
                case WeaponType.Thompson:
                    WorldShake_V2.AddImpulse(WorldShakeImpulseKind_V2.ThompsonShot);
                    break;
                case WeaponType.Ithaca:
                    WorldShake_V2.AddImpulse(WorldShakeImpulseKind_V2.ThompsonShot);
                    break;
            }
        }

        private HeroShotResult_V2 ResolveIthacaPelletShot(HeroShotContext_V2 context)
        {
            HeroShotResult_V2 combined = default;
            bool anyHit = false;
            float closestDistance = float.PositiveInfinity;
            float pelletDamageShare = 1f / IthacaPelletCount;
            float pelletBaseDamage = context.BaseDamage * pelletDamageShare;
            float pelletAircraftDamage = context.AircraftDamage * pelletDamageShare;
            int centerPelletIndex = IthacaPelletCount / 2;

            for (int pelletIndex = 0; pelletIndex < IthacaPelletCount; pelletIndex++)
            {
                float spread = Random.Range(-IthacaPelletSpreadDegrees, IthacaPelletSpreadDegrees);
                Vector2 pelletDirection = RotateDirection2D(context.Direction, spread);
                HeroShotContext_V2 pelletContext = context;
                pelletContext.Direction = pelletDirection;
                pelletContext.BaseDamage = pelletBaseDamage;
                pelletContext.AircraftDamage = pelletAircraftDamage;

                HeroShotResult_V2 pelletResult = _shotResolver.ResolveShot(pelletContext);
                _ithacaPelletSnapshots[pelletIndex] = new IthacaPelletVisualSnapshot_V2(
                    pelletDirection,
                    pelletResult.FinalPos,
                    pelletResult.DidHit,
                    pelletResult.Hit);

                if (!pelletResult.DidHit)
                {
                    continue;
                }

                anyHit = true;
                float distance = pelletResult.Hit.distance;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    combined = pelletResult;
                }
            }

            if (!anyHit)
            {
                IthacaPelletVisualSnapshot_V2 centerPellet = _ithacaPelletSnapshots[centerPelletIndex];
                combined = new HeroShotResult_V2
                {
                    DidHit = false,
                    FinalPos = centerPellet.FinalPos,
                    Hit = default
                };
            }

            combined.DidHit = anyHit;
            return combined;
        }

        // Full pellet damage at point-blank; IthacaFarDamageMultiplier at weapon max range (quadratic falloff).
        public static float GetIthacaPelletDamageMultiplierByDistance(float hitDistance, float weaponRange)
        {
            float normalized = Mathf.Clamp01(hitDistance / Mathf.Max(0.5f, weaponRange));
            float falloffT = normalized * normalized;
            return Mathf.Lerp(1f, IthacaFarDamageMultiplier, falloffT);
        }

        public static bool ShouldIthacaCauseExplosiveGib(float hitDistance, float weaponRange)
        {
            return GetIthacaPelletDamageMultiplierByDistance(hitDistance, weaponRange) >=
                   IthacaExplosiveGibMinDistanceMultiplier;
        }

        public static float GetIthacaExplosionForceForHit(float hitDistance, float weaponRange)
        {
            float closeness = GetIthacaPelletDamageMultiplierByDistance(hitDistance, weaponRange);
            return Mathf.Lerp(3.5f, 9f, closeness);
        }

        private static Vector2 RotateDirection2D(Vector2 direction, float degrees)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector2.right;
            }

            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            Vector2 normalized = direction.normalized;
            return new Vector2(
                normalized.x * cos - normalized.y * sin,
                normalized.x * sin + normalized.y * cos);
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
            if (_model.currentAmmo == _model.maxAmmo)
            {
                return false;
            }

            if (HeroWeaponAmmoRules_V2.HasInfiniteReserveAmmo(_model.currentWeaponType))
            {
                return true;
            }

            if (_model.currentReserveAmmo <= 0)
            {
                return false;
            }

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

        public void Enable()
        {
            isDisabled = false;
            _isReloading = false;
            _lastMinigunHeatUpdateTime = Time.time;
        }

        public bool IsCombatDisabled => isDisabled;

        internal void Shoot()
        {
            // Backwards-compatible entry point while caller migration is in progress.
            TryShoot();
        }

        public HeroShotContext_V2 CreateShotContext(
            Vector2 origin,
            Vector2 direction,
            bool defaultDebugDrawShotRay,
            float flamethrowerViewReachFraction = -1f)
        {
            TickMinigunHeat();
            HeroWeaponRuntimeState_V2 activeWeapon = _inventory.ActiveWeapon;
            float range = activeWeapon != null ? activeWeapon.Definition.Range : 100f;
            float baseDamage = activeWeapon != null ? activeWeapon.Definition.BaseDamage : 30f;
            float aircraftDamage = activeWeapon != null ? activeWeapon.Definition.DamageVsAircraft : baseDamage;
            bool debugRay = activeWeapon != null ? activeWeapon.Definition.DebugDrawShotRay : defaultDebugDrawShotRay;

            WeaponType weaponForDamage =
                activeWeapon != null && activeWeapon.Definition != null
                    ? activeWeapon.Definition.WeaponType
                    : _model.currentWeaponType;

            Vector2 adjustedDirection = direction;
            if (weaponForDamage == WeaponType.Minigun)
            {
                adjustedDirection = ApplyMinigunSpread(direction);
            }

            float viewReachFraction = 1f;
            if (_model.currentWeaponType == WeaponType.Flamethrower)
            {
                debugRay = false;
                viewReachFraction = flamethrowerViewReachFraction >= 0f
                    ? Mathf.Clamp01(flamethrowerViewReachFraction)
                    : HeroCombatCameraReach_V2.DefaultFlamethrowerViewReachFraction;
            }

            range = HeroCombatCameraReach_V2.ClampShotRangeToCombatView(
                Camera.main,
                origin,
                adjustedDirection,
                range,
                viewReachFraction);

            return new HeroShotContext_V2
            {
                Origin = origin,
                Direction = adjustedDirection,
                Range = range,
                WhatToHit = LayerMask.GetMask("EnemyBodyPart", "Aircraft"),
                BaseDamage = baseDamage,
                AircraftDamage = aircraftDamage,
                DebugDrawShotRay = debugRay,
                WeaponType = weaponForDamage
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
            AccumulateMinigunHeatFromShot();

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

            if (!_inventory.ContainsWeaponType(weaponType))
            {
                return false;
            }

            // Inventory.SetActiveByType returns false when that weapon is already active (no-op).
            // Callers like combat tests expect "switch to current" to succeed.
            HeroWeaponRuntimeState_V2 active = _inventory.ActiveWeapon;
            if (active != null && active.Definition != null && active.Definition.WeaponType == weaponType)
            {
                // Rare but possible: inventory slot matches while HeroModel_V2 still reflects another weapon
                // (e.g. skipped programmatic refresh). CreateShotContext reads inventory → wrong DamageInfo weapon.
                if (_model.currentWeaponType != weaponType)
                {
                    _isReloading = false;
                    if (_model.currentWeaponType != WeaponType.Minigun)
                    {
                        _minigunHeat01 = 0f;
                    }

                    ApplyActiveWeaponToModel();
                }

                return true;
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

            return state.Definition != null &&
                   (HeroWeaponAmmoRules_V2.HasInfiniteReserveAmmo(state.Definition.WeaponType) ||
                    state.CurrentAmmo > 0 ||
                    state.CurrentReserveAmmo > 0);
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

            if (added)
            {
                SetWeaponAmmoToMax(definition);
            }

            if (autoEquip)
            {
                EquipWeaponFromShop(definition);
            }
            else if (added)
            {
                HeroWeaponRuntimeState_V2 active = _inventory.ActiveWeapon;
                if (active != null &&
                    active.Definition != null &&
                    active.Definition.WeaponType == definition.WeaponType)
                {
                    ApplyActiveWeaponToModel();
                }
            }

            return added;
        }

        private void SetWeaponAmmoToMax(HeroWeaponDefinition_V2 definition)
        {
            if (definition == null ||
                !_inventory.TryGetWeaponState(definition, out HeroWeaponRuntimeState_V2 state) ||
                state.Definition == null)
            {
                return;
            }

            state.CurrentAmmo = state.Definition.MaxAmmo;
            state.CurrentReserveAmmo = state.Definition.MaxReserveAmmo;
        }

        // Shop purchases run while combat gate has disabled shooting; still sync inventory + HeroModel_V2.
        public bool EquipWeaponFromShop(HeroWeaponDefinition_V2 definition)
        {
            if (definition == null || _model.isDead)
            {
                return false;
            }

            if (!_inventory.ContainsWeaponType(definition.WeaponType))
            {
                return false;
            }

            _inventory.SetActiveByType(definition.WeaponType);
            _isReloading = false;
            if (_model.currentWeaponType != WeaponType.Minigun && definition.WeaponType != WeaponType.Minigun)
            {
                _minigunHeat01 = 0f;
            }

            ApplyActiveWeaponToModel();
            return true;
        }

        public bool HasWeaponUnlocked(HeroWeaponDefinition_V2 definition)
        {
            return definition != null && _inventory.HasWeapon(definition);
        }

        /// <summary>Scene profile: drop weapons not in <paramref name="allowed"/> and sync model to active weapon.</summary>
        public void RestrictInventoryToAllowedWeaponTypes(IReadOnlyList<WeaponType> allowed)
        {
            if (allowed == null || allowed.Count == 0 || isDisabled || _model.isDead)
            {
                return;
            }

            var keep = new HashSet<WeaponType>(allowed);
            _inventory.RemoveAllExcept(keep);
            _isReloading = false;
            ApplyActiveWeaponToModel();
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

        public bool TryGetWeaponAmmoCounts(
            HeroWeaponDefinition_V2 definition,
            out int currentMagazine,
            out int maxMagazine,
            out int currentReserve,
            out int maxReserve)
        {
            currentMagazine = 0;
            maxMagazine = 0;
            currentReserve = 0;
            maxReserve = 0;
            if (definition == null || !_inventory.TryGetWeaponState(definition, out HeroWeaponRuntimeState_V2 state) ||
                state.Definition == null)
            {
                return false;
            }

            currentMagazine = state.CurrentAmmo;
            currentReserve = state.CurrentReserveAmmo;
            maxMagazine = state.Definition.MaxAmmo;
            maxReserve = state.Definition.MaxReserveAmmo;
            return true;
        }

        /// <summary>Fills mag + reserve for an unlocked weapon type (used by automation / weapon test range).</summary>
        public bool TryRefillMagazineForWeaponType(WeaponType weaponType)
        {
            if (!_inventory.TryGetWeaponStateByType(weaponType, out HeroWeaponRuntimeState_V2 state) ||
                state == null ||
                state.Definition == null)
            {
                return false;
            }

            return TryRefillMagazineForWeapon(state.Definition);
        }

        // Life retry / shop refill: top up every owned weapon to max mag + reserve; sync active weapon to HeroModel_V2.
        public void RefillAllWeaponsToMax()
        {
            _isReloading = false;
            for (int i = 0; i < _inventory.WeaponCount; i++)
            {
                HeroWeaponRuntimeState_V2 state = _inventory.GetWeaponStateAtIndex(i);
                if (state == null || state.Definition == null)
                {
                    continue;
                }

                state.CurrentAmmo = state.Definition.MaxAmmo;
                state.CurrentReserveAmmo = state.Definition.MaxReserveAmmo;
            }

            ApplyActiveWeaponToModel();
        }

        public bool TryRefillMagazineForWeapon(HeroWeaponDefinition_V2 definition)
        {
            // Shop ammo purchases and test-range setup must work while combat gate has disabled shooting (Shop phase).
            if (definition == null || _model.isDead)
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

            // Initial loadout uses StartingMagazineAmmo=0 on assets; fill mags like shop unlock does.
            RefillAllWeaponsToMax();
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
                if (HeroWeaponAmmoRules_V2.HasInfiniteReserveAmmo(active.Definition.WeaponType))
                {
                    active.CurrentAmmo = active.Definition.MaxAmmo;
                    active.CurrentReserveAmmo = active.Definition.MaxReserveAmmo;
                    _model.SetAmmoState(active.CurrentAmmo, active.CurrentReserveAmmo);
                    return;
                }

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
            if (_model.currentWeaponType != WeaponType.Minigun)
            {
                _minigunHeat01 = 0f;
            }
            ApplyActiveWeaponToModel();
            return true;
        }

        private void TickMinigunHeat()
        {
            float now = Time.time;
            float dt = Mathf.Max(0f, now - _lastMinigunHeatUpdateTime);
            _lastMinigunHeatUpdateTime = now;
            if (dt <= 0f)
            {
                return;
            }

            if (_model.currentWeaponType != WeaponType.Minigun)
            {
                _minigunHeat01 = Mathf.Max(0f, _minigunHeat01 - dt * (MinigunCooldownPerSecond * 1.75f));
                return;
            }

            _minigunHeat01 = Mathf.Max(0f, _minigunHeat01 - dt * MinigunCooldownPerSecond);
            if (_minigunHeat01 < MinigunOverheatRecoverThreshold)
            {
                // Allows firing again after a clear recover window.
                _minigunHeat01 = Mathf.Min(_minigunHeat01, MinigunOverheatRecoverThreshold - 0.0001f);
            }
        }

        private void AccumulateMinigunHeatFromShot()
        {
            if (_model.currentWeaponType != WeaponType.Minigun)
            {
                return;
            }

            _minigunHeat01 = Mathf.Clamp01(_minigunHeat01 + MinigunHeatPerShot);
        }

        private Vector2 ApplyMinigunSpread(Vector2 direction)
        {
            Vector2 baseDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float spread = Mathf.Lerp(1.25f, MinigunMaxSpreadDegreesAtFullHeat, Mathf.Clamp01(_minigunHeat01));
            float angle = UnityEngine.Random.Range(-spread, spread);
            float rad = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(
                baseDir.x * cos - baseDir.y * sin,
                baseDir.x * sin + baseDir.y * cos).normalized;
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
