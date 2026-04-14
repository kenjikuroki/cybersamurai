using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 複数の敵を管理する。
///
/// 構造:
///   activeEnemy   : プレイヤーと直接戦う敵（1体）
///   standbyEnemies: 距離を保って待機し、時々邪魔しに来る敵
///
/// アクティブ敵が倒れると次のスタンバイ敵がアクティブになる。
/// </summary>
public class MultiEnemyManager : MonoBehaviour
{
    [Tooltip("邪魔をしに来る確率（毎秒の判定）")]
    [Range(0f, 1f)]
    public float interferenceChancePerSecond = 0.15f;

    [Tooltip("邪魔が終わったら元の待機位置に戻るまでの秒数")]
    public float returnToStandbyDelay = 2.5f;

    [Tooltip("待機敵のX方向オフセット（プレイヤーの後ろ側に配置）")]
    public float standbyXOffset = 2.5f;

    [Tooltip("待機敵のY位置（プレイヤーとずらす）")]
    public float standbyYOffset = 0.35f;

    // -------------------------------------------------------------------------

    private ICombatStateActor                  playerActor;
    private Transform                          playerTransform;
    private List<EnemyRandomCombatAI2D>        allEnemyAIs   = new List<EnemyRandomCombatAI2D>();
    private List<CombatStateMachine2D>         allEnemySMs   = new List<CombatStateMachine2D>();
    private int                                activeIndex   = 0;
    private ProximityJankenBattle2D            battle;

    private float interferenceTimer;

    // -------------------------------------------------------------------------

    public void Setup(
        ICombatStateActor          player,
        List<EnemyRandomCombatAI2D> enemyAIs,
        ProximityJankenBattle2D     battleController)
    {
        playerActor     = player;
        playerTransform = (player as Component)?.transform;
        allEnemyAIs     = enemyAIs;
        battle          = battleController;

        foreach (var ai in allEnemyAIs)
            allEnemySMs.Add(ai.GetComponent<CombatStateMachine2D>());

        // 最初のアクティブ敵を設定
        ActivateEnemy(0);
    }

    private void Update()
    {
        if (playerActor == null) return;

        // アクティブ敵が死んだら次を起動
        if (activeIndex < allEnemySMs.Count)
        {
            if (allEnemySMs[activeIndex].CurrentStateType == CombatStateType.Dead)
                PromoteNextEnemy();
        }

        // スタンバイ敵の邪魔タイミング判定
        interferenceTimer -= Time.deltaTime;
        if (interferenceTimer <= 0f)
        {
            interferenceTimer = 1f; // 1秒ごとに判定
            TryInterference();
        }

        // スタンバイ敵を待機位置に誘導
        UpdateStandbyPositions();
    }

    // -------------------------------------------------------------------------

    private void ActivateEnemy(int index)
    {
        if (index >= allEnemyAIs.Count) return;

        activeIndex = index;

        // バトルコントローラーにアクティブ敵を通知
        var activeSM = allEnemySMs[index];
        battle.SetActors(playerActor, activeSM);
        allEnemyAIs[index].SetStandbyMode(false);

        Debug.Log($"[MultiEnemyManager] Enemy {index} is now active.", this);
    }

    private void PromoteNextEnemy()
    {
        int next = activeIndex + 1;
        if (next >= allEnemyAIs.Count)
        {
            Debug.Log("[MultiEnemyManager] All enemies defeated.", this);
            return;
        }
        ActivateEnemy(next);
    }

    /// <summary>スタンバイ敵がランダムに邪魔をしに来る。</summary>
    private void TryInterference()
    {
        for (int i = 0; i < allEnemyAIs.Count; i++)
        {
            if (i == activeIndex) continue;
            var sm = allEnemySMs[i];
            if (sm.CurrentStateType == CombatStateType.Dead) continue;
            if (!allEnemyAIs[i].IsStandby) continue;

            if (Random.value < interferenceChancePerSecond)
            {
                Debug.Log($"[MultiEnemyManager] Enemy {i} interfering!", this);
                allEnemyAIs[i].SetStandbyMode(false);

                // 一定時間後にスタンバイに戻す
                StartCoroutine(ReturnToStandby(i, returnToStandbyDelay));
            }
        }
    }

    /// <summary>スタンバイ敵をプレイヤーの後方・ずれた位置に誘導する。</summary>
    private void UpdateStandbyPositions()
    {
        if (playerTransform == null) return;

        int standbySlot = 0;
        for (int i = 0; i < allEnemyAIs.Count; i++)
        {
            if (i == activeIndex) continue;
            if (allEnemySMs[i].CurrentStateType == CombatStateType.Dead) continue;
            if (!allEnemyAIs[i].IsStandby) continue;

            // プレイヤーから見て後方に待機位置を設定
            float targetX = playerTransform.position.x
                + (standbySlot % 2 == 0 ? -standbyXOffset : standbyXOffset);
            float targetY = playerTransform.position.y
                + standbyYOffset * (standbySlot + 1);

            allEnemyAIs[i].SetStandbyTarget(new Vector3(targetX, targetY, 0f));
            standbySlot++;
        }
    }

    private System.Collections.IEnumerator ReturnToStandby(int index, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (index != activeIndex && allEnemySMs[index].CurrentStateType != CombatStateType.Dead)
        {
            allEnemyAIs[index].SetStandbyMode(true);
            Debug.Log($"[MultiEnemyManager] Enemy {index} returned to standby.", this);
        }
    }
}
