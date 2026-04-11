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
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("TitleScene: Space pressed -> Load GameScene", this);
            SceneManager.LoadScene(GameSceneName);
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
        GUI.Label(promptRect, "Press Space to Start", promptStyle);
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
        CreateText(canvas.transform, "StartText", "Press Space to Start", new Vector2(0f, -20f), new Vector2(500f, 60f), 28, TextAnchor.MiddleCenter);
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
