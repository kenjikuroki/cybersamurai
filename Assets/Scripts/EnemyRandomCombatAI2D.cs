using UnityEngine;

[RequireComponent(typeof(EnemyStateMachine2D))]
public class EnemyRandomCombatAI2D : MonoBehaviour
{
    [Header("Enemy Type")]
    public EnemyType enemyType = EnemyType.Attacker;

    [Header("Action Timing")]
    public float minActionInterval = 2f;
    public float maxActionInterval = 4f;
    public float chargeDuration    = 0.5f;

    [Header("Movement")]
    [Tooltip("攻撃のための踏み込み速度")]
    public float moveSpeed = 1.5f;
    [Tooltip("踏み込み時にこの距離まで詰める")]
    public float approachDistance = 0.8f;

    [Header("Action Selection（EnemyType以外のとき使う手動重み）")]
    [Tooltip("false にするとフェイントを選択しなくなる（パリィ調整用）")]
    public bool useFeint = true;

    [Header("Combo")]
    [Tooltip("1発 / 2発 / 3発コンボの選択重み")]
    public float combo1Weight = 2f;
    public float combo2Weight = 2f;
    public float combo3Weight = 1f;

    [Header("Counter Attack")]
    [Tooltip("相手の攻撃終わりに反撃する確率 (0〜1)")]
    [Range(0f, 1f)]
    public float counterAttackChance = 0.65f;
    [Tooltip("反撃時の踏み込み時間（通常より短い）")]
    public float counterChargeDuration = 0.15f;

    [Header("Feint Reaction")]
    [Tooltip("相手のフェイントを見てパリィしてしまう確率 (0〜1)。高いほどだまされやすい。")]
    [Range(0f, 1f)]
    public float feintReactionChance = 0.5f;

    [Header("Dodge")]
    [Tooltip("相手の攻撃を見て後退回避する確率 (0〜1)。Dodger タイプ以外でも設定可能。")]
    [Range(0f, 1f)]
    public float dodgeChance = 0f;
    [Tooltip("回避後退の速度")]
    public float dodgeSpeed = 2.5f;
    [Tooltip("後退し続ける時間（秒）。attackJudgmentTime より長くすること。")]
    public float dodgeDuration = 0.55f;

    [Header("Post-Attack Retreat（攻撃後の間合い取り直し）")]
    [Tooltip("攻撃後に間合いを取り直す確率 (0〜1)。高いほど前後運動が増える。")]
    [Range(0f, 1f)]
    public float postAttackRetreatChance = 0.5f;
    [Tooltip("間合い取り直し時の後退速度（踏み込みより速く）")]
    public float retreatSpeed = 2.2f;

    [Header("Rhythm（リズムのランダム化）")]
    [Tooltip("踏み込み（溜め）時間のランダム上乗せ幅。大きいほどアクションタイミングが読みにくい。")]
    public float chargeJitter = 0.4f;
    [Tooltip("行動前に一瞬止まる確率（タメのフェイク）。高いほど間が読めない。")]
    [Range(0f, 1f)]
    public float hesitationChance = 0.35f;
    [Tooltip("タメ（一瞬止まる）の長さ（秒）")]
    public float hesitationDuration = 0.45f;

    [Header("Lure Attack（引き込み）")]
    [Tooltip("後退中に相手が追いかけてきたら反転攻撃する確率")]
    [Range(0f, 1f)]
    public float lureAttackChance = 0.45f;
    [Tooltip("後退開始からこの秒数後にルアー判定を行う（短すぎると間合いが取れない）")]
    public float lureMinRetreatTime = 0.35f;

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

    [Header("Standby（スタンバイ移動）")]
    [Tooltip("スタンバイ時の移動速度（戦闘より遅く）")]
    public float standbyMoveSpeed = 0.30f;
    [Tooltip("スタンバイ目標への到達判定距離（これ以内なら停止）")]
    public float standbyArrivalRadius = 0.25f;
    [Tooltip("スタンバイ中にプレイヤーに近づかせない最小X距離（安全網）")]
    public float standbyMinPlayerDist = 1.8f;

    public MonoBehaviour targetActorSource;

    // スタンバイモード（待機中の敵）
    public bool IsStandby { get; private set; } = false;
    private Vector3 standbyTarget;

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

    // 間合い管理タイマー
    private float maaiReactionTimer;

    // コンボ制御
    private int pendingComboHits;

    // カウンター攻撃
    private CombatStateType lastTargetState   = CombatStateType.Guard;
    private bool            counterQueued;
    private bool            feintPunishQueued; // フェイント見切り（即攻撃）

    // 回避
    private float dodgeUntil;
    private bool  isDodging;

