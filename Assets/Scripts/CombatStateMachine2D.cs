using System.Collections.Generic;
using UnityEngine;

public abstract class CombatStateMachine2D : MonoBehaviour, ICombatStateActor
{
    public float idleDuration = -1f;
    public float guardDuration = -1f;
    public float attackDuration = 1.0f;   // 1回の攻撃ロック時間
    public float parryDuration = 0.4f;
    [Tooltip("パリィが空振りしたとき（攻撃が来なかった）に発生する隙の秒数。0 = 即フリー")]
    public float parryWhiffVulnerableDuration = 0.25f;
    [Tooltip("フェイントが空振りしたとき（誰も反応しなかった）に発生する隙の秒数。0 = 即フリー")]
    public float feintWhiffVulnerableDuration = 0.30f;
    public float feintDuration = 0.3f;
    public float vulnerableDuration = 0.5f;
    public float guardBreakDuration = 0.5f;
    public float deadDuration = -1f;

    [Header("Judgment Timing")]
    public float attackJudgmentTime = 0.25f;
    public float parryJudgmentTime  = 0.3f;

    [Header("Combo")]
    [Tooltip("連続攻撃の最大回数")]
    public int maxComboCount = 3;

    // ── パブリックイベント ──────────────────────────────────────────────────

    /// <summary>Attack / Parry の判定タイミングで発火。</summary>
    public System.Action OnJudgmentFired;

    /// <summary>コンボの2撃目・3撃目が始まるときに発火（アニメーション再生に使う）。</summary>
    public System.Action OnComboAttackStarted;

    /// <summary>ステートが変わった瞬間に発火。アニメーターはこれを購読して即座に切り替える。</summary>
    public System.Action<CombatStateType> OnStateChanged;

    // ── プロパティ ──────────────────────────────────────────────────────────

    public CombatStateType CurrentStateType => currentState?.StateType ?? CombatStateType.Guard;
    public float LastActionTimestamp { get; private set; }
    public int   CurrentComboCount   { get; private set; }

    public bool IsAttackJudgmentFired { get; private set; }

    public bool IsMovementLocked => CurrentStateType == CombatStateType.Attack
        || CurrentStateType == CombatStateType.Parry
        || CurrentStateType == CombatStateType.Feint
        || CurrentStateType == CombatStateType.Vulnerable
        || CurrentStateType == CombatStateType.GuardBreak
        || CurrentStateType == CombatStateType.Dead;

    public bool IsActionLocked => CurrentStateType == CombatStateType.Attack
        || CurrentStateType == CombatStateType.Parry
        || CurrentStateType == CombatStateType.Feint;

    protected bool HasPrimaryInput { get; private set; }

    // ── 内部フィールド ──────────────────────────────────────────────────────

    private readonly Dictionary<CombatStateType, CombatState> states
        = new Dictionary<CombatStateType, CombatState>();
    private CombatState currentState;
    private float guardUnavailableUntil;

    internal float vulnerableDurationOverride = -1f;
    internal bool  attackBuffered;          // Attack 中に次の攻撃が入力された

    // =========================================================================

    protected virtual void Awake()
    {
        states[CombatStateType.Idle]        = new IdleState(this);
        states[CombatStateType.Guard]       = new GuardState(this);
        states[CombatStateType.Attack]      = new AttackState(this);
        states[CombatStateType.Parry]       = new ParryState(this);
        states[CombatStateType.Feint]       = new FeintState(this);
        states[CombatStateType.Vulnerable]  = new VulnerableState(this);
        states[CombatStateType.GuardBreak]  = new GuardBreakState(this);
        states[CombatStateType.Dead]        = new DeadState(this);
    }

    protected virtual void Start()
    {
        ChangeState(CombatStateType.Guard);
    }

    protected virtual void Update()
    {
        HasPrimaryInput = QueryPrimaryInput();
        currentState?.Tick();
    }

