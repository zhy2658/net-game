using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    public GameObject weaponPrefab; // The knife prefab to spawn in hand
    public Transform rightHand;     // Will be auto-found if null
    public bool hasWeapon = false;

    private Animator _animator;
    private bool _isAttacking = false;
    private float _attackCooldown = 0.5f;
    private float _lastAttackTime;

    // Procedural Animation
    private Quaternion _initialRotation;
    private Transform _shoulder; // For procedural swing if needed

    void Start()
    {
        _animator = GetComponent<Animator>();
        
        // Auto-find right hand if using Humanoid
        if (_animator != null && _animator.isHuman)
        {
            rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            _shoulder = _animator.GetBoneTransform(HumanBodyBones.RightShoulder);
        }
    }

    // Input System
    private UnityEngine.InputSystem.InputAction attackAction;

    void Awake()
    {
        // Use a standalone InputAction instead of a Map for simplicity
        attackAction = new UnityEngine.InputSystem.InputAction(name: "Attack", type: UnityEngine.InputSystem.InputActionType.Button, binding: "<Mouse>/leftButton");
        attackAction.Enable();
    }

    void Update()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (UnityEngine.InputSystem.Keyboard.current.jKey.wasPressedThisFrame)
        {
            PerformAttack();
        }
#endif

        bool inputTriggered = false;
        if (attackAction != null && attackAction.WasPressedThisFrame()) inputTriggered = true;
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) inputTriggered = true;

        if (inputTriggered && !_isAttacking)
        {
            PerformAttack();
        }
    }

    public void EquipWeapon(GameObject weaponRef)
    {
        if (hasWeapon) return;
        
        hasWeapon = true;
        
        // Instantiate weapon in hand
        if (weaponPrefab != null && rightHand != null)
        {
            GameObject heldWeapon = Instantiate(weaponPrefab, rightHand);
            
            // Adjust position/rotation for the specific knife model
            // These values might need tweaking based on the model's pivot
            heldWeapon.transform.localPosition = new Vector3(0.05f, 0.05f, 0.02f); 
            heldWeapon.transform.localRotation = Quaternion.Euler(180, 0, 90); 
            // Fix Scale for HELD weapon too
            heldWeapon.transform.localScale = Vector3.one * 0.02f;
            
            // Remove rigidbody/colliders from the held weapon so it doesn't mess up physics
            Destroy(heldWeapon.GetComponent<Rigidbody>());
            Collider[] cols = heldWeapon.GetComponents<Collider>();
            foreach(var c in cols) Destroy(c);
        }
    }

    private void PerformAttack()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;
        
        Debug.Log("Attacking!");

        // Trigger Animation
        if (_animator != null)
        {
            // Check for "Attack" trigger (New Controller)
            bool hasAttack = false;
            foreach(var p in _animator.parameters)
            {
                if(p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger)
                {
                    hasAttack = true;
                    break;
                }
            }
            
            if (hasAttack)
            {
                _animator.SetTrigger("Attack");
            }
        }

        // Procedural Animation Fallback (Only if no animation clip playing)
        // Since we have an animation now, we might want to disable this procedural arm swing
        // But let's keep it as subtle layer or disable if we trust the clip.
        // For now, let's keep it but maybe reduce intensity if animation is playing?
        // Actually, if we have a clip, we don't need the arm swing.
        
        StartCoroutine(ResetAttack());
    }

    private System.Collections.IEnumerator ResetAttack()
    {
        yield return new WaitForSeconds(0.2f); // Delay for hit impact
        
        // Hit detection logic here (SphereCast)
        // ... (Hit logic)
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 direction = transform.forward;
        if (Physics.SphereCast(origin, 0.5f, direction, out RaycastHit hit, 2.0f))
        {
            // Check for Enemy
            EnemyAI enemy = hit.collider.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                Debug.Log("Hit Enemy!");
                enemy.TakeDamage(10); // Deal 10 damage
            }
        }

        yield return new WaitForSeconds(_attackCooldown - 0.2f);
        _isAttacking = false;
    }
    
    // Procedural animation attempt (Simple arm swing override)
    void LateUpdate()
    {
        // Only do procedural swing if we don't have an Attack animation parameter
        // OR if we want to combine them.
        // Let's disable procedural swing if we successfully triggered an animation to avoid conflict.
        if (_animator != null)
        {
             foreach(var p in _animator.parameters)
             {
                 if(p.name == "Attack") return; // Found attack param, so animation is handling it.
             }
        }

        if (_isAttacking && rightHand != null && _animator != null)
        {
            // This is a very hacky way to animate without an animation clip
            // It tries to override the arm rotation after the animator has updated
            // Swing movement
            float time = (Time.time - _lastAttackTime) / 0.2f; // 0 to 1 during swing
            if (time < 1.0f)
            {
                // Simple slash motion
                rightHand.Rotate(Vector3.right, Mathf.Sin(time * Mathf.PI) * 45f);
            }
        }
    }
}
