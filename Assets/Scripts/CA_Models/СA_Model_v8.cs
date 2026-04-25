using UnityEngine;

[CreateAssetMenu(menuName = "CA Models/V8 Model")]
public class CA_Model_v8 : CA_Model
{
    [Header("Residential Growth")]
    [SerializeField] private float residentialThreshold = 0.62f;
    [SerializeField] private float shrubResidentialThreshold = 0.58f;
    [SerializeField] private float residentialRoadBonus = 0.08f;
    [SerializeField] private float residentialGapFillBonus = 0.12f;
    [SerializeField] private float residentialLayerBonus = 0.10f;
    [SerializeField] private float residentialCenterBonus = 0.08f;

    [Header("Dense Growth")]
    [SerializeField] private float denseThreshold = 0.78f;
    [SerializeField] private float denseAdjacencyBonus = 0.22f;
    [SerializeField] private float denseCommercialBonus = 0.12f;
    [SerializeField] private float denseCenterBonus = 0.10f;

    [Header("Commercial Growth")]
    [SerializeField] private float commercialThreshold = 0.72f;
    [SerializeField] private float commercialRoadBonus = 0.18f;
    [SerializeField] private float commercialClusterBonus = 0.18f;
    [SerializeField] private float commercialCenterBonus = 0.12f;

    [Header("Industrial Growth")]
    [SerializeField] private float industrialThreshold = 0.74f;
    [SerializeField] private float industrialRoadBonus = 0.20f;
    [SerializeField] private float industrialClusterBonus = 0.20f;
    [SerializeField] private float industrialOuterBonus = 0.12f;

    [Header("Protection")]
    [SerializeField] private float forestPressureThreshold = 0.55f;
    [SerializeField] private int residentialIndustryBufferR1 = 1;
    [SerializeField] private int residentialIndustryBufferR2Count = 3;

    public override SimZone EvaluateCell(int x, int y, Cell[,] grid, int width, int height)
    {
        SimZone zone = grid[x, y].currentZone;

        switch (zone)
        {
            case SimZone.AGRICULTURE:
                return EvaluateOpenLand(x, y, grid, width, height, false);

            case SimZone.SHRUBLAND:
                return EvaluateOpenLand(x, y, grid, width, height, true);

            case SimZone.URBAN_RESIDENTIAL:
                return EvaluateResidential(x, y, grid, width, height);

            case SimZone.FOREST:
                return EvaluateForest(x, y, grid, width, height);

            case SimZone.COMMERCIAL:
            case SimZone.INDUSTRIAL:
            case SimZone.GREEN_PUBLIC:
            case SimZone.WATER:
            case SimZone.TRANSPORT_INFRA:
            case SimZone.BARE_OTHER:
            case SimZone.URBAN_DENSE:
            case SimZone.NODATA:
            default:
                return zone;
        }
    }

    private SimZone EvaluateOpenLand(int x, int y, Cell[,] grid, int width, int height, bool isShrubland)
    {
        int residentialR1 = CountResidential(x, y, 1, grid, width, height);
        int residentialR2 = CountResidential(x, y, 2, grid, width, height);
        int denseR1 = CountDense(x, y, 1, grid, width, height);
        int denseR2 = CountDense(x, y, 2, grid, width, height);
        int commercialR1 = CountCommercial(x, y, 1, grid, width, height);
        int commercialR2 = CountCommercial(x, y, 2, grid, width, height);
        int industrialR1 = CountIndustrial(x, y, 1, grid, width, height);
        int industrialR2 = CountIndustrial(x, y, 2, grid, width, height);
        int industrialR3 = CountIndustrial(x, y, 3, grid, width, height);

        int urbanR1 = residentialR1 + denseR1 + commercialR1;
        int urbanR2 = residentialR2 + denseR2 + commercialR2;

        float residentialDensityR2 = CAUtils.GetDensity(residentialR2 + denseR2, 2);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        float centerFactor = 1f - NormalizeCenterDistance(grid[x, y].distToCenter, width, height);

        bool nearRoadR1 = NearRoad(x, y, 1, grid, width, height);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);

