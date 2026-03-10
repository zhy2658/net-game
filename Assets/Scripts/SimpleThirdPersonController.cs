using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleThirdPersonController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public float rotationSpeed = 0.12f;
    public float speedSmoothTime = 0.1f;
    public float gravity = 20f;
    public float jumpForce = 1.5f;
    
    [Header("Ground Detection")]
    public float groundCheckOffset = 0.1f;
    public float groundCheckRadius = 0.25f;
    public LayerMask groundLayers = 1; // Default layer

    private CharacterController _characterController;
    private Animator _animator;
    private Transform _cameraTransform;
    
    // Input System
    private InputActionMap inputMap;
    private InputAction moveAction;
    private InputAction runAction;
    private InputAction jumpAction;

    private float _verticalVelocity;
    private float _currentSpeed;
    private float _speedVelocity;
    private float _targetRotation;
    private float _rotationVelocity;
    private bool _isGrounded;

    // Network Sync
    private NanoKcpClient _kcpClient;
    private float _lastSyncTime;

    void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        if (_animator != null) _animator.applyRootMotion = false; // Disable Root Motion
        
        // Find main camera
        if (Camera.main != null) _cameraTransform = Camera.main.transform;
        
        // Find KCP Client
        _kcpClient = FindFirstObjectByType<NanoKcpClient>();

        // Initialize Input System
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
    }

    void Start()
    {
        if (Time.timeScale == 0)
        {
            Debug.LogWarning("Time.timeScale was 0! Forcing to 1.");
            Time.timeScale = 1f;
        }
        
        if (_kcpClient == null) _kcpClient = FindFirstObjectByType<NanoKcpClient>();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        inputMap.Enable();

        Vector3 safePos = GameConstants.SafeSpawnPos;
        if (_characterController != null) _characterController.enabled = false;
        transform.position = safePos;
        if (_characterController != null) _characterController.enabled = true;

        if (Camera.main != null)
        {
            var camScript = Camera.main.GetComponent<ThirdPersonCamera>();
            if (camScript != null) camScript.target = transform;
        }
    }

    void OnEnable() 
    {
        // Don't enable input here, rely on OnNetworkSpawn for networked objects
    }
    
    void OnDisable() => inputMap.Disable();
    void OnDestroy() => inputMap?.Dispose();

    void Update()
    {
        try
        {
            if (_cameraTransform == null && Camera.main != null)
            {
                 _cameraTransform = Camera.main.transform;
                 
                 // Ensure Camera Script follows this player
                 var camScript = Camera.main.GetComponent<ThirdPersonCamera>();
                 if (camScript != null && camScript.target == null)
                 {
                     camScript.target = transform;
                     Debug.Log($"Camera target set to local player: {name}");
                 }
            }

            // Custom Ground Check (More reliable than CharacterController.isGrounded)
            GroundCheck();

            Vector2 moveInput = Vector2.zero;
            if (moveAction != null) moveInput = moveAction.ReadValue<Vector2>();
            
            // Only try fallback if NOT using New Input System exclusively
            // Check if Input System is active via simple check (avoid crash)
            #if !ENABLE_INPUT_SYSTEM
            if (moveInput == Vector2.zero)
            {
                float h = Input.GetAxis("Horizontal");
                float v = Input.GetAxis("Vertical");
                if (h != 0 || v != 0) moveInput = new Vector2(h, v);
            }
            #endif

            bool isRunning = false;
            bool jumpPressed = false;

            #if ENABLE_INPUT_SYSTEM
            if (runAction != null) isRunning = runAction.IsPressed();
            if (jumpAction != null) jumpPressed = jumpAction.WasPressedThisFrame();
            #else
            isRunning = Input.GetKey(KeyCode.LeftShift);
            jumpPressed = Input.GetKeyDown(KeyCode.Space);
            #endif
            
            // 1. Movement
            float targetSpeed = moveInput.magnitude > 0.1f ? (isRunning ? runSpeed : moveSpeed) : 0f;
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, speedSmoothTime);

            Vector3 move = Vector3.zero;

            if (moveInput.sqrMagnitude >= 0.01f && _cameraTransform != null)
            {
                Vector3 camForward = Vector3.Scale(_cameraTransform.forward, new Vector3(1, 0, 1)).normalized;
                Vector3 camRight = Vector3.Scale(_cameraTransform.right, new Vector3(1, 0, 1)).normalized;
                Vector3 direction = (camForward * moveInput.y + camRight * moveInput.x).normalized;

                if (direction != Vector3.zero)
                {
                    _targetRotation = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, rotationSpeed);
                    transform.rotation = Quaternion.Euler(0f, angle, 0f);
                    move = Quaternion.Euler(0f, _targetRotation, 0f) * Vector3.forward;
                }
            }

            move = move.normalized * _currentSpeed;

            // 2. Animation
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                // Check which parameters exist to be safe
                bool hasSpeedParam = AnimatorUtils.HasParameter("Speed", _animator);
                bool hasIsWalkParam = AnimatorUtils.HasParameter("isWalk", _animator);
                
                if (hasSpeedParam)
                {
                    // Set Speed (0 to 1) based on current speed relative to max run speed
                    float normalizedSpeed = _currentSpeed / runSpeed;
                    _animator.SetFloat("Speed", normalizedSpeed);
                }
                
                if (hasIsWalkParam)
                {
                    // Fallback/Legacy support
                    _animator.SetBool("isWalk", _currentSpeed > 0.1f);
                }
            }

            // 3. Gravity & Jump
            if (_isGrounded)
            {
                // Stop falling
                if (_verticalVelocity < 0.0f) _verticalVelocity = -2f;
                
                if (jumpPressed)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpForce * 2f * gravity);
                    
                    if (_animator != null && AnimatorUtils.HasParameter("Jump", _animator))
                    {
                        _animator.SetTrigger("Jump");
                    }
                }
            }
            else
            {
                // Apply Gravity
                _verticalVelocity -= gravity * Time.deltaTime;
            }

            move.y = _verticalVelocity;

            if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(move * Time.deltaTime);
            }
            
            // Sync to Server
            if (_kcpClient != null && _kcpClient.IsConnected && Time.time - _lastSyncTime > GameConstants.SyncInterval)
            {
                _lastSyncTime = Time.time;
                var moveMsg = new Protocol.MoveRequest
                {
                    Position = new Protocol.Vector3 { X = transform.position.x, Y = transform.position.y, Z = transform.position.z },
                    Rotation = new Protocol.Quaternion { X = transform.rotation.x, Y = transform.rotation.y, Z = transform.rotation.z, W = transform.rotation.w }
                };
                // Use Notify for movement to reduce overhead (no response needed)
                _kcpClient.SendNotify("room.move", moveMsg);
                // if (Time.frameCount % 60 == 0) Debug.Log($"Sending Move: {transform.position}");
            }
            else if (_kcpClient != null && !_kcpClient.IsConnected && Time.frameCount % 120 == 0)
            {
                Debug.LogWarning("KCP Client not connected yet...");
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (Keyboard.current.kKey.isPressed)
            {
                transform.position += Vector3.up * 5f * Time.deltaTime;
            }

            if (Keyboard.current.digit8Key.wasPressedThisFrame)
            {
                if (_animator != null) _animator.SetTrigger("Dead");
            }

            if (Keyboard.current.digit9Key.wasPressedThisFrame)
            {
                if (_animator != null) _animator.SetTrigger("Dance");
            }
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Update: {e.Message}\n{e.StackTrace}");
            lastError = e.Message;
        }
    }
    
    private string lastError = "";

    private void GroundCheck()
    {
        // Use Raycast for more precision and easier self-exclusion
        // Start ray slightly up from feet to avoid starting inside the floor
        Vector3 rayStart = transform.position + Vector3.up * 0.1f; 
        float rayDistance = 0.2f; // 0.1 to feet + 0.1 extra tolerance
        
        bool rayCheck = false;
        RaycastHit hit;
        
        // Cast a ray down
        if (Physics.Raycast(rayStart, Vector3.down, out hit, rayDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            // Check if we hit ourselves (just in case)
            if (hit.collider.transform.root != transform)
            {
                rayCheck = true;
            }
        }

        // 2. CharacterController.isGrounded: Reliable on flat surfaces
        bool ccGrounded = _characterController.isGrounded;

        // Combine them
        _isGrounded = rayCheck || ccGrounded;
        
        // FORCE FALSE if moving upwards (Jumping)
        if (_verticalVelocity > 0.1f) _isGrounded = false;
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private void OnGUI()
    {
        GUI.color = Color.red;
        GUILayout.BeginArea(new Rect(10, 200, 300, 200));
        GUILayout.Label($"Grounded: {_isGrounded}");
        GUILayout.Label($"Pos: {transform.position}");
        GUILayout.Label($"K=fly  8=dead  9=dance");
        
        if (GUILayout.Button("Teleport to spawn"))
        {
            Vector3 safePos = GameConstants.SafeSpawnPos;
            if (_characterController != null) _characterController.enabled = false;
            transform.position = safePos;
            if (_characterController != null) _characterController.enabled = true;
            _verticalVelocity = 0;
        }
        GUILayout.EndArea();
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        // Adjust Gizmo to match CheckSphere logic exactly
        Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y + groundCheckOffset, transform.position.z);
        Gizmos.DrawSphere(spherePosition, groundCheckRadius);
    }
}
