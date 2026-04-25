using UnityEngine;

[CreateAssetMenu(menuName = "CA Models/V5 Model")]
public class CA_Model_v5 : CA_Model
{
    [SerializeField] private float agricultureResistance = 0.85f;
    [SerializeField] private float shrublandResistance = 0.75f;
    [SerializeField] private float forestResistanceMultiplier = 0.20f;
    [SerializeField] private float residentialUrbanDensityWeight = 0.05f;
    [SerializeField] private float residentialRoadWeightR1 = 0.04f;
    [SerializeField] private float residentialRoadWeightR2 = 0.025f;
    [SerializeField] private float residentialIndustrialWeight = 0.015f;
    [SerializeField] private float residentialCenterWeight = 0.015f;
    [SerializeField] private float residentialWaterWeight = 0.005f;
    [SerializeField] private float denseUrbanDensityWeight = 0.015f;
    [SerializeField] private float denseNearbyDenseWeight = 0.03f;
    [SerializeField] private float denseCenterWeight = 0.01f;
    [SerializeField] private float residentialMaxProbability = 0.20f;
    [SerializeField] private float denseMaxProbability = 0.08f;
    [SerializeField] private float industrialMaxProbability = 0.08f;

    public override SimZone EvaluateCell(int x, int y, Cell[,] grid, int width, int height)
    {
        SimZone currentZone = grid[x, y].currentZone;

        switch (currentZone)
        {
            case SimZone.AGRICULTURE:
                return EvaluateAgriculture(x, y, grid, width, height);
            case SimZone.SHRUBLAND:
                return EvaluateShrubland(x, y, grid, width, height);
            case SimZone.URBAN_RESIDENTIAL:
                return EvaluateResidential(x, y, grid, width, height);
            case SimZone.FOREST:
                return EvaluateForest(x, y, grid, width, height);
            case SimZone.GREEN_PUBLIC:
                return EvaluateGreenPublic(x, y, grid, width, height);
            case SimZone.WATER:
            case SimZone.TRANSPORT_INFRA:
            case SimZone.BARE_OTHER:
            case SimZone.COMMERCIAL:
            case SimZone.URBAN_DENSE:
            case SimZone.INDUSTRIAL:
            case SimZone.NODATA:
            default:
                return currentZone;
        }
    }

    private SimZone EvaluateAgriculture(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        bool nearRoadR1 = NearRoad(x, y, 1, grid, width, height);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);
        bool nearIndustrialR2 = NearIndustrial(x, y, 2, grid, width, height);

        if (!HasUrbanFrontier(urbanR1, urbanR2, urbanDensityR2, nearRoadR1, nearRoadR2, nearIndustrialR2))
        {
            return TryIndustryEdge(x, y, grid, width, height, urbanR2, nearRoadR1, nearRoadR2, nearIndustrialR2);
        }

        if (urbanR1 + 1 < 2)
        {
            return TryIndustryEdge(x, y, grid, width, height, urbanR2, nearRoadR1, nearRoadR2, nearIndustrialR2);
        }

        float p = 0f;
        p += urbanDensityR2 * residentialUrbanDensityWeight;

        if (urbanR1 >= 1)
        {
            p += 0.03f;
        }

        if (urbanR1 >= 2)
        {
            p += 0.05f;
        }

        if (urbanR1 >= 3)
        {
            p += 0.04f;
        }

        if (nearRoadR1)
        {
            p += residentialRoadWeightR1;
        }
        else if (nearRoadR2)
        {
            p += residentialRoadWeightR2;
        }

        if (nearIndustrialR2)
        {
            p += residentialIndustrialWeight;
        }

        if (HasUrbanCorridorSupport(x, y, grid, width, height))
        {
            p += 0.03f;
        }

        if (HasGapFillSupport(x, y, grid, width, height))
        {
            p += 0.025f;
        }

        Cell cell = grid[x, y];
        p += (1f - NormalizeCenterDistance(cell.distToCenter, width, height)) * residentialCenterWeight;
        p += (1f - NormalizeWaterDistance(cell.distToWater, width, height)) * residentialWaterWeight;
        p = Mathf.Min(Mathf.Clamp01(p * agricultureResistance), residentialMaxProbability);

