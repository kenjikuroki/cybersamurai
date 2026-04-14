using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    public float moveSpeed = 5f;

    [Tooltip("前進（敵に近づく）時の速度倍率。moveSpeed に掛け合わせる。")]
    public float forwardSpeedMultiplier  = 0.5f;

    [Tooltip("後退（敵から離れる）時の速度倍率。moveSpeed に掛け合わせる。")]
    public float backwardSpeedMultiplier = 0.333f;

    [Tooltip("スティック入力のデッドゾーン")]
    public float stickDeadzone = 0.2f;

    private Rigidbody2D          rb;
    private CombatStateMachine2D stateMachine;
    private Transform            enemyTransform;
    private float                horizontalInput;
    private float                forwardMoveSpeed;
    private float                backwardMoveSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        CharacterStats stats = GetComponent<CharacterStats>();
        float baseSpeed = (stats != null) ? stats.moveSpeed : moveSpeed;
        moveSpeed         = baseSpeed;
        forwardMoveSpeed  = baseSpeed * forwardSpeedMultiplier;
        backwardMoveSpeed = baseSpeed * backwardSpeedMultiplier;

        GameObject enemy = GameObject.Find("Enemy1");
        if (enemy == null) enemy = GameObject.Find("Enemy");
        if (enemy != null) enemyTransform = enemy.transform;
    }

    private void Update()
    {
        if (stateMachine == null)
            stateMachine = GetComponent<CombatStateMachine2D>();

        if (stateMachine != null && stateMachine.IsMovementLocked)
        {
            horizontalInput = 0f;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        horizontalInput = GetHorizontalInput(stickDeadzone);
    }

    private void FixedUpdate()
    {
        float hSpeed = horizontalInput == 0f ? 0f : CalcSpeed(horizontalInput);
        rb.linearVelocity = new Vector2(horizontalInput * hSpeed, 0f);
    }

    private float CalcSpeed(float inputDir)
    {
        if (enemyTransform == null) return forwardMoveSpeed;
        float toEnemy = enemyTransform.position.x - transform.position.x;
        bool movingTowardEnemy = (inputDir > 0f) == (toEnemy > 0f);
        return movingTowardEnemy ? forwardMoveSpeed : backwardMoveSpeed;
    }

    public static float GetHorizontalInput(float deadzone = 0.2f)
    {
        // キーボード
        var kb = Keyboard.current;
        if (kb != null)
        {
            bool left  = kb.aKey.isPressed || kb.leftArrowKey.isPressed;
            bool right = kb.dKey.isPressed || kb.rightArrowKey.isPressed;
            if (left != right) return left ? -1f : 1f;
        }

        // ゲームパッド（左スティック＋十字キー）
        var gp = Gamepad.current;
        if (gp != null)
        {
            float stickX = gp.leftStick.x.ReadValue();
            if (Mathf.Abs(stickX) > deadzone) return Mathf.Sign(stickX);
            if (gp.dpad.left.isPressed)  return -1f;
            if (gp.dpad.right.isPressed) return  1f;
        }

        return 0f;
    }
}
