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

    // 同一フレームに両者の OnJudgmentFired が連続して届いた場合の二重解決防止
    // ※ NoEffect のときは lastResolutionTime を更新しない（早すぎパリィ→Guard連鎖を妨げないため）
    private float lastResolutionTime = -99f;
    private const float ResolutionCooldown = 0.05f;

    private CombatStateMachine2D subscribedPlayer;
    private CombatStateMachine2D subscribedEnemy;

    private void Awake()
    {
        resolver    = GetComponent<JankenCombatResolver2D>();
        playerActor = playerSource as ICombatStateActor;
        enemyActor  = enemySource  as ICombatStateActor;
    }

    private void OnDestroy()
    {
        UnsubscribeAll();
    }

    // -------------------------------------------------------------------------

    public void SetActors(ICombatStateActor player, ICombatStateActor enemy)
    {
        UnsubscribeAll();

        playerActor = player;
        enemyActor  = enemy;

        subscribedPlayer = player as CombatStateMachine2D;
        subscribedEnemy  = enemy  as CombatStateMachine2D;

        if (subscribedPlayer != null) subscribedPlayer.OnJudgmentFired += OnJudgmentFired;
        if (subscribedEnemy  != null) subscribedEnemy.OnJudgmentFired  += OnJudgmentFired;
    }

    public void ResetRoundState()
    {
        lastResolutionTime = -99f;
    }

    // -------------------------------------------------------------------------

    private void OnJudgmentFired()
    {
        if (Time.time - lastResolutionTime < ResolutionCooldown) return;
        if (resolver == null || playerActor == null || enemyActor == null) return;

        Component playerComponent = playerActor as Component;
        Component enemyComponent  = enemyActor  as Component;
        if (playerComponent == null || enemyComponent == null) return;

        float distance = Vector2.Distance(
            playerComponent.transform.position,
            enemyComponent.transform.position);

        if (distance > resolveDistance) return;

        CombatStateType playerStateBefore = playerActor.CurrentStateType;
        CombatStateType enemyStateBefore  = enemyActor.CurrentStateType;
        CombatResolutionResult result = resolver.Resolve(playerActor, enemyActor);

        // NoEffect のときはタイムスタンプを更新しない。
        // これにより「早すぎパリィ→Guard状態に戻る→次の攻撃判定」を
        // クールダウンで妨げないようにする。
        if (result == CombatResolutionResult.NoEffect) return;

        lastResolutionTime = Time.time;

        string resultMessage = FormatResult(
            playerComponent.name, playerStateBefore,
            enemyComponent.name,  enemyStateBefore,
            result);

        Debug.Log(resultMessage, this);

        if (presentation != null)
        {
            presentation.ShowResult(resultMessage);
            if (ShouldFlash(result)) presentation.TriggerFlash();
        }
    }

    // -------------------------------------------------------------------------

    private void UnsubscribeAll()
    {
        if (subscribedPlayer != null) subscribedPlayer.OnJudgmentFired -= OnJudgmentFired;
        if (subscribedEnemy  != null) subscribedEnemy.OnJudgmentFired  -= OnJudgmentFired;
        subscribedPlayer = null;
        subscribedEnemy  = null;
    }

    // -------------------------------------------------------------------------

    private static string FormatResult(
        string playerName, CombatStateType playerState,
        string enemyName,  CombatStateType enemyState,
        CombatResolutionResult result)
    {
        string left  = playerState.ToString().ToUpperInvariant();
        string right = enemyState.ToString().ToUpperInvariant();

        switch (result)
        {
            case CombatResolutionResult.GuardBlocked:          return $"{left} vs {right} -> GUARD BLOCK!";
            case CombatResolutionResult.InitiatorGuardBreak:   return $"{left} vs {right} -> {playerName} GUARDBREAK!";
            case CombatResolutionResult.ReceiverGuardBreak:    return $"{left} vs {right} -> {enemyName} GUARDBREAK!";
            case CombatResolutionResult.Clash:                 return $"{left} vs {right} -> CLASH!";
            case CombatResolutionResult.InitiatorVulnerable:   return $"{left} vs {right} -> {playerName} VULNERABLE!";
            case CombatResolutionResult.ReceiverVulnerable:    return $"{left} vs {right} -> {enemyName} VULNERABLE!";
            case CombatResolutionResult.InitiatorDead:         return $"{left} vs {right} -> {playerName} DEAD!";
            case CombatResolutionResult.ReceiverDead:          return $"{left} vs {right} -> {enemyName} DEAD!";
            // パリィ成功：攻撃側が Vulnerable になる
            case CombatResolutionResult.InitiatorParrySuccess: return $"PARRY!! {playerName} is Vulnerable!";
            case CombatResolutionResult.ReceiverParrySuccess:  return $"PARRY!! {enemyName} is Vulnerable!";
            default:                                           return $"{left} vs {right} -> NO EFFECT";
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
            || result == CombatResolutionResult.ReceiverDead
            || result == CombatResolutionResult.InitiatorParrySuccess
            || result == CombatResolutionResult.ReceiverParrySuccess;
    }
}
