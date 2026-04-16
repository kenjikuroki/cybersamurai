using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 複数の敵を管理する。
/// - activeEnemy   : プレイヤーと直接戦う敵（1体）
/// - standbyEnemies: 戦闘ゾーン外で待機。たまに邪魔攻撃を仕掛ける。
///                   邪魔時に確率でそのままアクティブ交代する。
/// </summary>
public class MultiEnemyManager : MonoBehaviour
{
    [Header("Standby Position（待機距離）")]
    [Tooltip("戦闘ゾーン端からの最低X距離")]
    public float standbyDistance = 2.2f;

    [Tooltip("プレイヤーに近づかせない最小X距離（安全網）")]
    public float minStandbyDistance = 1.8f;

    [Header("Standby Movement")]
    [Tooltip("プレイヤーがこの距離動いたら目標位置を更新")]
    public float targetUpdateThreshold = 0.30f;

    [Header("Interference（邪魔攻撃）")]
    [Tooltip("プレイヤーがスタンバイ敵のこの距離内に入ったとき発動を判定する")]
    public float interferenceProximity = 1.4f;

    [Tooltip("発動確率（毎秒の判定）。プレイヤーが近くにいる間だけ判定する")]
    [Range(0f, 1f)]
    public float interferenceChancePerSecond = 0.06f;

    [Tooltip("邪魔して戻るまでの秒数")]
    public float returnToStandbyDelay = 3.0f;

    [Tooltip("邪魔後そのままアクティブ交代する確率")]
    [Range(0f, 1f)]
    public float swapChance = 0.30f;

    [Header("Enemy Promotion")]
    [Tooltip("アクティブ敵が死んでから次の敵を起動するまでの秒数")]
    public float promotionDelay = 0.8f;

    // -------------------------------------------------------------------------

    public bool AllEnemiesDefeated { get; private set; } = false;

    // -------------------------------------------------------------------------

    private ICombatStateActor           playerActor;
    private Transform                   playerTransform;
    private CharacterFacing             playerFacing;
    private List<EnemyRandomCombatAI2D> allEnemyAIs       = new List<EnemyRandomCombatAI2D>();
    private List<CombatStateMachine2D>  allEnemySMs       = new List<CombatStateMachine2D>();
    private List<Vector3>               enemyStartPos     = new List<Vector3>();
    private int                         activeIndex       = 0;
    private ProximityJankenBattle2D     battle;
    private bool                        promotionPending  = false;

    private float   interferenceTimer  = 1f;
    private bool    interferenceActive = false; // 邪魔中フラグ（二重発動防止）
    private int     interferingIndex   = -1;    // 現在邪魔中の敵インデックス

    private float[]   lastTargetPlayerX;
    private Vector3[] cachedStandbyTarget;

    // -------------------------------------------------------------------------

    public void Setup(
        ICombatStateActor           player,
        List<EnemyRandomCombatAI2D> enemyAIs,
        ProximityJankenBattle2D     battleController,
        CharacterFacing             playerFacingRef = null)
    {
        playerActor        = player;
        playerTransform    = (player as Component)?.transform;
        playerFacing       = playerFacingRef;
        allEnemyAIs        = enemyAIs;
        battle             = battleController;
        AllEnemiesDefeated = false;
        promotionPending   = false;
        interferenceActive = false;
        interferingIndex   = -1;
        activeIndex        = 0;

        allEnemySMs.Clear();
        enemyStartPos.Clear();
        foreach (var ai in allEnemyAIs)
        {
            allEnemySMs.Add(ai.GetComponent<CombatStateMachine2D>());
            enemyStartPos.Add(ai.transform.position);
        }

        int n = enemyAIs.Count;
        lastTargetPlayerX   = new float[n];
        cachedStandbyTarget = new Vector3[n];

        InitStandbyCache();
        ActivateEnemy(0);
    }

    public void ResetForNewRound()
    {
        StopAllCoroutines();
        AllEnemiesDefeated = false;
        promotionPending   = false;
        interferenceActive = false;
        interferingIndex   = -1;

        for (int i = 0; i < allEnemyAIs.Count; i++)
        {
            if (i < enemyStartPos.Count)
                allEnemyAIs[i].transform.position = enemyStartPos[i];

            Rigidbody2D rb = allEnemyAIs[i].GetComponent<Rigidbody2D>();
            if (rb != null) { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; }

            allEnemySMs[i].ResetForRound();

            GuardGauge gg = allEnemyAIs[i].GetComponent<GuardGauge>();
            if (gg != null) gg.ResetToMax();

            if (i == 0) allEnemyAIs[i].SetStandbyMode(false);
            else        allEnemyAIs[i].SetStandbyMode(true);
        }

        InitStandbyCache();
        ActivateEnemy(0);
    }

