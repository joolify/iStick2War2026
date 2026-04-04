using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 8f;
    public float jumpForce = 12f;

    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private float moveHorizontal;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        moveHorizontal = Input.GetAxisRaw("Horizontal");

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
        Debug.Log("PlayerMovement.isGrounded: " + isGrounded);

        Debug.Log("PlayerMovement.Jump: " + Input.GetButtonDown("Jump"));
    }

    void FixedUpdate()
    {
        // Smooth horizontal movement
        rb.linearVelocity = new Vector2(moveHorizontal * moveSpeed, rb.linearVelocity.y);
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