    public void ChangeState(CombatStateType nextStateType)
    {
        if (!states.TryGetValue(nextStateType, out CombatState nextState))
        {
            Debug.LogWarning($"State not found: {nextStateType}", this);
            return;
        }

        if (currentState == nextState) return;

        CombatStateType previousStateType = currentState?.StateType ?? CombatStateType.Guard;
        currentState?.Exit();
        currentState = nextState;
        currentState.Enter();

        Debug.Log($"{name} State: {previousStateType} -> {currentState.StateType}", this);
        OnStateChanged?.Invoke(currentState.StateType);
        if (currentState.StateType == CombatStateType.Dead)
            Debug.Log($"{name} Dead", this);
    }

    public void CancelToNeutral()
    {
        ChangeState(GetNeutralState());
    }

    // =========================================================================
    // アクショントリガー
    // =========================================================================

    public virtual void TriggerAttack()
    {
        if (IsMovementLocked) return;

        // ── Attack 中 → コンボバッファ ──
        if (CurrentStateType == CombatStateType.Attack)
        {
            if (CurrentComboCount < maxComboCount)
                attackBuffered = true;
            return;
        }

        // Parry / Feint 中は不可
        if (IsActionLocked) return;

        CurrentComboCount = 1;
        attackBuffered    = false;
        LastActionTimestamp = Time.time;
        ChangeState(CombatStateType.Attack);
    }

    public virtual void TriggerParry()
    {
        if (IsActionLocked || IsMovementLocked) return;
        LastActionTimestamp = Time.time;
        ChangeState(CombatStateType.Parry);
    }

    public virtual void TriggerFeint()
    {
        if (IsActionLocked || IsMovementLocked) return;
        LastActionTimestamp = Time.time;
        ChangeState(CombatStateType.Feint);
    }

    public virtual void TriggerVulnerable()
    {
        if (CurrentStateType == CombatStateType.Dead) return;
        ResetCombo();
        vulnerableDurationOverride = -1f;
        ChangeState(CombatStateType.Vulnerable);
    }

    public void TriggerVulnerableWithDuration(float duration)
    {
        if (CurrentStateType == CombatStateType.Dead) return;
        ResetCombo();
        vulnerableDurationOverride = duration;
        ChangeState(CombatStateType.Vulnerable);
    }

    public virtual void TriggerGuardBreak(float guardLockDuration)
    {
        if (CurrentStateType == CombatStateType.Dead) return;
        ResetCombo();
        guardUnavailableUntil = Mathf.Max(guardUnavailableUntil, Time.time + guardLockDuration);
        ChangeState(CombatStateType.GuardBreak);
    }

    /// <summary>
    /// ガードブレイク時にパリィ成功と同じ Vulnerable 状態に入る。
    /// ガード不可タイマーは維持しつつ、見た目・挙動を Vulnerable と統一する。
    /// </summary>
    public void TriggerGuardBreakAsVulnerable(float lockDuration, float vulnerableDuration)
    {
        if (CurrentStateType == CombatStateType.Dead) return;
        ResetCombo();
        guardUnavailableUntil      = Mathf.Max(guardUnavailableUntil, Time.time + lockDuration);
        vulnerableDurationOverride = vulnerableDuration;
        ChangeState(CombatStateType.Vulnerable);
    }

    public virtual void TriggerDead()
    {
        ChangeState(CombatStateType.Dead);
    }

    public void ResetForRound()
    {
        guardUnavailableUntil     = 0f;
        LastActionTimestamp       = 0f;
        IsAttackJudgmentFired     = false;
        vulnerableDurationOverride = -1f;
        ResetCombo();
        // Rigidbody2D を Dynamic に戻す
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
        // コライダーをトリガーから通常に戻す
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = false;
        ChangeState(CombatStateType.Guard);
    }

    // =========================================================================

