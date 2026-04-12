using System.Collections.Generic;
using UnityEngine;

public abstract class CombatStateMachine2D : MonoBehaviour, ICombatStateActor
{
    public float idleDuration = -1f;
    public float guardDuration = -1f;
    public float attackDuration = 1.0f;   // 攻撃：1秒ロック
    public float parryDuration = 0.8f;    // パリィ：0.8秒ロック
    public float feintDuration = 0.3f;    // フェイント：0.3秒ロック
    public float vulnerableDuration = 0.5f;
    public float guardBreakDuration = 0.5f;
    public float deadDuration = -1f;

    [Header("Judgment Timing")]
    public float attackJudgmentTime = 0.25f;  // Attack 開始から判定が発生するまでの秒数（Jab 3フレーム×8fps のヒット瞬間に合わせる）
    public float parryJudgmentTime  = 0.3f;   // Parry  開始から判定が発生するまでの秒数

    /// <summary>
    /// 判定タイミングで発火するイベント。
    /// ProximityJankenBattle2D がこれを購読して解決処理を行う。
    /// </summary>
    public System.Action OnJudgmentFired;

    /// <summary>
    /// Attack 判定が発火済みかどうか（ステート終了まで true を保持）。
    /// パリィ遅延チェックに使用する。
    /// </summary>
    public bool IsAttackJudgmentFired { get; private set; }

    public CombatStateType CurrentStateType => currentState?.StateType ?? CombatStateType.Guard;
    public float LastActionTimestamp { get; private set; }

    public bool IsMovementLocked => CurrentStateType == CombatStateType.Vulnerable
        || CurrentStateType == CombatStateType.GuardBreak
        || CurrentStateType == CombatStateType.Dead;

    /// <summary>
    /// Attack / Parry / Feint のモーション中は true。
    /// この間は新しいアクション入力を無視する（バッファなし）。
    /// </summary>
    public bool IsActionLocked => CurrentStateType == CombatStateType.Attack
        || CurrentStateType == CombatStateType.Parry
        || CurrentStateType == CombatStateType.Feint;

    protected bool HasPrimaryInput { get; private set; }

    private readonly Dictionary<CombatStateType, CombatState> states = new Dictionary<CombatStateType, CombatState>();
    private CombatState currentState;
    private float guardUnavailableUntil;

    // VulnerableState に渡す一時的な持続時間オーバーライド（-1 = 使用しない）
    internal float vulnerableDurationOverride = -1f;

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

        if (currentState == nextState)
        {
            return;
        }

        CombatStateType previousStateType = currentState?.StateType ?? CombatStateType.Guard;
        currentState?.Exit();
        currentState = nextState;
        currentState.Enter();

