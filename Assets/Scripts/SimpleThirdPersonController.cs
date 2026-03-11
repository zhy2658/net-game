using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleThirdPersonController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public float rotationSpeed = 0.1f;
    public float speedSmoothTime = 0.1f;
    public float gravity = 20f;
    public float jumpForce = 1.5f;

    [Header("Ground Detection")]
    public float groundCheckOffset = 0.1f;
    public float groundCheckRadius = 0.25f;
    public LayerMask groundLayers = 1;

    private CharacterController _characterController;
    private Animator _animator;
    private Transform _cameraTransform;
    private ThirdPersonCamera _cameraScript;

    private InputActionMap inputMap;
    private InputAction moveAction;
    private InputAction runAction;
    private InputAction jumpAction;
    private InputAction attackAction;

    private float _verticalVelocity;
    private float _currentSpeed;
    private float _speedVelocity;
    private float _targetRotation;
    private float _rotationVelocity;
    private bool _isGrounded;

    private NanoKcpClient _kcpClient;
    private float _lastSyncTime;

    void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        if (_animator != null) _animator.applyRootMotion = false;

        _cameraScript = FindFirstObjectByType<ThirdPersonCamera>();
        if (_cameraScript != null)
            _cameraTransform = _cameraScript.transform;
        else if (Camera.main != null)
            _cameraTransform = Camera.main.transform;
        _kcpClient = FindFirstObjectByType<NanoKcpClient>();

        inputMap = new InputActionMap("ThirdPersonController");

        moveAction = inputMap.AddAction("Move", binding: "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        runAction = inputMap.AddAction("Run", binding: "<Keyboard>/leftShift");
        runAction.AddBinding("<Gamepad>/leftStickPress");

        jumpAction = inputMap.AddAction("Jump", binding: "<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        attackAction = inputMap.AddAction("Attack", binding: "<Mouse>/leftButton");
        attackAction.AddBinding("<Gamepad>/buttonWest");
    }

    void Start()
    {
        if (Time.timeScale == 0) Time.timeScale = 1f;
        if (_kcpClient == null) _kcpClient = FindFirstObjectByType<NanoKcpClient>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        inputMap.Enable();

        if (_characterController != null) _characterController.enabled = false;
        transform.position = GameConstants.SafeSpawnPos;
        if (_characterController != null) _characterController.enabled = true;

        if (_cameraScript != null)
        {
            _cameraScript.target = transform;
        }
        else if (Camera.main != null)
        {
            var camScript = Camera.main.GetComponent<ThirdPersonCamera>();
            if (camScript != null) camScript.target = transform;
        }
    }

    void OnEnable() { }
    void OnDisable() => inputMap.Disable();
    void OnDestroy() => inputMap?.Dispose();

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        if (_cameraScript == null)
        {
            _cameraScript = FindFirstObjectByType<ThirdPersonCamera>();
            if (_cameraScript != null)
            {
                _cameraTransform = _cameraScript.transform;
                if (_cameraScript.target == null)
                    _cameraScript.target = transform;
            }
            else if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        GroundCheck();

        Vector2 moveInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        bool isRunning = runAction?.IsPressed() ?? false;
        bool jumpPressed = jumpAction?.WasPressedThisFrame() ?? false;

        float inputMag = moveInput.magnitude;
        float targetSpeed = inputMag > 0.1f ? (isRunning ? runSpeed : moveSpeed) : 0f;
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, speedSmoothTime);

        Vector3 move = Vector3.zero;
        if (inputMag > 0.1f && _cameraTransform != null)
        {
            float camYaw = _cameraScript != null ? _cameraScript.Yaw : _cameraTransform.eulerAngles.y;
            Vector3 camForward = Quaternion.Euler(0f, camYaw, 0f) * Vector3.forward;
            Vector3 camRight = Quaternion.Euler(0f, camYaw, 0f) * Vector3.right;
            Vector3 direction = (camForward * moveInput.y + camRight * moveInput.x).normalized;

            if (direction.sqrMagnitude > 0.001f)
            {
                _targetRotation = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, rotationSpeed);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                move = direction * _currentSpeed;
            }
        }

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            float normalizedSpeed = _currentSpeed / runSpeed;
            if (AnimatorUtils.HasParameter("Speed", _animator))
                _animator.SetFloat("Speed", normalizedSpeed);
            if (AnimatorUtils.HasParameter("isWalk", _animator))
                _animator.SetBool("isWalk", _currentSpeed > 0.1f);
        }

        if (_isGrounded)
        {
            if (_verticalVelocity < 0f) _verticalVelocity = -2f;
            if (jumpPressed)
            {
                _verticalVelocity = Mathf.Sqrt(jumpForce * 2f * gravity);
                if (_animator != null && AnimatorUtils.HasParameter("Jump", _animator))
                    _animator.SetTrigger("Jump");
            }
        }
        else
        {
            _verticalVelocity -= gravity * Time.deltaTime;
        }

        move.y = _verticalVelocity;
        if (_characterController != null && _characterController.enabled)
            _characterController.Move(move * Time.deltaTime);

        if (attackAction != null && attackAction.WasPressedThisFrame())
        {
            CastSkill(1001);
        }

        SyncToServer();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        HandleDebugInput();
#endif
    }

    private void SyncToServer()
    {
        if (_kcpClient == null || !_kcpClient.IsConnected) return;
        if (Time.time - _lastSyncTime < GameConstants.SyncInterval) return;

        _lastSyncTime = Time.time;
        _kcpClient.SendNotify("room.move", new Protocol.MoveRequest
        {
            Position = new Protocol.Vector3 { X = transform.position.x, Y = transform.position.y, Z = transform.position.z },
            Rotation = new Protocol.Quaternion { X = transform.rotation.x, Y = transform.rotation.y, Z = transform.rotation.z, W = transform.rotation.w },
            Speed = Mathf.Clamp01(_currentSpeed / runSpeed),
            IsGrounded = _isGrounded,
            ClientTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    private void CastSkill(int skillId)
    {
        if (_animator != null) _animator.SetTrigger("Attack");
        if (_kcpClient == null || !_kcpClient.IsConnected) return;

        var req = new Protocol.CastSkillRequest
        {
            SkillInfo = new Protocol.SkillInfo
            {
                SkillId = skillId,
                Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Direction = new Protocol.Vector3 { X = transform.forward.x, Y = transform.forward.y, Z = transform.forward.z }
            }
        };
        _kcpClient.SendRequest("room.castSkill", req);
    }

    private void GroundCheck()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        bool rayCheck = false;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 0.2f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.root != transform)
                rayCheck = true;
        }

        _isGrounded = (rayCheck || _characterController.isGrounded) && _verticalVelocity <= 0.1f;
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void HandleDebugInput()
    {
        if (Keyboard.current.kKey.isPressed)
            transform.position += Vector3.up * 5f * Time.deltaTime;
        if (Keyboard.current.digit8Key.wasPressedThisFrame && _animator != null)
            _animator.SetTrigger("Dead");
        if (Keyboard.current.digit9Key.wasPressedThisFrame && _animator != null)
            _animator.SetTrigger("Dance");
    }

    private void OnGUI()
    {
        GUI.color = Color.red;
        GUILayout.BeginArea(new Rect(10, 200, 250, 80));
        GUILayout.Label($"Pos: {transform.position:F1}  Grounded: {_isGrounded}");
        GUILayout.Label("K=fly  8=dead  9=dance");
        GUILayout.EndArea();
    }
#endif
}
