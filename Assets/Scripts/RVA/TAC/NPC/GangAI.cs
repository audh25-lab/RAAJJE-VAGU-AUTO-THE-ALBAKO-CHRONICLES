using UnityEngine;
using UnityEngine.AI;

public class GangAI : AIAgent
{
    [Header("Gang Member Settings")]
    public float detectionRadius = 10f;
    public float attackRange = 2f;
    public Transform[] patrolPoints;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 5f;

    private NavMeshAgent navMeshAgent;
    private Transform playerTransform;
    private int currentPatrolIndex = 0;

    protected override void Start()
    {
        base.Start();
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
        }

        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        navMeshAgent.speed = patrolSpeed;
        currentState = AIState.Patrol;

        if (patrolPoints.Length > 0)
        {
            navMeshAgent.SetDestination(patrolPoints[currentPatrolIndex].position);
        }
        else
        {
            currentState = AIState.Idle;
        }
    }

    protected override void Idle()
    {
        // Stand still, maybe play an idle animation
        if (navMeshAgent.remainingDistance < 0.1f)
        {
            // Look for the player
            if (PlayerInRange(detectionRadius))
            {
                currentState = AIState.React;
            }
        }
    }

    protected override void Patrol()
    {
        navMeshAgent.speed = patrolSpeed;

        if (PlayerInRange(detectionRadius))
        {
            currentState = AIState.React;
            return;
        }

        if (patrolPoints.Length == 0)
        {
            currentState = AIState.Idle;
            return;
        }

        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance < 0.5f)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            navMeshAgent.SetDestination(patrolPoints[currentPatrolIndex].position);
        }
    }

    protected override void React()
    {
        // Player detected, move towards them
        navMeshAgent.speed = chaseSpeed;
        navMeshAgent.SetDestination(playerTransform.position);

        if (PlayerInRange(attackRange))
        {
            currentState = AIState.Attack;
        }
        else if (!PlayerInRange(detectionRadius))
        {
            currentState = AIState.Patrol;
        }
    }

    protected override void Attack()
    {
        // Stop moving and attack the player
        navMeshAgent.ResetPath();
        transform.LookAt(playerTransform);

        // TODO: Implement attack logic (e.g., melee, ranged)

        if (!PlayerInRange(attackRange))
        {
            currentState = AIState.React;
        }
    }

    protected override void Flee()
    {
        // Run away from the player
        navMeshAgent.speed = chaseSpeed;
        Vector3 fleeDirection = transform.position - playerTransform.position;
        Vector3 fleeDestination = transform.position + fleeDirection.normalized * detectionRadius;
        navMeshAgent.SetDestination(fleeDestination);

        if (!PlayerInRange(detectionRadius * 1.5f)) // Flee until a safe distance
        {
            currentState = AIState.Patrol;
        }
    }

    private bool PlayerInRange(float range)
    {
        if (playerTransform == null)
        {
            return false;
        }
        return Vector3.Distance(transform.position, playerTransform.position) <= range;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
