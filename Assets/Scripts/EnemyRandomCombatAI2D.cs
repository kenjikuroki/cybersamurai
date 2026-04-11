using UnityEngine;

[RequireComponent(typeof(EnemyStateMachine2D))]
public class EnemyRandomCombatAI2D : MonoBehaviour
{
    public float minActionInterval = 2f;
    public float maxActionInterval = 4f;
    public float chargeDuration = 0.5f;
    public float moveSpeed = 1.5f;
    public float approachDistance = 0.75f;
    public MonoBehaviour targetActorSource;

    private EnemyStateMachine2D enemyStateMachine;
    private GuardGauge guardGauge;
    private Rigidbody2D rb;
    private ICombatStateActor targetActor;
    private Transform targetTransform;
    private float nextActionTime;
    private float chargeEndTime;
    private CombatStateType queuedAction;
    private EnemyAiPhase phase = EnemyAiPhase.Waiting;

    private void Awake()
    {
        enemyStateMachine = GetComponent<EnemyStateMachine2D>();
        guardGauge = GetComponent<GuardGauge>();
        rb = GetComponent<Rigidbody2D>();
        ResolveTarget();
        ScheduleNextAction();
    }

    private void OnValidate()
    {
        if (minActionInterval < 0f)
        {
            minActionInterval = 0f;
        }

        if (maxActionInterval < minActionInterval)
        {
            maxActionInterval = minActionInterval;
        }

        if (chargeDuration < 0f)
        {
            chargeDuration = 0f;
        }

        if (moveSpeed < 0f)
        {
            moveSpeed = 0f;
        }

        if (approachDistance < 0f)
        {
            approachDistance = 0f;
        }
    }

    private void Update()
    {
        if (enemyStateMachine == null)
        {
            return;
        }

        ResolveTarget();
        if (targetTransform == null)
        {
            StopMovement();
            return;
        }

        if (enemyStateMachine.IsMovementLocked)
        {
            StopMovement();
            phase = EnemyAiPhase.Waiting;
            return;
        }

        if (!IsNeutralState())
        {
            StopMovement();
            return;
        }

        float distance = Mathf.Abs(targetTransform.position.x - transform.position.x);

        if (ShouldImmediateAttackTarget() && distance <= approachDistance)
        {
            queuedAction = CombatStateType.Attack;
            ExecuteChosenAction();
            ScheduleNextAction();
            return;
        }

        if (distance > approachDistance)
        {
            MoveToward(targetTransform.position);
            return;
        }

        StopMovement();

        if (phase == EnemyAiPhase.Waiting)
        {
            if (Time.time < nextActionTime)
            {
                return;
            }

            queuedAction = ShouldImmediateAttackTarget() ? CombatStateType.Attack : ChooseNextAction();
            Debug.Log($"Enemy chose: {queuedAction}", this);
            phase = EnemyAiPhase.Charging;
            chargeEndTime = Time.time + chargeDuration;
            return;
        }

        if (phase == EnemyAiPhase.Charging && Time.time >= chargeEndTime)
        {
            ExecuteChosenAction();
            ScheduleNextAction();
        }
    }

    public void SetTarget(ICombatStateActor newTarget)
    {
        targetActor = newTarget;
        targetTransform = (newTarget as Component)?.transform;
    }

    private bool IsNeutralState()
    {
        return enemyStateMachine.CurrentStateType == CombatStateType.Guard
            || enemyStateMachine.CurrentStateType == CombatStateType.Idle;
    }

    private bool ShouldImmediateAttackTarget()
    {
        if (targetActor == null)
        {
            return false;
        }

        return targetActor.CurrentStateType == CombatStateType.Vulnerable
            || targetActor.CurrentStateType == CombatStateType.GuardBreak;
    }

    private void ExecuteChosenAction()
    {
        switch (queuedAction)
        {
            case CombatStateType.Attack:
                enemyStateMachine.TriggerAttack();
                break;
            case CombatStateType.Parry:
                enemyStateMachine.TriggerParry();
                break;
            case CombatStateType.Feint:
                enemyStateMachine.TriggerFeint();
                break;
            default:
                Debug.LogWarning($"Unsupported enemy action: {queuedAction}", this);
                break;
        }
    }

    private CombatStateType ChooseNextAction()
    {
        float attackWeight = 1f;
        float parryWeight = 1f;
        float feintWeight = 1f;

        if (guardGauge != null && guardGauge.CurrentGuard <= 2)
        {
            attackWeight = 0.3f;
            parryWeight = 0.5f;
            feintWeight = 0.2f;
        }

        float totalWeight = attackWeight + parryWeight + feintWeight;
        float roll = Random.Range(0f, totalWeight);

        if (roll < attackWeight)
        {
            return CombatStateType.Attack;
        }

        if (roll < attackWeight + parryWeight)
        {
            return CombatStateType.Parry;
        }

        return CombatStateType.Feint;
    }

    private void MoveToward(Vector3 destination)
    {
        float deltaX = destination.x - transform.position.x;
        float direction = Mathf.Sign(deltaX);

        if (Mathf.Abs(deltaX) <= 0.01f)
        {
            StopMovement();
            return;
        }

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        }
        else
        {
            transform.position += new Vector3(direction * moveSpeed * Time.deltaTime, 0f, 0f);
        }
    }

    private void StopMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    private void ScheduleNextAction()
    {
        nextActionTime = Time.time + Random.Range(minActionInterval, maxActionInterval);
        phase = EnemyAiPhase.Waiting;
        chargeEndTime = 0f;
    }

    private void ResolveTarget()
    {
        if (targetActor == null)
        {
            targetActor = targetActorSource as ICombatStateActor;
        }

        if (targetActor != null)
        {
            targetTransform = (targetActor as Component)?.transform;
        }
    }

    private enum EnemyAiPhase
    {
        Waiting,
        Charging
    }
}
