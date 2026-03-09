using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 3.5f;
    public float height = 1.6f;
    public float damping = 5.0f;
    public float rotationSpeed = 0.2f; // Reduced from 2.0 to 0.2 for smoother control
    
    private float _currentX = 0.0f;
    private float _currentY = 0.0f;
    
    // Limits for vertical rotation
    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    // Input System
    private InputAction lookAction;

    void Awake()
    {
        // Setup Look Action
        lookAction = new InputAction("Look", binding: "<Mouse>/delta");
        lookAction.AddBinding("<Gamepad>/rightStick");
        lookAction.AddBinding("<Pointer>/delta"); // Catch-all for touch/pen/mouse
    }

    void OnEnable() => lookAction.Enable();
    void OnDisable() => lookAction.Disable();

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        _currentX = angles.y;
        _currentY = angles.x;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Read Input
        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        
        // Scale input if it's from mouse vs gamepad vs touch
        // Mouse delta is usually large (pixels), Gamepad is normalized (-1 to 1).
        // We might need to differentiate or just tune sensitivity.
        // For simplicity, let's just use rotationSpeed as a multiplier.
        // If it's a normalized vector (Gamepad), it might feel slow compared to mouse.
        // Let's check magnitude.
        
        // Simple heuristic: if magnitude is small (< 1.5), treat as normalized input and boost speed
        float sensitivityMultiplier = 1.0f;
        if (lookInput.magnitude < 1.5f && lookInput.magnitude > 0.001f) 
        {
             sensitivityMultiplier = 5.0f; // Boost for Gamepad/Joystick
        }

        _currentX += lookInput.x * rotationSpeed * sensitivityMultiplier;
        _currentY -= lookInput.y * rotationSpeed * sensitivityMultiplier;

        _currentY = Mathf.Clamp(_currentY, yMinLimit, yMaxLimit);

        // Calculate Rotation
        Quaternion rotation = Quaternion.Euler(_currentY, _currentX, 0);

        // Calculate Position
        // Look at target's head/chest (offset up)
        Vector3 targetPos = target.position + Vector3.up * height;
        Vector3 position = targetPos - (rotation * Vector3.forward * distance);

        // Apply
        transform.rotation = rotation;
        transform.position = position;
        
        // Optional: Camera Collision (Simple)
        if (Physics.Linecast(targetPos, position, out RaycastHit hit))
        {
            // If hit something that is not the player, pull camera closer
            if (hit.collider.transform.root != target.root && !hit.collider.isTrigger)
            {
                transform.position = hit.point + hit.normal * 0.2f;
            }
        }
    }
}
