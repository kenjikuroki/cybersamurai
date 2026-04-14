using UnityEngine;

[RequireComponent(typeof(JankenCombatResolver2D))]
public class ProximityJankenBattle2D : MonoBehaviour
{
    public MonoBehaviour playerSource;
    public MonoBehaviour enemySource;
    public float resolveDistance = 1.5f;

    [Tooltip("キャラクター間の最小距離（これより近づくと強制的に押し離す）")]
    public float minSeparationDistance = 0.42f;

    [Tooltip("Y方向（奥行き）の攻撃が届く最大差。これより離れると攻撃が当たらない。")]
    public float yAttackTolerance = 0.45f;
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

    // 事前ガードリアクション用：相手が Attack に入った瞬間にガード側へ通知
    private CharacterSpriteAnimator2D playerAnimator;
    private CharacterSpriteAnimator2D enemyAnimator;

    // 押し離し用 Rigidbody2D キャッシュ
    private Rigidbody2D playerRb;
    private Rigidbody2D enemyRb;

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

    private void FixedUpdate()
    {
        EnforceSeparation();
    }

    /// <summary>
    /// 2キャラが minSeparationDistance より近づいたとき強制的に押し離す。
    ///
    /// MovePosition は Dynamic Rigidbody2D に非推奨（物理と干渉して振動ロックが発生）。
    /// rb.position で直接補正 ＋ 「接近方向の速度だけキャンセル」することで
    /// ・近づきすぎは防ぐ
    /// ・離れる方向への移動は妨げない
    /// </summary>
    private void EnforceSeparation()
    {
        if (playerRb == null || enemyRb == null) return;

        float dx    = playerRb.position.x - enemyRb.position.x;
        float absDx = Mathf.Abs(dx);
        if (absDx >= minSeparationDistance) return;

        // dir = player が enemy から見てどちら側か（+1 = 右, -1 = 左）
        float dir     = dx >= 0f ? 1f : -1f;
        float overlap = minSeparationDistance - absDx;
        float half    = overlap * 0.5f;

        // ── 位置を直接補正（Dynamic でも rb.position setter は安全）──────────
        if (!playerRb.isKinematic)
            playerRb.position = new Vector2(playerRb.position.x + dir * half, playerRb.position.y);
        if (!enemyRb.isKinematic)
            enemyRb.position  = new Vector2(enemyRb.position.x  - dir * half, enemyRb.position.y);

        // ── 接近方向の速度だけキャンセル（離れる速度は残す）────────────────
        // player が enemy 方向（-dir 方向）へ動いていたら止める
        if (!playerRb.isKinematic && playerRb.linearVelocity.x * dir < 0f)
            playerRb.linearVelocity = new Vector2(0f, playerRb.linearVelocity.y);

        // enemy が player 方向（+dir 方向）へ動いていたら止める
        if (!enemyRb.isKinematic && enemyRb.linearVelocity.x * dir > 0f)
            enemyRb.linearVelocity = new Vector2(0f, enemyRb.linearVelocity.y);
    }

    // -------------------------------------------------------------------------

    public void SetActors(ICombatStateActor player, ICombatStateActor enemy)
    {
        UnsubscribeAll();

        playerActor = player;
        enemyActor  = enemy;

        subscribedPlayer = player as CombatStateMachine2D;
        subscribedEnemy  = enemy  as CombatStateMachine2D;

        if (subscribedPlayer != null)
        {
            subscribedPlayer.OnJudgmentFired += OnJudgmentFired;
            subscribedPlayer.OnStateChanged  += OnPlayerStateChanged;
        }
        if (subscribedEnemy != null)
        {
            subscribedEnemy.OnJudgmentFired += OnJudgmentFired;
            subscribedEnemy.OnStateChanged  += OnEnemyStateChanged;
        }

        // アニメーターキャッシュ
        playerAnimator = (player as Component)?.GetComponent<CharacterSpriteAnimator2D>();
        enemyAnimator  = (enemy  as Component)?.GetComponent<CharacterSpriteAnimator2D>();

        // 押し離し用 Rigidbody2D キャッシュ
        playerRb = (player as Component)?.GetComponent<Rigidbody2D>();
        enemyRb  = (enemy  as Component)?.GetComponent<Rigidbody2D>();
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

        // X方向の射程外なら判定しない
        float dx = Mathf.Abs(playerComponent.transform.position.x - enemyComponent.transform.position.x);
        float dy = Mathf.Abs(playerComponent.transform.position.y - enemyComponent.transform.position.y);
        if (dx > resolveDistance || dy > yAttackTolerance) return;

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

        // ヒットストップ
        TriggerHitstop(result);

        // パリィ成功アニメーションをパリィした側に通知
        NotifyParrySuccess(result);
    }

