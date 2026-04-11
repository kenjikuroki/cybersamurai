using UnityEngine;

[RequireComponent(typeof(CombatStateMachine2D))]
public class GuardGauge : MonoBehaviour
{
    public int maxGuard = 6;
    public float recoveryPerSecond = 1f;
    public float guardBreakLockDuration = 2f;

    public int CurrentGuard => currentGuard;
    public int MaxGuard => maxGuard;

    private CombatStateMachine2D stateMachine;
    private int currentGuard;
    private float recoveryTimer;

    private void Awake()
    {
        stateMachine = GetComponent<CombatStateMachine2D>();
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
        if (currentGuard >= maxGuard || recoveryPerSecond <= 0f)
        {
            return;
        }

        recoveryTimer += Time.deltaTime;
        float recoveryInterval = 1f / recoveryPerSecond;
        while (recoveryTimer >= recoveryInterval && currentGuard < maxGuard)
        {
            recoveryTimer -= recoveryInterval;
            currentGuard++;
            LogGuardValue();
        }
    }

    public bool ConsumeGuard()
    {
        if (stateMachine == null || stateMachine.CurrentStateType != CombatStateType.Guard)
        {
            return false;
        }

        currentGuard = Mathf.Max(0, currentGuard - 1);
        recoveryTimer = 0f;
        LogGuardValue();

        // ガードが削られた瞬間にリアクションアニメを再生
        CharacterSpriteAnimator2D anim = GetComponent<CharacterSpriteAnimator2D>();
        if (anim != null) anim.TriggerGuardReaction();

        if (currentGuard == 0)
        {
            stateMachine.TriggerGuardBreak(guardBreakLockDuration);
            Debug.Log($"{name} Guard Break", this);
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

    private void LogGuardValue()
    {
        Debug.Log($"{name} Guard: {currentGuard}/{maxGuard}", this);
    }
}
