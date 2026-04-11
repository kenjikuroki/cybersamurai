using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 状態マッピング:
///   Guard / Parry                    → Idle（ループ）
///   Idle（移動中）                    → Walk（ループ）
///   Attack                           → Jab（ステート持続時間に同期、1回再生）
///   Feint                            → Punch（ステート持続時間に同期、1回再生）
///   Parry                            → Idle（ステート持続時間に同期、1回再生）
///   Vulnerable / GuardBreak / Dead   → Hurt（ループ）
///   ガードヒット時（一時）            → Kick 先頭2枚
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CombatStateMachine2D))]
public class CharacterSpriteAnimator2D : MonoBehaviour
{
    [Tooltip("Idle / Walk / Hurt などループ系のフレームレート")]
    public float frameRate = 8f;

    [Tooltip("ガードリアクションの表示時間（秒）")]
    public float guardReactionSeconds = 0.4f;

    private SpriteRenderer       spriteRenderer;
    private CombatStateMachine2D stateMachine;

    private Sprite[] idleSprites;
    private Sprite[] walkSprites;
    private Sprite[] attackSprites;
    private Sprite[] feintSprites;
    private Sprite[] hurtSprites;
    private Sprite[] guardReactionSprites;

    private Sprite[] currentSprites;
    private int      currentFrame;
    private float    frameTimer;
    private float    currentFrameRate;   // 現在の再生レート（アニメごとに可変）
    private bool     loopAnimation;      // true=ループ / false=最終フレームで停止

    private CombatStateType lastState = (CombatStateType)(-1);
    private float guardReactionTimer;

    private static readonly string[] BrawlerRoots =
    {
        "StreetsOfFight/Sprites/Brawler-Girl",
        "Streets of Fight/Sprites/Brawler-Girl",
    };

