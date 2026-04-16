using UnityEngine;

/// <summary>
/// キャラクター頭上にガードゲージをワールド空間（SpriteRenderer）で表示する。
///
/// ■ 位置   : LateUpdate() 毎に SpriteRenderer.bounds.max.y（ワールド上端）を参照して配置。
///             CharacterSpriteAnimator2D が Update() でスプライトを差し替えた後に
///             LateUpdate() が動くため、アニメーション中でも常に正確な頭上を追従する。
///             キャラのスケール変更にも自動対応。
///
/// ■ 表示条件:
///     - 満タン    → 非表示（fadeOutDuration 秒でフワッとフェードアウト）
///     - 満タン未満 → 即座にパッと表示
/// </summary>
public class CharacterGuardBarUI : MonoBehaviour
{
    [Tooltip("監視するガードゲージ")]
    public GuardGauge guardGauge;

    [Tooltip("キャラ足元(transform.position.y)からバー中央までのワールドオフセット（正=上）\n" +
             "キャラ身長が約1.3unitの場合、1.15f で頭上に表示される")]
    public float yOffset = 1.05f;

    [Tooltip("バーのワールド幅")]
    public float barWidth = 0.26f;

    [Tooltip("バーのワールド高さ")]
    public float barHeight = 0.032f;

    [Tooltip("背景の最大アルファ")]
    [Range(0f, 1f)]
    public float bgAlpha = 0.70f;

    [Tooltip("満タン回復後のフェードアウト時間（秒）")]
    public float fadeOutDuration = 0.8f;

    [Tooltip("ソーティングオーダー（キャラより大きい値にすること）")]
    public int sortingOrder = 12;

    // ─────────────────────────────────────────────────────────────────────

    private Transform            fillTr;
    private Transform            bgTr;
    private SpriteRenderer       fillSr;
    private SpriteRenderer       bgSr;
    private CombatStateMachine2D combatSM; // Dead 判定用
    private float currentAlpha = 0f;
    private float fadeTimer    = 0f;
    private bool  wasFull      = true;

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        combatSM = GetComponent<CombatStateMachine2D>();
        Build();
    }

    private void LateUpdate()
    {
        if (guardGauge == null) return;

        // Dead 状態は常に非表示（ゲージ残量に関わらず）
        if (combatSM != null && combatSM.CurrentStateType == CombatStateType.Dead)
        {
            currentAlpha = 0f;
            fadeTimer    = 0f;
            bgTr.gameObject.SetActive(false);
            fillTr.gameObject.SetActive(false);
            return;
        }

        // ── アルファ制御 ──────────────────────────────────────────────────
        bool isFull = guardGauge.CurrentGuard >= guardGauge.MaxGuard;

        if (!isFull)
        {
            // 満タン未満 → 即座に表示
            currentAlpha = 1f;
            fadeTimer    = fadeOutDuration;
            wasFull      = false;
        }
        else
        {
            if (!wasFull) wasFull = true; // 満タンに戻った瞬間
            fadeTimer    = Mathf.Max(0f, fadeTimer - Time.deltaTime);
            currentAlpha = fadeOutDuration > 0f ? fadeTimer / fadeOutDuration : 0f;
        }

        // 完全に透明 → 非アクティブ化して描画コストゼロ
        if (currentAlpha <= 0.001f)
        {
            bgTr.gameObject.SetActive(false);
            fillTr.gameObject.SetActive(false);
            return;
        }
        bgTr.gameObject.SetActive(true);
        fillTr.gameObject.SetActive(true);

        // ── バーの World Y 位置 ──────────────────────────────────────────
        // キャラ中心 (transform.position.y) から yOffset だけ上に表示する。
        // スプライト上端は使わない（スプライトのサイズがアニメによって変動するため）。
        float barWorldY = transform.position.y + yOffset;

        // ── ワールド座標をそのまま子オブジェクトの position に設定 ──────
        // （子は SetParent worldPositionStays=false で作っているので
        //    position = world position として使える）
        float lossySX = Mathf.Abs(transform.lossyScale.x);
        float lossySY = Mathf.Abs(transform.lossyScale.y);
        float invSX   = lossySX > 0f ? 1f / lossySX : 1f;
        float invSY   = lossySY > 0f ? 1f / lossySY : 1f;

        // barLocalY: 親(キャラ)のローカル空間でのY
        float barLocalY = (barWorldY - transform.position.y) * invSY;

        // ワールド固定サイズになるよう localScale を設定
        float localW = barWidth  * invSX;
        float localH = barHeight * invSY;

        // ── 背景 ─────────────────────────────────────────────────────────
        bgTr.localPosition = new Vector3(0f, barLocalY, 0f);
        bgTr.localScale    = new Vector3(localW, localH, 1f);
        bgSr.color         = new Color(0f, 0f, 0f, bgAlpha * currentAlpha);

        // ── フィル（左端固定・右に伸びる）────────────────────────────────
        float ratio  = guardGauge.MaxGuard > 0
            ? Mathf.Clamp01((float)guardGauge.CurrentGuard / guardGauge.MaxGuard)
            : 0f;
        float fillLW = Mathf.Max(0.0001f * invSX, localW * ratio);

        fillTr.localPosition = new Vector3(-localW * 0.5f + fillLW * 0.5f, barLocalY, -0.01f);
        fillTr.localScale    = new Vector3(fillLW, localH, 1f);

        Color fc = GetColor(ratio);
        fc.a     = currentAlpha;
        fillSr.color = fc;
    }

    // ─────────────────────────────────────────────────────────────────────
    // キャラクタースプライトのワールド上端 Y を取得
    //   LateUpdate() 内での呼び出しなので CharacterSpriteAnimator2D の
    //   Update() によるスプライト更新後の値が得られる。
    // ─────────────────────────────────────────────────────────────────────
    private void Build()
    {
        Sprite sq = Square();

        // 背景
        var bgGo = new GameObject("GuardBar_BG");
        bgGo.transform.SetParent(transform, false);
        bgSr = bgGo.AddComponent<SpriteRenderer>();
        bgSr.sprite       = sq;
        bgSr.color        = new Color(0f, 0f, 0f, 0f);
        bgSr.sortingOrder = sortingOrder;
        bgTr = bgGo.transform;
        bgGo.SetActive(false);

        // フィル
        var fillGo = new GameObject("GuardBar_Fill");
        fillGo.transform.SetParent(transform, false);
        fillSr = fillGo.AddComponent<SpriteRenderer>();
        fillSr.sprite       = sq;
        fillSr.color        = new Color(0f, 1f, 0f, 0f);
        fillSr.sortingOrder = sortingOrder + 1;
        fillTr = fillGo.transform;
        fillGo.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────

    private static Color GetColor(float ratio)
    {
        if (ratio <= 0.25f) return Color.red;
        if (ratio <= 0.55f) return Color.yellow;
        return Color.green;
    }

    private static Sprite _sq;
    private static Sprite Square()
    {
        if (_sq != null) return _sq;
        var tex  = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var cols = new Color[16];
        for (int i = 0; i < 16; i++) cols[i] = Color.white;
        tex.SetPixels(cols);
        tex.Apply();
        _sq = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _sq;
    }
}
