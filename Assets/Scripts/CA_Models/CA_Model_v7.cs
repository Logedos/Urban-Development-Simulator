using UnityEngine;

[CreateAssetMenu(menuName = "CA Models/V7 Model")]
public class CA_Model_v7 : CA_Model
{
    [SerializeField] private float agricultureResistance = 0.85f;
    [SerializeField] private float shrublandResistance = 0.75f;
    [SerializeField] private float forestResistanceMultiplier = 0.20f;
    [SerializeField] private float residentialBlockWeight = 0.05f;
    [SerializeField] private float residentialGapFillWeight = 0.04f;
    [SerializeField] private float residentialLayerWeight = 0.03f;
    [SerializeField] private float residentialRoadWeight = 0.02f;
    [SerializeField] private float denseAdjacencyWeight = 0.04f;
    [SerializeField] private float denseLayerWeight = 0.02f;
    [SerializeField] private float denseCommercialWeight = 0.015f;
    [SerializeField] private float commercialRoadWeight = 0.03f;
    [SerializeField] private float commercialClusterWeight = 0.03f;
    [SerializeField] private float industrialRoadWeight = 0.03f;
    [SerializeField] private float industrialClusterWeight = 0.03f;
    [SerializeField] private float industrialEdgeWeight = 0.015f;
    [SerializeField] private float residentialMaxProbability = 0.16f;
    [SerializeField] private float denseMaxProbability = 0.08f;
    [SerializeField] private float commercialMaxProbability = 0.06f;
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

    // Block-based residential expansion with anti-noise gating.
    private SimZone EvaluateAgriculture(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        int residentialR1 = CountResidential(x, y, 1, grid, width, height);
        int residentialR2 = CountResidential(x, y, 2, grid, width, height);
        int commercialR2 = CountCommercial(x, y, 2, grid, width, height);
        int industrialR2 = CountIndustrial(x, y, 2, grid, width, height);
        int industrialR3 = CountIndustrial(x, y, 3, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        bool nearRoadR1 = NearRoad(x, y, 1, grid, width, height);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);
        bool twoSidedSupport = HasTwoSidedSupport(x, y, grid, width, height);
        bool blockSupport = HasBlockSupport(x, y, grid, width, height, urbanR1, urbanDensityR2);
        bool gapFill = SupportsGapFill(x, y, grid, width, height);
        bool smoothing = SupportsEdgeSmoothing(x, y, grid, width, height);

        if (urbanR1 >= 1 && blockSupport && !IsTooCloseToIndustry(x, y, grid, width, height) && PassesUrbanNoiseFilter(urbanR1, twoSidedSupport, nearRoadR1))
        {
            float p = 0f;
            p += CAUtils.GetDensity(residentialR2, 2) * residentialBlockWeight;

            if (residentialR1 >= 2)
            {
                p += 0.05f;
            }

            if (twoSidedSupport)
            {
                p += 0.04f;
            }

            if (gapFill)
            {
                p += residentialGapFillWeight;
            }

            if (smoothing)
            {
                p += residentialLayerWeight;
            }

            if (nearRoadR1)
            {
                p += residentialRoadWeight;
            }
            else if (nearRoadR2)
            {
                p += 0.01f;
            }

            p *= agricultureResistance;
            p = Mathf.Min(Mathf.Clamp01(p), residentialMaxProbability);

            if (Random.value < p)
            {
                return SimZone.URBAN_RESIDENTIAL;
            }
        }

        if ((nearRoadR1 || nearRoadR2) && blockSupport && (commercialR2 >= 1 || urbanR2 >= 2))
        {
            float p = 0f;

            if (nearRoadR1)
            {
                p += commercialRoadWeight;
            }
            else if (nearRoadR2)
            {
                p += 0.02f;
            }

            if (commercialR2 >= 1)
            {
                p += commercialClusterWeight;
            }

            if (urbanDensityR2 >= 0.35f)
            {
                p += 0.015f;
            }

            if (gapFill)
            {
                p += 0.015f;
            }

            p = Mathf.Min(Mathf.Clamp01(p), commercialMaxProbability);

            if (Random.value < p)
            {
                return SimZone.COMMERCIAL;
            }
        }

        float distToCenter = NormalizeCenterDistance(grid[x, y].distToCenter, width, height);
        if ((nearRoadR1 || nearRoadR2) && blockSupport && (industrialR2 >= 1 || industrialR3 >= 2) && distToCenter > 0.20f)
        {
            float p = 0f;

            if (nearRoadR1)
            {
                p += industrialRoadWeight;
            }
            else if (nearRoadR2)
            {
                p += 0.02f;
            }

            if (industrialR2 >= 1)
            {
                p += industrialClusterWeight;
            }

            if (industrialR3 >= 2)
            {
                p += 0.02f;
            }

            if (smoothing)
            {
                p += industrialEdgeWeight;
            }

            p = Mathf.Min(Mathf.Clamp01(p), industrialMaxProbability);

            if (Random.value < p)
            {
                return SimZone.INDUSTRIAL;
            }
        }

        return SimZone.AGRICULTURE;
    }

