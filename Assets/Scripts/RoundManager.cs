using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoundManager : MonoBehaviour
{
    public CombatStateMachine2D playerState;
    public CombatStateMachine2D enemyState;
    public GuardGauge playerGuardGauge;
    public GuardGauge enemyGuardGauge;
    public Text resultText;
    public Text scoreText;
    public float roundEndDisplayDuration = 2f;
    public int roundsToWin = 3;
    public Vector3 playerStartPosition = new Vector3(-0.5f, 0f, 0f);
    public Vector3 enemyStartPosition = new Vector3(0.5f, 0f, 0f);
    public ProximityJankenBattle2D battleController;

    private int playerWins;
    private int enemyWins;
    private int roundNumber = 1;
    private float roundEndTimer;
    private bool roundEnding;
    private bool matchEnded;
    private void Start()
    {
        UpdateScoreText();
        SetResultText(string.Empty);
        Debug.Log($"Round {roundNumber} Start", this);
    }

    private void Update()
    {
        if (playerState == null || enemyState == null)
        {
            return;
        }

        if (matchEnded)
        {
            HandleReturnToTitleInput();
            return;
        }

        if (!roundEnding)
        {
            CheckRoundEnd();
            return;
        }

        roundEndTimer += Time.deltaTime;
        if (roundEndTimer >= roundEndDisplayDuration)
        {
            StartNextRound();
        }
    }

    private void CheckRoundEnd()
    {
        if (playerState.CurrentStateType == CombatStateType.Dead)
        {
            enemyWins++;
            EndRound("Enemy Win");
            return;
        }

        if (enemyState.CurrentStateType == CombatStateType.Dead)
        {
            playerWins++;
            EndRound("Player Win");
        }
    }

    private void EndRound(string resultMessage)
    {
        roundEnding = true;
        roundEndTimer = 0f;
        SetResultText(resultMessage);
        UpdateScoreText();
        Debug.Log(resultMessage, this);

        if (playerWins >= roundsToWin || enemyWins >= roundsToWin)
        {
            matchEnded = true;
            roundEnding = false;
            SetResultText(playerWins >= roundsToWin ? "Player Win!" : "Game Over");
        }
    }

    private void StartNextRound()
    {
        roundEnding = false;
        roundEndTimer = 0f;
        SetResultText(string.Empty);

        ResetActor(playerState, playerStartPosition);
        ResetActor(enemyState, enemyStartPosition);

        if (playerGuardGauge != null)
        {
            playerGuardGauge.ResetToMax();
        }

        if (enemyGuardGauge != null)
        {
            enemyGuardGauge.ResetToMax();
        }

        if (battleController != null)
        {
            battleController.ResetRoundState();
        }

        roundNumber++;
        Debug.Log($"Round {roundNumber} Start", this);
    }

    private void HandleReturnToTitleInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.rKey.wasPressedThisFrame)
        {
            Debug.Log("GameScene: R pressed -> Load TitleScene", this);
            SceneManager.LoadScene("TitleScene");
        }
    }

    private static void ResetActor(CombatStateMachine2D stateMachine, Vector3 position)
    {
        if (stateMachine == null)
        {
            return;
        }

        stateMachine.transform.position = position;

        Rigidbody2D rb = stateMachine.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        stateMachine.ResetForRound();
    }

    private void SetResultText(string message)
    {
        if (resultText == null)
        {
            return;
        }

        resultText.text = message;
    }

    private void UpdateScoreText()
    {
        if (scoreText == null)
        {
            return;
        }

        scoreText.text = $"Player: {playerWins} - Enemy: {enemyWins}";
    }
}