        if (Random.value < p)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        return TryIndustryEdge(x, y, grid, width, height, urbanR2, nearRoadR1, nearRoadR2, nearIndustrialR2);
    }

    private SimZone EvaluateShrubland(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        bool nearRoadR1 = NearRoad(x, y, 1, grid, width, height);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);
        bool nearIndustrialR2 = NearIndustrial(x, y, 2, grid, width, height);

        if (!HasUrbanFrontier(urbanR1, urbanR2, urbanDensityR2, nearRoadR1, nearRoadR2, nearIndustrialR2))
        {
            return SimZone.SHRUBLAND;
        }

        if (urbanR1 + 1 < 2)
        {
            return SimZone.SHRUBLAND;
        }

        float p = 0f;
        p += urbanDensityR2 * 0.04f;

        if (urbanR1 >= 2)
        {
            p += 0.04f;
        }

        if (nearRoadR2)
        {
            p += 0.02f;
        }

        if (HasUrbanCorridorSupport(x, y, grid, width, height))
        {
            p += 0.02f;
        }

        if (HasGapFillSupport(x, y, grid, width, height))
        {
            p += 0.02f;
        }

        p += (1f - NormalizeCenterDistance(grid[x, y].distToCenter, width, height)) * 0.01f;
        p = Mathf.Min(Mathf.Clamp01(p * shrublandResistance), 0.16f);
        return Random.value < p ? SimZone.URBAN_RESIDENTIAL : SimZone.SHRUBLAND;
    }

    private SimZone EvaluateResidential(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        int denseR1 = CountDense(x, y, 1, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);

        if (denseR1 == 0 || urbanR1 < 5 || urbanDensityR2 < 0.65f)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        bool hasDenseNearby = HasDenseNearby(x, y, grid, width, height);
        int greenR1 = CAUtils.CountType(x, y, 1, SimZone.GREEN_PUBLIC, grid, width, height);
        float distToCenter = NormalizeCenterDistance(grid[x, y].distToCenter, width, height);
        float urbanDensityR4 = CAUtils.GetDensity(CountUrban(x, y, 4, grid, width, height), 4);

        if (!hasDenseNearby || greenR1 > 1 || (distToCenter >= 0.55f && urbanDensityR4 < 0.50f))
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        float p = 0f;
        p += urbanDensityR2 * denseUrbanDensityWeight;

        if (urbanR1 >= 6)
        {
            p += 0.015f;
        }

        if (hasDenseNearby)
        {
            p += denseNearbyDenseWeight;
        }

        p += (1f - distToCenter) * denseCenterWeight;
        p = Mathf.Min(Mathf.Clamp01(p), denseMaxProbability);
        return Random.value < p ? SimZone.URBAN_DENSE : SimZone.URBAN_RESIDENTIAL;
    }

    private SimZone EvaluateForest(int x, int y, Cell[,] grid, int width, int height)
    {
        int forestR2 = CAUtils.CountType(x, y, 2, SimZone.FOREST, grid, width, height);
        if (CAUtils.GetDensity(forestR2, 2) >= 0.65f)
        {
            return SimZone.FOREST;
        }

        int urbanR4 = CountUrban(x, y, 4, grid, width, height);
        float urbanDensityR4 = CAUtils.GetDensity(urbanR4, 4);
        bool urbanSupport = CountUrban(x, y, 2, grid, width, height) >= 1 || CountUrban(x, y, 3, grid, width, height) >= 1;

        if (urbanDensityR4 < 0.40f || !urbanSupport)
        {
            return SimZone.FOREST;
        }

        float p = urbanDensityR4 * 0.008f;

        if (NearRoad(x, y, 3, grid, width, height))
        {
            p += 0.004f;
        }

        p = Mathf.Clamp01(p * forestResistanceMultiplier);
        return Random.value < p ? SimZone.SHRUBLAND : SimZone.FOREST;
    }

    private static SimZone EvaluateGreenPublic(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        float urbanDensityR1 = CAUtils.GetDensity(urbanR1, 1);

        if (urbanDensityR1 < 0.75f || urbanR1 < 6 || !HasDenseNearby(x, y, grid, width, height))
        {
            return SimZone.GREEN_PUBLIC;
        }

        const float p = 0.015f;
        return Random.value < p ? SimZone.URBAN_RESIDENTIAL : SimZone.GREEN_PUBLIC;
    }

    private SimZone TryIndustryEdge(int x, int y, Cell[,] grid, int width, int height, int urbanR2, bool nearRoadR1, bool nearRoadR2, bool nearIndustrialR2)
    {
        float distToCenter = NormalizeCenterDistance(grid[x, y].distToCenter, width, height);

        if (!(nearRoadR1 || nearRoadR2) || !(nearIndustrialR2 || urbanR2 >= 1) || distToCenter <= 0.25f)
        {
            return SimZone.AGRICULTURE;
        }

        float p = 0f;

        if (nearRoadR2)
        {
            p += 0.025f;
        }

        if (nearIndustrialR2)
        {
            p += 0.03f;
        }

        if (distToCenter > 0.4f)
        {
            p += 0.01f;
        }

        p = Mathf.Min(Mathf.Clamp01(p), industrialMaxProbability);
        return Random.value < p ? SimZone.INDUSTRIAL : SimZone.AGRICULTURE;
    }

    private static int CountUrban(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        int dense = CAUtils.CountType(x, y, radius, SimZone.URBAN_DENSE, grid, width, height);
        int residential = CAUtils.CountType(x, y, radius, SimZone.URBAN_RESIDENTIAL, grid, width, height);
        int commercial = CAUtils.CountType(x, y, radius, SimZone.COMMERCIAL, grid, width, height);
        return dense + residential + commercial;
    }

    private static int CountDense(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        return CAUtils.CountType(x, y, radius, SimZone.URBAN_DENSE, grid, width, height);
    }

    private static bool HasDenseNearby(int x, int y, Cell[,] grid, int width, int height)
    {
        return CountDense(x, y, 1, grid, width, height) > 0 || CountDense(x, y, 2, grid, width, height) > 0;
    }

    private static bool NearRoad(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        return CAUtils.HasType(x, y, radius, SimZone.TRANSPORT_INFRA, grid, width, height);
    }

    private static bool NearIndustrial(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        return CAUtils.HasType(x, y, radius, SimZone.INDUSTRIAL, grid, width, height);
    }

    private static bool HasUrbanFrontier(int urbanR1, int urbanR2, float urbanDensityR2, bool nearRoadR1, bool nearRoadR2, bool nearIndustrialR2)
    {
        if (urbanR1 == 0)
        {
            return false;
        }

        if (urbanR1 >= 2)
        {
            return true;
        }

        if (urbanDensityR2 >= 0.20f)
        {
            return true;
        }

        if ((nearRoadR1 || nearRoadR2) && urbanR2 >= 1)
        {
            return true;
        }

        return nearIndustrialR2 && urbanR2 >= 1;
    }

    private static bool HasUrbanCorridorSupport(int x, int y, Cell[,] grid, int width, int height)
    {
        bool nearRoadR1 = NearRoad(x, y, 1, grid, width, height);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);

        if ((nearRoadR1 || nearRoadR2) && (urbanR1 >= 1 || urbanR2 >= 2))
        {
            return true;
        }

        return HasGapFillSupport(x, y, grid, width, height);
    }

    private static bool HasGapFillSupport(int x, int y, Cell[,] grid, int width, int height)
    {
        bool north = HasUrbanInDirection(x, y, 0, 1, 2, grid, width, height);
        bool south = HasUrbanInDirection(x, y, 0, -1, 2, grid, width, height);
        bool east = HasUrbanInDirection(x, y, 1, 0, 2, grid, width, height);
        bool west = HasUrbanInDirection(x, y, -1, 0, 2, grid, width, height);

        if ((north && south) || (east && west))
        {
            return true;
        }

        bool northEast = HasUrbanInDirection(x, y, 1, 1, 3, grid, width, height);
        bool southWest = HasUrbanInDirection(x, y, -1, -1, 3, grid, width, height);
        bool northWest = HasUrbanInDirection(x, y, -1, 1, 3, grid, width, height);
        bool southEast = HasUrbanInDirection(x, y, 1, -1, 3, grid, width, height);

        return (northEast && southWest) || (northWest && southEast);
    }

    private static bool HasUrbanInDirection(int x, int y, int stepX, int stepY, int maxDistance, Cell[,] grid, int width, int height)
    {
        for (int distance = 1; distance <= maxDistance; distance++)
        {
            int sampleX = x + (stepX * distance);
            int sampleY = y + (stepY * distance);

            if ((uint)sampleX >= (uint)width || (uint)sampleY >= (uint)height)
            {
                break;
            }

            SimZone zone = grid[sampleX, sampleY].currentZone;
            if (zone == SimZone.URBAN_DENSE || zone == SimZone.URBAN_RESIDENTIAL || zone == SimZone.COMMERCIAL)
            {
                return true;
            }
        }

        return false;
    }

    private static float NormalizeCenterDistance(float value, int width, int height)
    {
        if (value <= 1f)
        {
            return Mathf.Clamp01(value);
        }

        float halfDiagonal = Mathf.Sqrt((width * width) + (height * height)) * 0.5f;
        if (halfDiagonal <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(value / halfDiagonal);
    }

    private static float NormalizeWaterDistance(float value, int width, int height)
    {
        if (value <= 1f)
        {
            return Mathf.Clamp01(value);
        }

        float diagonal = Mathf.Sqrt((width * width) + (height * height));
        if (diagonal <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(value / diagonal);
    }
}
