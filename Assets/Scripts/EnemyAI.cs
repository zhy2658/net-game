using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public Transform target; // The player
    public float chaseRange = 15f;
    public float attackRange = 2.0f;
    public float patrolSpeed = 2.0f;
    public float chaseSpeed = 5.0f;
    public float damage = 10f;
    public float health = 50f;

    // State Machine
    private enum State { Idle, Patrol, Chase, Attack, Dead }
    private State _currentState = State.Idle;

    private NavMeshAgent _navAgent;
    private Animator _animator;
    private float _lastAttackTime;
    private Vector3 _startPosition;

    void Start()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _startPosition = transform.position;

        // Auto-find player if not set
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        // Setup NavMeshAgent
        if (_navAgent != null)
        {
            _navAgent.speed = patrolSpeed;
            _navAgent.stoppingDistance = attackRange - 0.5f;
        }
    }

    void Update()
    {
        if (_currentState == State.Dead) return;
        if (target == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);

        switch (_currentState)
        {
            case State.Idle:
            case State.Patrol:
                if (distanceToPlayer <= chaseRange)
                {
                    _currentState = State.Chase;
                    if (_animator) _animator.SetBool("isWalking", true); // Assuming param name
                }
                break;

            case State.Chase:
                if (_navAgent != null && _navAgent.isOnNavMesh) // Check isOnNavMesh first
                {
                    _navAgent.speed = chaseSpeed;
                    _navAgent.SetDestination(target.position);
                }

                if (distanceToPlayer <= attackRange)
                {
                    _currentState = State.Attack;
                }
                else if (distanceToPlayer > chaseRange * 1.5f) // Lost player
                {
                    _currentState = State.Patrol;
                    if (_navAgent != null && _navAgent.isOnNavMesh) _navAgent.SetDestination(_startPosition); // Return home
                }
                break;

            case State.Attack:
                // Face player
                Vector3 direction = (target.position - transform.position).normalized;
                direction.y = 0;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);

                if (Time.time - _lastAttackTime > 2.0f) // Attack cooldown
                {
                    _lastAttackTime = Time.time;
                    if (_animator) _animator.SetTrigger("Attack"); // Assuming trigger name
                    Debug.Log("Enemy Attacks Player!");
                    // Deal damage logic here
                }

                if (distanceToPlayer > attackRange)
                {
                    _currentState = State.Chase;
                }
                break;
        }
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0)
        {
            Die();
        }
        else
        {
            // Hit reaction?
            if (_animator) _animator.SetTrigger("Hit");
        }
    }

    private void Die()
    {
        _currentState = State.Dead;
        if (_animator) _animator.SetTrigger("Die");
        if (_navAgent) _navAgent.isStopped = true;
        Destroy(gameObject, 5f); // Clean up after 5 seconds
        Debug.Log("Enemy Died!");
    }
    
    // Debug Range
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
