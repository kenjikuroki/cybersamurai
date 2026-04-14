using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    public float moveSpeed = 5f;

    [Tooltip("前進（敵に近づく）時の速度倍率。moveSpeed に掛け合わせる。")]
    public float forwardSpeedMultiplier  = 0.5f;   // 前 = 今の半分

    [Tooltip("後退（敵から離れる）時の速度倍率。moveSpeed に掛け合わせる。")]
    public float backwardSpeedMultiplier = 0.333f;  // 後ろ = 今の3分の1

    private Rigidbody2D         rb;
    private CombatStateMachine2D stateMachine;
    private Transform           enemyTransform;     // 前進・後退判定に使う
    private float               horizontalInput;
    private float               forwardMoveSpeed;
    private float               backwardMoveSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // CharacterStats があればそちらの moveSpeed を使う
        CharacterStats stats = GetComponent<CharacterStats>();
        float baseSpeed = (stats != null) ? stats.moveSpeed : moveSpeed;
        moveSpeed         = baseSpeed;
        forwardMoveSpeed  = baseSpeed * forwardSpeedMultiplier;
        backwardMoveSpeed = baseSpeed * backwardSpeedMultiplier;

        // 敵オブジェクトを検索（前進・後退の方向判定に使用）
        GameObject enemy = GameObject.Find("Enemy");
        if (enemy != null) enemyTransform = enemy.transform;
    }

    private void Update()
    {
        // 遅延初期化：動的 AddComponent に対応
        if (stateMachine == null)
            stateMachine = GetComponent<CombatStateMachine2D>();

        if (stateMachine != null && stateMachine.IsMovementLocked)
        {
            horizontalInput = 0f;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        horizontalInput = GetHorizontalInput();
    }

    private void FixedUpdate()
    {
        if (horizontalInput == 0f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float speed = CalcSpeed(horizontalInput);
        rb.linearVelocity = new Vector2(horizontalInput * speed, rb.linearVelocity.y);
    }

    /// <summary>
    /// 入力方向と敵の位置から前進・後退を判定し、対応する速度を返す。
    /// 敵が不明なら前進速度を使う。
    /// </summary>
    private float CalcSpeed(float inputDir)
    {
        if (enemyTransform == null) return forwardMoveSpeed;

        float toEnemy = enemyTransform.position.x - transform.position.x;
        // 入力方向と敵方向が同じ符号 → 前進
        bool movingTowardEnemy = (inputDir > 0f) == (toEnemy > 0f);
        return movingTowardEnemy ? forwardMoveSpeed : backwardMoveSpeed;
    }

    private static float GetHorizontalInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return 0f;

        bool moveLeft  = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
        bool moveRight = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;

        if (moveLeft == moveRight) return 0f;
        return moveLeft ? -1f : 1f;
    }
}
