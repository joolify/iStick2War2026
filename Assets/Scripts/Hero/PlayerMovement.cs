using Spine.Unity;
using UnityEngine;

namespace iStick2War
{
    public class PlayerMovement : MonoBehaviour
    {
        public float moveSpeed = 10f;
        public float jumpForce = 40f;

        public Transform groundCheck;
        public float groundCheckRadius = 0.2f;
        public LayerMask groundLayer;

        private Rigidbody2D rigidBody2D;
        private float xVelocity;
        private bool isGrounded;

        private SkeletonAnimation skeletonAnimation;

        void Start()
        {
            rigidBody2D = GetComponent<Rigidbody2D>();
        }

        void Update()
        {
            xVelocity = Input.GetAxisRaw("Horizontal");

            //Debug.Log("speed: " + xVelocity);

            // Jump
            var isJumping = Input.GetButtonDown("Jump") && isGrounded;
            if (isJumping)
            {
                rigidBody2D.linearVelocity = new Vector2(rigidBody2D.linearVelocity.x, jumpForce);
            }

            //animator.SetBool("isJumping", !isGrounded);
            //Debug.Log("PlayerMovement.isGrounded: " + isGrounded);

            //Debug.Log("yVelocity: " + rigidBody2D.linearVelocity.y);

            //Debug.Log("PlayerMovement.Jump: " + Input.GetButtonDown("Jump"));
        }

        void FixedUpdate()
        {
            // Smooth horizontal movement
            rigidBody2D.linearVelocity = new Vector2(xVelocity * moveSpeed, rigidBody2D.linearVelocity.y);

            var speed = Mathf.Abs(xVelocity);
        }

        void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
            }
        }

        void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                isGrounded = true;
            }
        }

        void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                isGrounded = false;
            }
        }
    }
}

//public float runSpeed = 8f;
//public float jumpForce = 12f;

//public Transform groundCheck;
//public float groundCheckRadius = 0.2f;
//public LayerMask groundLayer;

//private float horizontalMove = 0f;
//private bool jump = false;
//private bool isGrounded;

//private Rigidbody2D rb;

//void Start()
//{
//    rb = GetComponent<Rigidbody2D>();
//}

//void Update()
//{
//    horizontalMove = Input.GetAxisRaw("Horizontal");

//    // Check if grounded
//    isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

//    if (Input.GetButtonDown("Jump") && isGrounded)
//    {
//        Debug.Log("JUMP PRESSED");
//        jump = true;
//    }
//}

//void FixedUpdate()
//{
//    // Horizontal movement
//    rb.linearVelocity = new Vector2(horizontalMove * runSpeed, rb.linearVelocity.y);

//    // Jump (ONLY once)
//    if (jump)
//    {
//        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
//        jump = false;
//    }
//}