using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public enum ComparisonViewMode
{
    Normal,
    Split,
    Diff
}

public class ReferenceMapComparer : MonoBehaviour
{
    public GridManager gridManager;
    public SimulationController simulationController;

    [FormerlySerializedAs("targetImage")]
    public RawImage simulationRawImage;

    public RawImage referenceRawImage;
    public RawImage referencePaletteImage;
    public RectTransform dividerRect;

    [FormerlySerializedAs("leftLabel")]
    public TMP_Text simulatedLabel;

    [FormerlySerializedAs("rightLabel")]
    public TMP_Text referenceLabel;

    public TMP_Text referenceInfoLabel;
    public AccuracyPanelUI accuracyPanelUI;
    public string referenceFilePath;
    public string referenceYearLabel = "2018";
    public string referencePaletteFilePath;
    public string referencePaletteYearLabel = "2018";
    public ComparisonViewMode viewMode = ComparisonViewMode.Normal;
    public bool autoRefreshOnSimulationTick = true;
    [Range(0f, 1f)]
    public float splitNormalized = 0.5f;

    private readonly StringBuilder infoBuilder = new StringBuilder(256);

    private ReferenceMapData referenceData;
    private AccuracyReport lastReport;
    private Texture simulationTexture;
    private Texture referenceRawImageOriginalTexture;
    private Texture2D referenceTexture;
    private Texture2D diffTexture;
    private Color32[] referenceBuffer;
    private Color32[] diffBuffer;
    private bool hasReferenceMap;
    private int lastComparedTick = -1;
    private bool missingReferenceWarningLogged;
    private bool missingSimulationWarningLogged;

    private static readonly Color32 DiffExactColor = new Color32(46, 125, 50, 255);
    private static readonly Color32 DiffCategoryColor = new Color32(253, 216, 53, 255);
    private static readonly Color32 DiffWrongColor = new Color32(183, 28, 28, 255);
    private static readonly Color32 TransparentColor = new Color32(0, 0, 0, 0);

    private void Awake()
    {
        ResolveGridManager();
        CacheAssignedTextures();
        UpdateLabels();
    }

    private void Start()
    {
        if (!string.IsNullOrWhiteSpace(referencePaletteFilePath))
        {
            LoadReferencePaletteMap(referencePaletteFilePath);
        }
        else if (!string.IsNullOrWhiteSpace(referenceFilePath))
        {
            LoadReferenceMap(referenceFilePath);
        }
        else
        {
            ApplyView();
        }
    }

    private void Update()
    {
        if (!hasReferenceMap || !autoRefreshOnSimulationTick || simulationController == null)
        {
            return;
        }

        if (simulationController.currentTick != lastComparedTick)
        {
            RefreshComparison();
        }
    }

    public void LoadReferenceMap(string path)
    {
        ResolveGridManager();
        if (gridManager == null)
        {
            Debug.LogError("ReferenceMapComparer requires a GridManager reference.", this);
            return;
        }

        if (!CorineImporter.TryLoadReferenceMap(path, gridManager.width, gridManager.height, referenceYearLabel, out referenceData))
        {
            hasReferenceMap = false;
            ApplyView();
            return;
        }

        if (referenceData.wasResampled)
        {
            Debug.LogWarning("Reference map dimensions differ from the simulation grid. Nearest-neighbour resampling was applied.", this);
        }

        hasReferenceMap = true;
        CacheAssignedTextures();
        RefreshComparison();
    }

    public void LoadReferencePaletteMap(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (referencePaletteImage == null && referenceRawImage == null)
        {
            Debug.LogWarning("ReferenceMapComparer: referencePaletteImage is not assigned.", this);
            return;
        }

        referenceFilePath = path;
        if (!string.IsNullOrWhiteSpace(referencePaletteYearLabel))
        {
            referenceYearLabel = referencePaletteYearLabel;
        }

        LoadReferenceMap(path);
    }

    public void RefreshComparison()
    {
        ResolveGridManager();
        if (!hasReferenceMap || gridManager == null)
        {
            return;
        }

        lastReport = AccuracyCalculator.ComputeAccuracy(gridManager.GetGridReference(), referenceData.zoneGrid);
        lastComparedTick = simulationController != null ? simulationController.currentTick : lastComparedTick;

        if (accuracyPanelUI != null)
        {
            accuracyPanelUI.UpdatePanel(lastReport, referenceData);
        }

        UpdateReferenceInfo();
        RebuildReferenceTexture();
        RebuildDiffTexture();
        ApplyView();
    }

