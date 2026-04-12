using UnityEngine;

public class JankenCombatResolver2D : MonoBehaviour
{
    public float clashThresholdSeconds = 0.2f;

    [Header("SF3-Style Parry Timing")]
    [Tooltip("攻撃開始からこの秒数以前のパリィ入力は早すぎてGuard扱いになる")]
    public float parryWindowStart = 0.2f;

    [Tooltip("攻撃開始からこの秒数以降のパリィ入力は遅すぎてDead扱いになる")]
    public float parryWindowEnd = 0.3f;

    [Tooltip("パリィ成功時に攻撃側が受けるVulnerable状態の持続秒数")]
    public float attackerVulnerableDuration = 0.5f;

    [Tooltip("パリィ成功時にパリィ側が受けるVulnerable状態の持続秒数（0 = 隙なし）")]
    public float parrierVulnerableDuration = 0.2f;

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

        // ── Attack vs Parry（SF3タイミング判定）─────────────────────────────
        //
        //  攻撃開始を基準にパリィ入力のタイムスタンプを比較する。
        //
        //  判定はどちらの OnJudgmentFired が先に発火するかで決まる:
        //   ・パリィ判定（T_p+0.3s）が先に発火するのは delay < 0.2s のとき（早すぎ）
        //   ・攻撃判定（T_a+0.5s）が先に発火するのは delay >= 0.2s のとき（成功/遅すぎ）
        //
        //  早すぎ (delay < parryWindowStart):
        //    パリィをGuardに即キャンセル → 攻撃判定でGuard扱いになる
        //
        //  成功  (parryWindowStart <= delay <= parryWindowEnd):
        //    攻撃側: attackerVulnerableDuration秒のVulnerable
        //    パリィ側: 即座にニュートラルへ（連続パリィ可能）
        //    → 白フラッシュ演出（ShouldFlash が ParrySuccess を含む）
        //
        //  遅すぎ (delay > parryWindowEnd):
        //    パリィ側: Dead
        //
        if (initiatorState == CombatStateType.Attack && receiverState == CombatStateType.Parry)
        {
            return ResolveAttackVsParry(
                attacker: initiator,
                parrier:  receiver,
                parrierIsReceiver: true);
        }

        if (initiatorState == CombatStateType.Parry && receiverState == CombatStateType.Attack)
        {
            return ResolveAttackVsParry(
                attacker: receiver,
                parrier:  initiator,
                parrierIsReceiver: false);
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

    // =========================================================================
    // SF3 パリィ成否判定
    // =========================================================================

    /// <summary>
    /// attacker.LastActionTimestamp を基準にパリィ入力の遅延を計算し、
    /// 早すぎ / 成功 / 遅すぎ の三分岐で処理する。
    ///
    /// parrierIsReceiver: true  = parrier は Resolve() の receiver 側
    ///                    false = parrier は Resolve() の initiator 側
    /// </summary>
    private CombatResolutionResult ResolveAttackVsParry(
        ICombatStateActor attacker,
        ICombatStateActor parrier,
        bool parrierIsReceiver)
    {
        var attackerSM = attacker as CombatStateMachine2D;

        // 攻撃判定がすでに発火済みなら（超遅延パリィ）解決をスキップ
        if (attackerSM != null && attackerSM.IsAttackJudgmentFired)
        {
            Debug.Log("[Parry] Skipped: attacker judgment already fired (parry too late).", this);
            return CombatResolutionResult.NoEffect;
        }

        // パリィ入力のディレイ（攻撃開始からの経過秒数）
        float delay = parrier.LastActionTimestamp - attacker.LastActionTimestamp;

        // ── 早すぎ（delay < parryWindowStart）────────────────────────────────
        if (delay < parryWindowStart)
        {
            Debug.Log($"[Parry] Too EARLY: delay={delay:F3}s (window={parryWindowStart}~{parryWindowEnd}s) → Guard扱い", this);

            // パリィをGuardにキャンセルし、次の攻撃判定でGuard consumeさせる
            var parrierSM = parrier as CombatStateMachine2D;
            parrierSM?.CancelToNeutral();

            // NoEffect を返すことで lastResolutionTime を更新しない
            // → 攻撃判定（Attack vs Guard）が正常に発火できる
            return CombatResolutionResult.NoEffect;
        }

        // ── 遅すぎ（delay > parryWindowEnd）──────────────────────────────────
        if (delay > parryWindowEnd)
        {
            Debug.Log($"[Parry] Too LATE: delay={delay:F3}s (window={parryWindowStart}~{parryWindowEnd}s) → Dead", this);
            parrier.ChangeState(CombatStateType.Dead);

            return parrierIsReceiver
                ? CombatResolutionResult.ReceiverDead
                : CombatResolutionResult.InitiatorDead;
        }

        // ── 成功（parryWindowStart <= delay <= parryWindowEnd）───────────────
        Debug.Log($"[Parry] SUCCESS! delay={delay:F3}s (window={parryWindowStart}~{parryWindowEnd}s)", this);

        // 攻撃側: attackerVulnerableDuration 秒の Vulnerable
        if (attackerSM != null)
            attackerSM.TriggerVulnerableWithDuration(attackerVulnerableDuration);
        else
            attacker.ChangeState(CombatStateType.Vulnerable);

        // パリィ側: parrierVulnerableDuration 秒の隙（0以下なら即フリー）
        var successParrierSM = parrier as CombatStateMachine2D;
        if (successParrierSM != null && parrierVulnerableDuration > 0f)
            successParrierSM.TriggerVulnerableWithDuration(parrierVulnerableDuration);
        else
            successParrierSM?.CancelToNeutral();

        // 攻撃側が Vulnerable になった → InitiatorVulnerable or ReceiverVulnerable
        // parrierIsReceiver = true  → attacker = initiator → InitiatorVulnerable
        // parrierIsReceiver = false → attacker = receiver  → ReceiverVulnerable
        return parrierIsReceiver
            ? CombatResolutionResult.InitiatorParrySuccess
            : CombatResolutionResult.ReceiverParrySuccess;
    }

    // =========================================================================

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
    ReceiverDead,
    /// <summary>パリィ成功：initiator（攻撃側）が Vulnerable になった</summary>
    InitiatorParrySuccess,
    /// <summary>パリィ成功：receiver（攻撃側）が Vulnerable になった</summary>
    ReceiverParrySuccess,
}
