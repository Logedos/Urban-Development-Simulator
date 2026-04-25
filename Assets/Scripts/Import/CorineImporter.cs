using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class CorineImporter : MonoBehaviour
{
    public string filePath;
    public bool loadOnStart;
    public bool loadAsyncOnStart = true;

    public Vector2 CityCenter => cityCenter;

    private int[,] idGrid;
    private Color32[,] zoneColorGrid;
    private bool[,] urbanMask;
    private bool[,] transportMask;
    private bool[,] industrialMask;
    private SimZone[,] smoothingZones;
    private int[] corineIdCounts;
    private Color32[] zonePixelBuffer;
    private int[] zoneCounterBuffer;
    private readonly Dictionary<Color32, int> colorCache = new Dictionary<Color32, int>(256);

    private Vector2 cityCenter;
    private Coroutine activeLoadRoutine;
    private int targetWidth;
    private int targetHeight;
    private float halfGridDiagonal;

    public static readonly (int id, Color32 rgb)[] corinePalette =
    {
        (111, new Color32(230, 0, 77, 255)),
        (112, new Color32(255, 0, 0, 255)),
        (121, new Color32(204, 77, 242, 255)),
        (122, new Color32(204, 0, 0, 255)),
        (123, new Color32(230, 204, 204, 255)),
        (124, new Color32(230, 204, 230, 255)),
        (131, new Color32(166, 0, 204, 255)),
        (132, new Color32(166, 77, 0, 255)),
        (133, new Color32(255, 77, 255, 255)),
        (141, new Color32(255, 166, 255, 255)),
        (142, new Color32(255, 230, 255, 255)),
        (211, new Color32(255, 255, 168, 255)),
        (212, new Color32(255, 255, 0, 255)),
        (213, new Color32(230, 230, 0, 255)),
        (221, new Color32(230, 128, 0, 255)),
        (222, new Color32(242, 166, 77, 255)),
        (223, new Color32(230, 166, 0, 255)),
        (231, new Color32(230, 230, 77, 255)),
        (241, new Color32(255, 230, 166, 255)),
        (242, new Color32(255, 230, 77, 255)),
        (243, new Color32(230, 204, 77, 255)),
        (244, new Color32(242, 204, 166, 255)),
        (311, new Color32(128, 255, 0, 255)),
        (312, new Color32(0, 166, 0, 255)),
        (313, new Color32(77, 255, 0, 255)),
        (321, new Color32(204, 242, 77, 255)),
        (322, new Color32(166, 255, 128, 255)),
        (323, new Color32(166, 230, 77, 255)),
        (324, new Color32(166, 242, 0, 255)),
        (331, new Color32(230, 230, 230, 255)),
        (332, new Color32(204, 204, 204, 255)),
        (333, new Color32(204, 255, 204, 255)),
        (334, new Color32(0, 0, 0, 255)),
        (335, new Color32(166, 230, 204, 255)),
        (411, new Color32(166, 166, 255, 255)),
        (412, new Color32(77, 77, 255, 255)),
        (421, new Color32(204, 204, 255, 255)),
        (422, new Color32(230, 230, 255, 255)),
        (423, new Color32(166, 166, 230, 255)),
        (511, new Color32(0, 204, 242, 255)),
        (512, new Color32(128, 242, 230, 255)),
        (521, new Color32(0, 255, 166, 255)),
        (522, new Color32(166, 255, 230, 255)),
        (523, new Color32(230, 242, 255, 255)),
        (999, new Color32(255, 255, 255, 255))
    };

    private static readonly Dictionary<int, Color32> corinePaletteLookup = BuildCorinePaletteLookup();

    private void Awake()
    {
        GridManager gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager != null)
        {
            gridManager.generateTestPatternOnStart = false;
        }
    }

    private void Start()
    {
        if (!loadOnStart || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (loadAsyncOnStart)
        {
            if (activeLoadRoutine != null)
            {
                StopCoroutine(activeLoadRoutine);
            }

            activeLoadRoutine = StartCoroutine(LoadAsync(filePath));
            return;
        }

        LoadFromFile(filePath);
    }

    public void LoadFromFile(string path)
    {
        if (activeLoadRoutine != null)
        {
            StopCoroutine(activeLoadRoutine);
            activeLoadRoutine = null;
        }

        GridManager gridManager = ResolveGridManager();
        if (gridManager == null)
        {
            Debug.LogError("CorineImporter could not find a GridManager instance.", this);
            return;
        }

        string resolvedPath = ResolvePath(path);
        if (!File.Exists(resolvedPath))
        {
            Debug.LogError("CorineImporter could not find file: " + resolvedPath, this);
            return;
        }

        Texture2D texture = LoadTextureFromFile(resolvedPath);
        if (texture == null)
        {
            return;
        }

        try
        {
            PrepareImport(gridManager);

            Color32[] sourcePixels = texture.GetPixels32();
            int sourceWidth = texture.width;
            int sourceHeight = texture.height;

            Pass1Classify(sourcePixels, sourceWidth, sourceHeight);
            gridManager.SetCorineDebugIds(idGrid);
            ValidateClassification();
            Pass2DetectCityCenter();

            int[] zoneCounts = new int[(int)SimZone.NODATA + 1];
            int nodataCount = Pass3PopulateGrid(gridManager, zoneCounts);
            ApplyMajoritySmoothing();
            RefreshZoneCounts(zoneCounts);

            Texture2D zoneTexture = BuildZoneTexture();
            SaveZoneTexture(zoneTexture);
            Destroy(zoneTexture);

            gridManager.MarkDirty();
            LogImportSummary(sourceWidth, sourceHeight, zoneCounts, nodataCount);
        }
        finally
        {
            Destroy(texture);
        }
    }

    public IEnumerator LoadAsync(string path)
    {
        GridManager gridManager = ResolveGridManager();
        if (gridManager == null)
        {
            Debug.LogError("CorineImporter could not find a GridManager instance.", this);
            activeLoadRoutine = null;
            yield break;
        }

        string resolvedPath = ResolvePath(path);
        if (!File.Exists(resolvedPath))
        {
            Debug.LogError("CorineImporter could not find file: " + resolvedPath, this);
            activeLoadRoutine = null;
            yield break;
        }

        Texture2D texture = LoadTextureFromFile(resolvedPath);
        if (texture == null)
        {
            activeLoadRoutine = null;
            yield break;
        }

        PrepareImport(gridManager);

        Color32[] sourcePixels = texture.GetPixels32();
        int sourceWidth = texture.width;
        int sourceHeight = texture.height;

        yield return Pass1ClassifyAsync(sourcePixels, sourceWidth, sourceHeight);
        gridManager.SetCorineDebugIds(idGrid);
        ValidateClassification();
        yield return Pass2DetectCityCenterAsync();

        int[] zoneCounts = new int[(int)SimZone.NODATA + 1];
        int nodataCount = 0;
        yield return Pass3PopulateGridAsync(gridManager, zoneCounts, value => nodataCount = value);
        ApplyMajoritySmoothing();
        RefreshZoneCounts(zoneCounts);

        Texture2D zoneTexture = BuildZoneTexture();
        SaveZoneTexture(zoneTexture);
        Destroy(zoneTexture);

        gridManager.MarkDirty();
        LogImportSummary(sourceWidth, sourceHeight, zoneCounts, nodataCount);

        Destroy(texture);
        activeLoadRoutine = null;
    }

    public Color32 SampleNearest(Color32[] pixels, int srcW, int srcH, int x, int y)
    {
        int srcX = Mathf.FloorToInt((x + 0.5f) * srcW / targetWidth);
        int srcY = Mathf.FloorToInt((y + 0.5f) * srcH / targetHeight);

        if (srcX < 0)
        {
            srcX = 0;
        }
        else if (srcX >= srcW)
        {
            srcX = srcW - 1;
        }

        if (srcY < 0)
        {
            srcY = 0;
        }
        else if (srcY >= srcH)
        {
            srcY = srcH - 1;
        }

        return pixels[(srcY * srcW) + srcX];
    }

    public int ClassifyPixel(Color32 px)
    {
        if (colorCache.TryGetValue(px, out int cachedId))
        {
            return cachedId;
        }

        int bestId = 999;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < corinePalette.Length; i++)
        {
            Color32 paletteColor = corinePalette[i].rgb;
            int deltaR = px.r - paletteColor.r;
            int deltaG = px.g - paletteColor.g;
            int deltaB = px.b - paletteColor.b;
            int distance = (deltaR * deltaR) + (deltaG * deltaG) + (deltaB * deltaB);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestId = corinePalette[i].id;

                if (distance < 50)
                {
                    break;
                }
            }
        }

        if (bestId == 999)
        {
            bestId = corinePalette[0].id;
            int bestDist = int.MaxValue;

            for (int i = 0; i < corinePalette.Length; i++)
            {
                Color32 c = corinePalette[i].rgb;
                int dr = px.r - c.r;
                int dg = px.g - c.g;
                int db = px.b - c.b;
                int dist = dr * dr + dg * dg + db * db;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestId = corinePalette[i].id;
                }
            }
        }

        colorCache[px] = bestId;
        return bestId;
    }

    public static bool TryGetCorineColor(int classId, out Color32 color)
    {
        return corinePaletteLookup.TryGetValue(classId, out color);
    }

    public static bool TryLoadReferenceMap(string path, int targetWidth, int targetHeight, string yearLabel, out ReferenceMapData data)
    {
        data = default;

        string resolvedPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        if (!File.Exists(resolvedPath))
        {
            Debug.LogError("Reference map file not found: " + resolvedPath);
            return false;
        }

        Texture2D texture = LoadTextureFromFileStatic(resolvedPath);
        if (texture == null)
        {
            return false;
        }

        try
        {
            int sourceWidth = texture.width;
            int sourceHeight = texture.height;
            Color32[] sourcePixels = texture.GetPixels32();
            int[,] loadedIdGrid = new int[targetWidth, targetHeight];
            SimZone[,] loadedZoneGrid = new SimZone[targetWidth, targetHeight];
            bool[,] loadedUrbanMask = new bool[targetWidth, targetHeight];
            bool[,] loadedTransportMask = new bool[targetWidth, targetHeight];
            bool[,] loadedIndustrialMask = new bool[targetWidth, targetHeight];
            Dictionary<Color32, int> localColorCache = new Dictionary<Color32, int>(256);
            Dictionary<SimZone, int> zoneCounts = new Dictionary<SimZone, int>((int)SimZone.NODATA + 1);

            for (int i = 0; i <= (int)SimZone.NODATA; i++)
            {
                zoneCounts[(SimZone)i] = 0;
            }

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int classId = ClassifyPixelStatic(
                        SampleNearestStatic(sourcePixels, sourceWidth, sourceHeight, targetWidth, targetHeight, x, y),
                        localColorCache);

                    loadedIdGrid[x, y] = classId;
                    loadedUrbanMask[x, y] = classId == 111 || classId == 112 || classId == 133;
                    loadedTransportMask[x, y] = classId == 122 || classId == 123 || classId == 124;
                    loadedIndustrialMask[x, y] = classId == 131 || classId == 132;
                }
            }

            Vector2 detectedCityCenter = DetectCityCenterStatic(loadedIdGrid, targetWidth, targetHeight);
            float localHalfGridDiagonal = Mathf.Sqrt((targetWidth * targetWidth) + (targetHeight * targetHeight)) * 0.5f;

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int classId = loadedIdGrid[x, y];
                    if (classId == 999)
                    {
                        classId = GetNearestValidNeighborStatic(loadedIdGrid, x, y, targetWidth, targetHeight);
                    }

                    SimZone zone = MapCorineToZoneStatic(
                        x,
                        y,
                        classId,
                        loadedUrbanMask,
                        loadedTransportMask,
                        loadedIndustrialMask,
                        detectedCityCenter,
                        targetWidth,
                        targetHeight,
                        localHalfGridDiagonal);

                    loadedZoneGrid[x, y] = zone;
                    zoneCounts[zone] = zoneCounts[zone] + 1;
                }
            }

            data = new ReferenceMapData
            {
                fileName = Path.GetFileName(resolvedPath),
                fullPath = resolvedPath,
                yearLabel = yearLabel,
                sourceWidth = sourceWidth,
                sourceHeight = sourceHeight,
                targetWidth = targetWidth,
                targetHeight = targetHeight,
                wasResampled = sourceWidth != targetWidth || sourceHeight != targetHeight,
                cityCenter = detectedCityCenter,
                zoneGrid = loadedZoneGrid,
                corineIdGrid = loadedIdGrid,
                zoneCounts = zoneCounts
            };

            return true;
        }
        finally
        {
            Destroy(texture);
        }
    }

    public SimZone Classify121(int x, int y)
    {
        float score = 0f;
        float dx = x - cityCenter.x;
        float dy = y - cityCenter.y;
        float distRatio = Mathf.Sqrt((dx * dx) + (dy * dy)) / halfGridDiagonal;

        if (distRatio < 0.35f)
        {
            score += 1f;
        }

        if (GetUrbanRatio(x, y, 2) > 0.4f)
        {
            score += 0.8f;
        }

        if (HasTransportNearby(x, y, 4))
        {
            score -= 0.6f;
        }

        if (HasIndustrialNearby(x, y, 2))
        {
            score -= 0.7f;
        }

        return score > 0.5f ? SimZone.COMMERCIAL : SimZone.INDUSTRIAL;
    }

    public Texture2D BuildZoneTexture()
    {
        Texture2D texture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        int index = 0;
        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                zonePixelBuffer[index] = zoneColorGrid[x, y];
                index++;
            }
        }

        texture.SetPixels32(zonePixelBuffer);
        texture.Apply(false, false);
        return texture;
    }

    private void PrepareImport(GridManager gridManager)
    {
        gridManager.generateTestPatternOnStart = false;
        targetWidth = gridManager.width;
        targetHeight = gridManager.height;
        halfGridDiagonal = Mathf.Sqrt((targetWidth * targetWidth) + (targetHeight * targetHeight)) * 0.5f;

        if (idGrid == null || idGrid.GetLength(0) != targetWidth || idGrid.GetLength(1) != targetHeight)
        {
            idGrid = new int[targetWidth, targetHeight];
            zoneColorGrid = new Color32[targetWidth, targetHeight];
            urbanMask = new bool[targetWidth, targetHeight];
            transportMask = new bool[targetWidth, targetHeight];
            industrialMask = new bool[targetWidth, targetHeight];
            smoothingZones = new SimZone[targetWidth, targetHeight];
            zonePixelBuffer = new Color32[targetWidth * targetHeight];
        }

        if (corineIdCounts == null || corineIdCounts.Length != 1000)
        {
            corineIdCounts = new int[1000];
        }

        if (zoneCounterBuffer == null || zoneCounterBuffer.Length != (int)SimZone.NODATA + 1)
        {
            zoneCounterBuffer = new int[(int)SimZone.NODATA + 1];
        }

        colorCache.Clear();
        System.Array.Clear(corineIdCounts, 0, corineIdCounts.Length);
    }

    private Texture2D LoadTextureFromFile(string path)
    {
        byte[] pngBytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (!texture.LoadImage(pngBytes, false))
        {
            Destroy(texture);
            Debug.LogError("CorineImporter failed to decode PNG: " + path, this);
            return null;
        }

        texture.filterMode = FilterMode.Point;
        texture.anisoLevel = 0;
        texture.mipMapBias = 0f;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private void Pass1Classify(Color32[] sourcePixels, int sourceWidth, int sourceHeight)
    {
        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                int classId = ClassifyPixel(SampleNearest(sourcePixels, sourceWidth, sourceHeight, x, y));
                idGrid[x, y] = classId;
                corineIdCounts[classId]++;
                urbanMask[x, y] = classId == 111 || classId == 112 || classId == 133;
                transportMask[x, y] = classId == 122 || classId == 123 || classId == 124;
                industrialMask[x, y] = classId == 131 || classId == 132;
            }
        }
    }

    private IEnumerator Pass1ClassifyAsync(Color32[] sourcePixels, int sourceWidth, int sourceHeight)
    {
        int processed = 0;

        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                int classId = ClassifyPixel(SampleNearest(sourcePixels, sourceWidth, sourceHeight, x, y));
                idGrid[x, y] = classId;
                corineIdCounts[classId]++;
                urbanMask[x, y] = classId == 111 || classId == 112 || classId == 133;
                transportMask[x, y] = classId == 122 || classId == 123 || classId == 124;
                industrialMask[x, y] = classId == 131 || classId == 132;

                processed++;
                if ((processed & 4095) == 0)
                {
                    yield return null;
                }
            }
        }
    }

    private void Pass2DetectCityCenter()
    {
        long sumX = 0L;
        long sumY = 0L;
        int urbanCount = 0;

        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                int classId = idGrid[x, y];
                if (classId == 111 || classId == 112)
                {
                    sumX += x;
                    sumY += y;
                    urbanCount++;
                }
            }
        }

        cityCenter = urbanCount > 0
            ? new Vector2((float)sumX / urbanCount, (float)sumY / urbanCount)
            : new Vector2((targetWidth - 1) * 0.5f, (targetHeight - 1) * 0.5f);
    }

    private IEnumerator Pass2DetectCityCenterAsync()
    {
        long sumX = 0L;
        long sumY = 0L;
        int urbanCount = 0;
        int processed = 0;

        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                int classId = idGrid[x, y];
                if (classId == 111 || classId == 112)
                {
                    sumX += x;
                    sumY += y;
                    urbanCount++;
                }

                processed++;
                if ((processed & 4095) == 0)
                {
                    yield return null;
                }
            }
        }

        cityCenter = urbanCount > 0
            ? new Vector2((float)sumX / urbanCount, (float)sumY / urbanCount)
            : new Vector2((targetWidth - 1) * 0.5f, (targetHeight - 1) * 0.5f);
    }

    private int Pass3PopulateGrid(GridManager gridManager, int[] zoneCounts)
    {
        int nodataCount = corineIdCounts[999];

        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                int classId = idGrid[x, y];
                if (classId == 999)
                {
                    classId = GetNearestValidNeighbor(x, y);
                }

                SimZone zone = MapCorineToZone(x, y, classId);
                Color32 zoneColor = GridManager.GetZoneColor(zone);

                zoneColorGrid[x, y] = zoneColor;
                zoneCounts[(int)zone]++;
                gridManager.SetCell(x, y, BuildCell(zone, x, y));
            }
        }

        return nodataCount;
    }

    private IEnumerator Pass3PopulateGridAsync(GridManager gridManager, int[] zoneCounts, System.Action<int> setNodataCount)
    {
        int nodataCount = corineIdCounts[999];
        int processed = 0;

        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                int classId = idGrid[x, y];
                if (classId == 999)
                {
                    classId = GetNearestValidNeighbor(x, y);
                }

                SimZone zone = MapCorineToZone(x, y, classId);
                Color32 zoneColor = GridManager.GetZoneColor(zone);

                zoneColorGrid[x, y] = zoneColor;
                zoneCounts[(int)zone]++;
                gridManager.SetCell(x, y, BuildCell(zone, x, y));

                processed++;
                if ((processed & 4095) == 0)
                {
                    yield return null;
                }
            }
        }

        setNodataCount(nodataCount);
    }

    private Cell BuildCell(SimZone zone, int x, int y)
    {
        float dx = x - cityCenter.x;
        float dy = y - cityCenter.y;

        Cell cell;
        cell.currentZone = zone;
        cell.nextZone = zone;
        cell.isActive = true;
        cell.urbanPressure = 0f;
        cell.distToWater = 0f;
        cell.distToCenter = Mathf.Sqrt((dx * dx) + (dy * dy));
        return cell;
    }

    private SimZone MapCorineToZone(int x, int y, int classId)
    {
        if (classId == 111)
        {
            return SimZone.URBAN_DENSE;
        }

        if (classId == 112 || classId == 133)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        if (classId == 121)
        {
            return Classify121(x, y);
        }

        if (classId == 131 || classId == 132)
        {
            return SimZone.INDUSTRIAL;
        }

        if (classId == 122 || classId == 123 || classId == 124)
        {
            return SimZone.TRANSPORT_INFRA;
        }

        if (classId == 141 || classId == 142)
        {
            return SimZone.GREEN_PUBLIC;
        }

        if (classId == 211 || classId == 212 || classId == 213 ||
            classId == 221 || classId == 222 || classId == 223 ||
            classId == 231 || classId == 241 || classId == 242 ||
            classId == 243 || classId == 244)
        {
            return SimZone.AGRICULTURE;
        }

        if (classId == 311 || classId == 312 || classId == 313)
        {
            return SimZone.FOREST;
        }

        if (classId == 321 || classId == 322 || classId == 323 || classId == 324)
        {
            return SimZone.SHRUBLAND;
        }

        if (classId == 511 || classId == 512 || classId == 521 || classId == 522 || classId == 523)
        {
            return SimZone.WATER;
        }

        if (classId == 331 || classId == 332 || classId == 333 || classId == 334 || classId == 335 ||
            classId == 411 || classId == 412 || classId == 421 || classId == 422 || classId == 423)
        {
            return SimZone.BARE_OTHER;
        }

        return SimZone.NODATA;
    }

    private float GetUrbanRatio(int x, int y, int radius)
    {
        int minX = x - radius;
        if (minX < 0)
        {
            minX = 0;
        }

        int maxX = x + radius;
        if (maxX >= targetWidth)
        {
            maxX = targetWidth - 1;
        }

        int minY = y - radius;
        if (minY < 0)
        {
            minY = 0;
        }

        int maxY = y + radius;
        if (maxY >= targetHeight)
        {
            maxY = targetHeight - 1;
        }

        int urbanCount = 0;
        int totalCount = 0;

        for (int neighborY = minY; neighborY <= maxY; neighborY++)
        {
            for (int neighborX = minX; neighborX <= maxX; neighborX++)
            {
                if (urbanMask[neighborX, neighborY])
                {
                    urbanCount++;
                }

                totalCount++;
            }
        }

        return totalCount > 0 ? (float)urbanCount / totalCount : 0f;
    }

    private bool HasTransportNearby(int x, int y, int radius)
    {
        int minX = x - radius;
        if (minX < 0)
        {
            minX = 0;
        }

        int maxX = x + radius;
        if (maxX >= targetWidth)
        {
            maxX = targetWidth - 1;
        }

        int minY = y - radius;
        if (minY < 0)
        {
            minY = 0;
        }

        int maxY = y + radius;
        if (maxY >= targetHeight)
        {
            maxY = targetHeight - 1;
        }

        for (int neighborY = minY; neighborY <= maxY; neighborY++)
        {
            for (int neighborX = minX; neighborX <= maxX; neighborX++)
            {
                if (neighborX == x && neighborY == y)
                {
                    continue;
                }

                if (transportMask[neighborX, neighborY])
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void ApplyMajoritySmoothing(int iterations = 1)
    {
        GridManager gridManager = ResolveGridManager();
        if (gridManager == null)
        {
            return;
        }

        Cell[,] grid = gridManager.GetGridReference();
        int zoneCount = (int)SimZone.NODATA + 1;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    SimZone currentZone = grid[x, y].currentZone;

                    if (currentZone == SimZone.WATER || currentZone == SimZone.TRANSPORT_INFRA)
                    {
                        smoothingZones[x, y] = currentZone;
                        continue;
                    }

                    for (int i = 0; i < zoneCount; i++)
                    {
                        zoneCounterBuffer[i] = 0;
                    }

                    int minX = x > 0 ? x - 1 : 0;
                    int maxX = x < targetWidth - 1 ? x + 1 : targetWidth - 1;
                    int minY = y > 0 ? y - 1 : 0;
                    int maxY = y < targetHeight - 1 ? y + 1 : targetHeight - 1;
                    SimZone dominantZone = currentZone;
                    int dominantCount = 0;

                    for (int neighborY = minY; neighborY <= maxY; neighborY++)
                    {
                        for (int neighborX = minX; neighborX <= maxX; neighborX++)
                        {
                            if (neighborX == x && neighborY == y)
                            {
                                continue;
                            }

                            SimZone neighborZone = grid[neighborX, neighborY].currentZone;
                            int count = ++zoneCounterBuffer[(int)neighborZone];

                            if (count > dominantCount)
                            {
                                dominantCount = count;
                                dominantZone = neighborZone;
                            }
                        }
                    }

                    smoothingZones[x, y] = dominantCount >= 5 ? dominantZone : currentZone;
                }
            }

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    SimZone zone = smoothingZones[x, y];
                    Cell cell = grid[x, y];
                    cell.currentZone = zone;
                    cell.nextZone = zone;
                    grid[x, y] = cell;
                    zoneColorGrid[x, y] = GridManager.GetZoneColor(zone);
                }
            }
        }
    }

    private void RefreshZoneCounts(int[] zoneCounts)
    {
        Cell[,] grid = ResolveGridManager().GetGridReference();

        for (int i = 0; i < zoneCounts.Length; i++)
        {
            zoneCounts[i] = 0;
        }

        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                zoneCounts[(int)grid[x, y].currentZone]++;
            }
        }
    }

    private int GetNearestValidNeighbor(int x, int y)
    {
        int radius = 2;

        for (int r = 1; r <= radius; r++)
        {
            for (int oy = -r; oy <= r; oy++)
            {
                for (int ox = -r; ox <= r; ox++)
                {
                    int nx = x + ox;
                    int ny = y + oy;

                    if (nx < 0 || ny < 0 || nx >= targetWidth || ny >= targetHeight)
                    {
                        continue;
                    }

                    int id = idGrid[nx, ny];
                    if (id != 999)
                    {
                        return id;
                    }
                }
            }
        }

        return 211;
    }

    private bool HasIndustrialNearby(int x, int y, int radius)
    {
        int minX = x - radius;
        if (minX < 0)
        {
            minX = 0;
        }

        int maxX = x + radius;
        if (maxX >= targetWidth)
        {
            maxX = targetWidth - 1;
        }

        int minY = y - radius;
        if (minY < 0)
        {
            minY = 0;
        }

        int maxY = y + radius;
        if (maxY >= targetHeight)
        {
            maxY = targetHeight - 1;
        }

        for (int neighborY = minY; neighborY <= maxY; neighborY++)
        {
            for (int neighborX = minX; neighborX <= maxX; neighborX++)
            {
                if (neighborX == x && neighborY == y)
                {
                    continue;
                }

                if (industrialMask[neighborX, neighborY])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ValidateClassification()
    {
        int totalCells = targetWidth * targetHeight;
        int[] topIds = new int[10];
        int[] topCounts = new int[10];

        for (int id = 0; id < corineIdCounts.Length; id++)
        {
            int count = corineIdCounts[id];
            if (count <= 0)
            {
                continue;
            }

            for (int i = 0; i < topCounts.Length; i++)
            {
                if (count <= topCounts[i])
                {
                    continue;
                }

                for (int shift = topCounts.Length - 1; shift > i; shift--)
                {
                    topCounts[shift] = topCounts[shift - 1];
                    topIds[shift] = topIds[shift - 1];
                }

                topCounts[i] = count;
                topIds[i] = id;
                break;
            }
        }

        StringBuilder builder = new StringBuilder(512);
        builder.Append("CORINE validation | 999=");
        builder.Append(corineIdCounts[999]);
        builder.Append(" | top10=");

        for (int i = 0; i < topCounts.Length; i++)
        {
            if (topCounts[i] <= 0)
            {
                break;
            }

            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(topIds[i]);
            builder.Append(':');
            builder.Append(topCounts[i]);
        }

        Debug.Log(builder.ToString(), this);

        if (totalCells > 0 && corineIdCounts[999] > totalCells * 0.05f)
        {
            Debug.LogWarning("CORINE validation detected more than 5% NODATA pixels after classification.", this);
        }
    }

    private void SaveZoneTexture(Texture2D texture)
    {
        string outputPath = Path.Combine(Application.dataPath, "debug_zone_map.png");
        File.WriteAllBytes(outputPath, texture.EncodeToPNG());
    }

    private void LogImportSummary(int sourceWidth, int sourceHeight, int[] zoneCounts, int nodataCount)
    {
        int totalCells = targetWidth * targetHeight;
        StringBuilder builder = new StringBuilder(512);
        builder.Append("CORINE import complete | source=");
        builder.Append(sourceWidth);
        builder.Append('x');
        builder.Append(sourceHeight);
        builder.Append(" target=");
        builder.Append(targetWidth);
        builder.Append('x');
        builder.Append(targetHeight);
        builder.Append(" totalCells=");
        builder.Append(totalCells);
        builder.Append(" nodata=");
        builder.Append(nodataCount);
        builder.Append(" cityCenter=(");
        builder.Append(cityCenter.x.ToString("F2"));
        builder.Append(", ");
        builder.Append(cityCenter.y.ToString("F2"));
        builder.Append(')');

        for (int i = 0; i < zoneCounts.Length; i++)
        {
            float percentage = totalCells > 0 ? (zoneCounts[i] * 100f) / totalCells : 0f;
            builder.Append(" | ");
            builder.Append((SimZone)i);
            builder.Append('=');
            builder.Append(percentage.ToString("F2"));
            builder.Append('%');
        }

        Debug.Log(builder.ToString(), this);
    }

    private GridManager ResolveGridManager()
    {
        if (GridManager.Instance != null)
        {
            return GridManager.Instance;
        }

        return FindFirstObjectByType<GridManager>();
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
    }

    private static Dictionary<int, Color32> BuildCorinePaletteLookup()
    {
        Dictionary<int, Color32> lookup = new Dictionary<int, Color32>(corinePalette.Length);

        for (int i = 0; i < corinePalette.Length; i++)
        {
            lookup[corinePalette[i].id] = corinePalette[i].rgb;
        }

        return lookup;
    }

    private static Texture2D LoadTextureFromFileStatic(string path)
    {
        byte[] pngBytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (!texture.LoadImage(pngBytes, false))
        {
            Destroy(texture);
            Debug.LogError("CorineImporter failed to decode PNG: " + path);
            return null;
        }

        texture.filterMode = FilterMode.Point;
        texture.anisoLevel = 0;
        texture.mipMapBias = 0f;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private static Color32 SampleNearestStatic(Color32[] pixels, int srcW, int srcH, int targetW, int targetH, int x, int y)
    {
        int srcX = Mathf.FloorToInt((x + 0.5f) * srcW / targetW);
        int srcY = Mathf.FloorToInt((y + 0.5f) * srcH / targetH);

        if (srcX < 0)
        {
            srcX = 0;
        }
        else if (srcX >= srcW)
        {
            srcX = srcW - 1;
        }

        if (srcY < 0)
        {
            srcY = 0;
        }
        else if (srcY >= srcH)
        {
            srcY = srcH - 1;
        }

        return pixels[(srcY * srcW) + srcX];
    }

    private static int ClassifyPixelStatic(Color32 px, Dictionary<Color32, int> localColorCache)
    {
        if (localColorCache.TryGetValue(px, out int cachedId))
        {
            return cachedId;
        }

        int bestId = 999;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < corinePalette.Length; i++)
        {
            Color32 paletteColor = corinePalette[i].rgb;
            int deltaR = px.r - paletteColor.r;
            int deltaG = px.g - paletteColor.g;
            int deltaB = px.b - paletteColor.b;
            int distance = (deltaR * deltaR) + (deltaG * deltaG) + (deltaB * deltaB);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestId = corinePalette[i].id;

                if (distance < 50)
                {
                    break;
                }
            }
        }

        if (bestId == 999)
        {
            bestId = corinePalette[0].id;
            int bestDist = int.MaxValue;

            for (int i = 0; i < corinePalette.Length; i++)
            {
                Color32 c = corinePalette[i].rgb;
                int dr = px.r - c.r;
                int dg = px.g - c.g;
                int db = px.b - c.b;
                int dist = dr * dr + dg * dg + db * db;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestId = corinePalette[i].id;
                }
            }
        }

        localColorCache[px] = bestId;
        return bestId;
    }

    private static Vector2 DetectCityCenterStatic(int[,] loadedIdGrid, int width, int height)
    {
        long sumX = 0L;
        long sumY = 0L;
        int urbanCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int classId = loadedIdGrid[x, y];
                if (classId == 111 || classId == 112)
                {
                    sumX += x;
                    sumY += y;
                    urbanCount++;
                }
            }
        }

        return urbanCount > 0
            ? new Vector2((float)sumX / urbanCount, (float)sumY / urbanCount)
            : new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
    }

    private static int GetNearestValidNeighborStatic(int[,] loadedIdGrid, int x, int y, int width, int height)
    {
        const int radius = 2;

        for (int r = 1; r <= radius; r++)
        {
            for (int oy = -r; oy <= r; oy++)
            {
                for (int ox = -r; ox <= r; ox++)
                {
                    int nx = x + ox;
                    int ny = y + oy;

                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    {
                        continue;
                    }

                    int id = loadedIdGrid[nx, ny];
                    if (id != 999)
                    {
                        return id;
                    }
                }
            }
        }

        return 211;
    }

    private static SimZone MapCorineToZoneStatic(
        int x,
        int y,
        int classId,
        bool[,] loadedUrbanMask,
        bool[,] loadedTransportMask,
        bool[,] loadedIndustrialMask,
        Vector2 detectedCityCenter,
        int width,
        int height,
        float localHalfGridDiagonal)
    {
        if (classId == 111)
        {
            return SimZone.URBAN_DENSE;
        }

        if (classId == 112 || classId == 133)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        if (classId == 121)
        {
            return Classify121Static(x, y, loadedUrbanMask, loadedTransportMask, loadedIndustrialMask, detectedCityCenter, width, height, localHalfGridDiagonal);
        }
        if (classId == 131 || classId == 132)
        {
            return SimZone.INDUSTRIAL;
        }
        if (classId == 122 || classId == 123 || classId == 124)
        {
            return SimZone.TRANSPORT_INFRA;
        }
        if (classId == 141 || classId == 142)
        {
            return SimZone.GREEN_PUBLIC;
        }
        if (classId == 211 || classId == 212 || classId == 213 ||
            classId == 221 || classId == 222 || classId == 223 ||
            classId == 231 || classId == 241 || classId == 242 ||
            classId == 243 || classId == 244)
        {
            return SimZone.AGRICULTURE;
        }

        if (classId == 311 || classId == 312 || classId == 313)
        {
            return SimZone.FOREST;
        }

        if (classId == 321 || classId == 322 || classId == 323 || classId == 324)
        {
            return SimZone.SHRUBLAND;
        }

        if (classId == 511 || classId == 512 || classId == 521 || classId == 522 || classId == 523)
        {
            return SimZone.WATER;
        }

        if (classId == 331 || classId == 332 || classId == 333 || classId == 334 || classId == 335 ||
            classId == 411 || classId == 412 || classId == 421 || classId == 422 || classId == 423)
        {
            return SimZone.BARE_OTHER;
        }

        return SimZone.NODATA;
    }
    private static SimZone Classify121Static(
        int x,
        int y,
        bool[,] loadedUrbanMask,
        bool[,] loadedTransportMask,
        bool[,] loadedIndustrialMask,
        Vector2 detectedCityCenter,
        int width,
        int height,
        float localHalfGridDiagonal)
    {
        float score = 0f;
        float dx = x - detectedCityCenter.x;
        float dy = y - detectedCityCenter.y;
        float distRatio = Mathf.Sqrt((dx * dx) + (dy * dy)) / localHalfGridDiagonal;
        if (distRatio < 0.35f)
        {
            score += 1f;
        }
        if (GetUrbanRatioStatic(x, y, 2, loadedUrbanMask, width, height) > 0.4f)
        {
            score += 0.8f;
        }
        if (HasMaskNearbyStatic(x, y, 4, loadedTransportMask, width, height))
        {
            score -= 0.6f;
        }
        if (HasMaskNearbyStatic(x, y, 2, loadedIndustrialMask, width, height))
        {
            score -= 0.7f;
        }
        return score > 0.5f ? SimZone.COMMERCIAL : SimZone.INDUSTRIAL;
    }
    private static float GetUrbanRatioStatic(int x, int y, int radius, bool[,] loadedUrbanMask, int width, int height)
    {
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
        int urbanCount = 0;
        int totalCount = 0;

        for (int neighborY = minY; neighborY <= maxY; neighborY++)
        {
            for (int neighborX = minX; neighborX <= maxX; neighborX++)
            {
                if (loadedUrbanMask[neighborX, neighborY])
                {
                    urbanCount++;
                }

                totalCount++;
            }
        }

        return totalCount > 0 ? (float)urbanCount / totalCount : 0f;
    }

    private static bool HasMaskNearbyStatic(int x, int y, int radius, bool[,] mask, int width, int height)
    {
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

        for (int neighborY = minY; neighborY <= maxY; neighborY++)
        {
            for (int neighborX = minX; neighborX <= maxX; neighborX++)
            {
                if (neighborX == x && neighborY == y)
                {
                    continue;
                }

                if (mask[neighborX, neighborY])
                {
                    return true;
                }
            }
        }

        return false; 
    }
}