    public void SetViewMode(int mode)
    {
        SetViewMode((ComparisonViewMode)mode);
    }

    public void SetViewMode(ComparisonViewMode mode)
    {
        viewMode = mode;
        ApplyView();
    }

    public void SetDividerNormalized(float value)
    {
        splitNormalized = Mathf.Clamp01(value);
        UpdateDividerVisual();
    }

    private void ResolveGridManager()
    {
        if (gridManager == null)
        {
            gridManager = GridManager.Instance != null ? GridManager.Instance : FindFirstObjectByType<GridManager>();
        }
    }

    private void CacheAssignedTextures()
    {
        if (referencePaletteImage == null)
        {
            referencePaletteImage = referenceRawImage;
        }

        if (simulationRawImage != null)
        {
            simulationTexture = simulationRawImage.texture;
            missingSimulationWarningLogged = false;
        }
        else if (!missingSimulationWarningLogged)
        {
            Debug.LogWarning("ReferenceMapComparer: simulationRawImage is not assigned.", this);
            missingSimulationWarningLogged = true;
        }

        if (referencePaletteImage != null)
        {
            referenceRawImageOriginalTexture = referencePaletteImage.texture;
            missingReferenceWarningLogged = false;
        }
        else if (!missingReferenceWarningLogged)
        {
            Debug.LogWarning("ReferenceMapComparer: referencePaletteImage is not assigned.", this);
            missingReferenceWarningLogged = true;
        }
    }

    private void ApplyView()
    {
        CacheAssignedTextures();
        UpdateLabels();
        UpdateDividerVisual();

        if (simulationRawImage == null || referencePaletteImage == null)
        {
            return;
        }

        if (simulationTexture != null && simulationRawImage.texture != simulationTexture)
        {
            simulationRawImage.texture = simulationTexture;
        }

        if (!hasReferenceMap)
        {
            if (referenceRawImageOriginalTexture != null)
            {
                referencePaletteImage.texture = referenceRawImageOriginalTexture;
            }

            return;
        }

        switch (viewMode)
        {
            case ComparisonViewMode.Diff:
                if (diffTexture != null)
                {
                    referencePaletteImage.texture = diffTexture;
                }
                break;
            case ComparisonViewMode.Split:
            default:
                if (referenceTexture != null)
                {
                    referencePaletteImage.texture = referenceTexture;
                }
                break;
        }
    }

    private void RebuildReferenceTexture()
    {
        if (referencePaletteImage == null)
        {
            Debug.LogWarning("ReferenceMapComparer: referencePaletteImage is not assigned.", this);
            return;
        }

        int width = gridManager.width;
        int height = gridManager.height;
        EnsureReferenceTexture(width, height);

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;

            for (int x = 0; x < width; x++)
            {
                referenceBuffer[rowOffset + x] = GridManager.GetZoneColor(referenceData.zoneGrid[x, y]);
            }
        }

