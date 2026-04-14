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

    [Tooltip("上下（奥行き）移動の速度")]
    public float verticalSpeed = 2.5f;

    [Tooltip("移動できるY座標の下限（手前）")]
    public float minY = -0.7f;

    [Tooltip("移動できるY座標の上限（奥）")]
    public float maxY =  0.4f;

    [Tooltip("スティック入力のデッドゾーン")]
    public float stickDeadzone = 0.2f;

    private Rigidbody2D          rb;
    private CombatStateMachine2D stateMachine;
    private Transform            enemyTransform;
    private float                horizontalInput;
    private float                verticalInput;
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

        // 敵オブジェクトを検索（前進・後退の方向判定に使用）
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
            verticalInput   = 0f;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        horizontalInput = GetHorizontalInput(stickDeadzone);
        verticalInput   = GetVerticalInput(stickDeadzone);
    }

    private void FixedUpdate()
    {
        float hSpeed = horizontalInput == 0f ? 0f : CalcSpeed(horizontalInput);
        float vSpeed = verticalInput * verticalSpeed;

        rb.linearVelocity = new Vector2(horizontalInput * hSpeed, vSpeed);

        // Y座標をクランプ（疑似3D の移動範囲）
        Vector2 pos = rb.position;
        if (pos.y < minY || pos.y > maxY)
        {
            pos.y             = Mathf.Clamp(pos.y, minY, maxY);
            rb.position       = pos;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        }
    }

    private float CalcSpeed(float inputDir)
    {
        if (enemyTransform == null) return forwardMoveSpeed;
        float toEnemy = enemyTransform.position.x - transform.position.x;
        bool movingTowardEnemy = (inputDir > 0f) == (toEnemy > 0f);
        return movingTowardEnemy ? forwardMoveSpeed : backwardMoveSpeed;
    }

    // -------------------------------------------------------------------------

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

    public static float GetVerticalInput(float deadzone = 0.2f)
    {
        // キーボード（W/S のみ）
        var kb = Keyboard.current;
        if (kb != null)
        {
            bool up   = kb.wKey.isPressed;
            bool down = kb.sKey.isPressed;
            if (up != down) return up ? 1f : -1f;
        }

        // ゲームパッド（左スティック上下＋十字キー上下）
        var gp = Gamepad.current;
        if (gp != null)
        {
            float stickY = gp.leftStick.y.ReadValue();
            if (Mathf.Abs(stickY) > deadzone) return Mathf.Sign(stickY);

            if (gp.dpad.up.isPressed)   return  1f;
            if (gp.dpad.down.isPressed) return -1f;
        }

        return 0f;
    }
}
