using UnityEngine;

[RequireComponent(typeof(CombatStateMachine2D))]
public class GuardGauge : MonoBehaviour
{
    public int maxGuard = 4;
    public float recoveryPerSecond = 1f;
    public float guardBreakLockDuration = 2f;

    [Tooltip("ガードブレイク時の Vulnerable 持続秒数（パリィ成功と同じ値にする）")]
    public float guardBreakVulnerableDuration = 1.2f;

    [Header("Guard Knockback")]
    [Tooltip("ガードダメージ量がこの値以上のとき後退させる")]
    public int knockbackDamageThreshold = 2;
    [Tooltip("後退の速度（大きいほど吹き飛ぶ）")]
    public float knockbackForce = 3.5f;
    [Tooltip("後退の持続時間（秒）")]
    public float knockbackDuration = 0.15f;

    public int CurrentGuard => currentGuard;
    public int MaxGuard => maxGuard;

    private CombatStateMachine2D stateMachine;
    private Rigidbody2D          rb;
    private int   currentGuard;
    private float recoveryTimer;
    private float knockbackTimer;
    private float knockbackDirection;

    private void Awake()
    {
        stateMachine = GetComponent<CombatStateMachine2D>();
        rb           = GetComponent<Rigidbody2D>();
        currentGuard = Mathf.Max(1, maxGuard);
    }

    private void Start()
    {
        LogGuardValue();
    }

    private void OnValidate()
    {
        if (maxGuard < 1)
        {
            maxGuard = 1;
        }

        if (recoveryPerSecond < 0f)
        {
            recoveryPerSecond = 0f;
        }
    }

    private void Update()
    {
        // バックノック
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.deltaTime;
            if (rb != null)
                rb.linearVelocity = new Vector2(knockbackDirection * knockbackForce, rb.linearVelocity.y);
        }

        // ゲージ回復
        if (currentGuard >= maxGuard || recoveryPerSecond <= 0f) return;

        recoveryTimer += Time.deltaTime;
        float recoveryInterval = 1f / recoveryPerSecond;
        while (recoveryTimer >= recoveryInterval && currentGuard < maxGuard)
        {
            recoveryTimer -= recoveryInterval;
            currentGuard++;
            LogGuardValue();
        }
    }

    /// <param name="damage">ガードゲージを削る量。攻撃側の CharacterStats.guardDamageDealt を渡す。</param>
    /// <param name="attackerPosition">攻撃者の位置（ノックバック方向の計算に使用）</param>
    public bool ConsumeGuard(int damage = 1, Vector3? attackerPosition = null)
    {
        if (stateMachine == null || stateMachine.CurrentStateType != CombatStateType.Guard)
        {
            return false;
        }

        currentGuard = Mathf.Max(0, currentGuard - Mathf.Max(1, damage));
        recoveryTimer = 0f;
        LogGuardValue();

        // 強い攻撃のとき後退させる
        if (damage >= knockbackDamageThreshold && attackerPosition.HasValue)
        {
            float dir = transform.position.x > attackerPosition.Value.x ? 1f : -1f;
            knockbackDirection = dir;
            knockbackTimer     = knockbackDuration;
        }

        if (currentGuard == 0)
        {
            // パリィ成功と同じ Vulnerable に入る（GuardBreak ではなく）
            stateMachine.TriggerGuardBreakAsVulnerable(guardBreakLockDuration, guardBreakVulnerableDuration);
            Debug.Log($"{name} Guard Break → Vulnerable {guardBreakVulnerableDuration}s", this);
            return true;
        }

        return false;
    }

    public void ResetToMax()
    {
        currentGuard = maxGuard;
        recoveryTimer = 0f;
        LogGuardValue();
    }

    /// <summary>maxGuard を変更してから満タンにリセットする（タイプ切り替え時に使用）。</summary>
    public void ResetWithMaxGuard(int newMaxGuard)
    {
        maxGuard     = Mathf.Max(1, newMaxGuard);
        currentGuard = maxGuard;
        recoveryTimer = 0f;
        LogGuardValue();
    }

    private void LogGuardValue()
    {
        Debug.Log($"{name} Guard: {currentGuard}/{maxGuard}", this);
    }
}