        bool twoSidedSupport = HasTwoSidedUrbanSupport(x, y, grid, width, height);
        bool gapFill = SupportsGapFill(x, y, grid, width, height);
        bool smoothLayer = SupportsLayerGrowth(x, y, grid, width, height);
        bool thinSpike = CreatesThinSpike(x, y, grid, width, height);

        float residentialScore = -1f;
        if (CanGrowResidential(urbanR1, urbanDensityR2, twoSidedSupport, gapFill, smoothLayer, thinSpike) &&
            !IsTooCloseToIndustry(x, y, grid, width, height))
        {
            residentialScore = 0f;

            residentialScore += residentialDensityR2 * 0.55f;
            residentialScore += CAUtils.GetDensity(urbanR1, 1) * 0.30f;

            if (twoSidedSupport) residentialScore += 0.18f;
            if (gapFill) residentialScore += residentialGapFillBonus;
            if (smoothLayer) residentialScore += residentialLayerBonus;
            if (nearRoadR1) residentialScore += residentialRoadBonus;
            else if (nearRoadR2) residentialScore += 0.04f;

            residentialScore += centerFactor * residentialCenterBonus;

            if (residentialR1 >= 2) residentialScore += 0.12f;
            if (denseR1 >= 1) residentialScore += 0.06f;
            if (commercialR1 >= 1) residentialScore += 0.05f;

            if (industrialR1 > 0) residentialScore = -1f;
            if (industrialR2 >= residentialIndustryBufferR2Count) residentialScore -= 0.25f;
        }

        float commercialScore = -1f;
        if (CanGrowCommercial(urbanR1, urbanDensityR2, nearRoadR1, nearRoadR2, twoSidedSupport, gapFill, thinSpike))
        {
            commercialScore = 0f;

            commercialScore += CAUtils.GetDensity(commercialR2 + residentialR2 + denseR2, 2) * 0.45f;
            commercialScore += CAUtils.GetDensity(urbanR1, 1) * 0.22f;

            if (nearRoadR1) commercialScore += commercialRoadBonus;
            else if (nearRoadR2) commercialScore += 0.10f;

            if (commercialR2 >= 1) commercialScore += commercialClusterBonus;
            if (gapFill) commercialScore += 0.08f;
            if (twoSidedSupport) commercialScore += 0.08f;

            commercialScore += centerFactor * commercialCenterBonus;
        }

        float industrialScore = -1f;
        if (CanGrowIndustrial(nearRoadR1, nearRoadR2, industrialR2, industrialR3, urbanR2, thinSpike))
        {
            industrialScore = 0f;

            if (nearRoadR1) industrialScore += industrialRoadBonus;
            else if (nearRoadR2) industrialScore += 0.10f;

            if (industrialR2 >= 1) industrialScore += industrialClusterBonus;
            if (industrialR3 >= 2) industrialScore += 0.14f;

            float outerFactor = 1f - centerFactor;
            industrialScore += outerFactor * industrialOuterBonus;

            if (SupportsIndustrialBelt(x, y, grid, width, height))
            {
                industrialScore += 0.10f;
            }
        }

        float resThreshold = isShrubland ? shrubResidentialThreshold : residentialThreshold;

        bool resOk = residentialScore >= resThreshold;
        bool comOk = commercialScore >= commercialThreshold;
        bool indOk = industrialScore >= industrialThreshold;

        if (!resOk && !comOk && !indOk)
        {
            return isShrubland ? SimZone.SHRUBLAND : SimZone.AGRICULTURE;
        }

        if (resOk && residentialScore >= commercialScore && residentialScore >= industrialScore)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        if (comOk && commercialScore >= industrialScore)
        {
            return SimZone.COMMERCIAL;
        }

        if (indOk)
        {
            return SimZone.INDUSTRIAL;
        }

