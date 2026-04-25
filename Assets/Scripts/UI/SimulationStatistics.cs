using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimulationStatistics : MonoBehaviour
{
    public RectTransform stackedBarContainer;
    public Image segmentTemplate;
    public GameObject tooltipRoot;
    public RectTransform tooltipRect;
    public TMP_Text tooltipText;
    public RawImage urbanChartImage;

    public int chartWidth = 256;
    public int chartHeight = 64;

    private readonly List<int> urbanHistory = new List<int>(256);
    private readonly List<StatisticsBarSegment> segments = new List<StatisticsBarSegment>();
    private readonly StringBuilder tooltipBuilder = new StringBuilder(128);

    private int[] zoneCounts;
    private Texture2D chartTexture;
    private Color32[] chartPixels;
    private int lastUpdatedTick = -5;

    private static readonly Color32 ChartBackground = new Color32(12, 12, 12, 0);
    private static readonly Color32 ChartLine = new Color32(224, 90, 78, 255);

    private void Awake()
    {
        int zoneCount = (int)SimZone.NODATA + 1;
        zoneCounts = new int[zoneCount];
        EnsureSegments(zoneCount);
        InitializeChart();
        HideTooltip();
    }

    public void Refresh(Cell[,] grid, int currentTick)
    {
        if (grid == null || currentTick - lastUpdatedTick < 5)
        {
            return;
        }

        lastUpdatedTick = currentTick;
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        for (int i = 0; i < zoneCounts.Length; i++)
        {
            zoneCounts[i] = 0;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                zoneCounts[(int)grid[x, y].currentZone]++;
            }
        }

        UpdateBar(width * height);
        UpdateUrbanHistory();
        UpdateChart();
    }

    public void ShowTooltip(SimZone zone, int cells, float percent, Vector2 screenPosition)
    {
        if (tooltipRoot == null || tooltipText == null)
        {
            return;
        }

        tooltipBuilder.Length = 0;
        tooltipBuilder.Append("Zone: ");
        tooltipBuilder.Append(zone);
        tooltipBuilder.Append('\n');
        tooltipBuilder.Append("Cells: ");
        tooltipBuilder.Append(cells);
        tooltipBuilder.Append('\n');
        tooltipBuilder.Append("Percent: ");
        tooltipBuilder.Append(percent.ToString("F1"));
        tooltipBuilder.Append('%');

        tooltipText.text = tooltipBuilder.ToString();
        tooltipRoot.SetActive(true);

        if (tooltipRect != null)
        {
            tooltipRect.position = screenPosition;
        }
    }

    public void HideTooltip()
    {
        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(false);
        }
    }

    private void EnsureSegments(int zoneCount)
    {
        if (stackedBarContainer == null || segmentTemplate == null)
        {
            return;
        }

        segmentTemplate.gameObject.SetActive(false);

        while (segments.Count < zoneCount)
        {
            Image segmentImage = Instantiate(segmentTemplate, stackedBarContainer);
            segmentImage.gameObject.SetActive(true);

            StatisticsBarSegment segment = segmentImage.GetComponent<StatisticsBarSegment>();
            if (segment == null)
            {
                segment = segmentImage.gameObject.AddComponent<StatisticsBarSegment>();
            }

            LayoutElement layoutElement = segmentImage.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = segmentImage.gameObject.AddComponent<LayoutElement>();
            }

            segment.Initialize(this);
            segments.Add(segment);
        }
    }

    private void UpdateBar(int totalCells)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            int count = zoneCounts[i];
            float percent = totalCells > 0 ? (count * 100f) / totalCells : 0f;
            Image image = segments[i].GetComponent<Image>();
            LayoutElement layoutElement = segments[i].GetComponent<LayoutElement>();

            image.color = GridManager.GetZoneColor((SimZone)i);
            layoutElement.flexibleWidth = count;
            layoutElement.minWidth = count > 0 ? 2f : 0f;
            segments[i].SetData((SimZone)i, count, percent);
        }
    }

    private void InitializeChart()
    {
        if (urbanChartImage == null)
        {
            return;
        }

        chartWidth = Mathf.Max(16, chartWidth);
        chartHeight = Mathf.Max(16, chartHeight);
        chartTexture = new Texture2D(chartWidth, chartHeight, TextureFormat.RGBA32, false);
        chartTexture.filterMode = FilterMode.Point;
        chartTexture.wrapMode = TextureWrapMode.Clamp;
        chartPixels = new Color32[chartWidth * chartHeight];
        urbanChartImage.texture = chartTexture;
    }

    private void UpdateUrbanHistory()
    {
        int urbanCount = zoneCounts[(int)SimZone.URBAN_DENSE] + zoneCounts[(int)SimZone.URBAN_RESIDENTIAL];
        urbanHistory.Add(urbanCount);

        if (urbanHistory.Count > chartWidth)
        {
            urbanHistory.RemoveAt(0);
        }
    }

    private void UpdateChart()
    {
        if (chartTexture == null || chartPixels == null)
        {
            return;
        }

        for (int i = 0; i < chartPixels.Length; i++)
        {
            chartPixels[i] = ChartBackground;
        }

        int maxValue = 1;
        for (int i = 0; i < urbanHistory.Count; i++)
        {
            if (urbanHistory[i] > maxValue)
            {
                maxValue = urbanHistory[i];
            }
        }

        for (int x = 0; x < urbanHistory.Count; x++)
        {
            int y = Mathf.Clamp(Mathf.RoundToInt((urbanHistory[x] / (float)maxValue) * (chartHeight - 1)), 0, chartHeight - 1);
            chartPixels[(y * chartWidth) + x] = ChartLine;
        }

        chartTexture.SetPixels32(chartPixels);
        chartTexture.Apply(false, false);
    }
}
