using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum DebugViewMode
{
    SimZone,
    CorineRaw
}

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Min(1)]
    public int width = 512;

    [Min(1)]
    public int height = 512;

    public RawImage targetImage;
    public bool generateTestPatternOnStart = true;
    public DebugViewMode debugViewMode = DebugViewMode.SimZone;

    #region Grid

    private Cell[,] grid;
    private int[,] corineDebugIds;

    public static readonly Dictionary<SimZone, Color32> ZoneColors = new Dictionary<SimZone, Color32>
    {
        { SimZone.URBAN_DENSE, new Color32(155, 35, 53, 255) },
        { SimZone.URBAN_RESIDENTIAL, new Color32(224, 90, 78, 255) },
        { SimZone.COMMERCIAL, new Color32(123, 94, 167, 255) },
        { SimZone.INDUSTRIAL, new Color32(121, 85, 72, 255) },
        { SimZone.TRANSPORT_INFRA, new Color32(84, 110, 122, 255) },
        { SimZone.GREEN_PUBLIC, new Color32(46, 125, 50, 255) },
        { SimZone.AGRICULTURE, new Color32(200, 160, 0, 255) },
        { SimZone.FOREST, new Color32(27, 94, 32, 255) },
        { SimZone.SHRUBLAND, new Color32(102, 187, 106, 255) },
        { SimZone.WATER, new Color32(21, 101, 192, 255) },
        { SimZone.BARE_OTHER, new Color32(158, 158, 158, 255) },
        { SimZone.NODATA, new Color32(0, 0, 0, 0) }
    };

    public static Color32 GetZoneColor(SimZone zone)
    {
        return ZoneColors[zone];
    }

    public Cell GetCell(int x, int y)
    {
        return IsInBounds(x, y) ? grid[x, y] : default;
    }

    public Cell[,] GetGridReference()
    {
        return grid;
    }

    public void SetGridReference(Cell[,] sourceGrid)
    {
        if (sourceGrid == null)
        {
            return;
        }

        grid = sourceGrid;
    }

    public void SetCorineDebugIds(int[,] ids)
    {
        corineDebugIds = ids;
        MarkDirty();
    }

    public void SetCell(int x, int y, Cell cell)
    {
        if (!IsInBounds(x, y))
        {
            return;
        }

        grid[x, y] = cell;
    }

    public void SetNextZone(int x, int y, SimZone zone)
    {
        if (!IsInBounds(x, y))
        {
            return;
        }

        Cell cell = grid[x, y];
        cell.nextZone = zone;
        grid[x, y] = cell;
    }

    public void GenerateTestPattern()
    {
        int halfWidth = width >> 1;
        int halfHeight = height >> 1;

        for (int y = 0; y < height; y++)
        {
            bool isTopHalf = y >= halfHeight;

            for (int x = 0; x < width; x++)
            {
                SimZone zone;

                if (isTopHalf)
                {
                    zone = x < halfWidth ? SimZone.URBAN_DENSE : SimZone.FOREST;
                }
                else
                {
                    zone = x < halfWidth ? SimZone.AGRICULTURE : SimZone.WATER;
                }

                Cell cell = grid[x, y];
                cell.currentZone = zone;
                cell.nextZone = zone;
                cell.isActive = true;
                cell.urbanPressure = 0f;
                cell.distToWater = 0f;
                cell.distToCenter = 0f;
                grid[x, y] = cell;
            }
        }

        corineDebugIds = null;
        MarkDirty();
    }

    private bool IsInBounds(int x, int y)
    {
        return (uint)x < (uint)width && (uint)y < (uint)height;
    }

    private void InitializeGrid()
    {
        grid = new Cell[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                grid[x, y].currentZone = SimZone.NODATA;
                grid[x, y].nextZone = SimZone.NODATA;
                grid[x, y].isActive = true;
            }
        }
    }

    #endregion

    #region Rendering

    private Texture2D texCurrent;
    private Texture2D texDiff;
    private Color32[] pixelBuffer;
    private bool isDirty;
    private DebugViewMode lastDebugViewMode;

    public void MarkDirty()
    {
        isDirty = true;
    }

    public void Render()
    {
        Color32[] buffer = pixelBuffer;
        Cell[,] cells = grid;
        int rowOffset;

        for (int y = 0; y < height; y++)
        {
            rowOffset = y * width;

            for (int x = 0; x < width; x++)
            {
                if (debugViewMode == DebugViewMode.CorineRaw && corineDebugIds != null)
                {
                    if (!CorineImporter.TryGetCorineColor(corineDebugIds[x, y], out buffer[rowOffset + x]))
                    {
                        buffer[rowOffset + x] = ZoneColors[SimZone.NODATA];
                    }

                    continue;
                }

                buffer[rowOffset + x] = ZoneColors[cells[x, y].currentZone];
            }
        }

        texCurrent.SetPixels32(buffer);
        texCurrent.Apply(false, false);
    }

    private void InitializeTextures()
    {
        texCurrent = CreateTexture("Grid_Current");
        texDiff = CreateTexture("Grid_Diff");
        pixelBuffer = new Color32[width * height];
    }

    private Texture2D CreateTexture(string textureName)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = textureName;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private void BindTargetImage()
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.texture = texCurrent;
        targetImage.raycastTarget = false;
    }

    #endregion

    #region Neighbors

    public int GetNeighbors(int x, int y, int radius, ref Cell[] buffer)
    {
        if (!IsInBounds(x, y) || radius < 1 || buffer == null)
        {
            return 0;
        }

        int diameter = (radius * 2) + 1;
        int requiredCapacity = (diameter * diameter) - 1;
        if (buffer.Length < requiredCapacity)
        {
            return 0;
        }

        int minX = x - radius;
        if (minX < 0)
        {
            minX = 0;
        }

        int maxX = x + radius;
        if (maxX >= width)
        {
            maxX = width - 1;
        }

        int minY = y - radius;
        if (minY < 0)
        {
            minY = 0;
        }

        int maxY = y + radius;
        if (maxY >= height)
        {
            maxY = height - 1;
        }

        int count = 0;

        for (int neighborY = minY; neighborY <= maxY; neighborY++)
        {
            for (int neighborX = minX; neighborX <= maxX; neighborX++)
            {
                if (neighborX == x && neighborY == y)
                {
                    continue;
                }

                buffer[count] = grid[neighborX, neighborY];
                count++;
            }
        }

        return count;
    }

    #endregion

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (targetImage == null)
        {
            targetImage = GetComponent<RawImage>();
        }

        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        InitializeGrid();
        InitializeTextures();
        BindTargetImage();
        lastDebugViewMode = debugViewMode;
        MarkDirty();
    }

    private void Start()
    {
        if (generateTestPatternOnStart)
        {
            GenerateTestPattern();
        }
    }

    private void Update()
    {
        if (lastDebugViewMode != debugViewMode)
        {
            lastDebugViewMode = debugViewMode;
            MarkDirty();
        }

        if (!isDirty)
        {
            return;
        }

        Render();
        isDirty = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (texCurrent != null)
        {
            Destroy(texCurrent);
        }

        if (texDiff != null)
        {
            Destroy(texDiff);
        }
    }

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        if (Application.isPlaying)
        {
            MarkDirty();
        }
    }
}
