using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
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
    internal class HeroModel_V2 : MonoBehaviour
    {
        // -------------------------
        // INPUT (read by controller)
        // -------------------------
        public Vector2 moveInput { get; internal set; }
        public bool isShootingPressed { get; internal set; }
        public bool isReloadPressed { get; internal set; }

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
        public float fireRate { get; private set; } = 0.1f;

        // -------------------------
        // TIMERS (for gameplay feel)
        // -------------------------
        public float lastShootTime { get; internal set; }
        public float reloadDuration { get; private set; } = 1.5f;
        public float reloadTimer { get; internal set; }

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
    }
}
