using UnityEngine;

/// <summary>
/// OnGUI（IMGUI）でEnemyType切り替えボタンを描画する。
/// EventSystem・Input System に一切依存しないため確実に動作する。
/// </summary>
public class EnemyTypeTestUI : MonoBehaviour
{
    private static readonly Color[] TypeColors = new Color[]
    {
        new Color(0.3f, 0.6f, 1.0f), // ParrySpammer : 青
        new Color(1.0f, 0.3f, 0.3f), // Attacker     : 赤
        new Color(0.8f, 0.3f, 1.0f), // Feinter      : 紫
        new Color(1.0f, 0.6f, 0.1f), // Rusher       : オレンジ
        new Color(0.3f, 0.8f, 0.4f), // Careful      : 緑
        new Color(0.2f, 0.9f, 0.9f), // Dodger       : シアン
    };

    private EnemyRandomCombatAI2D enemyAI;
    private SpriteRenderer        enemyRenderer;
    private EnemyType[]           types;
    private GUIStyle              buttonStyle;
    private GUIStyle              labelStyle;
    private bool                  stylesReady;

    // ボタンサイズ
    private const float BtnW    = 115f;
    private const float BtnH    = 32f;
    private const float Spacing = 6f;
    private const float PanelH  = 72f;

    // -------------------------------------------------------------------------

    public void Setup(EnemyRandomCombatAI2D ai)
    {
        enemyAI       = ai;
        enemyRenderer = ai.GetComponent<SpriteRenderer>();
        types         = (EnemyType[])System.Enum.GetValues(typeof(EnemyType));
        ApplyCurrentType();
    }

    // -------------------------------------------------------------------------

    private void OnGUI()
    {
        if (enemyAI == null || types == null) return;

        InitStyles();

        float screenW = Screen.width;

        // 背景パネル
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, screenW, PanelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 現在タイプ表示
        GUI.Label(new Rect(0, 2f, screenW, 28f),
            $"Enemy: {enemyAI.enemyType}", labelStyle);

        // ボタン群
        float totalW = types.Length * BtnW + (types.Length - 1) * Spacing;
        float startX = (screenW - totalW) / 2f;
        float btnY   = PanelH - BtnH - 6f;

        for (int i = 0; i < types.Length; i++)
        {
            Color col     = i < TypeColors.Length ? TypeColors[i] : Color.white;
            bool selected = enemyAI.enemyType == types[i];
            float posX    = startX + i * (BtnW + Spacing);

            // 選択中は明るく、未選択は暗く
            GUI.color = selected ? col : col * 0.5f;

            if (GUI.Button(new Rect(posX, btnY, BtnW, BtnH), types[i].ToString(), buttonStyle))
            {
                OnTypeSelected(types[i]);
            }
        }

        GUI.color = Color.white;
    }

    // -------------------------------------------------------------------------

    private void OnTypeSelected(EnemyType type)
    {
        if (enemyAI == null) return;
        enemyAI.SetEnemyType(type);
        ApplyCurrentType();
        Debug.Log($"[EnemyTypeTestUI] → {type}", this);
    }

    private void ApplyCurrentType()
    {
        if (enemyAI == null || enemyRenderer == null) return;
        int   idx = System.Array.IndexOf(types, enemyAI.enemyType);
        Color col = idx >= 0 && idx < TypeColors.Length ? TypeColors[idx] : Color.white;
        enemyRenderer.color = col;
    }

    private void InitStyles()
    {
        if (stylesReady) return;
        stylesReady = true;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        buttonStyle.normal.textColor  = Color.white;
        buttonStyle.hover.textColor   = Color.white;
        buttonStyle.active.textColor  = Color.yellow;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
        };
        labelStyle.normal.textColor = Color.white;
    }
}
