using UnityEngine;
using UnityEngine.UI;

public class GuardGaugeUI2D : MonoBehaviour
{
    public GuardGauge guardGauge;
    public Image fillImage;

    private void Update()
    {
        if (guardGauge == null || fillImage == null || guardGauge.MaxGuard <= 0)
        {
            return;
        }

        fillImage.fillAmount = (float)guardGauge.CurrentGuard / guardGauge.MaxGuard;
        fillImage.color = GetGaugeColor(guardGauge.CurrentGuard, guardGauge.MaxGuard);
    }

    private static Color GetGaugeColor(int current, int max)
    {
        if (current <= 1)
        {
            return Color.red;
        }

        if (current <= max / 2f)
        {
            return Color.yellow;
        }

        return Color.green;
    }
}
