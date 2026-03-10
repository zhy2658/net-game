using UnityEngine;
using Protocol;

public class RemotePlayerController : MonoBehaviour
{
    private UnityEngine.Vector3 targetPos;
    private UnityEngine.Quaternion targetRot;

    private Animator _animator;
    private CharacterController _cc;

    private float lastUpdateTime;
    private float _networkSpeed;
    private float _displaySpeed;
    private float _speedVelocity;

    private bool _animReady;
    private bool _hasSpeedParam;
    private bool _hasIsWalkParam;

    private const float LERP_SPEED = 10f;
    private const float SPEED_SMOOTH_TIME = 0.15f;
    private const float STALE_TIMEOUT = 2f;

    void Start()
    {
        targetPos = transform.position;
        targetRot = transform.rotation;

        _cc = GetComponent<CharacterController>();
        if (_cc != null) _cc.enabled = false;

        _animator = GetComponent<Animator>();
        TryInitAnimator();
    }

    public void SetTarget(Protocol.Vector3 pos, Protocol.Quaternion rot)
    {
        SetTarget(pos, rot, 0f, true);
    }

    public void SetTarget(Protocol.Vector3 pos, Protocol.Quaternion rot, float speed, bool isGrounded)
    {
        if (pos != null)
            targetPos = new UnityEngine.Vector3(pos.X, pos.Y, pos.Z);
        if (rot != null)
            targetRot = new UnityEngine.Quaternion(rot.X, rot.Y, rot.Z, rot.W);

        _networkSpeed = speed;
        lastUpdateTime = Time.time;
    }

    void Update()
    {
        transform.position = UnityEngine.Vector3.Lerp(transform.position, targetPos, Time.deltaTime * LERP_SPEED);
        transform.rotation = UnityEngine.Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * LERP_SPEED);

        bool isStale = (Time.time - lastUpdateTime) > STALE_TIMEOUT;
        _displaySpeed = Mathf.SmoothDamp(_displaySpeed, isStale ? 0f : _networkSpeed, ref _speedVelocity, SPEED_SMOOTH_TIME);

        if (!_animReady) TryInitAnimator();
        if (_animReady) UpdateAnimator();
    }

    private void TryInitAnimator()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_animator == null || _animator.runtimeAnimatorController == null)
            return;

        _hasSpeedParam = AnimatorUtils.HasParameter("Speed", _animator);
        _hasIsWalkParam = AnimatorUtils.HasParameter("isWalk", _animator);
        _animReady = true;
    }

    private void UpdateAnimator()
    {
        if (_hasSpeedParam)
            _animator.SetFloat("Speed", _displaySpeed);
        if (_hasIsWalkParam)
            _animator.SetBool("isWalk", _displaySpeed > 0.05f);
    }
}
