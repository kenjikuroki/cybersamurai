using UnityEngine;
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

    private void Start()
    {
        UpdateScoreText();
        SetResultText(string.Empty);
        Debug.Log($"Round {roundNumber} Start", this);
    }

    private void Update()
    {
        if (playerState == null || enemyState == null) return;

        if (!roundEnding)
        {
            CheckRoundEnd();
            return;
        }

        roundEndTimer += Time.deltaTime;
        if (roundEndTimer >= roundEndDisplayDuration)
            StartNextRound();
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

        // 先取ポイント到達したらスコアをリセットして続行（無限戦闘）
        if (playerWins >= roundsToWin || enemyWins >= roundsToWin)
        {
            SetResultText(playerWins >= roundsToWin ? "Player Win! Next Round!" : "Enemy Win! Next Round!");
            playerWins = 0;
            enemyWins  = 0;
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