    private SimZone EvaluateShrubland(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int residentialR1 = CountResidential(x, y, 1, grid, width, height);
        int residentialR2 = CountResidential(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(CountUrban(x, y, 2, grid, width, height), 2);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);
        bool twoSidedSupport = HasTwoSidedSupport(x, y, grid, width, height);
        bool blockSupport = HasBlockSupport(x, y, grid, width, height, urbanR1, urbanDensityR2);

        if (urbanR1 == 0 || !blockSupport || IsTooCloseToIndustry(x, y, grid, width, height) || !PassesUrbanNoiseFilter(urbanR1, twoSidedSupport, false))
        {
            return SimZone.SHRUBLAND;
        }

        float p = 0f;
        p += CAUtils.GetDensity(residentialR2, 2) * 0.04f;

        if (residentialR1 >= 2)
        {
            p += 0.04f;
        }

        if (twoSidedSupport)
        {
            p += 0.03f;
        }

        if (SupportsGapFill(x, y, grid, width, height))
        {
            p += 0.03f;
        }

        if (nearRoadR2)
        {
            p += 0.01f;
        }

        p *= shrublandResistance;
        p = Mathf.Min(Mathf.Clamp01(p), 0.14f);
        return Random.value < p ? SimZone.URBAN_RESIDENTIAL : SimZone.SHRUBLAND;
    }

    // Dense growth thickens existing cores.
    private SimZone EvaluateResidential(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        int denseR1 = CountDense(x, y, 1, grid, width, height);
        int denseR2 = CountDense(x, y, 2, grid, width, height);
        int commercialR2 = CountCommercial(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);

        if ((denseR1 < 1 && denseR2 < 2) || urbanDensityR2 < 0.60f || urbanR1 < 4)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        float p = 0f;

        if (denseR1 >= 1)
        {
            p += denseAdjacencyWeight;
        }

        if (denseR2 >= 2)
        {
            p += 0.03f;
        }

        p += urbanDensityR2 * 0.02f;

        if (commercialR2 >= 1)
        {
            p += denseCommercialWeight;
        }

        if (HasLayerSupport(x, y, grid, width, height))
        {
            p += denseLayerWeight;
        }

        p = Mathf.Min(Mathf.Clamp01(p), denseMaxProbability);
        return Random.value < p ? SimZone.URBAN_DENSE : SimZone.URBAN_RESIDENTIAL;
    }

    private SimZone EvaluateCommercial(int x, int y, Cell[,] grid, int width, int height)
    {
        int denseR2 = CountDense(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(CountUrban(x, y, 2, grid, width, height), 2);

        if (denseR2 < 1 || urbanDensityR2 < 0.70f || !HasLayerSupport(x, y, grid, width, height))
        {
            return SimZone.COMMERCIAL;
        }

        float p = Mathf.Min(0.03f, 0.015f + (urbanDensityR2 * 0.01f));
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

        if (urbanDensityR4 < 0.40f || (CountUrban(x, y, 2, grid, width, height) == 0 && CountUrban(x, y, 3, grid, width, height) == 0))
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

    private static int CountUrban(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        return CountResidential(x, y, radius, grid, width, height)
            + CountDense(x, y, radius, grid, width, height)
            + CountCommercial(x, y, radius, grid, width, height);
    }

    private static int CountResidential(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        return CAUtils.CountType(x, y, radius, SimZone.URBAN_RESIDENTIAL, grid, width, height);
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

    private static bool HasBlockSupport(int x, int y, Cell[,] grid, int width, int height, int urbanR1, float urbanDensityR2)
    {
        if (urbanR1 >= 2)
        {
            return true;
        }

        if (urbanDensityR2 >= 0.30f)
        {
            return true;
        }

        if (HasTwoSidedSupport(x, y, grid, width, height))
        {
            return true;
        }

        if (SupportsGapFill(x, y, grid, width, height))
        {
            return true;
        }

        return SupportsEdgeSmoothing(x, y, grid, width, height);
    }

    private static bool HasLayerSupport(int x, int y, Cell[,] grid, int width, int height)
    {
        return HasTwoSidedSupport(x, y, grid, width, height) || SupportsEdgeSmoothing(x, y, grid, width, height);
    }

    private static bool HasTwoSidedSupport(int x, int y, Cell[,] grid, int width, int height)
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

    private static bool IsTooCloseToIndustry(int x, int y, Cell[,] grid, int width, int height)
    {
        return CountIndustrial(x, y, 1, grid, width, height) > 0 || CountIndustrial(x, y, 2, grid, width, height) >= 3;
    }

    private static bool SupportsGapFill(int x, int y, Cell[,] grid, int width, int height)
    {
        bool left = HasUrbanInDirection(x, y, -1, 0, 3, grid, width, height);
        bool right = HasUrbanInDirection(x, y, 1, 0, 3, grid, width, height);
        bool up = HasUrbanInDirection(x, y, 0, 1, 3, grid, width, height);
        bool down = HasUrbanInDirection(x, y, 0, -1, 3, grid, width, height);
        return (left && right) || (up && down);
    }

    private static bool SupportsEdgeSmoothing(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        if (urbanR1 >= 3)
        {
            return true;
        }

        bool left = HasUrbanInDirection(x, y, -1, 0, 1, grid, width, height);
        bool right = HasUrbanInDirection(x, y, 1, 0, 1, grid, width, height);
        bool up = HasUrbanInDirection(x, y, 0, 1, 1, grid, width, height);
        bool down = HasUrbanInDirection(x, y, 0, -1, 1, grid, width, height);
        return (left && up) || (left && down) || (right && up) || (right && down);
    }

    private static bool PassesUrbanNoiseFilter(int urbanR1, bool twoSidedSupport, bool nearRoadR1)
    {
        if (urbanR1 == 0)
        {
            return false;
        }

        if (urbanR1 >= 2)
        {
            return true;
        }

        return twoSidedSupport && nearRoadR1;
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
            if (zone == SimZone.URBAN_RESIDENTIAL || zone == SimZone.URBAN_DENSE || zone == SimZone.COMMERCIAL)
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
