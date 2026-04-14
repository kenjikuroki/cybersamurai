using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleSceneController : MonoBehaviour
{
    private const string GameSceneName = "GameScene";
    private GUIStyle titleStyle;
    private GUIStyle promptStyle;

    private void Start()
    {
        SetupMainCamera();
        CreateTitleUi();
        LogBuildSettingsInstructions();
    }

    private void Update()
    {
        bool pressed = false;

        // New Input System (プロジェクト設定が "New Input System Only" の場合)
        var kb = Keyboard.current;
        if (kb != null)
            pressed = kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;

        // Gamepad の任意ボタンでも開始できるよう対応
        var gp = Gamepad.current;
        if (gp != null)
            pressed |= gp.startButton.wasPressedThisFrame || gp.buttonSouth.wasPressedThisFrame;

        if (!pressed) return;

        Debug.Log("TitleScene: Key pressed -> Load GameScene", this);

        // シーンがBuild Settingsにあるか確認してからロード
        bool found = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            if (SceneUtility.GetScenePathByBuildIndex(i).Contains(GameSceneName))
            {
                found = true;
                break;
            }
        }

        if (found)
        {
            SceneManager.LoadScene(GameSceneName);
        }
        else
        {
            Debug.LogError(
                $"「{GameSceneName}」が Build Settings に登録されていません。\n" +
                "File > Build Settings > Add Open Scenes で TitleScene と GameScene を両方追加し、" +
                "TitleScene を index 0、GameScene を index 1 にしてください。", this);
        }
    }

    private void OnGUI()
    {
        EnsureGuiStyles();

        float titleWidth = 700f;
        float titleHeight = 80f;
        Rect titleRect = new Rect(
            (Screen.width - titleWidth) * 0.5f,
            (Screen.height - titleHeight) * 0.5f - 70f,
            titleWidth,
            titleHeight);
        GUI.Label(titleRect, "CYBER SAMURAI", titleStyle);

        float promptWidth = 500f;
        float promptHeight = 50f;
        Rect promptRect = new Rect(
            (Screen.width - promptWidth) * 0.5f,
            (Screen.height - promptHeight) * 0.5f + 10f,
            promptWidth,
            promptHeight);
        GUI.Label(promptRect, "Press Space / Enter to Start", promptStyle);
    }

    private void SetupMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 5f;
        mainCamera.backgroundColor = new Color(0.05f, 0.08f, 0.12f, 1f);
    }

    private void CreateTitleUi()
    {
        GameObject canvasObject = FindOrCreate("TitleCanvas");
        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        GetOrAddComponent<CanvasScaler>(canvasObject);
        GetOrAddComponent<GraphicRaycaster>(canvasObject);

        CreateText(canvas.transform, "TitleText", "CYBER SAMURAI", new Vector2(0f, 60f), new Vector2(700f, 100f), 48, TextAnchor.MiddleCenter);
        CreateText(canvas.transform, "StartText", "Press Space / Enter to Start", new Vector2(0f, -20f), new Vector2(600f, 60f), 28, TextAnchor.MiddleCenter);
    }

    private void LogBuildSettingsInstructions()
    {
        Debug.Log("Build Settings: 1. Open File > Build Settings 2. Add open scene TitleScene 3. Open GameScene and add it 4. Ensure TitleScene is index 0 and GameScene is index 1", this);
    }

    private void EnsureGuiStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontSize = 42;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
        }

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label);
            promptStyle.alignment = TextAnchor.MiddleCenter;
            promptStyle.fontSize = 24;
            promptStyle.normal.textColor = Color.white;
        }
    }

    private Text CreateText(Transform parent, string objectName, string message, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = FindOrCreate(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = GetOrAddComponent<RectTransform>(textObject);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        GetOrAddComponent<CanvasRenderer>(textObject);
        Text text = GetOrAddComponent<Text>(textObject);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.supportRichText = false;
        text.text = message;
        return text;
    }

    private static GameObject FindOrCreate(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            return existing;
        }

        return new GameObject(name);
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
}
