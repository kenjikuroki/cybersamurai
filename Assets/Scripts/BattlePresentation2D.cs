using UnityEngine;
using UnityEngine.UI;

public class BattlePresentation2D : MonoBehaviour
{
    public Image flashImage;
    public Text resultText;
    public float flashDuration = 0.1f;
    public float resultDisplayDuration = 1f;

    private float flashTimer;
    private float resultTimer;

    private void Update()
    {
        UpdateFlash();
        UpdateResultText();
    }

    public void TriggerFlash()
    {
        flashTimer = flashDuration;
        if (flashImage != null)
        {
            flashImage.gameObject.SetActive(true);
            flashImage.color = new Color(1f, 1f, 1f, 0.9f);
        }
    }

    public void ShowResult(string message)
    {
        resultTimer = resultDisplayDuration;
        if (resultText != null)
        {
            resultText.text = message;
            resultText.gameObject.SetActive(true);
        }
    }

    private void UpdateFlash()
    {
        if (flashImage == null || flashTimer <= 0f)
        {
            if (flashImage != null && flashTimer <= 0f)
            {
                flashImage.gameObject.SetActive(false);
            }
            return;
        }

        flashTimer -= Time.deltaTime;
        float alpha = Mathf.Clamp01(flashTimer / Mathf.Max(0.0001f, flashDuration)) * 0.9f;
        flashImage.color = new Color(1f, 1f, 1f, alpha);
    }

    private void UpdateResultText()
    {
        if (resultText == null || resultTimer <= 0f)
        {
            if (resultText != null && resultTimer <= 0f)
            {
                resultText.gameObject.SetActive(false);
            }
            return;
        }

        resultTimer -= Time.deltaTime;
    }
}
