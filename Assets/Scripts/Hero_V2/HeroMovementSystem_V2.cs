using System;
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
        private const bool DebugMovementLogs = false;

        private HeroModel_V2 _model;
        private readonly Transform _transform;
        private readonly Rigidbody2D _rigidbody2D;
        private readonly Collider2D _collider2D;

        private Vector2 velocity;
        private bool isDisabled;

        // Movement tuning
        private float moveSpeed = 6f;
        private float jumpForce = 5.5f;
        private float gravity = -25f;
        private float groundFriction = 12f;

        private bool isGrounded;
        private const float GroundCheckDistance = 0.2f;
        private float _jumpGracePeriodTimer;
        public float JumpGracePeriod = 0.12f;
        public float FallThresholdVelocity = -0.1f;

        public HeroMovementSystem_V2(HeroModel_V2 model)
        {
            _model = model;
            _transform = model.transform;
            _rigidbody2D = model.GetComponent<Rigidbody2D>();
            _collider2D = model.GetComponent<Collider2D>();
        }

        // -------------------------
        // MAIN MOVE
        // -------------------------
        public void Move(Vector2 moveInput, float deltaTime)
        {
            if (isDisabled) return;

            if (_jumpGracePeriodTimer > 0f)
            {
                _jumpGracePeriodTimer = Mathf.Max(0f, _jumpGracePeriodTimer - deltaTime);
            }

            RefreshGroundedState();
            if (_rigidbody2D != null)
            {
                // Keep local velocity in sync with physics so gravity/falling are preserved.
                velocity = _rigidbody2D.linearVelocity;
            }
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

        public bool CanJump()
        {
            RefreshGroundedState();
            bool canJump = !isDisabled && isGrounded;
            LogMovement($"[HeroMovementSystem_V2] CanJump? {canJump} (isDisabled={isDisabled}, isGrounded={isGrounded})");
            return canJump;
        }

        public void Jump()
        {
            if (!CanJump())
            {
                LogMovement("[HeroMovementSystem_V2] Jump blocked by CanJump().");
                return;
            }

            LogMovement($"[HeroMovementSystem_V2] Jump triggered. jumpForce={jumpForce}");
            velocity.y = jumpForce;
            isGrounded = false;
            _jumpGracePeriodTimer = JumpGracePeriod;

            if (_rigidbody2D != null)
            {
                Vector2 rbVelocity = _rigidbody2D.linearVelocity;
                rbVelocity.y = jumpForce;
                _rigidbody2D.linearVelocity = rbVelocity;
                velocity = rbVelocity;
            }

            _model.velocity = velocity;
        }

        // -------------------------
        // GRAVITY
        // -------------------------
        private void HandleGravity(float deltaTime)
        {
            if (_rigidbody2D != null)
            {
                return;
            }

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
            if (_rigidbody2D != null)
            {
                Vector2 rbVelocity = _rigidbody2D.linearVelocity;
                rbVelocity.x = velocity.x;
                _rigidbody2D.linearVelocity = rbVelocity;
                velocity = rbVelocity;
                _model.velocity = _rigidbody2D.linearVelocity;
                return;
            }

            _transform.position += (Vector3)(velocity * deltaTime);
            _model.velocity = velocity;
        }

        // -------------------------
        // STOP (instant brake)
        // -------------------------
        public void Stop()
        {
            if (_rigidbody2D != null)
            {
                Vector2 rbVelocity = _rigidbody2D.linearVelocity;
                rbVelocity.x = Mathf.Lerp(rbVelocity.x, 0f, groundFriction * Time.deltaTime);
                _rigidbody2D.linearVelocity = rbVelocity;
                velocity = rbVelocity;
                _model.velocity = rbVelocity;
                return;
            }

            velocity.x = Mathf.Lerp(velocity.x, 0f, groundFriction * Time.deltaTime);
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

        public bool IsGrounded()
        {
            RefreshGroundedState();
            return isGrounded;
        }

        private void RefreshGroundedState()
        {
            if (_collider2D == null)
            {
                Debug.LogWarning("[HeroMovementSystem_V2] RefreshGroundedState: Collider2D missing.");
                return;
            }

            Bounds bounds = _collider2D.bounds;
            float halfWidth = bounds.extents.x * 0.9f;
            float rayStartY = bounds.min.y + 0.02f;

            Vector2 centerOrigin = new Vector2(bounds.center.x, rayStartY);
            Vector2 leftOrigin = new Vector2(bounds.center.x - halfWidth, rayStartY);
            Vector2 rightOrigin = new Vector2(bounds.center.x + halfWidth, rayStartY);

            RaycastHit2D centerHit = GetFirstValidGroundHit(centerOrigin);
            RaycastHit2D leftHit = GetFirstValidGroundHit(leftOrigin);
            RaycastHit2D rightHit = GetFirstValidGroundHit(rightOrigin);

            bool centerGrounded = centerHit.collider != null;
            bool leftGrounded = leftHit.collider != null;
            bool rightGrounded = rightHit.collider != null;
            bool potentiallyGrounded = centerGrounded || leftGrounded || rightGrounded;

            if (_jumpGracePeriodTimer > 0f)
            {
                bool isFalling = velocity.y <= FallThresholdVelocity;
                if (isFalling && potentiallyGrounded)
                {
                    isGrounded = true;
                    _jumpGracePeriodTimer = 0f;
                    LogMovement("[HeroMovementSystem_V2] Early landing detected during jump grace period.");
                }
                else
                {
                    isGrounded = false;
                }
            }
            else
            {
                isGrounded = potentiallyGrounded;
            }

            string centerName = centerHit.collider != null ? centerHit.collider.name : "null";
            string leftName = leftHit.collider != null ? leftHit.collider.name : "null";
            string rightName = rightHit.collider != null ? rightHit.collider.name : "null";
            LogMovement($"[HeroMovementSystem_V2] GroundCheck => grounded={isGrounded}, left={leftGrounded}({leftName}), center={centerGrounded}({centerName}), right={rightGrounded}({rightName}), grace={_jumpGracePeriodTimer:0.000}, vy={velocity.y:0.000}");
        }

        private RaycastHit2D GetFirstValidGroundHit(Vector2 origin)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, GroundCheckDistance);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider == _collider2D || hitCollider.isTrigger)
                {
                    continue;
                }

                return hits[i];
            }

            return default;
        }

        private static void LogMovement(string message)
        {
            if (DebugMovementLogs)
            {
                Debug.Log(message);
            }
        }
    }
}
