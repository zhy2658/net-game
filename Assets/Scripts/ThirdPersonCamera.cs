using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 3.5f;
    public float height = 1.6f;
    public float mouseSensitivity = 0.15f;
    public float gamepadSensitivity = 120f;

    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    private float _currentX;
    private float _currentY;

    public float Yaw => _currentX;

    private InputAction _mouseLookAction;
    private InputAction _gamepadLookAction;

    void Awake()
    {
        _mouseLookAction = new InputAction("MouseLook", binding: "<Mouse>/delta");
        _gamepadLookAction = new InputAction("GamepadLook", binding: "<Gamepad>/rightStick");
    }

    void OnEnable()
    {
        _mouseLookAction.Enable();
        _gamepadLookAction.Enable();
    }

    void OnDisable()
    {
        _mouseLookAction.Disable();
        _gamepadLookAction.Disable();
    }

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        _currentX = angles.y;
        _currentY = angles.x;
    }

    void Update()
    {
        Vector2 mouseDelta = _mouseLookAction.ReadValue<Vector2>();
        Vector2 gamepadDelta = _gamepadLookAction.ReadValue<Vector2>();

        _currentX += mouseDelta.x * mouseSensitivity + gamepadDelta.x * gamepadSensitivity * Time.deltaTime;
        _currentY -= mouseDelta.y * mouseSensitivity + gamepadDelta.y * gamepadSensitivity * Time.deltaTime;
        _currentY = Mathf.Clamp(_currentY, yMinLimit, yMaxLimit);
    }

    void LateUpdate()
    {
        if (target == null) return;

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
