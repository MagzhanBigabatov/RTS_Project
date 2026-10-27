using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class RTSUnit : MonoBehaviour
{
    [Header("Unit Settings")]
    [SerializeField] private int teamId = 0;
    [SerializeField] private bool isBuilder = false;
    [SerializeField, Min(0.1f)] private float attackRange = 3f;

    [Header("Turning")]
    [SerializeField, Min(1f)] private float turnSpeed = 540f;
    [SerializeField, Min(0.1f)] private float turnTolerance = 1f;

    [Header("Selection")]
    [SerializeField] private GameObject selectionIndicator;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private NavMeshAgent agent;
    private RTSUnit currentAttackTarget;

    private bool hasAttackOrder;
    private bool rotatingBeforeMove;
    private Vector3 pendingDestination;

    public int TeamId => teamId;
    public bool IsBuilder => isBuilder;
    public bool IsSelected { get; private set; }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            // Movement is controlled by NavMeshAgent.
            animator.applyRootMotion = false;
        }

        // NavMeshAgent controls rotation while the unit is moving.
        agent.updateRotation = true;

        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(false);
        }
    }

    private void Update()
    {
        HandleRotationBeforeMove();
        UpdateAttackMovement();
        UpdateAnimation();
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(selected);
        }
    }

    // Called when the player right-clicks on the ground.
    public void CommandMove(Vector3 requestedPosition)
    {
        currentAttackTarget = null;
        hasAttackOrder = false;

        BeginTurnThenMove(requestedPosition, 0.1f);
    }

    // Approaches an enemy. Damage and attack animations will be added later.
    public void CommandAttack(RTSUnit target)
    {
        if (isBuilder || target == null || target.TeamId == teamId)
        {
            return;
        }

        currentAttackTarget = target;
        hasAttackOrder = true;

        if (!BeginTurnThenMove(target.transform.position, attackRange))
        {
            currentAttackTarget = null;
            hasAttackOrder = false;
        }
    }

    private bool BeginTurnThenMove(
        Vector3 requestedPosition,
        float stoppingDistance)
    {
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            Debug.LogWarning(
                $"{name}: NavMeshAgent is not on a valid NavMesh.",
                this
            );

            return false;
        }

        if (!NavMesh.SamplePosition(
                requestedPosition,
                out NavMeshHit hit,
                3f,
                agent.areaMask))
        {
            Debug.LogWarning(
                $"{name}: No nearby NavMesh position was found.",
                this
            );

            return false;
        }

        pendingDestination = hit.position;

        // Stop and clear the previous path while turning.
        rotatingBeforeMove = false;
        agent.isStopped = true;
        agent.ResetPath();
        agent.stoppingDistance = stoppingDistance;

        Vector3 direction = pendingDestination - transform.position;
        direction.y = 0f;

        // If already facing the destination or very close to it, move directly.
        if (direction.sqrMagnitude < 0.001f)
        {
            return StartMovingToPendingDestination();
        }

        // We rotate the unit manually before movement begins.
        agent.updateRotation = false;
        rotatingBeforeMove = true;

        return true;
    }

    private void HandleRotationBeforeMove()
    {
        if (!rotatingBeforeMove)
        {
            return;
        }

        if (!agent.enabled || !agent.isOnNavMesh)
        {
            rotatingBeforeMove = false;
            agent.updateRotation = true;
            return;
        }

        Vector3 direction = pendingDestination - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            StartMovingToPendingDestination();
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized, Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );

        if (Quaternion.Angle(transform.rotation, targetRotation)
            <= turnTolerance)
        {
            transform.rotation = targetRotation;
            StartMovingToPendingDestination();
        }
    }

    private bool StartMovingToPendingDestination()
    {
        rotatingBeforeMove = false;

        if (!agent.enabled || !agent.isOnNavMesh)
        {
            agent.updateRotation = true;
            return false;
        }

        // Let NavMeshAgent handle turning along the path from this point on.
        agent.updateRotation = true;

        if (!agent.SetDestination(pendingDestination))
        {
            agent.isStopped = true;

            Debug.LogWarning(
                $"{name}: Could not set the movement destination.",
                this
            );

            return false;
        }

        agent.isStopped = false;
        return true;
    }

    private void UpdateAttackMovement()
    {
        // Do not update the attack path while the unit is turning initially.
        if (!hasAttackOrder || rotatingBeforeMove)
        {
            return;
        }

        if (currentAttackTarget == null ||
            !currentAttackTarget.gameObject.activeInHierarchy)
        {
            currentAttackTarget = null;
            hasAttackOrder = false;

            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.stoppingDistance = 0.1f;
            }

            return;
        }

        if (!agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        Vector3 myPosition = transform.position;
        Vector3 targetPosition = currentAttackTarget.transform.position;

        myPosition.y = 0f;
        targetPosition.y = 0f;

        float distance = Vector3.Distance(myPosition, targetPosition);

        if (distance <= attackRange)
        {
            agent.isStopped = true;
        }
        else
        {
            agent.isStopped = false;

            if (Vector3.Distance(
                    agent.destination,
                    currentAttackTarget.transform.position) > 0.75f)
            {
                UpdateAttackDestination();
            }
        }
    }

    private void UpdateAttackDestination()
    {
        if (currentAttackTarget == null || !agent.isOnNavMesh)
        {
            return;
        }

        if (NavMesh.SamplePosition(
                currentAttackTarget.transform.position,
                out NavMeshHit hit,
                3f,
                agent.areaMask))
        {
            agent.SetDestination(hit.position);
        }
    }

    private void UpdateAnimation()
    {
        if (animator == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        float normalizedSpeed = 0f;

        // If the unit is stopped or still turning, use exactly zero.
        if (!agent.isStopped && !rotatingBeforeMove)
        {
            normalizedSpeed =
                agent.velocity.magnitude / Mathf.Max(agent.speed, 0.01f);
        }

        // Remove tiny values caused by residual movement.
        if (normalizedSpeed < 0.05f)
        {
            normalizedSpeed = 0f;
        }

        // Set the value directly, without parameter damping.
        animator.SetFloat("Speed", normalizedSpeed);
    }
}