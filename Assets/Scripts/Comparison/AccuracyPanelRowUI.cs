using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class AccuracyPanelRowUI : MonoBehaviour
{
    public Image zoneColorImage;
    public TMP_Text zoneLabel;
    public TMP_Text simulatedCountLabel;
    public TMP_Text referenceCountLabel;
    public TMP_Text producerAccuracyLabel;
    public TMP_Text userAccuracyLabel;

    public void Bind(SimZone zone, int simulatedCount, int referenceCount, float producerAccuracy, float userAccuracy)
    {
        if (zoneColorImage != null)
        {
            zoneColorImage.color = GridManager.GetZoneColor(zone);
        }

        if (zoneLabel != null)
        {
            zoneLabel.text = zone.ToString();
        }

        if (simulatedCountLabel != null)
        {
            simulatedCountLabel.text = simulatedCount.ToString();
        }

        if (referenceCountLabel != null)
        {
            referenceCountLabel.text = referenceCount.ToString();
        }

        if (producerAccuracyLabel != null)
        {
            producerAccuracyLabel.text = (producerAccuracy * 100f).ToString("F1") + "%";
        }

        if (userAccuracyLabel != null)
        {
            userAccuracyLabel.text = (userAccuracy * 100f).ToString("F1") + "%";
        }
    }
}
