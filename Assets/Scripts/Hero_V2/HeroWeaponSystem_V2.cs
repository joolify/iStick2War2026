using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
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
        private HeroModel_V2 _model;

        private bool isDisabled;

        // Timing
        private float lastShootTime;
        private float _reloadEndTime;
        private bool _isReloading;
        private readonly HeroShotResolver_V2 _shotResolver = new HeroShotResolver_V2();

        public HeroWeaponSystem_V2(HeroModel_V2 model)
        {
            _model = model;
        }

        // -------------------------
        // SHOOT CHECK
        // -------------------------
        public bool CanShoot()
        {
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

            _model.ConsumeAmmo(1);

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
                Debug.Log($"[HeroWeaponSystem_V2] Shoot blocked. disabled={isDisabled}, dead={_model.isDead}, ammo={_model.currentAmmo}/{_model.maxAmmo}, fireRate={_model.fireRate}, sinceLastShot={Time.time - lastShootTime:0.000}");
                return false;
            }

            lastShootTime = Time.time;
            _model.ConsumeAmmo(1);

            shotResult = _shotResolver.ResolveShot(shotContext);
            Debug.Log($"[HeroWeaponSystem_V2] Shoot OK. didHit={shotResult.DidHit}, finalPos={shotResult.FinalPos}, ammoLeft={_model.currentAmmo}");
            return true;
        }

        // -------------------------
        // RELOAD CHECK
        // -------------------------
        public bool CanReload()
        {
            if (isDisabled) return false;
            if (_model.isDead) return false;
            if (_isReloading) return false;
            if (_model.currentAmmo == _model.maxAmmo) return false;

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

            _isReloading = false;
            _model.RefillAmmo();
            Debug.Log($"[HeroWeaponSystem_V2] Reload complete. ammo={_model.currentAmmo}/{_model.maxAmmo}");
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
    }
}
