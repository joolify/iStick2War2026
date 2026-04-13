using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    /*
 * HeroMovementSystem_V2 (Movement Simulation Layer)
 *
 * RESPONSIBILITY:
 * HeroMovementSystem_V2 handles all movement simulation and control feel.
 * It is responsible for translating movement rules into physical motion.
 *
 * ---------------------------------------------------------
 * HANDLED SYSTEMS:
 *
 * - Velocity calculation
 * - Jumping
 * - Gravity
 * - Dash (future feature)
 * - Grounded state handling
 *
 * ---------------------------------------------------------
 * CORE CONCEPT:
 *
 * HeroMovementSystem_V2 = Physics + Control + Responsiveness
 *
 * It is NOT input, and NOT state logic.
 * It is purely movement execution.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Read input directly
 * - Modify state machine
 * - Play animations
 * - Make combat decisions
 *
 * ---------------------------------------------------------
 * ✅ RESPONSIBILITIES:
 *
 * - Apply movement forces / velocity
 * - Handle gravity and jump behaviour
 * - Implement dash and movement abilities
 * - Ensure responsive and satisfying movement feel
 *
 * ---------------------------------------------------------
 * DESIGN GOAL:
 *
 * The system should feel tight, responsive, and skill-based.
 *
 * Target inspiration:
 * - Celeste (precision movement feel)
 * - Hotline Miami (fast responsiveness)
 *
 * ---------------------------------------------------------
 * TODO / FUTURE IMPROVEMENTS:
 *
 * - AAA movement feel tuning
 * - Dash + slow motion system (game feel / juice)
 * - Improved grounding, slope handling, ledge interaction
 */
    public class HeroMovementSystem_V2
    {
        private HeroModel_V2 _model;

        private Vector2 velocity;
        private bool isDisabled;

        // Movement tuning
        private float moveSpeed = 6f;
        private float gravity = -25f;
        private float groundFriction = 12f;

        private bool isGrounded;

        public HeroMovementSystem_V2(HeroModel_V2 model)
        {
            _model = model;
        }

        // -------------------------
        // MAIN MOVE
        // -------------------------
        public void Move(Vector2 moveInput, float deltaTime)
        {
            if (isDisabled) return;

            HandleHorizontalMovement(moveInput, deltaTime);
            HandleGravity(deltaTime);
            ApplyVelocity(deltaTime);
        }

        // -------------------------
        // HORIZONTAL MOVEMENT
        // -------------------------
        private void HandleHorizontalMovement(Vector2 input, float deltaTime)
        {
            Vector2 targetVelocity = new Vector2(input.x * moveSpeed, velocity.y);

            // Smooth acceleration (feel)
            velocity.x = Mathf.Lerp(velocity.x, targetVelocity.x, 12f * deltaTime);

            _model.velocity = velocity;
        }

        // -------------------------
        // GRAVITY
        // -------------------------
        private void HandleGravity(float deltaTime)
        {
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // keep grounded "stick feel"
            }
            else
            {
                velocity.y += gravity * deltaTime;
            }
        }

        // -------------------------
        // APPLY MOVEMENT
        // -------------------------
        private void ApplyVelocity(float deltaTime)
        {
            _model.velocity = velocity;

            // IMPORTANT:
            // här kopplar du senare till Rigidbody2D eller CharacterController
            // just nu bara data-layer
        }

        // -------------------------
        // STOP (instant brake)
        // -------------------------
        public void Stop()
        {
            velocity.x = Mathf.Lerp(velocity.x, 0, groundFriction * Time.deltaTime);
            _model.velocity = velocity;
        }

        // -------------------------
        // DISABLE
        // -------------------------
        public void Disable()
        {
            isDisabled = true;
            velocity = Vector2.zero;
            _model.velocity = Vector2.zero;
        }

        // -------------------------
        // OPTIONAL: grounded setter (from raycast / collision system)
        // -------------------------
        public void SetGrounded(bool grounded)
        {
            isGrounded = grounded;
        }
    }
}
