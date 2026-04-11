using UnityEngine;

public class JankenCombatResolver2D : MonoBehaviour
{
    public float clashThresholdSeconds = 0.2f;

    [Header("Parry Timing")]
    [Tooltip("攻撃開始からこの秒数以内にパリィ入力すれば成功")]
    public float parryWindow = 0.5f;

    [Tooltip("パリィ成功時に防御側が受ける Vulnerable 状態の持続秒数")]
    public float parrySuccessVulnerableDuration = 0.5f;

    public CombatResolutionResult Resolve(ICombatStateActor initiator, ICombatStateActor receiver)
    {
        if (initiator == null || receiver == null)
        {
            Debug.LogWarning("Janken resolution skipped because one of the actors is null.", this);
            return CombatResolutionResult.NoEffect;
        }

        CombatStateType initiatorState = initiator.CurrentStateType;
        CombatStateType receiverState  = receiver.CurrentStateType;

        if (initiatorState == CombatStateType.Dead || receiverState == CombatStateType.Dead)
        {
            Debug.Log("Janken resolution skipped because one actor is already dead.", this);
            return CombatResolutionResult.NoEffect;
        }

        // ── Attack vs Guard ──────────────────────────────────────────────────
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

        // ── Attack vs Vulnerable / GuardBreak ────────────────────────────────
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

        // ── Attack vs Parry（タイミング判定）────────────────────────────────
        //
        //   攻撃開始から parryWindow 秒以内にパリィ入力 → パリィ成功
        //     ・攻撃側：即座にロック解除（Guard へ戻る）
        //     ・防御側：parrySuccessVulnerableDuration 秒の Vulnerable
        //
        //   遅すぎたパリィ（Attack 判定が既に発火済み、または window 外）→ 防御側 Dead
        //
        if (initiatorState == CombatStateType.Attack && receiverState == CombatStateType.Parry)
        {
            return ResolveAttackVsParry(initiator, receiver,
                isInitiatorAttacker: true);
        }

        if (initiatorState == CombatStateType.Parry && receiverState == CombatStateType.Attack)
        {
            // 引数の順序を「attacker, parrier」に揃えて呼び、結果を反転する
            CombatResolutionResult r = ResolveAttackVsParry(receiver, initiator,
                isInitiatorAttacker: false);
            return r;
        }

        // ── Parry vs Feint ───────────────────────────────────────────────────
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

        // ── Feint vs Attack ──────────────────────────────────────────────────
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

        // ── Attack vs Attack ─────────────────────────────────────────────────
        if (initiatorState == CombatStateType.Attack && receiverState == CombatStateType.Attack)
        {
            float timingDifference = Mathf.Abs(initiator.LastActionTimestamp - receiver.LastActionTimestamp);

            if (timingDifference <= clashThresholdSeconds)
            {
                Debug.Log($"Attack vs Attack: clash occurred. Timing diff {timingDifference:F3}s.", this);
                return CombatResolutionResult.Clash;
            }

            ICombatStateActor laterActor = initiator.LastActionTimestamp > receiver.LastActionTimestamp
                ? initiator : receiver;
            laterActor.ChangeState(CombatStateType.Dead);
            Debug.Log($"Attack vs Attack: later attack lost. Timing diff {timingDifference:F3}s.", this);
            return laterActor == initiator
                ? CombatResolutionResult.InitiatorDead
                : CombatResolutionResult.ReceiverDead;
        }

        Debug.Log($"No janken rule matched: {initiatorState} vs {receiverState}", this);
        return CombatResolutionResult.NoEffect;
    }

    // -------------------------------------------------------------------------
    // パリィ成否判定
    // -------------------------------------------------------------------------

    /// <summary>
    /// attacker = 攻撃側、parrier = パリィした側（順不同で呼ばれる）。
    /// isInitiatorAttacker: true のとき initiator=attacker → 結果をそのまま返す。
    ///                      false のとき receiver=attacker → 結果を反転して返す。
    /// </summary>
    private CombatResolutionResult ResolveAttackVsParry(
        ICombatStateActor attacker,
        ICombatStateActor parrier,
        bool isInitiatorAttacker)
    {
        // 攻撃側の Attack 判定がすでに発火済みかチェック
        // （遅れてパリィ判定が届いた場合、攻撃はすでに解決済みなので無視）
        var attackerSM = attacker as CombatStateMachine2D;
        if (attackerSM != null && attackerSM.IsAttackJudgmentFired)
        {
            // 攻撃は既に解決済み（Attack 判定時点では Guard 扱いで guard consume 済み）
            // 遅いパリィ判定は無視する
            Debug.Log("Parry judgment ignored: attacker's judgment already fired.", this);
            return CombatResolutionResult.NoEffect;
        }

        // パリィ入力が攻撃開始からどのくらい遅れたか
        float parryDelay = parrier.LastActionTimestamp - attacker.LastActionTimestamp;
        bool success = parryDelay >= 0f && parryDelay <= parryWindow;

        if (success)
        {
            // ── パリィ成功 ──
            Debug.Log($"Parry SUCCESS! delay={parryDelay:F3}s (window={parryWindow}s)", this);

            // 攻撃側：即座にロック解除
            attackerSM?.CancelToNeutral();

            // 防御側：短い Vulnerable（parrySuccessVulnerableDuration）
            var parrierSM = parrier as CombatStateMachine2D;
            if (parrierSM != null)
                parrierSM.TriggerVulnerableWithDuration(parrySuccessVulnerableDuration);
            else
                parrier.ChangeState(CombatStateType.Vulnerable);

            // parrier が「イニシエーター側か受信者側か」で結果を返す
            return isInitiatorAttacker
                ? CombatResolutionResult.ReceiverVulnerable   // parrier = receiver
                : CombatResolutionResult.InitiatorVulnerable; // parrier = initiator
        }
        else
        {
            // ── パリィ失敗（タイミング外）──
            Debug.Log($"Parry FAILED! delay={parryDelay:F3}s (window={parryWindow}s) → parrier Dead", this);
            parrier.ChangeState(CombatStateType.Dead);

            return isInitiatorAttacker
                ? CombatResolutionResult.ReceiverDead   // parrier = receiver
                : CombatResolutionResult.InitiatorDead; // parrier = initiator
        }
    }

    // -------------------------------------------------------------------------

    private static bool TryConsumeGuard(ICombatStateActor actor)
    {
        Component actorComponent = actor as Component;
        if (actorComponent == null) return false;

        GuardGauge guardGauge = actorComponent.GetComponent<GuardGauge>();
        if (guardGauge == null) return false;

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
