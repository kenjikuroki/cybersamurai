using UnityEngine;

[RequireComponent(typeof(JankenCombatResolver2D))]
public class ProximityJankenBattle2D : MonoBehaviour
{
    public MonoBehaviour playerSource;
    public MonoBehaviour enemySource;
    public float resolveDistance = 1.5f;
    public BattlePresentation2D presentation;

    private ICombatStateActor playerActor;
    private ICombatStateActor enemyActor;
    private JankenCombatResolver2D resolver;
    private string lastResolvedSignature;

    private void Awake()
    {
        resolver = GetComponent<JankenCombatResolver2D>();
        playerActor = playerSource as ICombatStateActor;
        enemyActor = enemySource as ICombatStateActor;
    }

    private void Update()
    {
        if (resolver == null || playerActor == null || enemyActor == null)
        {
            return;
        }

        Component playerComponent = playerActor as Component;
        Component enemyComponent = enemyActor as Component;
        if (playerComponent == null || enemyComponent == null)
        {
            return;
        }

        float distance = Vector2.Distance(playerComponent.transform.position, enemyComponent.transform.position);
        if (distance > resolveDistance)
        {
            return;
        }

        if (!ShouldResolve(playerActor.CurrentStateType, enemyActor.CurrentStateType))
        {
            return;
        }

        string signature = BuildSignature(playerActor, enemyActor);
        if (signature == lastResolvedSignature)
        {
            return;
        }

        CombatStateType playerStateBefore = playerActor.CurrentStateType;
        CombatStateType enemyStateBefore = enemyActor.CurrentStateType;
        CombatResolutionResult result = resolver.Resolve(playerActor, enemyActor);
        lastResolvedSignature = signature;

        if (result == CombatResolutionResult.NoEffect)
        {
            return;
        }

        string resultMessage = FormatResult(playerComponent.name, playerStateBefore, enemyComponent.name, enemyStateBefore, result);
        Debug.Log(resultMessage, this);

        if (presentation != null)
        {
            presentation.ShowResult(resultMessage);
            if (ShouldFlash(result))
            {
                presentation.TriggerFlash();
            }
        }
    }

    public void SetActors(ICombatStateActor player, ICombatStateActor enemy)
    {
        playerActor = player;
        enemyActor = enemy;
    }

    public void ResetRoundState()
    {
        lastResolvedSignature = string.Empty;
    }

    private static bool ShouldResolve(CombatStateType playerState, CombatStateType enemyState)
    {
        return IsResolvableState(playerState) || IsResolvableState(enemyState);
    }

    private static bool IsResolvableState(CombatStateType state)
    {
        return state == CombatStateType.Attack
            || state == CombatStateType.Parry
            || state == CombatStateType.Feint
            || state == CombatStateType.Vulnerable
            || state == CombatStateType.GuardBreak;
    }

    private static string BuildSignature(ICombatStateActor player, ICombatStateActor enemy)
    {
        return $"{player.CurrentStateType}:{player.LastActionTimestamp:F3}|{enemy.CurrentStateType}:{enemy.LastActionTimestamp:F3}";
    }

    private static string FormatResult(
        string playerName,
        CombatStateType playerState,
        string enemyName,
        CombatStateType enemyState,
        CombatResolutionResult result)
    {
        string left = playerState.ToString().ToUpperInvariant();
        string right = enemyState.ToString().ToUpperInvariant();

        switch (result)
        {
            case CombatResolutionResult.GuardBlocked:
                return $"{left} vs {right} -> GUARD BLOCK!";
            case CombatResolutionResult.InitiatorGuardBreak:
                return $"{left} vs {right} -> {playerName} GUARDBREAK!";
            case CombatResolutionResult.ReceiverGuardBreak:
                return $"{left} vs {right} -> {enemyName} GUARDBREAK!";
            case CombatResolutionResult.Clash:
                return $"{left} vs {right} -> CLASH!";
            case CombatResolutionResult.InitiatorVulnerable:
                return $"{left} vs {right} -> {playerName} VULNERABLE!";
            case CombatResolutionResult.ReceiverVulnerable:
                return $"{left} vs {right} -> {enemyName} VULNERABLE!";
            case CombatResolutionResult.InitiatorDead:
                return $"{left} vs {right} -> {playerName} DEAD!";
            case CombatResolutionResult.ReceiverDead:
                return $"{left} vs {right} -> {enemyName} DEAD!";
            default:
                return $"{left} vs {right} -> NO EFFECT";
        }
    }

    private static bool ShouldFlash(CombatResolutionResult result)
    {
        return result == CombatResolutionResult.GuardBlocked
            || result == CombatResolutionResult.InitiatorGuardBreak
            || result == CombatResolutionResult.ReceiverGuardBreak
            || result == CombatResolutionResult.InitiatorVulnerable
            || result == CombatResolutionResult.ReceiverVulnerable
            || result == CombatResolutionResult.InitiatorDead
            || result == CombatResolutionResult.ReceiverDead;
    }
}
