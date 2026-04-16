using UnityEngine;
using UnityEngine.UI;

public class SampleSceneSetup2D : MonoBehaviour
{
    private static Sprite cachedSquareSprite;

    private void Awake()
    {
        GetOrAddComponent<HitstopManager>(gameObject);
        SetupMainCamera();
        PlayerStateMachine2D playerState = SetupPlayer();

        // 複数の敵を生成
        var enemyTypes = new EnemyType[] { EnemyType.Attacker, EnemyType.Rusher, EnemyType.Careful };
        var enemyStates = new System.Collections.Generic.List<EnemyStateMachine2D>();
        var enemyAIs    = new System.Collections.Generic.List<EnemyRandomCombatAI2D>();

        // 初期配置：Enemy1はプレイヤー右（アクティブ）、残りは左右遠くにスタンバイ
        var startPositions = new Vector3[]
        {
            new Vector3( 0.6f, 0f, 0f),   // Enemy1：すぐ右（アクティブ）
            new Vector3(-2.5f, 0f, 0f),   // Enemy2：左遠く（スタンバイ）
            new Vector3( 2.5f, 0f, 0f),   // Enemy3：右遠く（スタンバイ）
        };

        for (int i = 0; i < enemyTypes.Length; i++)
        {
            var es = SetupEnemy(playerState, $"Enemy{i + 1}", enemyTypes[i], startPositions[i]);
            enemyStates.Add(es);
            enemyAIs.Add(es.GetComponent<EnemyRandomCombatAI2D>());
        }

        SetupBackground();
        SetupGround();
        SetupForegroundProps();
        ProximityJankenBattle2D battle = SetupBattle(playerState, enemyStates[0]);

        // MultiEnemyManager のセットアップ
        // プレイヤーの CharacterFacing も渡して、敵切り替え時に向き先を更新させる
        CharacterFacing playerFacing = playerState.GetComponent<CharacterFacing>();
        MultiEnemyManager multiMgr = GetOrAddComponent<MultiEnemyManager>(gameObject);
        multiMgr.Setup(playerState, enemyAIs, battle, playerFacing);

        // スタンバイ敵（2体目以降）を待機モードに
        for (int i = 1; i < enemyAIs.Count; i++)
            enemyAIs[i].SetStandbyMode(true);

        // ゲージはキャラ頭上（CharacterGuardBarUI）で表示済み
        // Canvas は BattlePresentation / RoundManager のテキスト表示用に生成する
        GuardGauge playerGuard = playerState.GetComponent<GuardGauge>();
        GuardGauge enemyGuard  = enemyStates[0].GetComponent<GuardGauge>();
        Canvas canvas = SetupGuardGaugeUI(playerGuard, enemyGuard);
        BattlePresentation2D presentation = SetupBattlePresentation(canvas);
        GetComponent<ProximityJankenBattle2D>().presentation = presentation;
        SetupRoundManager(playerState, enemyStates[0], playerGuard, enemyGuard, canvas, multiMgr);

        // タイプ切り替えUI（最初のアクティブ敵）
        EnemyTypeTestUI testUI = GetOrAddComponent<EnemyTypeTestUI>(gameObject);
        testUI.Setup(enemyAIs[0]);
    }

