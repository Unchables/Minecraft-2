using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraFly : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Speed for forward, backward, left, and right movement.")]
    public float moveSpeed = 5.0f;
    [Tooltip("Speed for up and down movement.")]
    public float flySpeed = 3.0f;

    [Header("Look Settings")]
    [Tooltip("Sensitivity for mouse/gamepad look.")]
    public float lookSensitivity = 0.2f;

    // Private state for rotation
    private Vector2 rotation;

    private InputSystem inputSystem;
    
    void Awake()
    {
        // --- Lock and Hide Cursor ---
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
    }

    void OnEnable()
    {
        inputSystem = new  InputSystem();
        inputSystem.Enable();
    }

    void OnDisable()
    {
        inputSystem.Disable();
    }

    // Update is called once per frame
    void Update()
    {
        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        // Read the 2D move vector and the 1D fly float
        Vector2 moveInput = inputSystem.Player.Move.ReadValue<Vector2>();
        float jumpInput = inputSystem.Player.Jump.ReadValue<float>();
        float crouchInput = inputSystem.Player.Crouch.ReadValue<float>();

        float flyInput = 0;
        flyInput += jumpInput;
        flyInput -= crouchInput;

        // Create the movement vector from input
        // moveInput.y is forward/backward, moveInput.x is left/right
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);

        // Apply forward/strafe movement relative to the camera's orientation
        Vector3 worldRelativeMove = transform.TransformDirection(moveDirection);
        
        // The final velocity vector
        Vector3 velocity = (worldRelativeMove * moveSpeed) + (Vector3.up * flyInput * flySpeed);

        // Apply the movement
        transform.position += velocity * Time.deltaTime;
    }

    private void HandleRotation()
    {
        // Read the 2D look vector
        Vector2 lookInput = inputSystem.Player.Look.ReadValue<Vector2>();

        // Apply sensitivity
        lookInput *= lookSensitivity;

        // Add the input to our rotation state.
        // Horizontal look (yaw) is accumulated on the Y-axis.
        // Vertical look (pitch) is accumulated on the X-axis.
        rotation.y += lookInput.x;
        rotation.x -= lookInput.y; // Invert Y to match standard mouse controls

        // Clamp the vertical rotation (pitch) to prevent flipping upside down
        rotation.x = Mathf.Clamp(rotation.x, -90f, 90f);

        // Apply the final rotation to the transform
        transform.localRotation = Quaternion.Euler(rotation.x, rotation.y, 0f);
    }
}