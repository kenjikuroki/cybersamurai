using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 複数の敵を管理する。
///
/// 構造:
///   activeEnemy   : プレイヤーと直接戦う敵（1体）
///   standbyEnemies: プレイヤーを囲んでじりじり前後に動きながら待機。
///                   時々邪魔しに来て、すぐ戻る。
/// </summary>
public class MultiEnemyManager : MonoBehaviour
{
    [Header("Interference（邪魔）")]
    [Tooltip("邪魔をしに来る確率（毎秒の判定）")]
    [Range(0f, 1f)]
    public float interferenceChancePerSecond = 0.12f;

    [Tooltip("邪魔をしに来てから戻るまでの秒数")]
    public float returnToStandbyDelay = 2.5f;

    [Header("Standby Position（待機位置）")]
    [Tooltip("プレイヤーから見てスタンバイ敵を配置するX距離")]
    public float standbyDistance = 1.8f;

    [Tooltip("スタンバイ敵のY方向オフセット（手前側）")]
    public float standbyYBase = -0.30f;

    [Header("Standby Movement（じりじり動き）")]
    [Tooltip("前後に動く幅（歩数感覚）")]
    public float prowlRange = 0.35f;

    [Tooltip("前後ひとステップの所要秒数")]
    public float prowlStepDuration = 1.4f;

    [Tooltip("ステップ間のランダム待機幅（秒）")]
    public float prowlPauseJitter = 0.8f;

    // -------------------------------------------------------------------------

    private ICombatStateActor           playerActor;
    private Transform                   playerTransform;
    private List<EnemyRandomCombatAI2D> allEnemyAIs = new List<EnemyRandomCombatAI2D>();
    private List<CombatStateMachine2D>  allEnemySMs = new List<CombatStateMachine2D>();
    private int                         activeIndex = 0;
    private ProximityJankenBattle2D     battle;

    private float interferenceTimer;

    // スタンバイ各体のじりじりオフセット（X方向）
    private float[] prowlOffsets;
    private float[] prowlDirections;
    private float[] prowlTimers;

    // -------------------------------------------------------------------------

    public void Setup(
        ICombatStateActor           player,
        List<EnemyRandomCombatAI2D> enemyAIs,
        ProximityJankenBattle2D     battleController)
    {
        playerActor     = player;
        playerTransform = (player as Component)?.transform;
        allEnemyAIs     = enemyAIs;
        battle          = battleController;

        foreach (var ai in allEnemyAIs)
            allEnemySMs.Add(ai.GetComponent<CombatStateMachine2D>());

        // じりじり動き用配列を初期化
        prowlOffsets    = new float[enemyAIs.Count];
        prowlDirections = new float[enemyAIs.Count];
        prowlTimers     = new float[enemyAIs.Count];
        for (int i = 0; i < enemyAIs.Count; i++)
        {
            prowlOffsets[i]    = 0f;
            prowlDirections[i] = Random.value > 0.5f ? 1f : -1f;
            prowlTimers[i]     = Random.Range(0f, prowlStepDuration); // バラつき初期化
        }

        ActivateEnemy(0);
    }

    private void Update()
    {
        if (playerActor == null) return;

        // アクティブ敵が死んだら次を起動
        if (activeIndex < allEnemySMs.Count &&
            allEnemySMs[activeIndex].CurrentStateType == CombatStateType.Dead)
            PromoteNextEnemy();

        // 邪魔タイミング判定（1秒ごと）
        interferenceTimer -= Time.deltaTime;
        if (interferenceTimer <= 0f)
        {
            interferenceTimer = 1f;
            TryInterference();
        }

        // スタンバイ敵の更新
        UpdateStandbyEnemies();
    }

    // -------------------------------------------------------------------------

    private void ActivateEnemy(int index)
    {
        if (index >= allEnemyAIs.Count) return;
        activeIndex = index;
        battle.SetActors(playerActor, allEnemySMs[index]);
        allEnemyAIs[index].SetStandbyMode(false);
        Debug.Log($"[MultiEnemyManager] Enemy {index} is now active.", this);
    }

    private void PromoteNextEnemy()
    {
        int next = activeIndex + 1;
        if (next >= allEnemyAIs.Count) { Debug.Log("[MultiEnemyManager] All enemies defeated.", this); return; }
        ActivateEnemy(next);
    }

    private void TryInterference()
    {
        for (int i = 0; i < allEnemyAIs.Count; i++)
        {
            if (i == activeIndex) continue;
            if (allEnemySMs[i].CurrentStateType == CombatStateType.Dead) continue;
            if (!allEnemyAIs[i].IsStandby) continue;
            if (Random.value >= interferenceChancePerSecond) continue;

            Debug.Log($"[MultiEnemyManager] Enemy {i} interfering!", this);
            allEnemyAIs[i].SetStandbyMode(false);
            StartCoroutine(ReturnToStandby(i, returnToStandbyDelay));
        }
    }

    private void UpdateStandbyEnemies()
    {
        if (playerTransform == null) return;

        int slot = 0;
        for (int i = 0; i < allEnemyAIs.Count; i++)
        {
            if (i == activeIndex) continue;
            if (allEnemySMs[i].CurrentStateType == CombatStateType.Dead) continue;
            if (!allEnemyAIs[i].IsStandby) continue;

            // じりじり動きのオフセットを更新
            prowlTimers[i] -= Time.deltaTime;
            if (prowlTimers[i] <= 0f)
            {
                prowlOffsets[i] += prowlDirections[i] * prowlRange;
                prowlOffsets[i]  = Mathf.Clamp(prowlOffsets[i], -prowlRange, prowlRange);

                // 端に達したら方向転換
                if (Mathf.Abs(prowlOffsets[i]) >= prowlRange)
                    prowlDirections[i] *= -1f;

                // ランダムに方向転換することも
                if (Random.value < 0.3f) prowlDirections[i] *= -1f;

                prowlTimers[i] = prowlStepDuration + Random.Range(0f, prowlPauseJitter);
            }

            // 待機位置：プレイヤーの左右（交互）に配置 + じりじりオフセット
            float side    = (slot % 2 == 0) ? -1f : 1f;
            float targetX = playerTransform.position.x + side * standbyDistance + prowlOffsets[i];
            float targetY = standbyYBase - slot * 0.1f; // 少しずつ手前に

            allEnemyAIs[i].SetStandbyTarget(new Vector3(targetX, targetY, 0f));
            slot++;
        }
    }

    private IEnumerator ReturnToStandby(int index, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (index != activeIndex && allEnemySMs[index].CurrentStateType != CombatStateType.Dead)
        {
            allEnemyAIs[index].SetStandbyMode(true);
            Debug.Log($"[MultiEnemyManager] Enemy {index} returned to standby.", this);
        }
    }
}