    // -------------------------------------------------------------------------

    private void Update()
    {
        if (playerActor == null || AllEnemiesDefeated) return;

        // アクティブ敵が死んだら昇格
        if (!promotionPending
            && allEnemySMs[activeIndex].CurrentStateType == CombatStateType.Dead)
        {
            promotionPending = true;
            if (interferenceActive) CancelInterference(); // 邪魔中断
            StartCoroutine(PromoteWithDelay());
        }

        // 邪魔判定：プレイヤーがスタンバイ敵に近づいたときだけ発動を試みる
        if (!interferenceActive && !promotionPending)
        {
            interferenceTimer -= Time.deltaTime;
            if (interferenceTimer <= 0f)
            {
                interferenceTimer = 1f;
                TryInterference();
            }
        }

        UpdateStandbyEnemies();
    }

    // -------------------------------------------------------------------------

    private void ActivateEnemy(int index)
    {
        if (index >= allEnemyAIs.Count) return;
        activeIndex = index;
        battle.SetActors(playerActor, allEnemySMs[index]);
        allEnemyAIs[index].SetStandbyMode(false);

        if (playerFacing != null)
            playerFacing.target = allEnemyAIs[index].transform;

        Debug.Log($"[MultiEnemyManager] Enemy {index} is now active.", this);
    }

    // ── 昇格：生きているスタンバイからランダム選択 ──────────────────────────

    private IEnumerator PromoteWithDelay()
    {
        yield return new WaitForSeconds(promotionDelay);
        promotionPending = false;

        // 生きているスタンバイ（or 邪魔中）の敵をランダムに選ぶ
        var candidates = new List<int>();
        for (int i = 0; i < allEnemySMs.Count; i++)
        {
            if (i == activeIndex) continue;
            if (allEnemySMs[i].CurrentStateType == CombatStateType.Dead) continue;
            candidates.Add(i);
        }

        if (candidates.Count == 0)
        {
            AllEnemiesDefeated = true;
            Debug.Log("[MultiEnemyManager] All enemies defeated!", this);
            yield break;
        }

        // プレイヤーに最も近いスタンバイ敵を選ぶ
        float playerX = playerTransform != null ? playerTransform.position.x : 0f;
        int   next    = candidates[0];
        float minDist = Mathf.Abs(allEnemyAIs[candidates[0]].transform.position.x - playerX);
        for (int ci = 1; ci < candidates.Count; ci++)
        {
            float d = Mathf.Abs(allEnemyAIs[candidates[ci]].transform.position.x - playerX);
            if (d < minDist) { minDist = d; next = candidates[ci]; }
        }

        // もし邪魔中だった敵が選ばれたら邪魔状態を解除してからアクティブに
        if (next == interferingIndex)
        {
            interferenceActive = false;
            interferingIndex   = -1;
        }
        ActivateEnemy(next);
    }

    // ── 邪魔攻撃 ──────────────────────────────────────────────────────────────

    private void TryInterference()
    {
        if (playerTransform == null) return;

        float playerX = playerTransform.position.x;

        for (int i = 0; i < allEnemyAIs.Count; i++)
        {
            if (i == activeIndex) continue;
            if (allEnemySMs[i].CurrentStateType == CombatStateType.Dead) continue;
            if (!allEnemyAIs[i].IsStandby) continue;

            // プレイヤーがスタンバイ敵の近くにいるときだけ判定
            float dist = Mathf.Abs(allEnemyAIs[i].transform.position.x - playerX);
            if (dist > interferenceProximity) continue;

            if (Random.value >= interferenceChancePerSecond) continue;

            StartCoroutine(DoInterference(i));
            break; // 一度に1体だけ
        }
    }

