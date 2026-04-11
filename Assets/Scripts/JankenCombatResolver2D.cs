using UnityEngine;

public class JankenCombatResolver2D : MonoBehaviour
{
    public float clashThresholdSeconds = 0.2f;

    public CombatResolutionResult Resolve(ICombatStateActor initiator, ICombatStateActor receiver)
    {
        if (initiator == null || receiver == null)
        {
            Debug.LogWarning("Janken resolution skipped because one of the actors is null.", this);
            return CombatResolutionResult.NoEffect;
        }

        CombatStateType initiatorState = initiator.CurrentStateType;
        CombatStateType receiverState = receiver.CurrentStateType;

        if (initiatorState == CombatStateType.Dead || receiverState == CombatStateType.Dead)
        {
            Debug.Log("Janken resolution skipped because one actor is already dead.", this);
            return CombatResolutionResult.NoEffect;
        }

        if (initiatorState == CombatStateType.Attack && receiverState == CombatStateType.Guard)
        {
            bool guardBroken = TryConsumeGuard(receiver);
            return guardBroken ? CombatResolutionResult.ReceiverGuardBreak : CombatResolutionResult.GuardBlocked;
        }

        if (initiatorState == CombatStateType.Guard && receiverState == CombatStateType.Attack)
        {
            bool guardBroken = TryConsumeGuard(initiator);
            return guardBroken ? CombatResolutionResult.InitiatorGuardBreak : CombatResolutionResult.GuardBlocked;
        }

        if (initiatorState == CombatStateType.Attack
            && (receiverState == CombatStateType.Vulnerable || receiverState == CombatStateType.GuardBreak))
        {
            receiver.ChangeState(CombatStateType.Dead);
            Debug.Log("Attack hit a Vulnerable target. Receiver is now Dead.", this);
            return CombatResolutionResult.ReceiverDead;
        }

        if ((initiatorState == CombatStateType.Vulnerable || initiatorState == CombatStateType.GuardBreak)
            && receiverState == CombatStateType.Attack)
        {
            initiator.ChangeState(CombatStateType.Dead);
            Debug.Log("Attack hit a Vulnerable target. Initiator is now Dead.", this);
            return CombatResolutionResult.InitiatorDead;
        }

        if (initiatorState == CombatStateType.Attack && receiverState == CombatStateType.Parry)
        {
            initiator.ChangeState(CombatStateType.Vulnerable);
            Debug.Log("Attack vs Parry: initiator became Vulnerable.", this);
            return CombatResolutionResult.InitiatorVulnerable;
        }

        if (initiatorState == CombatStateType.Parry && receiverState == CombatStateType.Attack)
        {
            receiver.ChangeState(CombatStateType.Vulnerable);
            Debug.Log("Attack vs Parry: receiver became Vulnerable.", this);
            return CombatResolutionResult.ReceiverVulnerable;
        }

        if (initiatorState == CombatStateType.Parry && receiverState == CombatStateType.Feint)
        {
            initiator.ChangeState(CombatStateType.Vulnerable);
            Debug.Log("Parry vs Feint: initiator became Vulnerable.", this);
            return CombatResolutionResult.InitiatorVulnerable;
        }

        if (initiatorState == CombatStateType.Feint && receiverState == CombatStateType.Parry)
        {
            receiver.ChangeState(CombatStateType.Vulnerable);
            Debug.Log("Parry vs Feint: receiver became Vulnerable.", this);
            return CombatResolutionResult.ReceiverVulnerable;
        }

        if (initiatorState == CombatStateType.Feint && receiverState == CombatStateType.Attack)
        {
            initiator.ChangeState(CombatStateType.Vulnerable);
            Debug.Log("Feint vs Attack: initiator became Vulnerable.", this);
            return CombatResolutionResult.InitiatorVulnerable;
        }

        if (initiatorState == CombatStateType.Attack && receiverState == CombatStateType.Feint)
        {
            receiver.ChangeState(CombatStateType.Vulnerable);
            Debug.Log("Feint vs Attack: receiver became Vulnerable.", this);
            return CombatResolutionResult.ReceiverVulnerable;
        }

        if (initiatorState == CombatStateType.Attack && receiverState == CombatStateType.Attack)
        {
            float timingDifference = Mathf.Abs(initiator.LastActionTimestamp - receiver.LastActionTimestamp);

            if (timingDifference <= clashThresholdSeconds)
            {
                Debug.Log($"Attack vs Attack: clash occurred. Timing diff {timingDifference:F3}s.", this);
                return CombatResolutionResult.Clash;
            }

            ICombatStateActor laterActor = initiator.LastActionTimestamp > receiver.LastActionTimestamp ? initiator : receiver;
            laterActor.ChangeState(CombatStateType.Dead);
            Debug.Log($"Attack vs Attack: later attack lost and became Dead. Timing diff {timingDifference:F3}s.", this);
            return laterActor == initiator ? CombatResolutionResult.InitiatorDead : CombatResolutionResult.ReceiverDead;
        }

        Debug.Log($"No janken rule matched: {initiatorState} vs {receiverState}", this);
        return CombatResolutionResult.NoEffect;
    }

    private static bool TryConsumeGuard(ICombatStateActor actor)
    {
        Component actorComponent = actor as Component;
        if (actorComponent == null)
        {
            return false;
        }

        GuardGauge guardGauge = actorComponent.GetComponent<GuardGauge>();
        if (guardGauge == null)
        {
            return false;
        }

        return guardGauge.ConsumeGuard();
    }
}

public enum CombatResolutionResult
{
    NoEffect,
    GuardBlocked,
    InitiatorGuardBreak,
    ReceiverGuardBreak,
    Clash,
    InitiatorVulnerable,
    ReceiverVulnerable,
    InitiatorDead,
    ReceiverDead
}