        referenceTexture.SetPixels32(referenceBuffer);
        referenceTexture.Apply(false, false);
        referencePaletteImage.texture = referenceTexture;
    }

    private void RebuildDiffTexture()
    {
        int width = gridManager.width;
        int height = gridManager.height;
        Cell[,] simulationGrid = gridManager.GetGridReference();
        EnsureDiffTexture(width, height);

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;

            for (int x = 0; x < width; x++)
            {
                Cell simulationCell = simulationGrid[x, y];
                SimZone referenceZone = referenceData.zoneGrid[x, y];

                if (!simulationCell.isActive || referenceZone == SimZone.NODATA)
                {
                    diffBuffer[rowOffset + x] = TransparentColor;
                    continue;
                }

                SimZone predictedZone = simulationCell.currentZone;
                if (predictedZone == referenceZone)
                {
                    diffBuffer[rowOffset + x] = DiffExactColor;
                }
                else if (AccuracyCalculator.GetCategory(predictedZone) == AccuracyCalculator.GetCategory(referenceZone))
                {
                    diffBuffer[rowOffset + x] = DiffCategoryColor;
                }
                else
                {
                    diffBuffer[rowOffset + x] = DiffWrongColor;
                }
            }
        }

        diffTexture.SetPixels32(diffBuffer);
        diffTexture.Apply(false, false);
    }

    private void EnsureReferenceTexture(int width, int height)
    {
        int expectedLength = width * height;
        if (referenceBuffer == null || referenceBuffer.Length != expectedLength)
        {
            referenceBuffer = new Color32[expectedLength];
        }

        if (referenceTexture != null && (referenceTexture.width != width || referenceTexture.height != height))
        {
            Destroy(referenceTexture);
            referenceTexture = null;
        }

        if (referenceTexture == null)
        {
            referenceTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            referenceTexture.filterMode = FilterMode.Point;
            referenceTexture.wrapMode = TextureWrapMode.Clamp;
        }
    }

    private void EnsureDiffTexture(int width, int height)
    {
        int expectedLength = width * height;
        if (diffBuffer == null || diffBuffer.Length != expectedLength)
        {
            diffBuffer = new Color32[expectedLength];
        }

        if (diffTexture != null && (diffTexture.width != width || diffTexture.height != height))
        {
            Destroy(diffTexture);
            diffTexture = null;
        }

        if (diffTexture == null)
        {
            diffTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            diffTexture.filterMode = FilterMode.Point;
            diffTexture.wrapMode = TextureWrapMode.Clamp;
        }
    }

    private void UpdateLabels()
    {
        if (simulatedLabel != null)
        {
            simulatedLabel.text = "Simulated (2018)";
        }

        if (referenceLabel != null)
        {
            switch (viewMode)
            {
                case ComparisonViewMode.Diff:
                    referenceLabel.text = "Diff View";
                    break;
                default:
                    referenceLabel.text = "Reference (" + (string.IsNullOrEmpty(referenceData.yearLabel) ? "2018" : referenceData.yearLabel) + ")";
                    break;
            }
        }
    }

    private void UpdateDividerVisual()
    {
        if (dividerRect == null)
        {
            return;
        }

        dividerRect.gameObject.SetActive(viewMode == ComparisonViewMode.Split && hasReferenceMap);

        if (!dividerRect.gameObject.activeSelf)
        {
            return;
        }

        RectTransform parentRect = dividerRect.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Vector2 anchoredPosition = dividerRect.anchoredPosition;
        anchoredPosition.x = Mathf.Lerp(parentRect.rect.xMin, parentRect.rect.xMax, splitNormalized);
        dividerRect.anchoredPosition = anchoredPosition;
    }

    private void UpdateReferenceInfo()
    {
        if (referenceInfoLabel == null || !hasReferenceMap)
        {
            return;
        }

        infoBuilder.Length = 0;
        infoBuilder.Append("Reference: ");
        infoBuilder.Append(referenceData.fileName);
        infoBuilder.Append('\n');
        infoBuilder.Append("Source: ");
        infoBuilder.Append(referenceData.sourceWidth);
        infoBuilder.Append('x');
        infoBuilder.Append(referenceData.sourceHeight);
        infoBuilder.Append(" -> Grid: ");
        infoBuilder.Append(referenceData.targetWidth);
        infoBuilder.Append('x');
        infoBuilder.Append(referenceData.targetHeight);
        infoBuilder.Append('\n');
        infoBuilder.Append("Year: ");
        infoBuilder.Append(referenceData.yearLabel);
        infoBuilder.Append('\n');
        infoBuilder.Append("Zone distribution:");
        infoBuilder.Append('\n');

        int total = referenceData.targetWidth * referenceData.targetHeight;
        for (int i = 0; i < (int)SimZone.NODATA; i++)
        {
            SimZone zone = (SimZone)i;
            int count = referenceData.zoneCounts.TryGetValue(zone, out int value) ? value : 0;
            if (count <= 0)
            {
                continue;
            }

            infoBuilder.Append(zone);
            infoBuilder.Append(": ");
            infoBuilder.Append(((count * 100f) / total).ToString("F1"));
            infoBuilder.Append('%');
            infoBuilder.Append('\n');
        }

        referenceInfoLabel.text = infoBuilder.ToString();
    }

    private void OnDestroy()
    {
        if (referenceTexture != null)
        {
            Destroy(referenceTexture);
        }

        if (diffTexture != null)
        {
            Destroy(diffTexture);
        }
    }
}
