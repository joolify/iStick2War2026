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
 * HeroInput_V2 → HeroController_V2 → HeroMovementSystem_V2
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

        // Shoot input buffer after ammo hits zero so firing still feels responsive for a short window.
        public float shootBufferTime; // Hur länge vi ska buffra shoot-input efter att ammo tagit, gör att shooting känns “responsive”
        public bool ShootBuffered; // Om shoot-input är buffrad

        // -------------------------
        // BOT (AutoHero_V2): injected before keyboard read when enabled
        // -------------------------
        private bool _botDriving;
        private Vector2 _botMove;
        private bool _botShootHeld;
        private bool _botReloadPressed;
        private bool _manualReloadFromUi;
        private bool _manualSwitchNextWeaponFromUi;
        private bool _manualSwitchPreviousWeaponFromUi;

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

        // Mobile reload UI can fire after HeroInput_V2.Tick in the same frame; keep a one-frame latch.
        public void RequestManualReloadFromUi()
        {
            _manualReloadFromUi = true;
        }

        public void RequestSwitchNextWeaponFromUi()
        {
            _manualSwitchNextWeaponFromUi = true;
        }

        public void RequestSwitchPreviousWeaponFromUi()
        {
            _manualSwitchPreviousWeaponFromUi = true;
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
            }
            else if (GamePlatform_V2.UseMobileGameplayRules)
            {
                ReadMobileInput();
            }
            else
            {
                ReadMovement();
                ReadCombatInput();
            }

            ApplyQueuedWeaponSwitchButtons();
            ApplyQueuedReloadButton();
            ApplyManualReloadFromUi();
            ApplyManualWeaponSwitchFromUi();
        }

        private void ApplyQueuedWeaponSwitchButtons()
        {
            MobileGameplayTouchInput_V2 touch = MobileGameplayTouchInput_V2.Instance;
            if (touch == null)
            {
                return;
            }

            if (touch.ConsumeSwitchNextWeapon())
            {
                IsSwitchNextWeaponPressed = true;
            }

            if (touch.ConsumeSwitchPreviousWeapon())
            {
                IsSwitchPreviousWeaponPressed = true;
            }
        }

        private void ApplyQueuedReloadButton()
        {
            MobileGameplayTouchInput_V2 touch = MobileGameplayTouchInput_V2.Instance;
            if (touch == null)
            {
                return;
            }

            if (touch.ConsumeReload())
            {
                IsReloadPressed = true;
            }
        }

        private void ApplyManualReloadFromUi()
        {
            if (!_manualReloadFromUi)
            {
                return;
            }

            IsReloadPressed = true;
            _manualReloadFromUi = false;
        }

        private void ApplyManualWeaponSwitchFromUi()
        {
            if (_manualSwitchNextWeaponFromUi)
            {
                IsSwitchNextWeaponPressed = true;
                _manualSwitchNextWeaponFromUi = false;
            }

            if (_manualSwitchPreviousWeaponFromUi)
            {
                IsSwitchPreviousWeaponPressed = true;
                _manualSwitchPreviousWeaponFromUi = false;
            }
        }

        private void ReadMobileInput()
        {
            MoveInput = Vector2.zero;
            IsJumpPressed = false;
            IsReloadPressed = false;
            DirectWeaponSlot = -1;
            IsSwitchNextWeaponPressed = false;
            IsSwitchPreviousWeaponPressed = false;

            MobileGameplayTouchInput_V2 touch = MobileGameplayTouchInput_V2.Instance;
            if (touch == null)
            {
                IsShootingPressed = false;
                IsShootingHeld = false;
                IsShootingReleased = false;
                IsSwitchNextWeaponPressed = false;
                IsSwitchPreviousWeaponPressed = false;
                return;
            }

            IsShootingPressed = touch.CombatTouchBeganThisFrame;
            IsShootingHeld = touch.CombatTouchHeld;
            IsShootingReleased = touch.CombatTouchEndedThisFrame;
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
