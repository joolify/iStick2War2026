using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    /*
 * HeroWeaponSystem_V2 (Combat Execution System)
 *
 * PURPOSE:
 * HeroWeaponSystem_V2 is responsible for handling all weapon-related gameplay logic
 * such as shooting, reloading, and ammunition management.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Read input directly
 * - Modify or set gameplay state
 * - Play animations
 * - Spawn VFX directly (future event-based system may handle this)
 *
 * ---------------------------------------------------------
 * ✅ RESPONSIBILITIES:
 *
 * - Manage ammo and weapon state
 * - Handle fire rate and cooldowns
 * - Determine if shooting or reloading is allowed
 * - Execute shoot and reload logic
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE:
 *
 * This system owns the logic of "how weapons behave",
 * but does NOT decide when actions are triggered.
 */
    public class HeroWeaponSystem_V2 
    {
        private HeroModel_V2 _model;

        private bool isDisabled;

        // Timing
        private float lastShootTime;
        private float reloadTime = 1.5f;

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

        // -------------------------
        // RELOAD CHECK
        // -------------------------
        public bool CanReload()
        {
            if (isDisabled) return false;
            if (_model.isDead) return false;
            if (_model.currentAmmo == _model.maxAmmo) return false;

            return true;
        }

        // -------------------------
        // RELOAD EXECUTION
        // -------------------------
        public void Reload()
        {
            if (!CanReload()) return;

            // senare: animation timing + delay system
            _model.RefillAmmo();
        }

        // -------------------------
        // DISABLE SYSTEM
        // -------------------------
        public void Disable()
        {
            isDisabled = true;
        }
    }
}