    public float GetDuration(CombatStateType stateType)
    {
        switch (stateType)
        {
            case CombatStateType.Idle:        return idleDuration;
            case CombatStateType.Guard:       return guardDuration;
            case CombatStateType.Attack:      return attackDuration;
            case CombatStateType.Parry:       return parryDuration;
            case CombatStateType.Feint:       return feintDuration;
            case CombatStateType.Vulnerable:  return vulnerableDuration;
            case CombatStateType.GuardBreak:  return guardBreakDuration;
            case CombatStateType.Dead:        return deadDuration;
            default:                          return -1f;
        }
    }

    private void ResetCombo()
    {
        CurrentComboCount = 0;
        attackBuffered    = false;
    }

    protected bool CanEnterGuard()     => Time.time >= guardUnavailableUntil;
    protected CombatStateType GetNeutralState() => CanEnterGuard() ? CombatStateType.Guard : CombatStateType.Idle;

    protected virtual bool QueryPrimaryInput() => false;

    // =========================================================================
    // 内部ステートクラス
    // =========================================================================

    private abstract class CombatState
    {
        protected readonly CombatStateMachine2D stateMachine;
        protected float elapsedTime;

        public abstract CombatStateType StateType { get; }

        protected CombatState(CombatStateMachine2D sm) { stateMachine = sm; }

        public virtual void Enter()  { elapsedTime = 0f; }
        public virtual void Tick()   { elapsedTime += Time.deltaTime; }
        public virtual void Exit()   { }

        protected bool HasDurationElapsed()
        {
            float d = stateMachine.GetDuration(StateType);
            return d >= 0f && elapsedTime >= d;
        }
    }

    private sealed class IdleState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Idle;
        public IdleState(CombatStateMachine2D sm) : base(sm) { }

