using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    /*
 * INPUT SYSTEM PRINCIPLE (IMPORTANT)
 *
 * HeroInput_V2 is a PURE INPUT LAYER.
 *
 * ---------------------------------------------------------
 * FLOW:
 *
 * HeroInput_V2 → HeroController_V2 → HeroMovementSystem
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - Reads raw player input (buttons, axes, actions)
 * - Exposes current input state to the controller
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - No gameplay logic
 * - No state management
 * - No model modifications
 * - No movement execution
 * - No cooldown handling
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE:
 *
 * This system ONLY answers:
 * “What is the player currently pressing?”
 *
 * Nothing more.
 */
    internal class HeroInput_V2 : MonoBehaviour
{
        // -------------------------
        // RAW INPUT STATE
        // -------------------------
        public Vector2 MoveInput { get; private set; }

        public bool IsShootingPressed { get; private set; }
        public bool IsShootingHeld { get; private set; }
        public bool IsShootingReleased { get; private set; }

        public bool IsReloadPressed { get; private set; }
        public bool IsJumpPressed { get; private set; }

        //FIXME ChatGPT:a detta:
        public float shootBufferTime; // Hur länge vi ska buffra shoot-input efter att ammo tagit, gör att shooting känns “responsive”
        public bool ShootBuffered; // Om shoot-input är buffrad

        // -------------------------
        // UPDATE (called by Hero_V2 MonoBehaviour)
        // -------------------------
        public void Tick()
        {
            ReadMovement();
            ReadCombatInput();
        }

        // -------------------------
        // MOVEMENT
        // -------------------------
        private void ReadMovement()
        {
            float x = Input.GetAxisRaw("Horizontal");
            MoveInput = new Vector2(x, 0f).normalized;
        }

        // -------------------------
        // COMBAT
        // -------------------------
        private void ReadCombatInput()
        {
            IsShootingPressed = Input.GetButtonDown("Fire1");
            IsShootingHeld = Input.GetButton("Fire1");
            IsShootingReleased = Input.GetButtonUp("Fire1");

            IsReloadPressed = Input.GetKeyDown(KeyCode.R);
            IsJumpPressed =
                Input.GetButtonDown("Jump") ||
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.W);
        }
    }
}
