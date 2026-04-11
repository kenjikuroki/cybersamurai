using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CombatStateMachine2D))]
public class CharacterStateVisual2D : MonoBehaviour
{
    public bool isPlayer;
    public float blinkSpeed = 10f;

    private SpriteRenderer spriteRenderer;
    private CombatStateMachine2D stateMachine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        stateMachine = GetComponent<CombatStateMachine2D>();
    }

    private void Update()
    {
        if (spriteRenderer == null || stateMachine == null)
        {
            return;
        }

        spriteRenderer.color = GetStateColor(stateMachine.CurrentStateType);
    }

    private Color GetStateColor(CombatStateType state)
    {
        switch (state)
        {
            case CombatStateType.Guard:
                return Color.green;
            case CombatStateType.Attack:
                return Color.yellow;
            case CombatStateType.Parry:
                return Color.blue;
            case CombatStateType.Feint:
                return new Color(0.7f, 0.2f, 1f, 1f);
            case CombatStateType.Vulnerable:
                return Blink(new Color(1f, 0.5f, 0f, 1f), Color.clear);
            case CombatStateType.GuardBreak:
                return Blink(Color.red, Color.clear);
            case CombatStateType.Dead:
                return Color.black;
            case CombatStateType.Idle:
            default:
                return isPlayer ? Color.white : Color.red;
        }
    }

    private Color Blink(Color visible, Color hidden)
    {
        float t = Mathf.PingPong(Time.time * blinkSpeed, 1f);
        return Color.Lerp(hidden, visible, t);
    }
}
