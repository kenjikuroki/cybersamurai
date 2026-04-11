using UnityEngine;
using UnityEngine.UI;

public class SampleSceneSetup2D : MonoBehaviour
{
    private static Sprite cachedSquareSprite;

    private void Awake()
    {
        SetupMainCamera();
        PlayerStateMachine2D playerState = SetupPlayer();
        EnemyStateMachine2D enemyState = SetupEnemy(playerState);
        SetupGround();
        SetupBattle(playerState, enemyState);

        EnemyRandomCombatAI2D enemyAi = enemyState.GetComponent<EnemyRandomCombatAI2D>();
        if (enemyAi != null)
        {
            enemyAi.targetActorSource = playerState;
        }

        GuardGauge playerGuard = playerState.GetComponent<GuardGauge>();
        GuardGauge enemyGuard = enemyState.GetComponent<GuardGauge>();
        Canvas canvas = SetupGuardGaugeUI(playerGuard, enemyGuard);
        BattlePresentation2D presentation = SetupBattlePresentation(canvas);
        GetComponent<ProximityJankenBattle2D>().presentation = presentation;
        SetupRoundManager(playerState, enemyState, playerGuard, enemyGuard, canvas);
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
        GetOrAddComponent<GuardGauge>(player);
        CharacterStateVisual2D visual = GetOrAddComponent<CharacterStateVisual2D>(player);
        visual.isPlayer = true;
        GetOrAddComponent<CharacterSpriteAnimator2D>(player);
        return stateMachine;
    }

    private EnemyStateMachine2D SetupEnemy(PlayerStateMachine2D playerState)
    {
        GameObject enemy = FindOrCreate("Enemy", new Vector3(0.5f, 0f, 0f));
        enemy.transform.localScale = new Vector3(1f, 1f, 1f);
        // 色はスクリプト(CharacterStateVisual2D)で赤に設定するため白で初期化
        SpriteRenderer spriteRenderer = SetupSpriteRenderer(enemy, Color.white, 1);
        SetupDynamicBody(enemy);
        SetupBoxCollider(enemy, spriteRenderer.sprite.bounds.size, Vector2.zero);
        EnemyStateMachine2D enemyStateMachine = GetOrAddComponent<EnemyStateMachine2D>(enemy);
        SetupActionDurations(enemyStateMachine);
        GetOrAddComponent<GuardGauge>(enemy);
        CharacterStateVisual2D visual = GetOrAddComponent<CharacterStateVisual2D>(enemy);
        visual.isPlayer = false;
        EnemyRandomCombatAI2D ai = GetOrAddComponent<EnemyRandomCombatAI2D>(enemy);
        ai.targetActorSource = playerState;
        ai.minActionInterval = 2f;
        ai.maxActionInterval = 4f;
        ai.chargeDuration = 0.5f;
        ai.approachDistance = 0.4f;
        ai.moveSpeed = 1.5f;
        GetOrAddComponent<CharacterSpriteAnimator2D>(enemy);
        // FlipX=true で右側のキャラクターを左向き（プレイヤーと向き合う）
        SpriteRenderer enemySr = enemy.GetComponent<SpriteRenderer>();
        if (enemySr != null) enemySr.flipX = true;
        return enemyStateMachine;
    }

    private static void SetupActionDurations(CombatStateMachine2D sm)
    {
        sm.attackDuration      = 1.0f;  // 攻撃：1秒ロック（判定は0.5秒時点）
        sm.parryDuration       = 0.4f;  // パリィ：外れ時のリカバリー0.4秒（成功/失敗時は即キャンセル）
        sm.feintDuration       = 0.3f;  // フェイント：0.3秒ロック（判定なし）
        sm.vulnerableDuration  = 0.8f;  // ダメージ硬直
        sm.guardBreakDuration  = 1.2f;  // ガードブレイク硬直
    }

    private void SetupGround()
    {
        GameObject ground = FindOrCreate("Ground", new Vector3(0f, -0.8f, 0f));
        ground.transform.localScale = new Vector3(10f, 0.2f, 1f);
        BoxCollider2D groundCollider = GetOrAddComponent<BoxCollider2D>(ground);
        groundCollider.size = Vector2.one;
        groundCollider.offset = Vector2.zero;
    }

    private void SetupBattle(PlayerStateMachine2D playerState, EnemyStateMachine2D enemyState)
    {
        GetOrAddComponent<JankenCombatResolver2D>(gameObject);
        ProximityJankenBattle2D battle = GetOrAddComponent<ProximityJankenBattle2D>(gameObject);
        battle.resolveDistance = 1.5f;
        battle.playerSource = playerState;
        battle.enemySource = enemyState;
        battle.SetActors(playerState, enemyState);
    }

    private Canvas SetupGuardGaugeUI(GuardGauge playerGauge, GuardGauge enemyGauge)
    {
        GameObject canvasObject = FindOrCreate("HUDCanvas", Vector3.zero);
        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        GetOrAddComponent<CanvasScaler>(canvasObject);
        GetOrAddComponent<GraphicRaycaster>(canvasObject);

        CreateGaugeUI(canvas.transform, "PlayerGuardGauge", new Vector2(20f, -20f), new Vector2(220f, 24f), playerGauge, TextAnchor.UpperLeft);
        CreateGaugeUI(canvas.transform, "EnemyGuardGauge", new Vector2(-20f, -20f), new Vector2(220f, 24f), enemyGauge, TextAnchor.UpperRight);
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
        EnemyStateMachine2D enemyState,
        GuardGauge playerGuard,
        GuardGauge enemyGuard,
        Canvas canvas)
    {
        RoundManager roundManager = GetOrAddComponent<RoundManager>(gameObject);
        roundManager.playerState = playerState;
        roundManager.enemyState = enemyState;
        roundManager.playerGuardGauge = playerGuard;
        roundManager.enemyGuardGauge = enemyGuard;
        roundManager.playerStartPosition = new Vector3(-0.5f, 0f, 0f);
        roundManager.enemyStartPosition = new Vector3(0.5f, 0f, 0f);
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
        if (mainCamera == null)
        {
            return;
        }

        mainCamera.orthographicSize = 1f;
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
        rb.gravityScale = 3f;
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