        public override void Tick()
        {
            base.Tick();
            if (!stateMachine.HasPrimaryInput || HasDurationElapsed())
                stateMachine.ChangeState(stateMachine.GetNeutralState());
        }
    }

    private sealed class GuardState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Guard;
        public GuardState(CombatStateMachine2D sm) : base(sm) { }

        public override void Tick()
        {
            base.Tick();
            if (stateMachine.HasPrimaryInput || HasDurationElapsed())
                stateMachine.ChangeState(CombatStateType.Idle);
        }
    }

    /// <summary>
    /// Attack：attackDuration 秒ロック、attackJudgmentTime で判定発火。
    /// バッファがあれば最大 maxComboCount 回まで同ステートを継続する。
    /// </summary>
    private sealed class AttackState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Attack;
        private bool judgmentFired;

        public AttackState(CombatStateMachine2D sm) : base(sm) { }

        public override void Enter()
        {
            base.Enter();
            judgmentFired = false;
            stateMachine.IsAttackJudgmentFired = false;
        }

        public override void Tick()
        {
            base.Tick();

            // 判定（1回のみ）
            if (!judgmentFired && elapsedTime >= stateMachine.attackJudgmentTime)
            {
                judgmentFired = true;
                stateMachine.OnJudgmentFired?.Invoke();   // Resolve() を先に呼ぶ
                stateMachine.IsAttackJudgmentFired = true; // その後に「発火済み」フラグを立てる
            }

            if (HasDurationElapsed())
            {
                if (stateMachine.attackBuffered &&
                    stateMachine.CurrentComboCount < stateMachine.maxComboCount)
                {
                    // ── コンボ継続：同ステートのままタイマーをリセット ──
                    stateMachine.CurrentComboCount++;
                    stateMachine.attackBuffered        = false;
                    stateMachine.LastActionTimestamp   = Time.time;
                    stateMachine.IsAttackJudgmentFired = false;
                    judgmentFired = false;
                    elapsedTime   = 0f;
                    stateMachine.OnComboAttackStarted?.Invoke();
                }
                else
                {
                    // ── コンボ終了 ──
                    stateMachine.CurrentComboCount = 0;
                    stateMachine.attackBuffered    = false;
                    stateMachine.ChangeState(stateMachine.GetNeutralState());
                }
            }
        }

        public override void Exit()
        {
            judgmentFired = false;
            stateMachine.IsAttackJudgmentFired = false;
        }
    }

    private sealed class ParryState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Parry;
        private bool judgmentFired;

        public ParryState(CombatStateMachine2D sm) : base(sm) { }

        public override void Enter()  { base.Enter(); judgmentFired = false; }

        public override void Tick()
        {
            base.Tick();
            if (!judgmentFired && elapsedTime >= stateMachine.parryJudgmentTime)
            {
                judgmentFired = true;
                stateMachine.OnJudgmentFired?.Invoke();
            }
            if (HasDurationElapsed())
            {
                // まだ Parry 状態のまま = 空振り（攻撃が来なかった / NoEffect）
                // → 短い隙を発生させて連打を抑制する
                if (stateMachine.CurrentStateType == CombatStateType.Parry)
                {
                    if (stateMachine.parryWhiffVulnerableDuration > 0f)
                    {
                        Debug.Log($"{stateMachine.name} Parry whiff → Vulnerable {stateMachine.parryWhiffVulnerableDuration:F2}s", stateMachine);
                        stateMachine.TriggerVulnerableWithDuration(stateMachine.parryWhiffVulnerableDuration);
                    }
                    else
                    {
                        stateMachine.ChangeState(stateMachine.GetNeutralState());
                    }
                }
                // else: 判定解決ですでに別の状態に遷移済み → 何もしない
            }
        }

        public override void Exit() { judgmentFired = false; }
    }

    private sealed class FeintState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Feint;
        public FeintState(CombatStateMachine2D sm) : base(sm) { }

        public override void Tick()
        {
            base.Tick();
            if (HasDurationElapsed())
            {
                // フェイント終了後に短い隙を発生させる
                if (stateMachine.feintWhiffVulnerableDuration > 0f)
                {
                    Debug.Log($"{stateMachine.name} Feint → Vulnerable {stateMachine.feintWhiffVulnerableDuration:F2}s", stateMachine);
                    stateMachine.TriggerVulnerableWithDuration(stateMachine.feintWhiffVulnerableDuration);
                }
                else
                    stateMachine.ChangeState(stateMachine.GetNeutralState());
            }
        }
    }

    private sealed class VulnerableState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Vulnerable;
        private float activeDuration;

        public VulnerableState(CombatStateMachine2D sm) : base(sm) { }

        public override void Enter()
        {
            base.Enter();
            activeDuration = stateMachine.vulnerableDurationOverride > 0f
                ? stateMachine.vulnerableDurationOverride
                : stateMachine.vulnerableDuration;
            stateMachine.vulnerableDurationOverride = -1f;
        }

        public override void Tick()
        {
            base.Tick();
            if (activeDuration >= 0f && elapsedTime >= activeDuration)
                stateMachine.ChangeState(stateMachine.GetNeutralState());
        }
    }

    private sealed class GuardBreakState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.GuardBreak;
        public GuardBreakState(CombatStateMachine2D sm) : base(sm) { }

        public override void Tick()
        {
            base.Tick();
            if (HasDurationElapsed())
                stateMachine.ChangeState(stateMachine.GetNeutralState());
        }
    }

    private sealed class DeadState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Dead;
        public DeadState(CombatStateMachine2D sm) : base(sm) { }

        public override void Enter()
        {
            base.Enter();
            // Kinematic にして重力・物理を止め、その場に留まらせる
            Rigidbody2D rb = stateMachine.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            // トリガーにして他のキャラが通り抜けられるようにする
            Collider2D col = stateMachine.GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        public override void Tick()
        {
            base.Tick();
            if (HasDurationElapsed())
                Debug.Log("Dead state duration ended, remaining in Dead state.", stateMachine);
        }
    }
}

public interface ICombatStateActor
{
    CombatStateType CurrentStateType { get; }
    float LastActionTimestamp { get; }
    void ChangeState(CombatStateType nextStateType);
}

public enum CombatStateType
{
    Idle, Guard, Attack, Parry, Feint, Vulnerable, GuardBreak, Dead
}
