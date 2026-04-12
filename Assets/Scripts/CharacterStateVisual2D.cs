using UnityEngine;

/// <summary>
/// 通常時は白（スプライト本来の色）を表示する。
/// Vulnerable 状態中はオレンジと白の間をサイン波で点滅させる。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CombatStateMachine2D))]
public class CharacterStateVisual2D : MonoBehaviour
{
    public bool isPlayer; // 他スクリプトとの互換用に残す

    [Header("Vulnerable Blink")]
    [Tooltip("点滅時に使うオレンジ色")]
    public Color vulnerableColor = new Color(1f, 0.45f, 0f, 1f);

    [Tooltip("1秒あたりの点滅回数")]
    public float blinkFrequency = 6f;

    private SpriteRenderer     spriteRenderer;
    private CombatStateMachine2D stateMachine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        stateMachine   = GetComponent<CombatStateMachine2D>();

        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;
    }

    private void Update()
    {
        if (spriteRenderer == null || stateMachine == null) return;

        if (stateMachine.CurrentStateType == CombatStateType.Vulnerable)
        {
            // サイン波で 0〜1 に正規化し、白↔オレンジを補間
            float t = (Mathf.Sin(Time.time * blinkFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
            spriteRenderer.color = Color.Lerp(Color.white, vulnerableColor, t);
        }
        else
        {
            spriteRenderer.color = Color.white;
        }
    }
}