    private IEnumerator DoInterference(int index)
    {
        interferenceActive = true;
        interferingIndex   = index;

        // スタンバイ解除→必ず攻撃（パリィ・フェイントなし）
        allEnemyAIs[index].SetStandbyMode(false);
        allEnemyAIs[index].SetForceAttackOnce(); // 最初の行動を強制 Attack に

        // バトル解決をこの敵に向ける（プレイヤーがガード・パリィで対応できる）
        battle.SetActors(playerActor, allEnemySMs[index]);
        Debug.Log($"[MultiEnemyManager] Enemy {index} interfering!", this);

        yield return new WaitForSeconds(returnToStandbyDelay);

        // 邪魔中に死んだ or すでに昇格で別の敵がアクティブになった場合は何もしない
        if (!interferenceActive) yield break;
        if (allEnemySMs[index].CurrentStateType == CombatStateType.Dead)
        {
            interferenceActive = false;
            interferingIndex   = -1;
            // バトルをアクティブ敵に戻す
            battle.SetActors(playerActor, allEnemySMs[activeIndex]);
            yield break;
        }

        // ── スワップ判定：そのままアクティブ交代するか、戻るか ────────────────
        bool doSwap = Random.value < swapChance;

        if (doSwap)
        {
            // 旧アクティブをスタンバイへ（生きている場合）
            if (allEnemySMs[activeIndex].CurrentStateType != CombatStateType.Dead)
                allEnemyAIs[activeIndex].SetStandbyMode(true);

            interferenceActive = false;
            interferingIndex   = -1;
            ActivateEnemy(index); // 邪魔していた敵がアクティブに
            Debug.Log($"[MultiEnemyManager] Enemy {index} swapped to active!", this);
        }
        else
        {
            // 戻る：スタンバイに戻してバトルを元のアクティブ敵に復元
            allEnemyAIs[index].SetStandbyMode(true);
            battle.SetActors(playerActor, allEnemySMs[activeIndex]);
            interferenceActive = false;
            interferingIndex   = -1;
            Debug.Log($"[MultiEnemyManager] Enemy {index} returned to standby.", this);
        }
    }

    private void CancelInterference()
    {
        if (!interferenceActive) return;
        interferenceActive = false;
        // 邪魔中の敵が生きていればスタンバイへ
        if (interferingIndex >= 0
            && interferingIndex < allEnemyAIs.Count
            && allEnemySMs[interferingIndex].CurrentStateType != CombatStateType.Dead)
        {
            allEnemyAIs[interferingIndex].SetStandbyMode(true);
        }
        interferingIndex = -1;
    }

    // ── スタンバイ位置制御 ───────────────────────────────────────────────────

    private void UpdateStandbyEnemies()
    {
        if (playerTransform == null) return;

        float playerX = playerTransform.position.x;
        float activeX = allEnemyAIs[activeIndex].transform.position.x;
        float fightLeft  = Mathf.Min(playerX, activeX);
        float fightRight = Mathf.Max(playerX, activeX);

        for (int i = 0; i < allEnemyAIs.Count; i++)
        {
            if (i == activeIndex) continue;
            if (allEnemySMs[i].CurrentStateType == CombatStateType.Dead) continue;
            if (!allEnemyAIs[i].IsStandby) continue;

            float enemyX      = allEnemyAIs[i].transform.position.x;
            float currentSide = Mathf.Sign(enemyX - playerX);
            if (currentSide == 0f) currentSide = (i % 2 == 0) ? 1f : -1f;

            float idealX = currentSide > 0f
                ? fightRight + standbyDistance
                : fightLeft  - standbyDistance;

            bool tooClose  = Mathf.Abs(enemyX - playerX) < minStandbyDistance;
            bool zoneMoved = Mathf.Abs(lastTargetPlayerX[i] - playerX) > targetUpdateThreshold;

            if (zoneMoved || tooClose)
            {
                lastTargetPlayerX[i]   = playerX;
                cachedStandbyTarget[i] = new Vector3(idealX, 0f, 0f);
            }

            allEnemyAIs[i].SetStandbyTarget(cachedStandbyTarget[i]);
        }
    }

    private void InitStandbyCache()
    {
        float px   = playerTransform != null ? playerTransform.position.x : 0f;
        int   slot = 0;
        for (int i = 0; i < allEnemyAIs.Count; i++)
        {
            if (i == 0) continue;
            float side = (slot % 2 == 0) ? 1f : -1f;
            lastTargetPlayerX[i]   = px;
            cachedStandbyTarget[i] = new Vector3(px + side * standbyDistance, 0f, 0f);
            slot++;
        }
    }
}