        Debug.Log($"{name} State: {previousStateType} -> {currentState.StateType}", this);
        if (currentState.StateType == CombatStateType.Dead)
        {
            Debug.Log($"{name} Dead", this);
        }
    }

    /// <summary>
    /// アクションロックを即座に解除してニュートラル（Guard / Idle）に戻す。
    /// パリィ成功時に攻撃側のロックを強制解除するために使用。
    /// </summary>
    public void CancelToNeutral()
    {
        ChangeState(GetNeutralState());
    }

    public virtual void TriggerAttack()
    {
        if (IsActionLocked || IsMovementLocked)
        {
            return;
        }

        LastActionTimestamp = Time.time;
        ChangeState(CombatStateType.Attack);
    }

    public virtual void TriggerParry()
    {
        if (IsActionLocked || IsMovementLocked)
        {
            return;
        }

        LastActionTimestamp = Time.time;
        ChangeState(CombatStateType.Parry);
    }

    public virtual void TriggerFeint()
    {
        if (IsActionLocked || IsMovementLocked)
        {
            return;
        }

        LastActionTimestamp = Time.time;
        ChangeState(CombatStateType.Feint);
    }

    public virtual void TriggerVulnerable()
    {
        if (CurrentStateType == CombatStateType.Dead)
        {
            return;
        }

        vulnerableDurationOverride = -1f;
        ChangeState(CombatStateType.Vulnerable);
    }

    /// <summary>
    /// 指定した持続時間で Vulnerable 状態に入る。
    /// パリィ成功時に防御側へ短い硬直を与えるために使用。
    /// </summary>
    public void TriggerVulnerableWithDuration(float duration)
    {
        if (CurrentStateType == CombatStateType.Dead)
        {
            return;
        }

        vulnerableDurationOverride = duration;
        ChangeState(CombatStateType.Vulnerable);
    }

    public virtual void TriggerGuardBreak(float guardLockDuration)
    {
        if (CurrentStateType == CombatStateType.Dead)
        {
            return;
        }

        guardUnavailableUntil = Mathf.Max(guardUnavailableUntil, Time.time + guardLockDuration);
        ChangeState(CombatStateType.GuardBreak);
    }

    public virtual void TriggerDead()
    {
        ChangeState(CombatStateType.Dead);
    }

    public void ResetForRound()
    {
        guardUnavailableUntil = 0f;
        LastActionTimestamp = 0f;
        IsAttackJudgmentFired = false;
        vulnerableDurationOverride = -1f;
        ChangeState(CombatStateType.Guard);
    }

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

    protected bool CanEnterGuard()
    {
        return Time.time >= guardUnavailableUntil;
    }

    protected CombatStateType GetNeutralState()
    {
        return CanEnterGuard() ? CombatStateType.Guard : CombatStateType.Idle;
    }

    protected virtual bool QueryPrimaryInput()
    {
        return false;
    }

    // =========================================================================
    // 内部ステートクラス
    // =========================================================================

    private abstract class CombatState
    {
        protected readonly CombatStateMachine2D stateMachine;
        protected float elapsedTime;

        public abstract CombatStateType StateType { get; }

        protected CombatState(CombatStateMachine2D stateMachine)
        {
            this.stateMachine = stateMachine;
        }

        public virtual void Enter()
        {
            elapsedTime = 0f;
        }

        public virtual void Tick()
        {
            elapsedTime += Time.deltaTime;
        }

        public virtual void Exit()
        {
        }

        protected bool HasDurationElapsed()
        {
            float duration = stateMachine.GetDuration(StateType);
            return duration >= 0f && elapsedTime >= duration;
        }
    }

    private sealed class IdleState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Idle;

        public IdleState(CombatStateMachine2D stateMachine) : base(stateMachine) { }

        public override void Tick()
        {
            base.Tick();

            if (!stateMachine.HasPrimaryInput)
            {
                stateMachine.ChangeState(stateMachine.GetNeutralState());
                return;
            }

            if (HasDurationElapsed())
            {
                stateMachine.ChangeState(stateMachine.GetNeutralState());
            }
        }
    }

    private sealed class GuardState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Guard;

        public GuardState(CombatStateMachine2D stateMachine) : base(stateMachine) { }

        public override void Tick()
        {
            base.Tick();

            if (stateMachine.HasPrimaryInput)
            {
                stateMachine.ChangeState(CombatStateType.Idle);
                return;
            }

            if (HasDurationElapsed())
            {
                stateMachine.ChangeState(CombatStateType.Idle);
            }
        }
    }

    /// <summary>
    /// Attack：1秒ロック / attackJudgmentTime 秒時点で OnJudgmentFired を1回発火
    /// </summary>
    private sealed class AttackState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Attack;
        private bool judgmentFired;

        public AttackState(CombatStateMachine2D stateMachine) : base(stateMachine) { }

        public override void Enter()
        {
            base.Enter();
            judgmentFired = false;
            stateMachine.IsAttackJudgmentFired = false;
        }

        public override void Tick()
        {
            base.Tick();

            if (!judgmentFired && elapsedTime >= stateMachine.attackJudgmentTime)
            {
                judgmentFired = true;
                stateMachine.IsAttackJudgmentFired = true;
                stateMachine.OnJudgmentFired?.Invoke();
            }

            if (HasDurationElapsed())
            {
                stateMachine.ChangeState(stateMachine.GetNeutralState());
            }
        }

        public override void Exit()
        {
            judgmentFired = false;
            stateMachine.IsAttackJudgmentFired = false;
        }
    }

    /// <summary>
    /// Parry：0.8秒ロック / parryJudgmentTime 秒時点で OnJudgmentFired を1回発火
    /// （Parry vs Feint などの解決に使用。Attack vs Parry の判定は Attack 側の発火で処理）
    /// </summary>
    private sealed class ParryState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Parry;
        private bool judgmentFired;

        public ParryState(CombatStateMachine2D stateMachine) : base(stateMachine) { }

        public override void Enter()
        {
            base.Enter();
            judgmentFired = false;
        }

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
                stateMachine.ChangeState(stateMachine.GetNeutralState());
            }
        }

        public override void Exit()
        {
            judgmentFired = false;
        }
    }

    /// <summary>
    /// Feint：0.3秒ロック / 判定なし
    /// </summary>
    private sealed class FeintState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Feint;

        public FeintState(CombatStateMachine2D stateMachine) : base(stateMachine) { }

        public override void Tick()
        {
            base.Tick();

            if (HasDurationElapsed())
            {
                stateMachine.ChangeState(stateMachine.GetNeutralState());
            }
        }
    }

    /// <summary>
    /// Vulnerable：vulnerableDurationOverride が設定されている場合はその時間を使用する。
    /// </summary>
    private sealed class VulnerableState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Vulnerable;
        private float activeDuration;

        public VulnerableState(CombatStateMachine2D stateMachine) : base(stateMachine) { }

        public override void Enter()
        {
            base.Enter();
            // オーバーライドがあればそれを使い、なければデフォルト値を使う
            if (stateMachine.vulnerableDurationOverride > 0f)
            {
                activeDuration = stateMachine.vulnerableDurationOverride;
                stateMachine.vulnerableDurationOverride = -1f;
            }
            else
            {
                activeDuration = stateMachine.vulnerableDuration;
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (activeDuration >= 0f && elapsedTime >= activeDuration)
            {
                stateMachine.ChangeState(stateMachine.GetNeutralState());
            }
        }
    }

    private sealed class GuardBreakState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.GuardBreak;

        public GuardBreakState(CombatStateMachine2D stateMachine) : base(stateMachine) { }

        public override void Tick()
        {
            base.Tick();

            if (HasDurationElapsed())
            {
                stateMachine.ChangeState(stateMachine.GetNeutralState());
            }
        }
    }

    private sealed class DeadState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Dead;

        public DeadState(CombatStateMachine2D stateMachine) : base(stateMachine) { }

        public override void Tick()
        {
            base.Tick();

            if (HasDurationElapsed())
            {
                Debug.Log("Dead state duration ended, remaining in Dead state.", stateMachine);
            }
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
    Idle,
    Guard,
    Attack,
    Parry,
    Feint,
    Vulnerable,
    GuardBreak,
    Dead
}
