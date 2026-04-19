using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace iStick2War_V2
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
        public bool IsSwitchNextWeaponPressed { get; private set; }
        public bool IsSwitchPreviousWeaponPressed { get; private set; }
        public int DirectWeaponSlot { get; private set; } = -1;

        //FIXME ChatGPT:a detta:
        public float shootBufferTime; // Hur länge vi ska buffra shoot-input efter att ammo tagit, gör att shooting känns “responsive”
        public bool ShootBuffered; // Om shoot-input är buffrad

        // -------------------------
        // BOT (AutoHero_V2): injected before keyboard read when enabled
        // -------------------------
        private bool _botDriving;
        private Vector2 _botMove;
        private bool _botShootHeld;
        private bool _botReloadPressed;

        /// <summary>When true, <see cref="Tick"/> uses the last bot frame instead of Unity input.</summary>
        public void SetBotDriving(bool enabled)
        {
            _botDriving = enabled;
        }

        /// <summary>Call each frame before <see cref="Tick"/> while bot is active.</summary>
        public void SetBotFrame(Vector2 move, bool shootHeld, bool reloadPressed)
        {
            _botMove = move;
            _botShootHeld = shootHeld;
            _botReloadPressed = reloadPressed;
        }

        // -------------------------
        // UPDATE (called by Hero_V2 MonoBehaviour)
        // -------------------------
        public void Tick()
        {
            if (_botDriving)
            {
                MoveInput = _botMove.sqrMagnitude > 0.0001f ? _botMove.normalized : Vector2.zero;
                IsShootingPressed = false;
                IsShootingHeld = _botShootHeld;
                IsShootingReleased = false;
                IsReloadPressed = _botReloadPressed;
                IsSwitchNextWeaponPressed = false;
                IsSwitchPreviousWeaponPressed = false;
                DirectWeaponSlot = -1;
                IsJumpPressed = false;
                return;
            }

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
            IsSwitchPreviousWeaponPressed = Input.GetKeyDown(KeyCode.Q);
            IsSwitchNextWeaponPressed = Input.GetKeyDown(KeyCode.E);
            DirectWeaponSlot = ReadDirectWeaponSlot();
            IsJumpPressed =
                Input.GetButtonDown("Jump") ||
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.W);
        }

        private static int ReadDirectWeaponSlot()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha4)) return 3;
            if (Input.GetKeyDown(KeyCode.Alpha5)) return 4;
            if (Input.GetKeyDown(KeyCode.Alpha6)) return 5;
            if (Input.GetKeyDown(KeyCode.Alpha7)) return 6;
            if (Input.GetKeyDown(KeyCode.Alpha8)) return 7;
            if (Input.GetKeyDown(KeyCode.Alpha9)) return 8;
            return -1;
        }
    }
}
