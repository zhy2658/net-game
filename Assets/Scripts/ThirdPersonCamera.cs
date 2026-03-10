using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 3.5f;
    public float height = 1.6f;
    public float rotationSpeed = 0.2f;

    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    private float _currentX;
    private float _currentY;
    private InputAction lookAction;

    void Awake()
    {
        lookAction = new InputAction("Look", binding: "<Mouse>/delta");
        lookAction.AddBinding("<Gamepad>/rightStick");
        lookAction.AddBinding("<Pointer>/delta");
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

        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        // Boost sensitivity for normalized gamepad input
        float sensitivity = lookInput.magnitude < 1.5f && lookInput.magnitude > 0.001f ? 5f : 1f;

        _currentX += lookInput.x * rotationSpeed * sensitivity;
        _currentY -= lookInput.y * rotationSpeed * sensitivity;
        _currentY = Mathf.Clamp(_currentY, yMinLimit, yMaxLimit);

        Quaternion rotation = Quaternion.Euler(_currentY, _currentX, 0);
        Vector3 targetPos = target.position + Vector3.up * height;
        Vector3 position = targetPos - rotation * Vector3.forward * distance;

        transform.rotation = rotation;
        transform.position = position;

        if (Physics.Linecast(targetPos, position, out RaycastHit hit))
        {
            if (hit.collider.transform.root != target.root && !hit.collider.isTrigger)
                transform.position = hit.point + hit.normal * 0.2f;
        }
    }
}
