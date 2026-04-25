using System.Text;
using Simulation;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CellInspector : MonoBehaviour
{
    public RectTransform gridRect;
    public GameObject tooltipRoot;
    public RectTransform tooltipRect;
    public TMP_Text tooltipText;
    public CASimulator caSimulator;

    private Cell[] neighborBuffer;
    private int[] neighborCounts;
    private readonly StringBuilder builder = new StringBuilder(512);

    private void Awake()
    {
        int radius = 2;
        neighborBuffer = new Cell[((radius * 2 + 1) * (radius * 2 + 1)) - 1];
        neighborCounts = new int[(int)SimZone.NODATA + 1];
        HideTooltip();
    }

    private void Update()
    {
        GridManager gridManager = GridManager.Instance;
        if (gridManager == null || gridRect == null || tooltipText == null)
        {
            return;
        }

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, Input.mousePosition, null, out localPoint))
        {
            HideTooltip();
            return;
        }

        Rect rect = gridRect.rect;
        if (!rect.Contains(localPoint))
        {
            HideTooltip();
            return;
        }

        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        int x = Mathf.Clamp(Mathf.FloorToInt(normalizedX * gridManager.width), 0, gridManager.width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(normalizedY * gridManager.height), 0, gridManager.height - 1);

        Cell cell = gridManager.GetCell(x, y);
        int neighborCount = gridManager.GetNeighbors(x, y, 2, ref neighborBuffer);

        for (int i = 0; i < neighborCounts.Length; i++)
        {
            neighborCounts[i] = 0;
        }

        for (int i = 0; i < neighborCount; i++)
        {
            neighborCounts[(int)neighborBuffer[i].currentZone]++;
        }

        builder.Length = 0;
        builder.Append("Zone: ");
        builder.Append(cell.currentZone);
        builder.Append('\n');
        builder.Append("urbanPressure: ");
        builder.Append(cell.urbanPressure.ToString("F2"));
        builder.Append('\n');
        builder.Append("distToWater: ");
        builder.Append(cell.distToWater.ToString("F1"));
        builder.Append('\n');
        builder.Append("distToCenter: ");
        builder.Append(cell.distToCenter.ToString("F1"));

        for (int i = 0; i < neighborCounts.Length; i++)
        {
            if (neighborCounts[i] <= 0)
            {
                continue;
            }

            builder.Append('\n');
            builder.Append((SimZone)i);
            builder.Append(": ");
            builder.Append(neighborCounts[i]);
        }

        if (caSimulator != null && caSimulator.TryGetTransitionProbabilities(x, y, out float residentialChance, out float industrialChance))
        {
            builder.Append('\n');
            builder.Append("Chance -> RESIDENTIAL: ");
            builder.Append(residentialChance.ToString("F3"));
            builder.Append('\n');
            builder.Append("Chance -> INDUSTRIAL: ");
            builder.Append(industrialChance.ToString("F3"));
        }

        tooltipText.text = builder.ToString();
        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(true);
        }

        if (tooltipRect != null)
        {
            tooltipRect.position = Input.mousePosition;
        }
    }

    private void HideTooltip()
    {
        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(false);
        }
    }
}
