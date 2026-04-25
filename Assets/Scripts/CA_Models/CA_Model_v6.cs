using UnityEngine;

[CreateAssetMenu(menuName = "CA Models/V6 Model")]
public class CA_Model_v6 : CA_Model
{
    [SerializeField] private float agricultureResistance = 0.85f;
    [SerializeField] private float shrublandResistance = 0.75f;
    [SerializeField] private float forestResistanceMultiplier = 0.20f;
    [SerializeField] private float residentialBaseWeight = 0.04f;
    [SerializeField] private float residentialRoadWeightR1 = 0.03f;
    [SerializeField] private float residentialRoadWeightR2 = 0.02f;
    [SerializeField] private float residentialTwoSidedSupportWeight = 0.035f;
    [SerializeField] private float denseBaseWeight = 0.012f;
    [SerializeField] private float denseNearbyDenseWeight = 0.025f;
    [SerializeField] private float denseCommercialWeight = 0.015f;
    [SerializeField] private float commercialRoadWeight = 0.04f;
    [SerializeField] private float commercialNodeWeight = 0.025f;
    [SerializeField] private float industrialRoadWeight = 0.03f;
    [SerializeField] private float industrialClusterWeight = 0.03f;
    [SerializeField] private float residentialMaxProbability = 0.18f;
    [SerializeField] private float denseMaxProbability = 0.05f;
    [SerializeField] private float commercialMaxProbability = 0.07f;
    [SerializeField] private float industrialMaxProbability = 0.06f;

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
            case SimZone.COMMERCIAL:
                return EvaluateCommercial(x, y, grid, width, height);
            case SimZone.FOREST:
                return EvaluateForest(x, y, grid, width, height);
            case SimZone.GREEN_PUBLIC:
                return EvaluateGreenPublic(x, y, grid, width, height);
            case SimZone.WATER:
            case SimZone.TRANSPORT_INFRA:
            case SimZone.BARE_OTHER:
            case SimZone.URBAN_DENSE:
            case SimZone.INDUSTRIAL:
            case SimZone.NODATA:
            default:
                return currentZone;
        }
    }

    // Main residential fringe growth.
    private SimZone EvaluateAgriculture(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        bool nearRoadR1 = NearRoad(x, y, 1, grid, width, height);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);
        bool nearIndustrialR2 = NearIndustrial(x, y, 2, grid, width, height);
        bool twoSidedSupport = HasTwoSidedUrbanSupport(x, y, grid, width, height);

        if (!HasSupportedFront(urbanR1, urbanDensityR2, twoSidedSupport, nearRoadR1))
        {
            return TryCommercialOrIndustrial(x, y, grid, width, height, urbanR1, urbanR2, nearRoadR1, nearRoadR2, nearIndustrialR2);
        }

        float p = 0f;
        p += urbanDensityR2 * residentialBaseWeight;

        if (urbanR1 >= 2)
        {
            p += 0.04f;
        }

        if (urbanR1 >= 3)
        {
            p += 0.03f;
        }

        if (twoSidedSupport)
        {
            p += residentialTwoSidedSupportWeight;
        }

        if (nearRoadR1)
        {
            p += residentialRoadWeightR1;
        }
        else if (nearRoadR2)
        {
            p += residentialRoadWeightR2;
        }

        if (HasGapFillSupport(x, y, grid, width, height))
        {
            p += 0.025f;
        }

        p += (1f - NormalizeCenterDistance(grid[x, y].distToCenter, width, height)) * 0.01f;
        p = Mathf.Min(Mathf.Clamp01(p * agricultureResistance), residentialMaxProbability);

        if (Random.value < p)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        return TryCommercialOrIndustrial(x, y, grid, width, height, urbanR1, urbanR2, nearRoadR1, nearRoadR2, nearIndustrialR2);
    }

    private SimZone EvaluateShrubland(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);
        bool twoSidedSupport = HasTwoSidedUrbanSupport(x, y, grid, width, height);

        if (!HasSupportedFront(urbanR1, urbanDensityR2, twoSidedSupport, false))
        {
            return SimZone.SHRUBLAND;
        }

        float p = 0f;
        p += urbanDensityR2 * 0.035f;

        if (urbanR1 >= 2)
        {
            p += 0.03f;
        }

        if (twoSidedSupport)
        {
            p += 0.03f;
        }

        if (nearRoadR2)
        {
            p += 0.015f;
        }

        if (HasGapFillSupport(x, y, grid, width, height))
        {
            p += 0.02f;
        }

        p = Mathf.Min(Mathf.Clamp01(p * shrublandResistance), 0.15f);
        return Random.value < p ? SimZone.URBAN_RESIDENTIAL : SimZone.SHRUBLAND;
    }

    // Compact core intensification only.
    private SimZone EvaluateResidential(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrbanCore(x, y, 1, grid, width, height);
        int urbanR2 = CountUrbanCore(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        int denseCountR2 = CountDense(x, y, 2, grid, width, height);
        int commercialR2 = CountCommercial(x, y, 2, grid, width, height);
        int greenR1 = CAUtils.CountType(x, y, 1, SimZone.GREEN_PUBLIC, grid, width, height);
        float distToCenter = NormalizeCenterDistance(grid[x, y].distToCenter, width, height);

        if (denseCountR2 == 0 || urbanR1 < 5 || urbanDensityR2 < 0.65f)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        if (greenR1 > 1 || (commercialR2 < 1 && distToCenter >= 0.45f))
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        float p = 0f;
        p += urbanDensityR2 * denseBaseWeight;

        if (urbanR1 >= 6)
        {
            p += 0.015f;
        }

        if (denseCountR2 >= 1)
        {
            p += denseNearbyDenseWeight;
        }

        if (commercialR2 >= 1)
        {
            p += denseCommercialWeight;
        }

        p += (1f - distToCenter) * 0.01f;
        p = Mathf.Min(Mathf.Clamp01(p), denseMaxProbability);
        return Random.value < p ? SimZone.URBAN_DENSE : SimZone.URBAN_RESIDENTIAL;
    }

    private SimZone EvaluateCommercial(int x, int y, Cell[,] grid, int width, int height)
    {
        float urbanDensityR2 = CAUtils.GetDensity(CountUrbanCore(x, y, 2, grid, width, height), 2);
        int denseCountR2 = CountDense(x, y, 2, grid, width, height);
        float distToCenter = NormalizeCenterDistance(grid[x, y].distToCenter, width, height);

        if (urbanDensityR2 < 0.70f || denseCountR2 < 1 || distToCenter >= 0.45f)
        {
            return SimZone.COMMERCIAL;
        }

        float p = Mathf.Min(0.03f, 0.01f + ((1f - distToCenter) * 0.01f) + (urbanDensityR2 * 0.01f));
        return Random.value < p ? SimZone.URBAN_DENSE : SimZone.COMMERCIAL;
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

        p = Mathf.Min(Mathf.Clamp01(p * forestResistanceMultiplier), 0.01f);
        return Random.value < p ? SimZone.SHRUBLAND : SimZone.FOREST;
    }

    private static SimZone EvaluateGreenPublic(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrbanCore(x, y, 1, grid, width, height);
        float urbanDensityR1 = CAUtils.GetDensity(urbanR1, 1);

        if (urbanDensityR1 < 0.75f || urbanR1 < 6 || CountDense(x, y, 2, grid, width, height) == 0)
        {
            return SimZone.GREEN_PUBLIC;
        }

        const float p = 0.01f;
        return Random.value < p ? SimZone.URBAN_RESIDENTIAL : SimZone.GREEN_PUBLIC;
    }

    private SimZone TryCommercialOrIndustrial(int x, int y, Cell[,] grid, int width, int height, int urbanR1, int urbanR2, bool nearRoadR1, bool nearRoadR2, bool nearIndustrialR2)
    {
        bool twoSidedSupport = HasTwoSidedUrbanSupport(x, y, grid, width, height);
        int commercialR2 = CountCommercial(x, y, 2, grid, width, height);
        int industrialR2 = CountIndustrial(x, y, 2, grid, width, height);
        float distToCenter = NormalizeCenterDistance(grid[x, y].distToCenter, width, height);

        if (HasSupportedFront(urbanR1, CAUtils.GetDensity(urbanR2, 2), twoSidedSupport, nearRoadR1) && (nearRoadR1 || nearRoadR2) && (commercialR2 >= 1 || urbanR1 >= 2))
        {
            float pCommercial = 0f;

            if (nearRoadR1)
            {
                pCommercial += commercialRoadWeight;
            }
            else if (nearRoadR2)
            {
                pCommercial += 0.025f;
            }

            if (commercialR2 >= 1)
            {
                pCommercial += commercialNodeWeight;
            }

            if (urbanR1 >= 2)
            {
                pCommercial += 0.015f;
            }

            if (HasGapFillSupport(x, y, grid, width, height))
            {
                pCommercial += 0.01f;
            }

            pCommercial += (1f - distToCenter) * 0.01f;
            pCommercial = Mathf.Min(Mathf.Clamp01(pCommercial), commercialMaxProbability);

            if (Random.value < pCommercial)
            {
                return SimZone.COMMERCIAL;
            }
        }

        if ((nearRoadR1 || nearRoadR2) && (industrialR2 >= 1 || urbanR2 >= 1) && distToCenter > 0.20f && twoSidedSupport)
        {
            float pIndustrial = 0f;

            if (nearRoadR1)
            {
                pIndustrial += industrialRoadWeight;
            }
            else if (nearRoadR2)
            {
                pIndustrial += 0.02f;
            }

            if (industrialR2 >= 1)
            {
                pIndustrial += industrialClusterWeight;
            }

            if (distToCenter > 0.35f)
            {
                pIndustrial += 0.01f;
            }

            pIndustrial = Mathf.Min(Mathf.Clamp01(pIndustrial), industrialMaxProbability);

            if (Random.value < pIndustrial)
            {
                return SimZone.INDUSTRIAL;
            }
        }

        return SimZone.AGRICULTURE;
    }

    private static int CountUrban(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        int dense = CountDense(x, y, radius, grid, width, height);
        int residential = CAUtils.CountType(x, y, radius, SimZone.URBAN_RESIDENTIAL, grid, width, height);
        int commercial = CountCommercial(x, y, radius, grid, width, height);
        int industrial = CountIndustrial(x, y, radius, grid, width, height);
        return dense + residential + commercial + industrial;
    }

    private static int CountUrbanCore(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        int dense = CountDense(x, y, radius, grid, width, height);
        int residential = CAUtils.CountType(x, y, radius, SimZone.URBAN_RESIDENTIAL, grid, width, height);
        int commercial = CountCommercial(x, y, radius, grid, width, height);
        return dense + residential + commercial;
    }

    private static int CountDense(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        return CAUtils.CountType(x, y, radius, SimZone.URBAN_DENSE, grid, width, height);
    }

    private static int CountCommercial(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        return CAUtils.CountType(x, y, radius, SimZone.COMMERCIAL, grid, width, height);
    }

    private static int CountIndustrial(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        return CAUtils.CountType(x, y, radius, SimZone.INDUSTRIAL, grid, width, height);
    }

    private static bool NearRoad(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        return CAUtils.HasType(x, y, radius, SimZone.TRANSPORT_INFRA, grid, width, height);
    }

    private static bool NearIndustrial(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        return CAUtils.HasType(x, y, radius, SimZone.INDUSTRIAL, grid, width, height);
    }

    private static bool HasSupportedFront(int urbanR1, float urbanDensityR2, bool twoSidedSupport, bool nearRoadR1)
    {
        if (urbanR1 >= 2)
        {
            return true;
        }

        if (urbanR1 >= 1 && urbanDensityR2 >= 0.25f)
        {
            return true;
        }

        if (twoSidedSupport)
        {
            return true;
        }

        return urbanR1 >= 1 && nearRoadR1;
    }

    private static bool HasTwoSidedUrbanSupport(int x, int y, Cell[,] grid, int width, int height)
    {
        bool left = HasUrbanInDirection(x, y, -1, 0, 2, grid, width, height);
        bool right = HasUrbanInDirection(x, y, 1, 0, 2, grid, width, height);
        bool up = HasUrbanInDirection(x, y, 0, 1, 2, grid, width, height);
        bool down = HasUrbanInDirection(x, y, 0, -1, 2, grid, width, height);

        if ((left && right) || (up && down))
        {
            return true;
        }

        bool upLeft = HasUrbanInDirection(x, y, -1, 1, 2, grid, width, height);
        bool upRight = HasUrbanInDirection(x, y, 1, 1, 2, grid, width, height);
        bool downLeft = HasUrbanInDirection(x, y, -1, -1, 2, grid, width, height);
        bool downRight = HasUrbanInDirection(x, y, 1, -1, 2, grid, width, height);
        return (upLeft && downRight) || (upRight && downLeft);
    }

    private static bool HasGapFillSupport(int x, int y, Cell[,] grid, int width, int height)
    {
        bool north = HasUrbanInDirection(x, y, 0, 1, 3, grid, width, height);
        bool south = HasUrbanInDirection(x, y, 0, -1, 3, grid, width, height);
        bool east = HasUrbanInDirection(x, y, 1, 0, 3, grid, width, height);
        bool west = HasUrbanInDirection(x, y, -1, 0, 3, grid, width, height);
        return (north && south) || (east && west);
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
            if (zone == SimZone.URBAN_DENSE || zone == SimZone.URBAN_RESIDENTIAL || zone == SimZone.COMMERCIAL || zone == SimZone.INDUSTRIAL)
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
}
