using UnityEngine;

/// <summary>
/// キャラクター固有の戦闘パラメーターを一元管理するコンポーネント。
/// CombatStateMachine2D / GuardGauge / JankenCombatResolver2D が参照する。
///
/// プレイヤーと敵の両方にアタッチする。
/// EnemyRandomCombatAI2D.SetEnemyType() から ApplyToCombatStateMachine() を呼ぶことで
/// タイプ切り替え時にも即座に反映される。
/// </summary>
public class CharacterStats : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("移動速度。プレイヤーより大きい値は設定しないこと。")]
    public float moveSpeed = 5f;

    [Header("Attack")]
    [Tooltip("1打撃のロック時間（小さいほど速い）")]
    public float attackDuration = 0.65f;

    [Header("Guard")]
    [Tooltip("この キャラの攻撃が相手のガードゲージを何ポイント削るか")]
    public int guardDamageDealt = 1;

    [Header("Parry")]
    [Tooltip("パリィが成立する時間ウィンドウ（大きいほど上手い / 判定が甘い）")]
    public float parryWindowSize = 0.35f;

    // -------------------------------------------------------------------------

    /// <summary>
    /// このキャラの attackDuration を CombatStateMachine2D に適用する。
    /// SetEnemyType() などタイプ変更時に呼ぶ。
    /// </summary>
    public void ApplyToCombatStateMachine()
    {
        CombatStateMachine2D sm = GetComponent<CombatStateMachine2D>();
        if (sm != null) sm.attackDuration = attackDuration;
    }

    /// <summary>
    /// このキャラの moveSpeed を PlayerMovement2D / EnemyRandomCombatAI2D に適用する。
    /// </summary>
    public void ApplyMoveSpeed()
    {
        PlayerMovement2D playerMovement = GetComponent<PlayerMovement2D>();
        if (playerMovement != null) playerMovement.moveSpeed = moveSpeed;

        EnemyRandomCombatAI2D enemyAI = GetComponent<EnemyRandomCombatAI2D>();
        if (enemyAI != null) enemyAI.moveSpeed = moveSpeed;
    }
}
