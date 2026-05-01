using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using iStick2War;

namespace iStick2War_V2
{
    /*
 * HeroModel_V2 (Pure Data Layer)
 *
 * RESPONSIBILITY:
 * HeroModel_V2 represents the pure game state of the Hero.
 * It contains ONLY data and no behaviour.
 *
 * ---------------------------------------------------------
 * STORED DATA:
 *
 * - Health
 * - State
 * - Velocity
 * - Ammo
 * - IsDead
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT CONTAIN:
 *
 * - No Unity logic (no Update, FixedUpdate, etc.)
 * - No gameplay logic
 * - No physics
 * - No animation handling
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE:
 *
 * This is a pure data container ("game DNA").
 * It is modified by systems (Controller, DamageReceiver, etc.)
 * and read by other systems (StateMachine, View, etc.).
 */
    public class HeroModel_V2 : MonoBehaviour
    {
        // -------------------------
        // INPUT (read by controller)
        // -------------------------
        public Vector2 moveInput { get; internal set; }
        public bool isShootingPressed { get; internal set; }
        public bool isReloadPressed { get; internal set; }
        public bool isJumpPressed { get; internal set; }

        // -------------------------
        // STATE
        // -------------------------
        public HeroState currentState { get; private set; } = HeroState.Idle;
        public bool isDead { get; private set; }

        // -------------------------
        // HEALTH
        // -------------------------
        public int maxHealth { get; private set; } = 100;
        public int currentHealth { get; private set; } = 100;

        // -------------------------
        // MOVEMENT
        // -------------------------
        public Vector2 velocity { get; internal set; }
        public float moveSpeed { get; private set; } = 5f;

        // -------------------------
        // WEAPON
        // -------------------------
        public int maxAmmo { get; private set; } = 30;
        public int currentAmmo { get; private set; } = 30;
        public int maxReserveAmmo { get; private set; } = 90;
        public int currentReserveAmmo { get; private set; } = 90;
        public float fireRate { get; private set; } = 0.1f;

        // -------------------------
        // TIMERS (for gameplay feel)
        // -------------------------
        public float lastShootTime { get; internal set; }
        public float reloadDuration { get; private set; } = 0.5f;
        public float reloadTimer { get; internal set; }
        public WeaponType currentWeaponType { get; private set; } = WeaponType.Colt45;
        public HeroWeaponDefinition_V2 currentWeaponDefinition { get; private set; }

        // -------------------------
        // STATE SETTERS (controlled)
        // -------------------------
        public void SetState(HeroState newState)
        {
            currentState = newState;
        }

        public void SetDead()
        {
            isDead = true;
            currentState = HeroState.Dead;
        }

        public void ReviveToHealthFraction(float healthFraction01)
        {
            float f = Mathf.Clamp01(healthFraction01);
            isDead = false;
            currentHealth = Mathf.Clamp(Mathf.RoundToInt(maxHealth * f), 1, maxHealth);
            currentState = HeroState.Idle;
        }

        // -------------------------
        // HEALTH LOGIC (safe)
        // -------------------------
        public void TakeDamage(int damage)
        {
            if (isDead) return;

            currentHealth -= damage;

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                SetDead();
            }
        }

        public void Heal(int amount)
        {
            if (isDead) return;

            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        }

        /// <summary>
        /// Balance / integration tests (e.g. AutoHero): set max and current HP once the hero is constructed.
        /// If <paramref name="currentHp"/> is 0, the hero becomes dead. Does not resurrect if already dead.
        /// </summary>
        public void ApplyHealthOverrideForTesting(int maxHp, int currentHp)
        {
            if (isDead)
            {
                return;
            }

            maxHp = Mathf.Max(1, maxHp);
            maxHealth = maxHp;
            currentHealth = Mathf.Clamp(currentHp, 0, maxHealth);
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                SetDead();
            }
        }

        // -------------------------
        // AMMO LOGIC (safe)
        // -------------------------
        public bool HasAmmo()
        {
            return currentAmmo > 0;
        }

        public void ConsumeAmmo(int amount)
        {
            currentAmmo = Mathf.Max(0, currentAmmo - amount);
        }

        public void RefillAmmo()
        {
            currentAmmo = maxAmmo;
        }

        public void SetAmmoState(int weaponCurrentAmmo, int weaponCurrentReserveAmmo)
        {
            currentAmmo = Mathf.Clamp(weaponCurrentAmmo, 0, maxAmmo);
            currentReserveAmmo = Mathf.Clamp(weaponCurrentReserveAmmo, 0, maxReserveAmmo);
        }

        public void ConfigureWeaponState(
            HeroWeaponDefinition_V2 weaponDefinition,
            WeaponType weaponType,
            int weaponMaxAmmo,
            int weaponCurrentAmmo,
            int weaponMaxReserveAmmo,
            int weaponCurrentReserveAmmo,
            float weaponFireRate,
            float weaponReloadDuration)
        {
            currentWeaponDefinition = weaponDefinition;
            currentWeaponType = weaponType;
            maxAmmo = Mathf.Max(1, weaponMaxAmmo);
            currentAmmo = Mathf.Clamp(weaponCurrentAmmo, 0, maxAmmo);
            maxReserveAmmo = Mathf.Max(0, weaponMaxReserveAmmo);
            currentReserveAmmo = Mathf.Clamp(weaponCurrentReserveAmmo, 0, maxReserveAmmo);
            fireRate = Mathf.Max(0.01f, weaponFireRate);
            reloadDuration = Mathf.Max(0.01f, weaponReloadDuration);
        }
    }
}