    // -----------------------------------------------------------------------

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        stateMachine   = GetComponent<CombatStateMachine2D>();
    }

    private void Start()
    {
        idleSprites          = LoadFolder("Idle");
        walkSprites          = LoadFolder("Walk");
        attackSprites        = LoadFolder("Jab");
        feintSprites         = LoadFolder("Punch");
        hurtSprites          = LoadFolder("Hurt");
        guardReactionSprites = LoadFolder("Kick", maxFrames: 2);

        PlayAnimation(idleSprites, loop: true);
    }

    private void Update()
    {
        // ガードリアクション中
        if (guardReactionTimer > 0f)
        {
            guardReactionTimer -= Time.deltaTime;
            if (guardReactionTimer <= 0f)
            {
                guardReactionTimer = 0f;
                lastState = (CombatStateType)(-1); // 強制リフレッシュ
            }
            else
            {
                AdvanceFrame();
                return;
            }
        }

        UpdateState();
        AdvanceFrame();
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// GuardGauge.ConsumeGuard() から呼ぶ。
    /// ガードが削られた瞬間に一時的にガードリアクションアニメを再生する。
    /// </summary>
    public void TriggerGuardReaction()
    {
        if (guardReactionSprites == null || guardReactionSprites.Length == 0) return;
        guardReactionTimer = guardReactionSeconds;
        PlayAnimation(guardReactionSprites,
                      duration: guardReactionSeconds,
                      loop: true);
    }

    // -----------------------------------------------------------------------

    private void UpdateState()
    {
        CombatStateType current = stateMachine.CurrentStateType;
        if (current == lastState) return;
        lastState = current;

        switch (current)
        {
            // ── アクション系：固定fps で1回再生し最終フレームで停止 ──
            // （ステート持続時間でフレームを引き伸ばさない）
            case CombatStateType.Attack:
                PlayAnimation(attackSprites, loop: false);
                break;

            case CombatStateType.Feint:
                PlayAnimation(feintSprites, loop: false);
                break;

            case CombatStateType.Parry:
                PlayAnimation(idleSprites, loop: false);
                break;

            // ── 移動 ──
            case CombatStateType.Idle:
                PlayAnimation(walkSprites, loop: true);
                break;

            // ── ダメージ系 ──
            case CombatStateType.Vulnerable:
            case CombatStateType.GuardBreak:
            case CombatStateType.Dead:
                PlayAnimation(hurtSprites, loop: true);
                break;

            // ── 通常待機（Guard） ──
            default:
                PlayAnimation(idleSprites, loop: true);
                break;
        }
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// アニメーションを開始する。
    /// duration > 0 のとき「フレーム数 ÷ duration」でフレームレートを算出し、
    /// ステート終了と同時にアニメが完了するよう同期する。
    /// duration 未指定のときはデフォルトの frameRate を使用。
    /// </summary>
    private void PlayAnimation(Sprite[] sprites, float duration = -1f, bool loop = true)
    {
        currentSprites = sprites;
        currentFrame   = 0;
        frameTimer     = 0f;
        loopAnimation  = loop;

        if (sprites != null && sprites.Length > 0 && duration > 0f)
            currentFrameRate = sprites.Length / duration;   // 同期レート
        else
            currentFrameRate = frameRate;                   // デフォルトレート

        if (currentSprites != null && currentSprites.Length > 0)
            spriteRenderer.sprite = currentSprites[0];
    }

    private void AdvanceFrame()
    {
        if (currentSprites == null || currentSprites.Length <= 1) return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(currentFrameRate, 0.01f);

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;

            if (loopAnimation)
            {
                currentFrame = (currentFrame + 1) % currentSprites.Length;
            }
            else
            {
                // 非ループ：最終フレームで停止
                if (currentFrame < currentSprites.Length - 1)
                    currentFrame++;
            }

            spriteRenderer.sprite = currentSprites[currentFrame];
        }
    }

    // -----------------------------------------------------------------------
    // PNG 直接ロード（System.IO / AssetDatabase 不要）
    // -----------------------------------------------------------------------

    private static Sprite[] LoadFolder(string folderName, int maxFrames = int.MaxValue)
    {
        string dataPath = Application.dataPath;

        foreach (string root in BrawlerRoots)
        {
            string folderPath = Path.Combine(dataPath, root, folderName)
                                    .Replace('/', Path.DirectorySeparatorChar);

            if (!Directory.Exists(folderPath)) continue;

            string[] pngFiles = Directory.GetFiles(folderPath, "*.png");
            if (pngFiles.Length == 0) continue;

            System.Array.Sort(pngFiles, NaturalCompare);

            var sprites = new List<Sprite>();
            foreach (string file in pngFiles)
            {
                if (sprites.Count >= maxFrames) break;
                Sprite s = LoadSpriteFromFile(file);
                if (s != null) sprites.Add(s);
            }

            if (sprites.Count > 0)
            {
                Debug.Log($"[CSA] {folderName}: {sprites.Count}枚 ({folderPath})");
                return sprites.ToArray();
            }
        }

        Debug.LogWarning($"[CSA] '{folderName}' が見つかりません。dataPath={Application.dataPath}");
        return System.Array.Empty<Sprite>();
    }

    private static Sprite LoadSpriteFromFile(string filePath)
    {
        byte[] data;
        try { data = File.ReadAllBytes(filePath); }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CSA] 読み込み失敗: {filePath}\n{e.Message}");
            return null;
        }

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        if (!tex.LoadImage(data))
        {
            Debug.LogWarning($"[CSA] LoadImage 失敗: {filePath}");
            return null;
        }

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0f),
            tex.width * 0.5f);

        sprite.name = Path.GetFileNameWithoutExtension(filePath);
        return sprite;
    }

    private static int NaturalCompare(string a, string b)
    {
        string nameA = Path.GetFileNameWithoutExtension(a);
        string nameB = Path.GetFileNameWithoutExtension(b);

        string[] partsA = Regex.Split(nameA, @"(\d+)");
        string[] partsB = Regex.Split(nameB, @"(\d+)");

        int len = Mathf.Min(partsA.Length, partsB.Length);
        for (int i = 0; i < len; i++)
        {
            int cmp;
            if (int.TryParse(partsA[i], out int numA) && int.TryParse(partsB[i], out int numB))
                cmp = numA.CompareTo(numB);
            else
                cmp = string.Compare(partsA[i], partsB[i], System.StringComparison.Ordinal);

            if (cmp != 0) return cmp;
        }
        return partsA.Length.CompareTo(partsB.Length);
    }
}