    /// <summary>
    /// パリィ成功時、パリィした側のキャラクターに TriggerParrySuccess() を送る。
    /// InitiatorParrySuccess → enemy がパリィした（enemy = initiator の相手）
    ///   ここでは player=initiator, enemy=receiver なので
    ///   ReceiverParrySuccess → enemy がパリィ成功、InitiatorParrySuccess → player がパリィ成功
    /// </summary>
    private void NotifyParrySuccess(CombatResolutionResult result)
    {
        Component parrierComponent  = null;
        Component attackerComponent = null;

        if (result == CombatResolutionResult.InitiatorParrySuccess)
        {
            // initiator(player) が攻撃 → receiver(enemy) がパリィ成功
            parrierComponent  = enemyActor  as Component;
            attackerComponent = playerActor as Component;
        }
        else if (result == CombatResolutionResult.ReceiverParrySuccess)
        {
            // receiver(enemy) が攻撃 → initiator(player) がパリィ成功
            parrierComponent  = playerActor as Component;
            attackerComponent = enemyActor  as Component;
        }

        // パリィした側：成功フラッシュ（Jump）
        parrierComponent?.GetComponent<CharacterSpriteAnimator2D>()?.TriggerParrySuccess();

        // パリィされた側（攻撃側）：隙モーション（Jump_kick）
        attackerComponent?.GetComponent<CharacterSpriteAnimator2D>()?.TriggerParryVulnerable();
    }

    // -------------------------------------------------------------------------

    private void UnsubscribeAll()
    {
        if (subscribedPlayer != null)
        {
            subscribedPlayer.OnJudgmentFired -= OnJudgmentFired;
            subscribedPlayer.OnStateChanged  -= OnPlayerStateChanged;
        }
        if (subscribedEnemy != null)
        {
            subscribedEnemy.OnJudgmentFired -= OnJudgmentFired;
            subscribedEnemy.OnStateChanged  -= OnEnemyStateChanged;
        }
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

    // -------------------------------------------------------------------------
    // 事前ガードリアクション
    // -------------------------------------------------------------------------

    /// <summary>
    /// プレイヤーが Attack に入った瞬間：敵が Guard 状態なら即座にガードアニメを出す。
    /// 判定発火（attackJudgmentTime）を待たずに同フレームで反応させるための仕組み。
    /// </summary>
    private void OnPlayerStateChanged(CombatStateType newState)
    {
        if (newState != CombatStateType.Attack) return;
        if (enemyActor?.CurrentStateType != CombatStateType.Guard) return;
        if (!IsInResolveRange()) return;
        enemyAnimator?.TriggerGuardReaction();
    }

    /// <summary>敵が Attack に入った瞬間：プレイヤーが Guard 状態なら即座にガードアニメを出す。</summary>
    private void OnEnemyStateChanged(CombatStateType newState)
    {
        if (newState != CombatStateType.Attack) return;
        if (playerActor?.CurrentStateType != CombatStateType.Guard) return;
        if (!IsInResolveRange()) return;
        playerAnimator?.TriggerGuardReaction();
    }

    private bool IsInResolveRange()
    {
        Component pc = playerActor as Component;
        Component ec = enemyActor  as Component;
        if (pc == null || ec == null) return false;
        return Vector2.Distance(pc.transform.position, ec.transform.position) <= resolveDistance * 1.5f;
    }

    // -------------------------------------------------------------------------

    private static void TriggerHitstop(CombatResolutionResult result)
    {
        HitstopManager hs = HitstopManager.Instance;
        if (hs == null) return;

        switch (result)
        {
            // パリィ成功 → 完全停止＋スロー
            case CombatResolutionResult.InitiatorParrySuccess:
            case CombatResolutionResult.ReceiverParrySuccess:
                hs.TriggerParrySuccess();
                break;

            // ガードブレイク → やや長い停止
            case CombatResolutionResult.InitiatorGuardBreak:
            case CombatResolutionResult.ReceiverGuardBreak:
                hs.TriggerGuardBreak();
                break;

            // 通常ヒット（Dead / Vulnerable）→ 短い停止
            case CombatResolutionResult.InitiatorDead:
            case CombatResolutionResult.ReceiverDead:
            case CombatResolutionResult.InitiatorVulnerable:
            case CombatResolutionResult.ReceiverVulnerable:
                hs.TriggerHit();
                break;

            // ガードブロック → ごく短い停止（手応え）
            case CombatResolutionResult.GuardBlocked:
                hs.TriggerHit();
                break;
        }
    }

    private static bool ShouldFlash(CombatResolutionResult result)
    {
        // パリィ成功はヒットストップのスロー演出で見せるため白フラッシュなし
        return result == CombatResolutionResult.GuardBlocked
            || result == CombatResolutionResult.InitiatorGuardBreak
            || result == CombatResolutionResult.ReceiverGuardBreak
            || result == CombatResolutionResult.InitiatorVulnerable
            || result == CombatResolutionResult.ReceiverVulnerable
            || result == CombatResolutionResult.InitiatorDead
            || result == CombatResolutionResult.ReceiverDead;
    }
}
