using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AccuracyPanelUI : MonoBehaviour
{
    public TMP_Text overallAccuracyLabel;
    public TMP_Text kappaLabel;
    public TMP_Text summaryLabel;
    public TMP_Text metadataLabel;
    public RectTransform tableContainer;
    public AccuracyPanelRowUI rowTemplate;
    public float rowSpacing = 6f;
    public float topPadding = 4f;
    public bool useVerticalLayoutGroup = true;
    public float layoutSpacing = 6f;
    public int layoutPaddingTop = 4;
    public int layoutPaddingBottom = 4;

    private readonly List<AccuracyPanelRowUI> rows = new List<AccuracyPanelRowUI>();
    private readonly StringBuilder builder = new StringBuilder(256);

    public void UpdatePanel(AccuracyReport report, ReferenceMapData referenceData)
    {
        if (overallAccuracyLabel != null)
        {
            overallAccuracyLabel.text = "Overall Accuracy: " + (report.overallAccuracy * 100f).ToString("F1") + "%";
        }

        if (kappaLabel != null)
        {
            kappaLabel.text = "Kappa: " + report.kappaCoefficient.ToString("F2") + " (" + InterpretKappa(report.kappaCoefficient) + ")";
        }

        if (summaryLabel != null)
        {
            summaryLabel.text =
                "Compared: " + report.totalCompared +
                "\nExact Matches: " + report.exactMatches +
                "\nSame-Category Mismatches: " + report.sameCategoryMismatches +
                "\nFull Mismatches: " + report.fullMismatches;
        }

        if (metadataLabel != null)
        {
            builder.Length = 0;
            builder.Append("Reference: ");
            builder.Append(referenceData.fileName);
            builder.Append('\n');
            builder.Append("Source: ");
            builder.Append(referenceData.sourceWidth);
            builder.Append('x');
            builder.Append(referenceData.sourceHeight);
            builder.Append(" -> Grid: ");
            builder.Append(referenceData.targetWidth);
            builder.Append('x');
            builder.Append(referenceData.targetHeight);
            builder.Append('\n');
            builder.Append("Year: ");
            builder.Append(string.IsNullOrEmpty(referenceData.yearLabel) ? "Reference" : referenceData.yearLabel);
            builder.Append('\n');
            builder.Append("Zone distribution:");

            int total = referenceData.targetWidth * referenceData.targetHeight;
            for (int i = 0; i < (int)SimZone.NODATA; i++)
            {
                SimZone zone = (SimZone)i;
                int count = referenceData.zoneCounts.TryGetValue(zone, out int zoneCount) ? zoneCount : 0;
                if (count <= 0)
                {
                    continue;
                }

                builder.Append('\n');
                builder.Append(zone);
                builder.Append(": ");
                builder.Append(((count * 100f) / total).ToString("F1"));
                builder.Append('%');
            }

            metadataLabel.text = builder.ToString();
        }

        EnsureRows((int)SimZone.NODATA);
        for (int i = 0; i < (int)SimZone.NODATA; i++)
        {
            SimZone zone = (SimZone)i;
            rows[i].Bind(
                zone,
                report.simulatedCounts.TryGetValue(zone, out int simulatedCount) ? simulatedCount : 0,
                report.referenceCounts.TryGetValue(zone, out int referenceCount) ? referenceCount : 0,
                report.producerAccuracy.TryGetValue(zone, out float producerAccuracy) ? producerAccuracy : 0f,
                report.userAccuracy.TryGetValue(zone, out float userAccuracy) ? userAccuracy : 0f);
        }

        ApplyRowLayout();
    }

    private void EnsureRows(int requiredCount)
    {
        if (tableContainer == null || rowTemplate == null)
        {
            return;
        }

        rowTemplate.gameObject.SetActive(false);

        while (rows.Count < requiredCount)
        {
            AccuracyPanelRowUI row = Instantiate(rowTemplate, tableContainer);
            row.gameObject.SetActive(true);
            RectTransform rowRect = row.transform as RectTransform;
            if (rowRect != null)
            {
                rowRect.localScale = Vector3.one;
                rowRect.anchoredPosition3D = Vector3.zero;
            }

            rows.Add(row);
        }
    }

    private void ApplyRowLayout()
    {
        if (tableContainer == null)
        {
            return;
        }

        VerticalLayoutGroup verticalLayoutGroup = tableContainer.GetComponent<VerticalLayoutGroup>();
        if (useVerticalLayoutGroup && verticalLayoutGroup != null)
        {
            verticalLayoutGroup.spacing = layoutSpacing;
            RectOffset padding = verticalLayoutGroup.padding;
            padding.top = layoutPaddingTop;
            padding.bottom = layoutPaddingBottom;
            verticalLayoutGroup.padding = padding;
            return;
        }

        float currentY = topPadding;
        for (int i = 0; i < rows.Count; i++)
        {
            RectTransform rowRect = rows[i].transform as RectTransform;
            if (rowRect == null)
            {
                continue;
            }

            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.localScale = Vector3.one;

            float rowHeight = rowRect.rect.height;
            if (rowHeight <= 0f)
            {
                rowHeight = rowRect.sizeDelta.y;
            }

            if (rowHeight <= 0f)
            {
                LayoutElement layoutElement = rowRect.GetComponent<LayoutElement>();
                rowHeight = layoutElement != null && layoutElement.preferredHeight > 0f ? layoutElement.preferredHeight : 28f;
            }

            rowRect.anchoredPosition = new Vector2(0f, -currentY);
            currentY += rowHeight + rowSpacing;
        }
    }

    private static string InterpretKappa(float kappa)
    {
        if (kappa < 0.4f)
        {
            return "Poor";
        }

        if (kappa < 0.6f)
        {
            return "Moderate";
        }

        if (kappa < 0.8f)
        {
            return "Good";
        }

        return "Excellent";
    }
}