    private PlayerStateMachine2D SetupPlayer()
    {
        GameObject player = FindOrCreate("Player", new Vector3(-0.5f, 0f, 0f));
        player.transform.localScale = new Vector3(1f, 1f, 1f);
        SpriteRenderer spriteRenderer = SetupSpriteRenderer(player, Color.white, 1);
        SetupDynamicBody(player);
        SetupBoxCollider(player, spriteRenderer.sprite.bounds.size, Vector2.zero);
        GetOrAddComponent<PlayerMovement2D>(player);
        PlayerStateMachine2D stateMachine = GetOrAddComponent<PlayerStateMachine2D>(player);
        SetupActionDurations(stateMachine);
        GuardGauge playerGuardGaugeSetup = GetOrAddComponent<GuardGauge>(player);
        playerGuardGaugeSetup.maxGuard                    = 3;
        playerGuardGaugeSetup.guardBreakVulnerableDuration = 1.2f;
        CharacterStateVisual2D visual = GetOrAddComponent<CharacterStateVisual2D>(player);
        visual.isPlayer = true;
        GetOrAddComponent<CharacterSpriteAnimator2D>(player);

        // キャラクター向き（CharacterFacing）：Brawler-Girl は右向きデフォルト
        CharacterFacing playerFacing = GetOrAddComponent<CharacterFacing>(player);
        playerFacing.defaultFacingLeft = false;

        // プレイヤーはフェイント後に隙なし（フェイントは攻撃のための布石として使える）
        // 敵は SetupActionDurations で設定した 0.3f のまま維持
        stateMachine.feintWhiffVulnerableDuration = 0f;

        // プレイヤー固有スタッツ
        CharacterStats playerStats = GetOrAddComponent<CharacterStats>(player);
        playerStats.moveSpeed        = 5f;    // 敵より常に速い
        playerStats.attackDuration   = 0.65f;
        playerStats.guardDamageDealt = 1;
        playerStats.parryWindowSize  = 0.35f;
        playerStats.ApplyToCombatStateMachine();

        // 頭上ガードゲージ（ワールド空間）
        CharacterGuardBarUI playerGuardBar = GetOrAddComponent<CharacterGuardBarUI>(player);
        playerGuardBar.guardGauge   = playerGuardGaugeSetup;
        playerGuardBar.yOffset      = 1.05f;
        playerGuardBar.sortingOrder = 12;

        return stateMachine;
    }

    private EnemyStateMachine2D SetupEnemy(PlayerStateMachine2D playerState,
                                            string objectName = "Enemy",
                                            EnemyType type = EnemyType.Attacker,
                                            Vector3? startPos = null)
    {
        Vector3 pos = startPos ?? new Vector3(0.5f, 0f, 0f);
        GameObject enemy = FindOrCreate(objectName, pos);
        enemy.transform.localScale = new Vector3(1f, 1f, 1f);
        // 色はスクリプト(CharacterStateVisual2D)で赤に設定するため白で初期化
        SpriteRenderer spriteRenderer = SetupSpriteRenderer(enemy, Color.white, 1);
        SetupDynamicBody(enemy);
        SetupBoxCollider(enemy, spriteRenderer.sprite.bounds.size, Vector2.zero);
        EnemyStateMachine2D enemyStateMachine = GetOrAddComponent<EnemyStateMachine2D>(enemy);
        SetupActionDurations(enemyStateMachine);
        GuardGauge enemyGuardGaugeSetup = GetOrAddComponent<GuardGauge>(enemy);
        enemyGuardGaugeSetup.maxGuard                    = 3;
        enemyGuardGaugeSetup.guardBreakVulnerableDuration = 1.2f;
        CharacterStateVisual2D visual = GetOrAddComponent<CharacterStateVisual2D>(enemy);
        visual.isPlayer = false;
        EnemyRandomCombatAI2D ai = GetOrAddComponent<EnemyRandomCombatAI2D>(enemy);
        ai.targetActorSource = playerState;
        ai.minActionInterval = 1.2f;
        ai.maxActionInterval = 2.5f;
        ai.chargeDuration = 0.5f;
        ai.approachDistance = 0.8f;  // 踏み込み時にこの距離まで詰める
        ai.moveSpeed = 1.5f;
        ai.useFeint = false;
        ai.standbyMinPlayerDist = 1.8f;

        ai.SetEnemyType(type);

        // 頭上ガードゲージ（ワールド空間）
        CharacterGuardBarUI enemyGuardBar = GetOrAddComponent<CharacterGuardBarUI>(enemy);
        enemyGuardBar.guardGauge   = enemyGuardGaugeSetup;
        enemyGuardBar.yOffset      = 1.05f;
        enemyGuardBar.sortingOrder = 12;

        CharacterSpriteAnimator2D enemyAnim = GetOrAddComponent<CharacterSpriteAnimator2D>(enemy);
        enemyAnim.characterSkin = "Enemy-Punk";

        // キャラクター向き（CharacterFacing）：Enemy-Punk は左向きデフォルト
        CharacterFacing enemyFacing = GetOrAddComponent<CharacterFacing>(enemy);
        enemyFacing.defaultFacingLeft = true;
        enemyFacing.target = playerState.transform;

        return enemyStateMachine;
    }

