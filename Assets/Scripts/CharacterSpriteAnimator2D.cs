using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 状態マッピング:
///   Guard                            → Idle（ループ）
///   Idle（移動中）                    → Walk（ループ）
///   Attack                           → Jab → なければ Punch（1回再生）
///   Feint                            → Punch → なければ Idle（1回再生）
///   Parry                            → Jump_kick → なければ Idle（1回再生）
///   Vulnerable / GuardBreak / Dead   → Hurt（ループ）
///   ガードヒット時（一時）            → Kick 先頭2枚 → なければ Hurt 先頭2枚
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CombatStateMachine2D))]
public class CharacterSpriteAnimator2D : MonoBehaviour
{
    [Tooltip("使用するキャラクタースキンのフォルダ名（例: Brawler-Girl / Enemy-Punk）")]
    public string characterSkin = "Brawler-Girl";

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
    private Sprite[] parrySprites;
    private Sprite[] hurtSprites;
    private Sprite[] guardReactionSprites;

    private Sprite[] currentSprites;
    private int      currentFrame;
    private float    frameTimer;
    private float    currentFrameRate;
    private bool     loopAnimation;

    private CombatStateType lastState = (CombatStateType)(-1);
    private float guardReactionTimer;

    // -----------------------------------------------------------------------

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        stateMachine   = GetComponent<CombatStateMachine2D>();
    }

    private void Start()
    {
        idleSprites  = Load("Idle");
        walkSprites  = Load("Walk");
        hurtSprites  = Load("Hurt");

        // アニメーションがない場合は代替フォルダにフォールバック
        attackSprites        = FirstNonEmpty(Load("Jab"),       Load("Punch"));
        feintSprites         = FirstNonEmpty(Load("Punch"),     Load("Idle"));
        parrySprites         = FirstNonEmpty(Load("Jump_kick"), Load("Idle"));
        guardReactionSprites = FirstNonEmpty(Load("Kick",  maxFrames: 2),
                                             Load("Hurt",  maxFrames: 2));

        PlayAnimation(idleSprites, loop: true);
    }

    private void Update()
    {
        if (guardReactionTimer > 0f)
        {
            guardReactionTimer -= Time.deltaTime;
            if (guardReactionTimer <= 0f)
            {
                guardReactionTimer = 0f;
                lastState = (CombatStateType)(-1);
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

    public void TriggerGuardReaction()
    {
        if (guardReactionSprites == null || guardReactionSprites.Length == 0) return;
        guardReactionTimer = guardReactionSeconds;
        PlayAnimation(guardReactionSprites, duration: guardReactionSeconds, loop: true);
    }

    // -----------------------------------------------------------------------

    private void UpdateState()
    {
        CombatStateType current = stateMachine.CurrentStateType;
        if (current == lastState) return;
        lastState = current;

        switch (current)
        {
            case CombatStateType.Attack:
                PlayAnimation(attackSprites, loop: false);
                break;

            case CombatStateType.Feint:
                PlayAnimation(feintSprites, loop: false);
                break;

            case CombatStateType.Parry:
                PlayAnimation(parrySprites, loop: false);
                break;

            case CombatStateType.Idle:
                PlayAnimation(walkSprites, loop: true);
                break;

            case CombatStateType.Vulnerable:
            case CombatStateType.GuardBreak:
            case CombatStateType.Dead:
                PlayAnimation(hurtSprites, loop: false);  // 1回再生して最終フレームで停止
                break;

            default:
                PlayAnimation(idleSprites, loop: true);
                break;
        }
    }

    // -----------------------------------------------------------------------

    private void PlayAnimation(Sprite[] sprites, float duration = -1f, bool loop = true)
    {
        currentSprites = sprites;
        currentFrame   = 0;
        frameTimer     = 0f;
        loopAnimation  = loop;

        if (sprites != null && sprites.Length > 0 && duration > 0f)
            currentFrameRate = sprites.Length / duration;
        else
            currentFrameRate = frameRate;

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
                currentFrame = (currentFrame + 1) % currentSprites.Length;
            else if (currentFrame < currentSprites.Length - 1)
                currentFrame++;

            spriteRenderer.sprite = currentSprites[currentFrame];
        }
    }

    // -----------------------------------------------------------------------
    // スキン別 PNG ロード
    // -----------------------------------------------------------------------

    /// <summary>指定フォルダを characterSkin のパスから読み込む。</summary>
    private Sprite[] Load(string folderName, int maxFrames = int.MaxValue)
    {
        return LoadFolder(characterSkin, folderName, maxFrames);
    }

    /// <summary>candidates の中で最初に要素を持つ配列を返す。すべて空なら空配列。</summary>
    private static Sprite[] FirstNonEmpty(params Sprite[][] candidates)
    {
        foreach (var arr in candidates)
            if (arr != null && arr.Length > 0) return arr;
        return System.Array.Empty<Sprite>();
    }

    private static Sprite[] LoadFolder(string skin, string folderName, int maxFrames = int.MaxValue)
    {
        string dataPath = Application.dataPath;

        string[] roots =
        {
            $"StreetsOfFight/Sprites/{skin}",
            $"Streets of Fight/Sprites/{skin}",
        };

        foreach (string root in roots)
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
                Debug.Log($"[CSA:{skin}] {folderName}: {sprites.Count}枚 ({folderPath})");
                return sprites.ToArray();
            }
        }

        Debug.LogWarning($"[CSA:{skin}] '{folderName}' が見つかりません。");
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
