using UnityEngine;
using Protocol;

public class RemotePlayerController : MonoBehaviour
{
    private UnityEngine.Vector3 targetPos;
    private UnityEngine.Quaternion targetRot;
    private UnityEngine.Vector3 previousPos;

    private Animator _animator;
    private CharacterController _cc;

    private float lastUpdateTime;
    private float _currentSpeed;
    private float _speedVelocity;

    private const float LERP_SPEED = 10f;
    private const float SPEED_SMOOTH_TIME = 0.1f;
    private const float STALE_TIMEOUT = 2f;

    void Start()
    {
        targetPos = transform.position;
        targetRot = transform.rotation;
        previousPos = transform.position;

        _animator = GetComponent<Animator>();
        _cc = GetComponent<CharacterController>();

        // Disable CharacterController on remote players — we drive position directly
        if (_cc != null) _cc.enabled = false;
    }

    public void SetTarget(Protocol.Vector3 pos, Protocol.Quaternion rot)
    {
        if (pos != null)
            targetPos = new UnityEngine.Vector3(pos.X, pos.Y, pos.Z);

        if (rot != null)
            targetRot = new UnityEngine.Quaternion(rot.X, rot.Y, rot.Z, rot.W);

        lastUpdateTime = Time.time;
    }

    void Update()
    {
        // Interpolate position & rotation
        transform.position = UnityEngine.Vector3.Lerp(transform.position, targetPos, Time.deltaTime * LERP_SPEED);
        transform.rotation = UnityEngine.Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * LERP_SPEED);

        // Calculate movement speed from actual displacement
        float displacement = UnityEngine.Vector3.Distance(transform.position, previousPos);
        float instantSpeed = displacement / Mathf.Max(Time.deltaTime, 0.001f);
        previousPos = transform.position;

        // If no updates from server for a while, assume stopped
        bool isStale = (Time.time - lastUpdateTime) > STALE_TIMEOUT;
        float targetSpeed = isStale ? 0f : instantSpeed;

        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, SPEED_SMOOTH_TIME);

        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;

        // Speed: normalized 0-1 (same as local player: _currentSpeed / runSpeed)
        float normalizedSpeed = Mathf.Clamp01(_currentSpeed / GameConstants.RunSpeed);

        if (AnimatorUtils.HasParameter("Speed", _animator))
            _animator.SetFloat("Speed", normalizedSpeed);

        if (AnimatorUtils.HasParameter("isWalk", _animator))
            _animator.SetBool("isWalk", _currentSpeed > 0.1f);
    }
}
