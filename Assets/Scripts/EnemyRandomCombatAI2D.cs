using UnityEngine;

[RequireComponent(typeof(EnemyStateMachine2D))]
public class EnemyRandomCombatAI2D : MonoBehaviour
{
    [Header("Action Timing")]
    public float minActionInterval = 2f;
    public float maxActionInterval = 4f;
    public float chargeDuration    = 0.5f;

    [Header("Movement")]
    [Tooltip("攻撃のための踏み込み速度")]
    public float moveSpeed = 1.5f;
    [Tooltip("踏み込み時にこの距離まで詰める")]
    public float approachDistance = 0.8f;

    [Header("Ma-ai（間合い）")]
    [Tooltip("維持したい理想の間合い（距離）")]
    public float maaiDistance = 1.2f;
    [Tooltip("この幅の中では動かない（剣道の遊び）")]
    public float maaiTolerance = 0.25f;
    [Tooltip("距離変化に反応するまでのディレイ（機械的な追従を防ぐ）")]
    public float maaiReactionDelay = 0.5f;
    [Tooltip("ディレイへのランダム幅（0〜この値を追加）")]
    public float maaiReactionJitter = 0.4f;
    [Tooltip("間合い調整時の移動速度（踏み込みより遅くする）")]
    public float maaiMoveSpeed = 0.7f;

    public MonoBehaviour targetActorSource;

    // -------------------------------------------------------------------------

    private EnemyStateMachine2D enemyStateMachine;
    private GuardGauge          guardGauge;
    private Rigidbody2D         rb;
    private ICombatStateActor   targetActor;
    private Transform           targetTransform;

    private float           nextActionTime;
    private float           chargeEndTime;
    private CombatStateType queuedAction;
    private EnemyAiPhase    phase = EnemyAiPhase.Waiting;

    // 間合い管理タイマー：一定時間だけ様子を見てから動く
    private float maaiReactionTimer;

    // -------------------------------------------------------------------------

    private void Awake()
    {
        enemyStateMachine = GetComponent<EnemyStateMachine2D>();
        guardGauge        = GetComponent<GuardGauge>();
        rb                = GetComponent<Rigidbody2D>();
        ResolveTarget();
        ScheduleNextAction();
        ResetMaaiTimer();
    }

    private void OnValidate()
    {
        minActionInterval  = Mathf.Max(0f, minActionInterval);
        maxActionInterval  = Mathf.Max(minActionInterval, maxActionInterval);
        chargeDuration     = Mathf.Max(0f, chargeDuration);
        moveSpeed          = Mathf.Max(0f, moveSpeed);
        approachDistance   = Mathf.Max(0f, approachDistance);
        maaiDistance       = Mathf.Max(0f, maaiDistance);
        maaiTolerance      = Mathf.Max(0f, maaiTolerance);
        maaiReactionDelay  = Mathf.Max(0f, maaiReactionDelay);
        maaiReactionJitter = Mathf.Max(0f, maaiReactionJitter);
        maaiMoveSpeed      = Mathf.Max(0f, maaiMoveSpeed);
    }

    // -------------------------------------------------------------------------

    private void Update()
    {
        if (enemyStateMachine == null) return;

        ResolveTarget();
        if (targetTransform == null) { StopMovement(); return; }

        // ダメージ/死亡中は停止
        if (enemyStateMachine.IsMovementLocked)
        {
            StopMovement();
            phase = EnemyAiPhase.Waiting;
            return;
        }

        // アクション中（Attack/Parry/Feint）は移動しない
        if (!IsNeutralState()) { StopMovement(); return; }

        float distance = Mathf.Abs(targetTransform.position.x - transform.position.x);

        // ── チャンス攻撃（相手が硬直中）───────────────────────────────────
        if (ShouldImmediateAttackTarget())
        {
            if (distance <= approachDistance)
            {
                queuedAction = CombatStateType.Attack;
                ExecuteChosenAction();
                ScheduleNextAction();
            }
            else
            {
                // 素早く踏み込む
                MoveToward(targetTransform.position, moveSpeed);
            }
            return;
        }

        // ── 踏み込みフェーズ（攻撃前の接近）──────────────────────────────
        if (phase == EnemyAiPhase.Charging)
        {
            if (distance > approachDistance)
            {
                MoveToward(targetTransform.position, moveSpeed);
            }
            else if (Time.time >= chargeEndTime)
            {
                ExecuteChosenAction();
                ScheduleNextAction();
            }
            // （approachDistance に着いたが chargeEndTime 待ち → 停止）
            else
            {
                StopMovement();
            }
            return;
        }

        // ── 待機フェーズ：間合い（Ma-ai）管理 ─────────────────────────────
        //
        //   理想距離 ±tolerance の外に出たら reaction timer を減らす。
        //   timer がゼロになったら初めて足を動かす。
        //   適正範囲に入ったら timer をランダムリセット → 遊びが生まれる。
        //
        float inner = maaiDistance - maaiTolerance;  // これより近ければ退く
        float outer = maaiDistance + maaiTolerance;  // これより遠ければ詰める

        if (distance < inner)
        {
            // プレイヤーが間合いに入り過ぎ → 退く
            maaiReactionTimer -= Time.deltaTime;
            if (maaiReactionTimer <= 0f)
                MoveAwayFrom(targetTransform.position, maaiMoveSpeed);
            else
                StopMovement(); // まだ様子見
        }
        else if (distance > outer)
        {
            // プレイヤーが遠すぎ → 詰める
            maaiReactionTimer -= Time.deltaTime;
            if (maaiReactionTimer <= 0f)
                MoveToward(targetTransform.position, maaiMoveSpeed);
            else
                StopMovement();
        }
        else
        {
            // 適正距離 → 停止してタイマーリセット
            StopMovement();
            ResetMaaiTimer();
        }

        // 攻撃タイミングが来たら踏み込みへ移行
        if (Time.time >= nextActionTime)
        {
            StopMovement();
            queuedAction  = ChooseNextAction();
            Debug.Log($"Enemy chose: {queuedAction}", this);
            phase         = EnemyAiPhase.Charging;
            chargeEndTime = Time.time + chargeDuration;
        }
    }