        return isShrubland ? SimZone.SHRUBLAND : SimZone.AGRICULTURE;
    }

    private SimZone EvaluateResidential(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrbanCore(x, y, 1, grid, width, height);
        int urbanR2 = CountUrbanCore(x, y, 2, grid, width, height);
        int denseR1 = CountDense(x, y, 1, grid, width, height);
        int denseR2 = CountDense(x, y, 2, grid, width, height);
        int commercialR2 = CountCommercial(x, y, 2, grid, width, height);

        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        float centerFactor = 1f - NormalizeCenterDistance(grid[x, y].distToCenter, width, height);

        if (denseR1 == 0 && denseR2 < 2)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        if (urbanDensityR2 < 0.65f || urbanR1 < 5)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        if (!SupportsLayerGrowth(x, y, grid, width, height))
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        float denseScore = 0f;
        if (denseR1 >= 1) denseScore += denseAdjacencyBonus;
        if (denseR2 >= 2) denseScore += 0.18f;

        denseScore += urbanDensityR2 * 0.28f;

        if (commercialR2 >= 1) denseScore += denseCommercialBonus;
       // if (HasDenseLayerSupport(x, y, grid, width, height)) denseScore += denseLayerWeight;

        denseScore += centerFactor * denseCenterBonus;

        return denseScore >= denseThreshold ? SimZone.URBAN_DENSE : SimZone.URBAN_RESIDENTIAL;
    }

    private SimZone EvaluateForest(int x, int y, Cell[,] grid, int width, int height)
    {
        int forestR2 = CAUtils.CountType(x, y, 2, SimZone.FOREST, grid, width, height);
        if (CAUtils.GetDensity(forestR2, 2) >= 0.65f)
        {
            return SimZone.FOREST;
        }

        int urbanR3 = CountUrbanCore(x, y, 3, grid, width, height);
        int urbanR4 = CountUrbanCore(x, y, 4, grid, width, height);
        float urbanDensityR4 = CAUtils.GetDensity(urbanR4, 4);

        if (urbanR3 == 0)
        {
            return SimZone.FOREST;
        }

        if (urbanDensityR4 >= forestPressureThreshold && NearRoad(x, y, 3, grid, width, height))
        {
            return SimZone.SHRUBLAND;
        }

        return SimZone.FOREST;
    }

    private static bool CanGrowResidential(
        int urbanR1,
        float urbanDensityR2,
        bool twoSidedSupport,
        bool gapFill,
        bool smoothLayer,
        bool thinSpike)
    {
        if (urbanR1 == 0 || thinSpike)
        {
            return false;
        }

        if (urbanR1 >= 2)
        {
            return true;
        }

        if (urbanDensityR2 >= 0.28f)
        {
            return true;
        }

        return twoSidedSupport || gapFill || smoothLayer;
    }

    private static bool CanGrowCommercial(
        int urbanR1,
        float urbanDensityR2,
        bool nearRoadR1,
        bool nearRoadR2,
        bool twoSidedSupport,
        bool gapFill,
        bool thinSpike)
    {
        if (thinSpike)
        {
            return false;
        }

        if (!(nearRoadR1 || nearRoadR2))
        {
            return false;
        }

        if (urbanR1 >= 2)
        {
            return true;
        }

        if (urbanDensityR2 >= 0.32f)
        {
            return true;
        }

        return twoSidedSupport || gapFill;
    }

    private static bool CanGrowIndustrial(
        bool nearRoadR1,
        bool nearRoadR2,
        int industrialR2,
        int industrialR3,
        int urbanR2,
        bool thinSpike)
    {
        if (thinSpike)
        {
            return false;
        }

        if (!(nearRoadR1 || nearRoadR2))
        {
            return false;
        }

        if (industrialR2 >= 1 || industrialR3 >= 2)
        {
            return true;
        }

        return urbanR2 >= 3 && nearRoadR1;
    }

    private static int CountUrbanCore(int x, int y, int radius, Cell[,] grid, int width, int height)
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

    private bool IsTooCloseToIndustry(int x, int y, Cell[,] grid, int width, int height)
    {
        return CountIndustrial(x, y, residentialIndustryBufferR1, grid, width, height) > 0 ||
               CountIndustrial(x, y, 2, grid, width, height) >= residentialIndustryBufferR2Count;
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

    private static bool SupportsGapFill(int x, int y, Cell[,] grid, int width, int height)
    {
        bool left = HasUrbanInDirection(x, y, -1, 0, 3, grid, width, height);
        bool right = HasUrbanInDirection(x, y, 1, 0, 3, grid, width, height);
        bool up = HasUrbanInDirection(x, y, 0, 1, 3, grid, width, height);
        bool down = HasUrbanInDirection(x, y, 0, -1, 3, grid, width, height);

        return (left && right) || (up && down);
    }

    private static bool SupportsLayerGrowth(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrbanCore(x, y, 1, grid, width, height);
        if (urbanR1 >= 3)
        {
            return true;
        }

        bool left = HasUrbanInDirection(x, y, -1, 0, 1, grid, width, height);
        bool right = HasUrbanInDirection(x, y, 1, 0, 1, grid, width, height);
        bool up = HasUrbanInDirection(x, y, 0, 1, 1, grid, width, height);
        bool down = HasUrbanInDirection(x, y, 0, -1, 1, grid, width, height);

        int pairCount = 0;
        if (left) pairCount++;
        if (right) pairCount++;
        if (up) pairCount++;
        if (down) pairCount++;

        return pairCount >= 2;
    }

    private static bool HasDenseLayerSupport(int x, int y, Cell[,] grid, int width, int height)
    {
        bool left = HasDenseInDirection(x, y, -1, 0, 2, grid, width, height);
        bool right = HasDenseInDirection(x, y, 1, 0, 2, grid, width, height);
        bool up = HasDenseInDirection(x, y, 0, 1, 2, grid, width, height);
        bool down = HasDenseInDirection(x, y, 0, -1, 2, grid, width, height);

        return (left && right) || (up && down) || ((left || right) && (up || down));
    }

    private static bool SupportsIndustrialBelt(int x, int y, Cell[,] grid, int width, int height)
    {
        bool left = HasIndustrialInDirection(x, y, -1, 0, 2, grid, width, height);
        bool right = HasIndustrialInDirection(x, y, 1, 0, 2, grid, width, height);
        bool up = HasIndustrialInDirection(x, y, 0, 1, 2, grid, width, height);
        bool down = HasIndustrialInDirection(x, y, 0, -1, 2, grid, width, height);

        return (left && right) || (up && down) || ((left || right) && (up || down));
    }

    private static bool CreatesThinSpike(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrbanCore(x, y, 1, grid, width, height);
        if (urbanR1 >= 2)
        {
            return false;
        }

        return !HasTwoSidedUrbanSupport(x, y, grid, width, height) &&
               !SupportsGapFill(x, y, grid, width, height) &&
               !SupportsLayerGrowth(x, y, grid, width, height);
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

    private static bool HasDenseInDirection(int x, int y, int stepX, int stepY, int maxDistance, Cell[,] grid, int width, int height)
    {
        for (int distance = 1; distance <= maxDistance; distance++)
        {
            int sampleX = x + (stepX * distance);
            int sampleY = y + (stepY * distance);

            if ((uint)sampleX >= (uint)width || (uint)sampleY >= (uint)height)
            {
                break;
            }

            if (grid[sampleX, sampleY].currentZone == SimZone.URBAN_DENSE)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasIndustrialInDirection(int x, int y, int stepX, int stepY, int maxDistance, Cell[,] grid, int width, int height)
    {
        for (int distance = 1; distance <= maxDistance; distance++)
        {
            int sampleX = x + (stepX * distance);
            int sampleY = y + (stepY * distance);

            if ((uint)sampleX >= (uint)width || (uint)sampleY >= (uint)height)
            {
                break;
            }

            if (grid[sampleX, sampleY].currentZone == SimZone.INDUSTRIAL)
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