using System.Collections.Generic;
using UnityEngine;

public abstract class CombatStateMachine2D : MonoBehaviour, ICombatStateActor
{
    public float idleDuration = -1f;
    public float guardDuration = -1f;
    public float attackDuration = 0.4f;
    public float parryDuration = 0.2f;
    public float feintDuration = 0.25f;
    public float vulnerableDuration = 0.5f;
    public float guardBreakDuration = 0.5f;
    public float deadDuration = -1f;

    public CombatStateType CurrentStateType => currentState?.StateType ?? CombatStateType.Guard;
    public float LastActionTimestamp { get; private set; }
    public bool IsMovementLocked => CurrentStateType == CombatStateType.Vulnerable
        || CurrentStateType == CombatStateType.GuardBreak
        || CurrentStateType == CombatStateType.Dead;
    protected bool HasPrimaryInput { get; private set; }

    private readonly Dictionary<CombatStateType, CombatState> states = new Dictionary<CombatStateType, CombatState>();
    private CombatState currentState;
    private float guardUnavailableUntil;

    protected virtual void Awake()
    {
        states[CombatStateType.Idle] = new IdleState(this);
        states[CombatStateType.Guard] = new GuardState(this);
        states[CombatStateType.Attack] = new AttackState(this);
        states[CombatStateType.Parry] = new ParryState(this);
        states[CombatStateType.Feint] = new FeintState(this);
        states[CombatStateType.Vulnerable] = new VulnerableState(this);
        states[CombatStateType.GuardBreak] = new GuardBreakState(this);
        states[CombatStateType.Dead] = new DeadState(this);
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

    public virtual void TriggerAttack()
    {
        if (IsMovementLocked)
        {
            return;
        }

        LastActionTimestamp = Time.time;
        ChangeState(CombatStateType.Attack);
    }

    public virtual void TriggerParry()
    {
        if (IsMovementLocked)
        {
            return;
        }

        LastActionTimestamp = Time.time;
        ChangeState(CombatStateType.Parry);
    }

    public virtual void TriggerFeint()
    {
        if (IsMovementLocked)
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
        ChangeState(CombatStateType.Guard);
    }

    public float GetDuration(CombatStateType stateType)
    {
        switch (stateType)
        {
            case CombatStateType.Idle:
                return idleDuration;
            case CombatStateType.Guard:
                return guardDuration;
            case CombatStateType.Attack:
                return attackDuration;
            case CombatStateType.Parry:
                return parryDuration;
            case CombatStateType.Feint:
                return feintDuration;
            case CombatStateType.Vulnerable:
                return vulnerableDuration;
            case CombatStateType.GuardBreak:
                return guardBreakDuration;
            case CombatStateType.Dead:
                return deadDuration;
            default:
                return -1f;
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

        public IdleState(CombatStateMachine2D stateMachine) : base(stateMachine)
        {
        }

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

        public GuardState(CombatStateMachine2D stateMachine) : base(stateMachine)
        {
        }

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

    private sealed class AttackState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Attack;

        public AttackState(CombatStateMachine2D stateMachine) : base(stateMachine)
        {
        }

        public override void Tick()
        {
            base.Tick();

            if (HasDurationElapsed())
            {
                stateMachine.ChangeState(stateMachine.GetNeutralState());
            }
        }
    }

    private sealed class ParryState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Parry;

        public ParryState(CombatStateMachine2D stateMachine) : base(stateMachine)
        {
        }

        public override void Tick()
        {
            base.Tick();

            if (HasDurationElapsed())
            {
                stateMachine.ChangeState(stateMachine.GetNeutralState());
            }
        }
    }

    private sealed class FeintState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Feint;

        public FeintState(CombatStateMachine2D stateMachine) : base(stateMachine)
        {
        }

        public override void Tick()
        {
            base.Tick();

            if (HasDurationElapsed())
            {
                stateMachine.ChangeState(stateMachine.GetNeutralState());
            }
        }
    }

    private sealed class VulnerableState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.Vulnerable;

        public VulnerableState(CombatStateMachine2D stateMachine) : base(stateMachine)
        {
        }

        public override void Tick()
        {
            base.Tick();

            if (HasDurationElapsed())
            {
                stateMachine.ChangeState(stateMachine.GetNeutralState());
            }
        }
    }

    private sealed class GuardBreakState : CombatState
    {
        public override CombatStateType StateType => CombatStateType.GuardBreak;

        public GuardBreakState(CombatStateMachine2D stateMachine) : base(stateMachine)
        {
        }

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

        public DeadState(CombatStateMachine2D stateMachine) : base(stateMachine)
        {
        }

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