    // -------------------------------------------------------------------------

    public void SetTarget(ICombatStateActor newTarget)
    {
        targetActor     = newTarget;
        targetTransform = (newTarget as Component)?.transform;
    }

    // -------------------------------------------------------------------------

    private bool IsNeutralState()
    {
        return enemyStateMachine.CurrentStateType == CombatStateType.Guard
            || enemyStateMachine.CurrentStateType == CombatStateType.Idle;
    }

    private bool ShouldImmediateAttackTarget()
    {
        if (targetActor == null) return false;
        return targetActor.CurrentStateType == CombatStateType.Vulnerable
            || targetActor.CurrentStateType == CombatStateType.GuardBreak;
    }

    private void ExecuteChosenAction()
    {
        switch (queuedAction)
        {
            case CombatStateType.Attack: enemyStateMachine.TriggerAttack(); break;
            case CombatStateType.Parry:  enemyStateMachine.TriggerParry();  break;
            case CombatStateType.Feint:  enemyStateMachine.TriggerFeint();  break;
            default: Debug.LogWarning($"Unsupported enemy action: {queuedAction}", this); break;
        }
    }

    private CombatStateType ChooseNextAction()
    {
        float attackWeight = 1f;
        float parryWeight  = 1f;
        float feintWeight  = 1f;

        if (guardGauge != null && guardGauge.CurrentGuard <= 2)
        {
            attackWeight = 0.3f;
            parryWeight  = 0.5f;
            feintWeight  = 0.2f;
        }

        float total = attackWeight + parryWeight + feintWeight;
        float roll  = Random.Range(0f, total);

        if (roll < attackWeight)              return CombatStateType.Attack;
        if (roll < attackWeight + parryWeight) return CombatStateType.Parry;
        return CombatStateType.Feint;
    }

    // -------------------------------------------------------------------------

    private void MoveToward(Vector3 destination, float speed)
    {
        float deltaX = destination.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= 0.01f) { StopMovement(); return; }

        float dir = Mathf.Sign(deltaX);
        if (rb != null)
            rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
        else
            transform.position += new Vector3(dir * speed * Time.deltaTime, 0f, 0f);
    }

    private void MoveAwayFrom(Vector3 from, float speed)
    {
        float deltaX = transform.position.x - from.x;  // 自分 → 相手 の逆方向
        float dir = Mathf.Abs(deltaX) <= 0.01f ? 1f : Mathf.Sign(deltaX);

        if (rb != null)
            rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
        else
            transform.position += new Vector3(dir * speed * Time.deltaTime, 0f, 0f);
    }

    private void StopMovement()
    {
        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void ScheduleNextAction()
    {
        nextActionTime = Time.time + Random.Range(minActionInterval, maxActionInterval);
        phase          = EnemyAiPhase.Waiting;
        chargeEndTime  = 0f;
        ResetMaaiTimer();
    }

    /// <summary>
    /// 反応タイマーをリセット。maaiReactionDelay ＋ ランダム幅で設定することで
    /// 毎回異なるタイミングで動き出し、機械的な追従を防ぐ。
    /// </summary>
    private void ResetMaaiTimer()
    {
        maaiReactionTimer = maaiReactionDelay + Random.Range(0f, maaiReactionJitter);
    }

    private void ResolveTarget()
    {
        if (targetActor == null)
            targetActor = targetActorSource as ICombatStateActor;

        if (targetActor != null)
            targetTransform = (targetActor as Component)?.transform;
    }

    private enum EnemyAiPhase { Waiting, Charging }
}