    private static void SetupActionDurations(CombatStateMachine2D sm)
    {
        sm.attackDuration               = 0.65f; // 攻撃：0.65秒ロック（コンボをテンポよく）
        sm.attackJudgmentTime           = 0.4f;  // 攻撃判定：0.4秒時点
        sm.parryDuration                = 0.4f;  // パリィ：受付時間
        sm.parryWhiffVulnerableDuration = 0.9f;  // パリィ空振り時の隙（反応できる長さ）
        sm.feintDuration                = 0.3f;  // フェイント：0.3秒ロック
        sm.feintWhiffVulnerableDuration = 0.3f;  // フェイント後の隙（敵用。プレイヤーは下で0に上書き）
        sm.vulnerableDuration           = 0.8f;  // ダメージ硬直
        sm.guardBreakDuration           = 1.2f;  // ガードブレイク硬直
    }

    private void SetupBackground()
    {
        // Resources/Background から画像をロード（複数枚ある場合は最初の1枚を使用）
        // 画像が見つからない場合は無地の暗い背景にフォールバック
        Texture2D tex = Resources.Load<Texture2D>("Background/ComfyUI_00015_");
        if (tex == null)
        {
            // フォールバック：暗いグレーの無地背景
            tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.10f));
            tex.Apply();
        }

        Sprite bgSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);

        // カメラ Y=+1.0、size=2.0 → 画面上端 Y=+3.0
        // 背景はキャラより上：Y=+1.0（カメラ中心）あたりを中心に配置
        // 画面幅に合わせてスケーリング
        GameObject bg = FindOrCreate("Background", new Vector3(0f, 1.5f, 0f));
        SpriteRenderer bgSr = GetOrAddComponent<SpriteRenderer>(bg);
        bgSr.sprite       = bgSprite;
        bgSr.sortingOrder = -100; // 最背面

        // 画面にフィットするようスケールを計算
        // 画面幅 = size * aspect、画面高さ = size * 2
        Camera cam = Camera.main;
        if (cam != null)
        {
            float screenH = cam.orthographicSize * 2f;          // 4.0
            float screenW = screenH * cam.aspect;               // 例: 7.1 (16:9)
            float texAspect = (float)tex.width / tex.height;

            // 高さを画面の75%（背景エリア分）に合わせる
            float bgHeight = screenH * 0.75f;
            float bgWidth  = bgHeight * texAspect;

            // 幅が画面より狭い場合は幅基準でスケール
            if (bgWidth < screenW)
            {
                bgWidth  = screenW;
                bgHeight = bgWidth / texAspect;
            }

            float scaleX = bgWidth  / (tex.width  / 100f);
            float scaleY = bgHeight / (tex.height / 100f);
            bg.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
    }

    private void SetupGround()
    {
        // コライダーのみ（見た目は背景画像に任せる）
        GameObject ground = FindOrCreate("Ground", new Vector3(0f, -0.05f, 0f));
        BoxCollider2D groundCollider = GetOrAddComponent<BoxCollider2D>(ground);
        groundCollider.size   = new Vector2(25f, 0.1f);
        groundCollider.offset = Vector2.zero;
    }

    /// <summary>
    /// 手前の前景オブジェクト（低い箱・障害物のシルエット）。
    /// 画面最下部に配置し、sortingOrder を高くしてキャラの前に描画。
    /// </summary>
    private void SetupForegroundProps()
    {
        // 手前ライン Y≈-1.6〜-1.7（画面ほぼ下端）
        // (X, 幅, 高さ)
        // 画面下端 Y=-1.0 に揃える
        var props = new (float x, float w, float h)[]
        {
            (-4.0f, 0.50f, 0.22f),
            (-2.2f, 0.32f, 0.16f),
            (-0.7f, 0.26f, 0.14f),
            ( 1.5f, 0.55f, 0.20f),
            ( 2.9f, 0.33f, 0.15f),
            ( 4.3f, 0.44f, 0.18f),
        };

        for (int i = 0; i < props.Length; i++)
        {
            var p = props[i];
            float propY = -1.00f + p.h * 0.5f; // 下端を Y=-1.0 に揃える
            GameObject obj = FindOrCreate($"FgProp{i}", new Vector3(p.x, propY, 0f));
            obj.transform.localScale = new Vector3(p.w, p.h, 1f);
            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sprite       = GetSquareSprite();
            sr.color        = new Color(0.07f, 0.06f, 0.10f, 1f); // ほぼ黒の紫シルエット
            sr.sortingOrder = 50;
        }
    }

    private ProximityJankenBattle2D SetupBattle(PlayerStateMachine2D playerState, EnemyStateMachine2D enemyState)
    {
        JankenCombatResolver2D resolver = GetOrAddComponent<JankenCombatResolver2D>(gameObject);
        // パリィ受付ウィンドウ（attackJudgmentTime=0.5f に対して 0〜0.45s）
        resolver.parryWindowStart            = 0.0f;   // 早すぎペナルティなし
        resolver.parryWindowEnd              = 0.35f;  // 攻撃判定(0.4s)の直前まで受付
        resolver.attackerVulnerableDuration  = 2.0f;  // パリィ成功後の敵の隙（余裕を持って攻撃できる長さ）
        resolver.parrierVulnerableDuration   = 0.0f;   // パリィ成功後は即フリー（すぐ攻撃に移れる）
        resolver.earlyParryVulnerableDuration= 0.0f;   // 早すぎペナルティなし

        ProximityJankenBattle2D battle = GetOrAddComponent<ProximityJankenBattle2D>(gameObject);
        battle.resolveDistance       = 0.9f;
        battle.minSeparationDistance = 0.42f;
        battle.playerSource = playerState;
        battle.enemySource  = enemyState;
        battle.SetActors(playerState, enemyState);
        return battle;
    }

    // ゲージはキャラ頭上（CharacterGuardBarUI）で表示するため
    // ここでは Canvas のみを生成する（BattlePresentation・RoundManager が使用）
    private Canvas SetupGuardGaugeUI(GuardGauge playerGauge, GuardGauge enemyGauge)
    {
        GameObject canvasObject = FindOrCreate("HUDCanvas", Vector3.zero);
        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        GetOrAddComponent<CanvasScaler>(canvasObject);
        GetOrAddComponent<GraphicRaycaster>(canvasObject);
        return canvas;
    }

    private BattlePresentation2D SetupBattlePresentation(Canvas canvas)
    {
        BattlePresentation2D presentation = GetOrAddComponent<BattlePresentation2D>(gameObject);

        GameObject flashObject = FindChildOrCreate(canvas.transform, "HitFlash");
        RectTransform flashRect = GetOrAddComponent<RectTransform>(flashObject);
        flashRect.SetParent(canvas.transform, false);
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;

        Image flashImage = GetOrAddComponent<Image>(flashObject);
        flashImage.sprite = GetSquareSprite();
        flashImage.color = new Color(1f, 1f, 1f, 0f);
        flashImage.gameObject.SetActive(false);

        Text battleResultText = CreateTextUI(
            canvas.transform,
            "BattleResultText",
            new Vector2(0f, -90f),
            new Vector2(760f, 60f),
            TextAnchor.MiddleCenter,
            24);
        battleResultText.gameObject.SetActive(false);

        presentation.flashImage = flashImage;
        presentation.resultText = battleResultText;
        presentation.flashDuration = 0.1f;
        presentation.resultDisplayDuration = 1f;
        return presentation;
    }

    private void CreateGaugeUI(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, GuardGauge guardGauge, TextAnchor anchor)
    {
        GameObject root = FindChildOrCreate(parent, objectName);
        RectTransform rootRect = GetOrAddComponent<RectTransform>(root);
        rootRect.SetParent(parent, false);
        rootRect.sizeDelta = size;

        if (anchor == TextAnchor.UpperLeft)
        {
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
        }
        else
        {
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
        }

        rootRect.anchoredPosition = anchoredPosition;

        Image background = GetOrAddComponent<Image>(root);
        background.sprite = GetSquareSprite();
        background.type = Image.Type.Sliced;
        background.color = new Color(0f, 0f, 0f, 0.5f);

        GameObject fillObject = FindChildOrCreate(root.transform, "Fill");
        RectTransform fillRect = GetOrAddComponent<RectTransform>(fillObject);
        fillRect.SetParent(root.transform, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        Image fillImage = GetOrAddComponent<Image>(fillObject);
        fillImage.sprite = GetSquareSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;

        GuardGaugeUI2D gaugeUI = GetOrAddComponent<GuardGaugeUI2D>(root);
        gaugeUI.guardGauge = guardGauge;
        gaugeUI.fillImage = fillImage;
    }

    private void SetupRoundManager(
        PlayerStateMachine2D playerState,
        EnemyStateMachine2D  enemyState,
        GuardGauge           playerGuard,
        GuardGauge           enemyGuard,
        Canvas               canvas,
        MultiEnemyManager    multiMgr = null)
    {
        RoundManager roundManager = GetOrAddComponent<RoundManager>(gameObject);
        roundManager.playerState       = playerState;
        roundManager.enemyState        = enemyState;
        roundManager.multiEnemyManager = multiMgr;
        roundManager.playerGuardGauge  = playerGuard;
        roundManager.enemyGuardGauge   = enemyGuard;
        roundManager.playerStartPosition = new Vector3(-0.5f, 0f, 0f);
        roundManager.enemyStartPosition  = new Vector3( 0.5f, 0f, 0f);
        roundManager.battleController = GetComponent<ProximityJankenBattle2D>();
        roundManager.scoreText = CreateTextUI(canvas.transform, "ScoreText", new Vector2(0f, -20f), new Vector2(320f, 32f), TextAnchor.UpperCenter, 24);
        roundManager.resultText = CreateTextUI(canvas.transform, "ResultText", new Vector2(0f, 0f), new Vector2(400f, 60f), TextAnchor.MiddleCenter, 36);
    }

    private Text CreateTextUI(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        TextAnchor alignment,
        int fontSize)
    {
        GameObject textObject = FindChildOrCreate(parent, objectName);
        RectTransform rect = GetOrAddComponent<RectTransform>(textObject);
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, alignment == TextAnchor.MiddleCenter ? 0.5f : 1f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0.5f, alignment == TextAnchor.MiddleCenter ? 0.5f : 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        GetOrAddComponent<CanvasRenderer>(textObject);
        Text text = GetOrAddComponent<Text>(textObject);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = false;
        text.supportRichText = false;
        text.text = string.Empty;
        return text;
    }

    private static void SetupMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        // size=2.0、Y=+1.0 → 画面範囲 Y:-1.0〜+3.0
        // キャラ(Y=0)は画面上から75%の位置 → 背景が画面の75%を占める
        mainCamera.orthographicSize = 2.0f;
        Vector3 pos = mainCamera.transform.position;
        mainCamera.transform.position = new Vector3(pos.x, 1.0f, pos.z);
    }

    private static SpriteRenderer SetupSpriteRenderer(GameObject target, Color color, int sortingOrder)
    {
        SpriteRenderer spriteRenderer = GetOrAddComponent<SpriteRenderer>(target);
        spriteRenderer.sprite = GetSquareSprite();
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;
        return spriteRenderer;
    }

    private static void SetupDynamicBody(GameObject target)
    {
        Rigidbody2D rb = GetOrAddComponent<Rigidbody2D>(target);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;   // 疑似3D：重力なし、Y軸を奥行きとして使う
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private static void SetupBoxCollider(GameObject target, Vector2 size, Vector2 offset)
    {
        BoxCollider2D boxCollider = GetOrAddComponent<BoxCollider2D>(target);
        boxCollider.size = size;
        boxCollider.offset = offset;
    }

    private static GameObject FindOrCreate(string objectName, Vector3 position)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null)
        {
            existing.transform.position = position;
            return existing;
        }

        GameObject created = new GameObject(objectName);
        created.transform.position = position;
        return created;
    }

    private static GameObject FindChildOrCreate(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject created = new GameObject(objectName);
        created.transform.SetParent(parent, false);
        return created;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }

    private static Sprite GetSquareSprite()
    {
        if (cachedSquareSprite != null)
        {
            return cachedSquareSprite;
        }

        cachedSquareSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        cachedSquareSprite.name = "RuntimeWhiteSquare";
        return cachedSquareSprite;
    }
}