    // リズム（タメ）
    private bool  hesitating;
    private float hesitationEndTime;

    // 引き込み（後退からの反転攻撃）
    private float retreatStartTime;
    private bool  lureTriggered;
    private float prevDistToTarget;


    // アクション履歴（同じアクション連続防止）
    private CombatStateType lastChosenAction = (CombatStateType)(-1);
    private int             actionRepeatCount;

    // 強制攻撃フラグ（邪魔攻撃時にセット）
    private bool forceAttackOnce = false;

    // チャンス攻撃の反応遅延（パリィ後の即反応を防ぐ）
    private float vulnerableReactUntil = 0f;  // この時刻以降に反応可能
    private bool  vulnerableReactArmed = false; // 反応待機中フラグ

    // -------------------------------------------------------------------------

    private void Awake()
    {
        enemyStateMachine = GetComponent<EnemyStateMachine2D>();
        guardGauge        = GetComponent<GuardGauge>();
        rb                = GetComponent<Rigidbody2D>();
        ResolveTarget();
        ScheduleNextAction();
        ResetMaaiTimer();

        enemyStateMachine.OnComboAttackStarted += OnEnemyComboHitStarted;
    }

    private void OnDestroy()
    {
        if (enemyStateMachine != null)
            enemyStateMachine.OnComboAttackStarted -= OnEnemyComboHitStarted;
    }

