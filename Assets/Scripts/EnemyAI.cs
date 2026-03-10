using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public Transform target;
    public float chaseRange = 15f;
    public float attackRange = 2.0f;
    public float patrolSpeed = 2.0f;
    public float chaseSpeed = 5.0f;
    public float damage = 10f;
    public float health = 50f;

    [Header("Network Sync")]
    public bool isLocalAuthority = true;
    public string networkId;

    public enum State { Idle, Patrol, Chase, Attack, Dead }
    private State _currentState = State.Idle;
    public State CurrentState => _currentState;

    private NavMeshAgent _navAgent;
    private Animator _animator;
    private float _lastAttackTime;
    private Vector3 _startPosition;

    // Network interpolation (for remote-driven enemies)
    private Vector3 _netTargetPos;
    private Quaternion _netTargetRot;
    private float _lastNetUpdateTime;

    void Start()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _startPosition = transform.position;
        _netTargetPos = transform.position;
        _netTargetRot = transform.rotation;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (_navAgent != null)
        {
            _navAgent.speed = patrolSpeed;
            _navAgent.stoppingDistance = attackRange - 0.5f;
        }
    }

    void Update()
    {
        if (_currentState == State.Dead) return;

        if (isLocalAuthority)
        {
            UpdateLocalAI();
        }
        else
        {
            UpdateRemoteInterpolation();
        }
    }

    private void UpdateLocalAI()
    {
        if (target == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);

        switch (_currentState)
        {
            case State.Idle:
            case State.Patrol:
                if (distanceToPlayer <= chaseRange)
                {
                    _currentState = State.Chase;
                    if (_animator) _animator.SetBool("isWalking", true);
                }
                break;

            case State.Chase:
                if (_navAgent != null && _navAgent.isOnNavMesh)
                {
                    _navAgent.speed = chaseSpeed;
                    _navAgent.SetDestination(target.position);
                }

                if (distanceToPlayer <= attackRange)
                {
                    _currentState = State.Attack;
                }
                else if (distanceToPlayer > chaseRange * 1.5f)
                {
                    _currentState = State.Patrol;
                    if (_navAgent != null && _navAgent.isOnNavMesh)
                        _navAgent.SetDestination(_startPosition);
                }
                break;

            case State.Attack:
                Vector3 direction = (target.position - transform.position).normalized;
                direction.y = 0;
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction),
                    Time.deltaTime * 5f);

                if (Time.time - _lastAttackTime > 2.0f)
                {
                    _lastAttackTime = Time.time;
                    if (_animator) _animator.SetTrigger("Attack");
                }

                if (distanceToPlayer > attackRange)
                {
                    _currentState = State.Chase;
                }
                break;
        }
    }

    private void UpdateRemoteInterpolation()
    {
        float t = Time.deltaTime * 10f;
        transform.position = Vector3.Lerp(transform.position, _netTargetPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, _netTargetRot, t);
    }

    /// <summary>
    /// Apply state received from server/host. Used for remote-driven enemies.
    /// </summary>
    public void ApplyNetworkState(Vector3 position, Quaternion rotation, State state)
    {
        _netTargetPos = position;
        _netTargetRot = rotation;
        _lastNetUpdateTime = Time.time;

        if (_currentState != state)
        {
            _currentState = state;
            UpdateAnimatorState(state);
        }
    }

    private void UpdateAnimatorState(State state)
    {
        if (_animator == null) return;

        switch (state)
        {
            case State.Idle:
            case State.Patrol:
                _animator.SetBool("isWalking", false);
                break;
            case State.Chase:
                _animator.SetBool("isWalking", true);
                break;
            case State.Attack:
                _animator.SetTrigger("Attack");
                break;
            case State.Dead:
                _animator.SetTrigger("Die");
                break;
        }
    }

    public void TakeDamage(float amount)
    {
        if (!isLocalAuthority) return;

        health -= amount;
        if (health <= 0)
        {
            Die();
        }
        else
        {
            if (_animator) _animator.SetTrigger("Hit");
        }
    }

    private void Die()
    {
        _currentState = State.Dead;
        if (_animator) _animator.SetTrigger("Die");
        if (_navAgent) _navAgent.isStopped = true;
        Destroy(gameObject, 5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