    private void OnValidate()
    {
        minActionInterval     = Mathf.Max(0f, minActionInterval);
        maxActionInterval     = Mathf.Max(minActionInterval, maxActionInterval);
        chargeDuration        = Mathf.Max(0f, chargeDuration);
        moveSpeed             = Mathf.Max(0f, moveSpeed);
        approachDistance      = Mathf.Max(0f, approachDistance);
        combo1Weight          = Mathf.Max(0f, combo1Weight);
        combo2Weight          = Mathf.Max(0f, combo2Weight);
        combo3Weight          = Mathf.Max(0f, combo3Weight);
        counterAttackChance   = Mathf.Clamp01(counterAttackChance);
        counterChargeDuration = Mathf.Max(0f, counterChargeDuration);
        maaiDistance          = Mathf.Max(0f, maaiDistance);
        maaiTolerance         = Mathf.Max(0f, maaiTolerance);
        maaiReactionDelay     = Mathf.Max(0f, maaiReactionDelay);
        maaiReactionJitter    = Mathf.Max(0f, maaiReactionJitter);
        maaiMoveSpeed         = Mathf.Max(0f, maaiMoveSpeed);
        postAttackRetreatChance = Mathf.Clamp01(postAttackRetreatChance);
        retreatSpeed          = Mathf.Max(0f, retreatSpeed);
        chargeJitter          = Mathf.Max(0f, chargeJitter);
        hesitationChance      = Mathf.Clamp01(hesitationChance);
        hesitationDuration    = Mathf.Max(0f, hesitationDuration);
        lureAttackChance      = Mathf.Clamp01(lureAttackChance);
        lureMinRetreatTime    = Mathf.Max(0f, lureMinRetreatTime);
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// 外部（テストUI等）からタイプを切り替える。
    /// ガードゲージの maxGuard もタイプに合わせて更新する。
    /// </summary>
    public void SetEnemyType(EnemyType type)
    {
        enemyType = type;

        // CharacterStats にタイプ別パラメーターを書き込み、各システムへ反映する
        CharacterStats stats = GetComponent<CharacterStats>();
        if (stats == null) stats = gameObject.AddComponent<CharacterStats>();

        ApplyStatsForType(type, stats);
        stats.ApplyToCombatStateMachine(); // attackDuration → CombatStateMachine2D
        stats.ApplyMoveSpeed();            // moveSpeed → EnemyRandomCombatAI2D.moveSpeed

        if (guardGauge != null)
            ApplyGuardStatsForType(type, guardGauge);

        ScheduleNextAction();

        Debug.Log($"Enemy type changed to: {type} | atk={stats.attackDuration}s " +
                  $"spd={stats.moveSpeed} guardDmg={stats.guardDamageDealt} " +
                  $"parry={stats.parryWindowSize:F2}s", this);
    }

    /// <summary>タイプ別の数値を CharacterStats と AI フィールドに書き込む。</summary>
    private void ApplyStatsForType(EnemyType type, CharacterStats stats)
    {
        switch (type)
        {
            case EnemyType.ParrySpammer:
                // 辛抱強く待ち、タメが長くリズムが読みにくい
                stats.attackDuration   = 0.80f;
                stats.moveSpeed        = 1.0f;
                stats.guardDamageDealt = 1;
                stats.parryWindowSize  = 0.45f;
                feintReactionChance    = 0.80f;
                dodgeChance            = 0f;
                hesitationChance       = 0.55f; // 頻繁にタメを入れてリズムを崩す
                chargeJitter           = 0.55f; // 踏み込み時間のばらつき大
                lureAttackChance       = 0.35f;
                lureMinRetreatTime     = 0.40f;
                break;

            case EnemyType.Attacker:
                // 強引に突っ込む。タメなし、引き込みも少ない
                stats.attackDuration   = 0.50f;
                stats.moveSpeed        = 1.4f;
                stats.guardDamageDealt = 2;
                stats.parryWindowSize  = 0.10f;
                feintReactionChance    = 0.20f;
                dodgeChance            = 0f;
                hesitationChance       = 0.10f; // ほぼタメなし（読みやすいが速い）
                chargeJitter           = 0.20f;
                lureAttackChance       = 0.20f;
                lureMinRetreatTime     = 0.30f;
                break;

            case EnemyType.Feinter:
                // 騙しが得意。タメが多く、アクションが読みにくい
                stats.attackDuration   = 0.65f;
                stats.moveSpeed        = 1.2f;
                stats.guardDamageDealt = 1;
                stats.parryWindowSize  = 0.20f;
                feintReactionChance    = 0.30f;
                dodgeChance            = 0f;
                hesitationChance       = 0.65f; // 高頻度でタメを入れる
                chargeJitter           = 0.65f; // 踏み込みタイミングが最もバラつく
                lureAttackChance       = 0.40f;
                lureMinRetreatTime     = 0.35f;
                break;

            case EnemyType.Rusher:
                // 考えずに突っ込む。タメなし・引き込みなし・正直すぎる
                stats.attackDuration   = 0.45f;
                stats.moveSpeed        = 1.5f;
                stats.guardDamageDealt = 1;
                stats.parryWindowSize  = 0.08f;
                feintReactionChance    = 0.10f;
                dodgeChance            = 0f;
                hesitationChance       = 0.0f;  // 全くタメなし（最も読みやすい）
                chargeJitter           = 0.10f;
                lureAttackChance       = 0.10f; // 引き込みもほぼしない
                lureMinRetreatTime     = 0.50f;
                break;

            case EnemyType.Careful:
                // 慎重派。じっくり待ち、引き込みが得意。フェイントに反応しやすい
                stats.attackDuration   = 0.75f;
                stats.moveSpeed        = 0.8f;
                stats.guardDamageDealt = 1;
                stats.parryWindowSize  = 0.30f;
                feintReactionChance    = 0.60f;
                dodgeChance            = 0f;
                hesitationChance       = 0.50f; // 頻繁に止まる
                chargeJitter           = 0.50f;
                lureAttackChance       = 0.65f; // 引き込みが最も得意
                lureMinRetreatTime     = 0.30f; // 少し下がっただけで反転
                break;

            case EnemyType.Dodger:
                // 後退が主戦法 → 引き込みが最高得意。タメは中程度。
                stats.attackDuration   = 0.55f;
                stats.moveSpeed        = 2.0f;
                stats.guardDamageDealt = 1;
                stats.parryWindowSize  = 0.15f;
                feintReactionChance    = 0.15f;
                dodgeChance            = 0.75f;
                dodgeSpeed             = 3.0f;
                dodgeDuration          = 0.50f;
                hesitationChance       = 0.30f;
                chargeJitter           = 0.30f;
                lureAttackChance       = 0.75f; // 後退からの反転が最も得意
                lureMinRetreatTime     = 0.25f; // 少し下がっただけで素早く反転
                break;
        }
    }

    // -------------------------------------------------------------------------

    private void Update()
    {
        if (enemyStateMachine == null) return;

        // ── スタンバイモード（待機中）──────────────────────────────────────────
        if (IsStandby)
        {
            // 安全網：プレイヤーに近づきすぎたら強制的に離れる（X軸のみ）
            if (targetTransform != null)
            {
                float dxToPlayer = transform.position.x - targetTransform.position.x;
                if (Mathf.Abs(dxToPlayer) < standbyMinPlayerDist)
                {
                    float awayDir = dxToPlayer >= 0f ? 1f : -1f;
                    float escapeX = targetTransform.position.x + awayDir * (standbyMinPlayerDist + 0.8f);
                    MoveTowardX(escapeX, standbyMoveSpeed * 3f);
                    return;
                }
            }
            MoveTowardX(standbyTarget.x, standbyMoveSpeed);
            return;
        }

        ResolveTarget();
        if (targetTransform == null) { StopMovement(); return; }

        // コンボバッファ（Attack 中に残ヒット数があれば次の攻撃を積む）
        if (enemyStateMachine.CurrentStateType == CombatStateType.Attack && pendingComboHits > 0)
            enemyStateMachine.TriggerAttack();

        // 距離変化を記録（引き込み判定：前フレームより近ければ相手が追ってきている）
        float distNow = targetTransform != null
            ? Mathf.Abs(targetTransform.position.x - transform.position.x)
            : 999f;
        bool targetApproachingUs = distNow < prevDistToTarget - 0.005f;
        prevDistToTarget = distNow;

        // Vulnerable / GuardBreak / Dead 中は停止、コンボもキャンセル
        if (IsDamageLocked())
        {
            StopMovement();
            pendingComboHits  = 0;
            hesitating        = false;
            feintPunishQueued = false;
            phase = EnemyAiPhase.Waiting;
            return;
        }

        // Attack / Parry / Feint 中は移動しない
        if (enemyStateMachine.IsMovementLocked || !IsNeutralState())
        {
            StopMovement();
            return;
        }

        // ── 後退フェーズ（引き込み＋間合い取り直し）────────────────────────────
        if (phase == EnemyAiPhase.Retreating)
        {
            // 引き込み判定：後退開始から一定時間後、相手が追ってきていたら反転攻撃
            if (!lureTriggered
                && Time.time - retreatStartTime >= lureMinRetreatTime
                && targetApproachingUs
                && Random.value < lureAttackChance)
            {
                lureTriggered    = true;
                queuedAction     = CombatStateType.Attack;
                pendingComboHits = ChooseComboCount() - 1;
                phase            = EnemyAiPhase.Charging;
                chargeEndTime    = Time.time + counterChargeDuration; // 素早く踏み込む
                Debug.Log($"[{enemyType}] 引き込み成功！反転攻撃 (combo+{pendingComboHits})", this);
                return;
            }

            if (distNow >= maaiDistance - maaiTolerance)
            {
                // 間合いまで戻れた → 次のアクションをスケジュール
                StopMovement();
                ScheduleNextAction();
            }
            else
            {
                MoveAwayFrom(targetTransform.position, retreatSpeed);
            }
            return;
        }

        // 相手の攻撃開始を検知して後退回避
        ReactToDodge();

        // 回避後退中
        if (isDodging)
        {
            if (Time.time < dodgeUntil)
            {
                MoveAwayFrom(targetTransform.position, dodgeSpeed);
                return;
            }
            // 回避終了 → カウンターチャンスとして攻撃へ
            isDodging     = false;
            counterQueued = true;
        }

        // 相手の攻撃終了を検知してカウンター機会を生成
        TrackTargetStateForCounter();

        // 相手のフェイントを検知してパリィしてしまう（だまされ）
        ReactToFeint();

        float distance = Mathf.Abs(targetTransform.position.x - transform.position.x);

        // カウンター攻撃（タイプによって攻撃・フェイント・パリィを混ぜる）
        if (counterQueued)
        {
            counterQueued = false;
            // カウンター時もタイプ別行動選択（攻撃一辺倒にしない）
            // ただし攻撃の重みを高めにして「基本は反撃」の印象を維持
            float aw, pw, fw;
            GetActionWeights(enemyType, out aw, out pw, out fw);
            aw *= 2f; // 攻撃を2倍優先
            float total = aw + pw + fw;
            float roll  = Random.Range(0f, total);
            queuedAction = roll < aw ? CombatStateType.Attack
                         : roll < aw + pw ? CombatStateType.Parry
                         : CombatStateType.Feint;
            pendingComboHits = queuedAction == CombatStateType.Attack ? ChooseComboCount() - 1 : 0;
            phase            = EnemyAiPhase.Charging;
            chargeEndTime    = Time.time + counterChargeDuration;
            Debug.Log($"Enemy counter: {queuedAction} (combo+{pendingComboHits})", this);
        }

        // ── フェイント見切り攻撃（即座・最優先）──────────────────────────────
        // フェイント→Vulnerable を検知した瞬間にフレーム遅延なく攻撃する
        if (feintPunishQueued)
        {
            feintPunishQueued = false;
            if (distance <= approachDistance * 1.3f) // 少し遠くても届く
            {
                queuedAction     = CombatStateType.Attack;
                pendingComboHits = 0; // フェイント見切りは単発（Vulnerable時間が短いため）
                ExecuteChosenAction();
                ScheduleNextAction();
            }
            // 距離が遠い場合はチャンスを見逃す（リアリティ）
            return;
        }

        // チャンス攻撃（ランダム遅延後にVulnerableへ反応）
        if (vulnerableReactArmed && Time.time >= vulnerableReactUntil)
        {
            vulnerableReactArmed = false;
            // まだVulnerable/GuardBreakか確認（硬直が終わっていたら見逃す）
            if (targetActor.CurrentStateType == CombatStateType.Vulnerable
             || targetActor.CurrentStateType == CombatStateType.GuardBreak)
            {
                if (distance <= approachDistance)
                {
                    queuedAction     = CombatStateType.Attack;
                    pendingComboHits = ChooseComboCount() - 1;
                    ExecuteChosenAction();
                    ScheduleNextAction();
                    TryQueueRetreat();
                }
                else
                {
                    MoveToward(targetTransform.position, moveSpeed);
                }
                return;
            }
        }

        // 踏み込みフェーズ
        if (phase == EnemyAiPhase.Charging)
        {
            if (distance > approachDistance)
                MoveToward(targetTransform.position, moveSpeed);
            else if (Time.time >= chargeEndTime)
            {
                ExecuteChosenAction();
                ScheduleNextAction();
                TryQueueRetreat(); // 攻撃後は確率で間合いを取り直す
            }
            else
                StopMovement();
            return;
        }

        // 間合い管理
        float inner = maaiDistance - maaiTolerance;
        float outer = maaiDistance + maaiTolerance;

        if (distance < inner)
        {
            maaiReactionTimer -= Time.deltaTime;
            if (maaiReactionTimer <= 0f) MoveAwayFrom(targetTransform.position, maaiMoveSpeed);
            else StopMovement();
        }
        else if (distance > outer)
        {
            maaiReactionTimer -= Time.deltaTime;
            if (maaiReactionTimer <= 0f) MoveToward(targetTransform.position, maaiMoveSpeed);
            else StopMovement();
        }
        else
        {
            StopMovement();
            ResetMaaiTimer();
        }

        // 攻撃タイミングが来たら踏み込みへ（タメのランダム演出付き）
        if (Time.time >= nextActionTime)
        {
            StopMovement();

            // タメ中ならタイマーを待つ（一瞬止まってリズムを崩す）
            if (hesitating)
            {
                if (Time.time < hesitationEndTime) return;
                hesitating = false; // タメ終了 → そのまま行動選択へ
            }
            else if (Random.value < hesitationChance)
            {
                // 確率でタメを入れる（今回のアクション1回限り）
                hesitating        = true;
                hesitationEndTime = Time.time + hesitationDuration;
                return;
            }

            queuedAction     = ChooseNextAction();
            pendingComboHits = queuedAction == CombatStateType.Attack ? ChooseComboCount() - 1 : 0;
            Debug.Log($"[{enemyType}] chose: {queuedAction} (combo+{pendingComboHits})", this);
            phase         = EnemyAiPhase.Charging;
            // chargeJitter でタイミングをランダム化（踏み込み時間がばらつく）
            chargeEndTime = Time.time + chargeDuration + Random.Range(0f, chargeJitter);
        }
    }

    // -------------------------------------------------------------------------

    public void SetTarget(ICombatStateActor newTarget)
    {
        targetActor     = newTarget;
        targetTransform = (newTarget as Component)?.transform;
    }

    /// <summary>スタンバイモードの切り替え。true=待機、false=戦闘。</summary>
    public void SetStandbyMode(bool standby)
    {
        IsStandby = standby;
        if (standby)
        {
            forceAttackOnce = false;
            ScheduleNextAction();
            StopMovement();
        }
    }

    /// <summary>
    /// 次のアクションを強制的に Attack にする（邪魔攻撃時に呼ぶ）。
    /// SetStandbyMode(false) の直後に呼ぶこと。
    /// </summary>
    public void SetForceAttackOnce()
    {
        forceAttackOnce = true;
        // すぐ踏み込みフェーズへ移行させるためインターバルをリセット
        nextActionTime = Time.time;
    }

    /// <summary>スタンバイ中の目標位置を MultiEnemyManager から設定される。</summary>
    public void SetStandbyTarget(Vector3 target)
    {
        standbyTarget = target;
    }

    // -------------------------------------------------------------------------

    private void TrackTargetStateForCounter()
    {
        if (targetActor == null) return;

        CombatStateType nowTargetState = targetActor.CurrentStateType;

        // 攻撃終了 → カウンター
        bool targetJustFinishedAttack =
            lastTargetState == CombatStateType.Attack
            && nowTargetState != CombatStateType.Attack
            && nowTargetState != CombatStateType.Dead;

        if (targetJustFinishedAttack
            && !counterQueued
            && phase == EnemyAiPhase.Waiting
            && Random.value < counterAttackChance)
        {
            counterQueued = true;
        }

        // フェイント → Vulnerable 遷移を即座に検知して攻撃チャンスを設定
        bool targetFeintBecameVulnerable =
            lastTargetState == CombatStateType.Feint
            && nowTargetState == CombatStateType.Vulnerable;

        if (targetFeintBecameVulnerable && !feintPunishQueued)
        {
            feintPunishQueued = true;
            Debug.Log($"[{enemyType}] フェイント見切り！即攻撃チャンス", this);
        }

        // パリィ空振り / その他 → Vulnerable 遷移：ランダム遅延付きで反応
        // フェイント由来はすでに上で処理しているので除外
        bool targetJustBecameVulnerable =
            lastTargetState != CombatStateType.Vulnerable
            && lastTargetState != CombatStateType.GuardBreak
            && lastTargetState != CombatStateType.Dead
            && lastTargetState != CombatStateType.Feint // フェイント由来は別処理
            && (nowTargetState == CombatStateType.Vulnerable
             || nowTargetState == CombatStateType.GuardBreak);

        if (targetJustBecameVulnerable && !vulnerableReactArmed && !counterQueued)
        {
            // タイプ別の反応確率チェック（全員が反応するわけではない）
            float reactChance = GetVulnerableReactChance();
            if (Random.value < reactChance)
            {
                // ランダム遅延：0.1〜0.6秒後に反応（機械的な即反応を防ぐ）
                float delay = Random.Range(0.10f, 0.60f);
                vulnerableReactUntil = Time.time + delay;
                vulnerableReactArmed = true;
                Debug.Log($"[{enemyType}] Vulnerable検知、{delay:F2}s後に反応", this);
            }
        }

        // Vulnerable が終わったらリセット
        if (vulnerableReactArmed
            && nowTargetState != CombatStateType.Vulnerable
            && nowTargetState != CombatStateType.GuardBreak)
        {
            vulnerableReactArmed = false;
        }

        lastTargetState = nowTargetState;
    }

    /// <summary>タイプ別のVulnerable反応確率。慎重な敵ほど高く、攻撃的な敵は低め。</summary>
    private float GetVulnerableReactChance()
    {
        switch (enemyType)
        {
            case EnemyType.Attacker:     return 0.55f; // 積極的だが見切りは粗い
            case EnemyType.Rusher:       return 0.45f; // 突っ込むが観察力は低い
            case EnemyType.Careful:      return 0.80f; // 慎重で隙を見逃さない
            case EnemyType.ParrySpammer: return 0.70f; // パリィ後のチャンスに敏感
            case EnemyType.Feinter:      return 0.60f;
            case EnemyType.Dodger:       return 0.65f;
            default:                     return 0.55f;
        }
    }

    /// <summary>
    /// 相手が Attack 状態に入った瞬間を検知し、dodgeChance の確率で後退回避を開始する。
    /// 回避中は後退し続け、終了後にカウンター攻撃を試みる。
    /// </summary>
    private void ReactToDodge()
    {
        if (dodgeChance <= 0f || targetActor == null) return;
        if (isDodging) return;
        if (!IsNeutralState()) return;                       // 自分が行動中なら回避できない

        bool targetJustAttacked =
            lastTargetState != CombatStateType.Attack
            && targetActor.CurrentStateType == CombatStateType.Attack;

        if (!targetJustAttacked) return;
        if (Random.value >= dodgeChance) return;

        Debug.Log($"[{enemyType}] Dodge! Back-stepping for {dodgeDuration}s", this);
        isDodging    = true;
        dodgeUntil   = Time.time + dodgeDuration;
        phase        = EnemyAiPhase.Waiting;  // 踏み込みをキャンセル
    }

    /// <summary>
    /// 相手が Feint 状態に入った瞬間を検知し、feintReactionChance の確率でパリィを実行する。
    /// パリィしてしまうと Parry vs Feint ルールで Vulnerable になる（だまされた状態）。
    /// </summary>
    private void ReactToFeint()
    {
        if (targetActor == null) return;

        bool targetJustEnteredFeint =
            lastTargetState != CombatStateType.Feint
            && targetActor.CurrentStateType == CombatStateType.Feint;

        if (!targetJustEnteredFeint) return;
        if (!IsNeutralState()) return;                     // 何か行動中なら反応できない
        if (Random.value >= feintReactionChance) return;   // 確率判定

        Debug.Log($"[{enemyType}] Fooled by feint! TriggerParry (chance={feintReactionChance:P0})", this);

        // 踏み込み中だったらキャンセルしてパリィへ
        phase = EnemyAiPhase.Waiting;
        StopMovement();
        enemyStateMachine.TriggerParry();
    }

    private void OnEnemyComboHitStarted()
    {
        pendingComboHits = Mathf.Max(0, pendingComboHits - 1);
    }

    // -------------------------------------------------------------------------

    private bool IsDamageLocked()
    {
        CombatStateType t = enemyStateMachine.CurrentStateType;
        return t == CombatStateType.Vulnerable
            || t == CombatStateType.GuardBreak
            || t == CombatStateType.Dead;
    }

    private bool IsNeutralState()
    {
        return enemyStateMachine.CurrentStateType == CombatStateType.Guard
            || enemyStateMachine.CurrentStateType == CombatStateType.Idle;
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

    // -------------------------------------------------------------------------
    // タイプ別パラメーター
    // -------------------------------------------------------------------------

    /// <summary>タイプに応じた行動インターバル倍率を返す。</summary>
    private float GetIntervalMultiplierForType(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Feinter: return 0.5f;
            case EnemyType.Rusher:  return 0.5f;
            case EnemyType.Careful: return 2.0f;
            case EnemyType.Dodger:  return 0.7f; // やや素早め
            default:                return 1.0f;
        }
    }

    /// <summary>
    /// タイプに応じたガード関連パラメーターを GuardGauge に書き込み、ゲージを満タンにリセットする。
    ///
    /// ガード耐久力の目安（プレイヤー guardDamageDealt=1 で連続ガードした場合）:
    ///   Rusher / Feinter : maxGuard=3, recovery遅い → 3連続ガードでブレイク ★
    ///   Attacker / Careful: maxGuard=5, recovery普通 → 5発以上必要
    ///   ParrySpammer      : maxGuard=8, recovery速い → ほぼ崩れない
    ///   Dodger            : maxGuard=2, recovery遅い → 2連続でブレイク（回避前提）
    /// </summary>
    private static void ApplyGuardStatsForType(EnemyType type, GuardGauge gauge)
    {
        switch (type)
        {
            case EnemyType.ParrySpammer:
                // パリィで凌ぐのが得意 → ガードも鉄壁
                gauge.recoveryPerSecond          = 2.0f; // 回復が速く削りにくい
                gauge.guardBreakVulnerableDuration = 0.8f; // 崩れても隙が小さい
                gauge.ResetWithMaxGuard(8);
                break;

            case EnemyType.Attacker:
                // 攻め主体でガードはそこそこ
                gauge.recoveryPerSecond          = 1.0f;
                gauge.guardBreakVulnerableDuration = 1.2f;
                gauge.ResetWithMaxGuard(5);
                break;

            case EnemyType.Feinter:
                // フェイントで惑わす。ガードは薄く、崩されると大きな隙 ★
                gauge.recoveryPerSecond          = 0.4f; // 遅い → 連続ガードで削られる
                gauge.guardBreakVulnerableDuration = 1.6f; // 崩れると長い隙
                gauge.ResetWithMaxGuard(3);
                break;

            case EnemyType.Rusher:
                // 攻め一辺倒でガードが雑 ★
                gauge.recoveryPerSecond          = 0.4f; // 遅い → 3連続ガードでブレイク
                gauge.guardBreakVulnerableDuration = 1.6f;
                gauge.ResetWithMaxGuard(3);
                break;

            case EnemyType.Careful:
                // 慎重派でガード管理が上手い
                gauge.recoveryPerSecond          = 1.5f; // 回復が速い
                gauge.guardBreakVulnerableDuration = 1.0f;
                gauge.ResetWithMaxGuard(5);
                break;

            case EnemyType.Dodger:
                // 回避が主戦法 → ガード耐久は最低。崩されると長い隙
                gauge.recoveryPerSecond          = 0.3f;
                gauge.guardBreakVulnerableDuration = 2.0f;
                gauge.ResetWithMaxGuard(2);
                break;

            default:
                gauge.recoveryPerSecond          = 1.0f;
                gauge.guardBreakVulnerableDuration = 1.2f;
                gauge.ResetWithMaxGuard(3);
                break;
        }
    }

    private CombatStateType ChooseNextAction()
    {
        // 邪魔攻撃フラグが立っていたら必ず Attack
        if (forceAttackOnce)
        {
            forceAttackOnce = false;
            return CombatStateType.Attack;
        }

        float attackW, parryW, feintW;
        GetActionWeights(enemyType, out attackW, out parryW, out feintW);

        // 同じアクションが2回以上続いている場合は重みを大幅に下げてパリィを読まれにくくする
        if (actionRepeatCount >= 2)
        {
            if (lastChosenAction == CombatStateType.Attack) attackW *= 0.25f;
            else if (lastChosenAction == CombatStateType.Parry)  parryW  *= 0.25f;
            else if (lastChosenAction == CombatStateType.Feint)  feintW  *= 0.25f;
        }

        float total = Mathf.Max(attackW + parryW + feintW, 0.001f);
        float roll  = Random.Range(0f, total);

        CombatStateType chosen;
        if (roll < attackW)               chosen = CombatStateType.Attack;
        else if (roll < attackW + parryW) chosen = CombatStateType.Parry;
        else                              chosen = CombatStateType.Feint;

        // 履歴更新
        if (chosen == lastChosenAction) actionRepeatCount++;
        else { actionRepeatCount = 1; lastChosenAction = chosen; }

        return chosen;
    }

    private static void GetActionWeights(EnemyType type,
        out float attackW, out float parryW, out float feintW)
    {
        switch (type)
        {
            case EnemyType.ParrySpammer:
                attackW = 0.20f; parryW = 0.70f; feintW = 0.10f; break;
            case EnemyType.Attacker:
                attackW = 0.80f; parryW = 0.10f; feintW = 0.10f; break;
            case EnemyType.Feinter:
                attackW = 0.20f; parryW = 0.10f; feintW = 0.70f; break;
            case EnemyType.Rusher:
                attackW = 0.60f; parryW = 0.20f; feintW = 0.20f; break;
            case EnemyType.Careful:
                attackW = 0.33f; parryW = 0.33f; feintW = 0.34f; break;
            case EnemyType.Dodger:
                // 回避後カウンターが主戦法なので、待機行動は攻撃多め・パリィ少なめ
                attackW = 0.65f; parryW = 0.15f; feintW = 0.20f; break;
            default:
                attackW = 0.33f; parryW = 0.33f; feintW = 0.34f; break;
        }
    }

    private int ChooseComboCount()
    {
        float total = combo1Weight + combo2Weight + combo3Weight;
        if (total <= 0f) return 1;

        float roll = Random.Range(0f, total);
        if (roll < combo1Weight)                return 1;
        if (roll < combo1Weight + combo2Weight) return 2;
        return 3;
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// 攻撃実行後、確率で間合い取り直し（Retreating フェーズ）に移行する。
    /// ScheduleNextAction() の後に呼ぶこと（Waiting を上書きする形で Retreating をセット）。
    /// </summary>
    private void TryQueueRetreat()
    {
        if (queuedAction != CombatStateType.Attack) return;
        if (Random.value >= postAttackRetreatChance) return;

        phase            = EnemyAiPhase.Retreating;
        retreatStartTime = Time.time;
        lureTriggered    = false;
        // 後退開始時の距離を基準に（以降フレームで縮まれば追ってきていると判断）
        if (targetTransform != null)
            prevDistToTarget = Mathf.Abs(targetTransform.position.x - transform.position.x);
        Debug.Log($"[{enemyType}] Post-attack retreat start (maai={maaiDistance:F1})", this);
    }

    /// <summary>X軸のみ目標へ移動する（スタンバイ移動用）。</summary>
    private void MoveTowardX(float targetX, float speed)
    {
        float dx = targetX - transform.position.x;
        if (Mathf.Abs(dx) <= standbyArrivalRadius)
        {
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }
        float dir = Mathf.Sign(dx);
        if (rb != null) rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
        else transform.position += new Vector3(dir * speed * Time.deltaTime, 0f, 0f);
    }

    private void MoveToward(Vector3 destination, float speed)
    {
        float deltaX = destination.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= 0.01f) { StopMovement(); return; }
        float dir = Mathf.Sign(deltaX);
        if (rb != null) rb.linearVelocity = new Vector2(dir * speed, 0f);
        else transform.position += new Vector3(dir * speed * Time.deltaTime, 0f, 0f);
    }

    private void MoveAwayFrom(Vector3 from, float speed)
    {
        float deltaX = transform.position.x - from.x;
        float dir = Mathf.Abs(deltaX) <= 0.01f ? 1f : Mathf.Sign(deltaX);
        if (rb != null) rb.linearVelocity = new Vector2(dir * speed, 0f);
        else transform.position += new Vector3(dir * speed * Time.deltaTime, 0f, 0f);
    }

    private void StopMovement()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    private void ScheduleNextAction()
    {
        float mult     = GetIntervalMultiplierForType(enemyType);
        nextActionTime = Time.time + Random.Range(minActionInterval, maxActionInterval) * mult;
        phase          = EnemyAiPhase.Waiting;
        chargeEndTime  = 0f;
        hesitating     = false;   // タメをリセット（次アクションで再判定）
        // ※ pendingComboHits はここでリセットしない → コンボが途中で中断されない
        ResetMaaiTimer();
    }

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

    private enum EnemyAiPhase { Waiting, Charging, Retreating }
}
