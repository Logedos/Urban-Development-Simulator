using UnityEngine;

[CreateAssetMenu(menuName = "CA Models/V9 Calibrated Block Model")]
public class CA_Model_v9 : CA_Model
{
    [Header("Residential thresholds")]
    [SerializeField] private float agricultureToResidentialThreshold = 0.74f;
    [SerializeField] private float shrublandToResidentialThreshold = 0.69f;
    [SerializeField] private float bareToResidentialThreshold = 0.66f;

    [Header("Commercial thresholds")]
    [SerializeField] private float agricultureToCommercialThreshold = 0.82f;
    [SerializeField] private float bareToCommercialThreshold = 0.74f;
    [SerializeField] private float residentialToCommercialThreshold = 0.84f;

    [Header("Industrial thresholds")]
    [SerializeField] private float agricultureToIndustrialThreshold = 0.86f;
    [SerializeField] private float bareToIndustrialThreshold = 0.78f;

    [Header("Dense threshold")]
    [SerializeField] private float residentialToDenseThreshold = 0.88f;

    [Header("Protection / buffers")]
    [SerializeField] private int residentialIndustryBufferRadius = 1;
    [SerializeField] private int industrialResidentialConflictCountR1 = 2;
    [SerializeField] private int industrialResidentialConflictCountR2 = 6;
    [SerializeField] private float forestPressureThreshold = 0.62f;

    [Header("Weights - residential")]
    [SerializeField] private float residentialLocalDensityWeight = 0.42f;
    [SerializeField] private float residentialFrontWeight = 0.20f;
    [SerializeField] private float residentialGapFillBonus = 0.12f;
    [SerializeField] private float residentialLayerBonus = 0.10f;
    [SerializeField] private float residentialTwoSidedBonus = 0.10f;
    [SerializeField] private float residentialRoadBonusR1 = 0.08f;
    [SerializeField] private float residentialRoadBonusR2 = 0.04f;
    [SerializeField] private float residentialCenterBonus = 0.07f;
    [SerializeField] private float residentialWaterBonus = 0.03f;

    [Header("Weights - commercial")]
    [SerializeField] private float commercialRoadBonusR1 = 0.20f;
    [SerializeField] private float commercialRoadBonusR2 = 0.10f;
    [SerializeField] private float commercialClusterBonus = 0.18f;
    [SerializeField] private float commercialCorridorBonus = 0.16f;
    [SerializeField] private float commercialUrbanSupportWeight = 0.26f;
    [SerializeField] private float commercialCenterBonus = 0.10f;

    [Header("Weights - industrial")]
    [SerializeField] private float industrialRoadBonusR1 = 0.22f;
    [SerializeField] private float industrialRoadBonusR2 = 0.12f;
    [SerializeField] private float industrialClusterBonus = 0.20f;
    [SerializeField] private float industrialBeltBonus = 0.16f;
    [SerializeField] private float industrialOuterBonus = 0.12f;
    [SerializeField] private float industrialWaterBonus = 0.08f;

    [Header("Weights - dense")]
    [SerializeField] private float denseAdjacencyBonus = 0.26f;
    [SerializeField] private float denseCoreBonus = 0.18f;
    [SerializeField] private float denseCommercialBonus = 0.10f;
    [SerializeField] private float denseCenterBonus = 0.08f;
    [SerializeField] private float denseUrbanDensityWeight = 0.28f;

    public override SimZone EvaluateCell(int x, int y, Cell[,] grid, int width, int height)
    {
        SimZone zone = grid[x, y].currentZone;

        switch (zone)
        {
            case SimZone.AGRICULTURE:
                return EvaluateOpenLand(x, y, grid, width, height, SimZone.AGRICULTURE);

            case SimZone.SHRUBLAND:
                return EvaluateOpenLand(x, y, grid, width, height, SimZone.SHRUBLAND);

            case SimZone.BARE_OTHER:
                return EvaluateOpenLand(x, y, grid, width, height, SimZone.BARE_OTHER);

            case SimZone.URBAN_RESIDENTIAL:
                return EvaluateResidential(x, y, grid, width, height);

            case SimZone.COMMERCIAL:
                return EvaluateCommercial(x, y, grid, width, height);

            case SimZone.INDUSTRIAL:
                return EvaluateIndustrial(x, y, grid, width, height);

            case SimZone.FOREST:
                return EvaluateForest(x, y, grid, width, height);

            case SimZone.GREEN_PUBLIC:
            case SimZone.WATER:
            case SimZone.TRANSPORT_INFRA:
            case SimZone.URBAN_DENSE:
            case SimZone.NODATA:
            default:
                return zone;
        }
    }

    private SimZone EvaluateOpenLand(int x, int y, Cell[,] grid, int width, int height, SimZone sourceZone)
    {
        bool isAgriculture = sourceZone == SimZone.AGRICULTURE;
        bool isShrubland = sourceZone == SimZone.SHRUBLAND;
        bool isBare = sourceZone == SimZone.BARE_OTHER;

        int residentialR1 = CountResidential(x, y, 1, grid, width, height);
        int residentialR2 = CountResidential(x, y, 2, grid, width, height);
        int residentialR3 = CountResidential(x, y, 3, grid, width, height);

        int denseR1 = CountDense(x, y, 1, grid, width, height);
        int denseR2 = CountDense(x, y, 2, grid, width, height);

        int commercialR1 = CountCommercial(x, y, 1, grid, width, height);
        int commercialR2 = CountCommercial(x, y, 2, grid, width, height);
        int commercialR3 = CountCommercial(x, y, 3, grid, width, height);

        int industrialR1 = CountIndustrial(x, y, 1, grid, width, height);
        int industrialR2 = CountIndustrial(x, y, 2, grid, width, height);
        int industrialR3 = CountIndustrial(x, y, 3, grid, width, height);

        int urbanR1 = residentialR1 + denseR1 + commercialR1;
        int urbanR2 = residentialR2 + denseR2 + commercialR2;
        int urbanR3 = residentialR3 + CountDense(x, y, 3, grid, width, height) + commercialR3;

        float residentialDensityR2 = CAUtils.GetDensity(residentialR2 + denseR2, 2);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        float urbanDensityR3 = CAUtils.GetDensity(urbanR3, 3);

        bool nearRoadR1 = NearRoad(x, y, 1, grid, width, height);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);
        bool twoSidedUrban = HasTwoSidedUrbanSupport(x, y, grid, width, height);
        bool gapFillUrban = SupportsUrbanGapFill(x, y, grid, width, height);
        bool layerUrban = SupportsUrbanLayerGrowth(x, y, grid, width, height);
        bool corridorCommercial = SupportsCommercialCorridor(x, y, grid, width, height);
        bool industrialBelt = SupportsIndustrialBelt(x, y, grid, width, height);
        bool thinUrbanSpike = CreatesThinUrbanSpike(x, y, grid, width, height);

        float centerFactor = 1f - NormalizeCenterDistance(grid[x, y].distToCenter, width, height);
        float waterFactor = 1f - NormalizeDistance01(grid[x, y].distToWater, width, height);

        float residentialScore = -999f;
        if (CanGrowResidential(urbanR1, urbanR2, urbanDensityR2, twoSidedUrban, gapFillUrban, layerUrban, nearRoadR1, nearRoadR2, thinUrbanSpike)
            && PassesUrbanNoiseFilter(urbanR1, twoSidedUrban, gapFillUrban, layerUrban, nearRoadR1, nearRoadR2)
            && !IsTooCloseToIndustry(x, y, grid, width, height))
        {
            residentialScore = 0f;
            residentialScore += residentialDensityR2 * residentialLocalDensityWeight;
            residentialScore += CAUtils.GetDensity(urbanR1, 1) * residentialFrontWeight;

            if (gapFillUrban) residentialScore += residentialGapFillBonus;
            if (layerUrban) residentialScore += residentialLayerBonus;
            if (twoSidedUrban) residentialScore += residentialTwoSidedBonus;

            if (nearRoadR1) residentialScore += residentialRoadBonusR1;
            else if (nearRoadR2) residentialScore += residentialRoadBonusR2;

            residentialScore += centerFactor * residentialCenterBonus;
            residentialScore += waterFactor * residentialWaterBonus;

            if (residentialR1 >= 2) residentialScore += 0.10f;
            if (denseR1 >= 1) residentialScore += 0.05f;
            if (commercialR1 >= 1) residentialScore += 0.04f;

            if (industrialR1 > 0) residentialScore = -999f;
            if (industrialR2 >= 3) residentialScore -= 0.25f;
        }

        float commercialScore = -999f;
        if (CanGrowCommercial(urbanR1, urbanDensityR2, nearRoadR1, nearRoadR2, corridorCommercial, thinUrbanSpike, commercialR2))
        {
            commercialScore = 0f;
            commercialScore += CAUtils.GetDensity(commercialR2 + commercialR3, 3) * 0.22f;
            commercialScore += urbanDensityR2 * commercialUrbanSupportWeight;

            if (nearRoadR1) commercialScore += commercialRoadBonusR1;
            else if (nearRoadR2) commercialScore += commercialRoadBonusR2;

            if (commercialR2 >= 1) commercialScore += commercialClusterBonus;
            if (corridorCommercial) commercialScore += commercialCorridorBonus;
            if (gapFillUrban) commercialScore += 0.08f;
            if (twoSidedUrban) commercialScore += 0.06f;

            commercialScore += centerFactor * commercialCenterBonus;

            if (industrialR1 > 0) commercialScore -= 0.08f;
        }

        float industrialScore = -999f;
        if (CanGrowIndustrial(nearRoadR1, nearRoadR2, industrialR2, industrialR3, urbanR2, thinUrbanSpike, x, y, grid, width, height))
        {
            industrialScore = 0f;

            if (nearRoadR1) industrialScore += industrialRoadBonusR1;
            else if (nearRoadR2) industrialScore += industrialRoadBonusR2;

            if (industrialR2 >= 1) industrialScore += industrialClusterBonus;
            if (industrialR3 >= 2) industrialScore += 0.12f;
            if (industrialBelt) industrialScore += industrialBeltBonus;

            float outerFactor = 1f - centerFactor;
            industrialScore += outerFactor * industrialOuterBonus;
            industrialScore += waterFactor * industrialWaterBonus;

            if (residentialR1 >= industrialResidentialConflictCountR1) industrialScore -= 0.25f;
            if (residentialR2 >= industrialResidentialConflictCountR2) industrialScore -= 0.18f;
        }

        float resThreshold = GetResidentialThreshold(sourceZone);
        float comThreshold = GetCommercialThreshold(sourceZone);
        float indThreshold = GetIndustrialThreshold(sourceZone);

        bool residentialOk = residentialScore >= resThreshold;
        bool commercialOk = commercialScore >= comThreshold;
        bool industrialOk = industrialScore >= indThreshold;

        if (!residentialOk && !commercialOk && !industrialOk)
        {
            return sourceZone;
        }

        if (residentialOk && residentialScore >= commercialScore && residentialScore >= industrialScore)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        if (commercialOk && commercialScore >= industrialScore)
        {
            return SimZone.COMMERCIAL;
        }

        if (industrialOk)
        {
            return SimZone.INDUSTRIAL;
        }

        return sourceZone;
    }

    private SimZone EvaluateResidential(int x, int y, Cell[,] grid, int width, int height)
    {
        int residentialR1 = CountResidential(x, y, 1, grid, width, height);
        int residentialR2 = CountResidential(x, y, 2, grid, width, height);
        int denseR1 = CountDense(x, y, 1, grid, width, height);
        int denseR2 = CountDense(x, y, 2, grid, width, height);
        int commercialR1 = CountCommercial(x, y, 1, grid, width, height);
        int commercialR2 = CountCommercial(x, y, 2, grid, width, height);

        int urbanR1 = residentialR1 + denseR1 + commercialR1;
        int urbanR2 = residentialR2 + denseR2 + commercialR2;

        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        float centerFactor = 1f - NormalizeCenterDistance(grid[x, y].distToCenter, width, height);

        float denseScore = -999f;
        if ((denseR1 >= 1 || denseR2 >= 2) &&
            urbanDensityR2 >= 0.60f &&
            urbanR1 >= 5 &&
            HasDenseLayerSupport(x, y, grid, width, height))
        {
            denseScore = 0f;

            if (denseR1 >= 1) denseScore += denseAdjacencyBonus;
            if (denseR2 >= 2) denseScore += denseCoreBonus;

            denseScore += urbanDensityR2 * denseUrbanDensityWeight;

            if (commercialR2 >= 1) denseScore += denseCommercialBonus;
            denseScore += centerFactor * denseCenterBonus;
        }

        float commercialScore = -999f;
        bool nearRoadR1 = NearRoad(x, y, 1, grid, width, height);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);
        bool corridorCommercial = SupportsCommercialCorridor(x, y, grid, width, height);

        if ((nearRoadR1 || nearRoadR2) &&
            corridorCommercial &&
            (commercialR1 >= 1 || commercialR2 >= 2) &&
            denseR1 == 0)
        {
            commercialScore = 0f;
            if (nearRoadR1) commercialScore += 0.22f;
            else if (nearRoadR2) commercialScore += 0.10f;

            commercialScore += CAUtils.GetDensity(commercialR2, 2) * 0.30f;
            commercialScore += CAUtils.GetDensity(urbanR1, 1) * 0.18f;
            commercialScore += centerFactor * 0.08f;
        }

        bool denseOk = denseScore >= GetDenseThreshold();
        bool commercialOk = commercialScore >= GetResidentialToCommercialThreshold();

        if (denseOk && denseScore >= commercialScore)
        {
            return SimZone.URBAN_DENSE;
        }

        if (commercialOk)
        {
            return SimZone.COMMERCIAL;
        }

        return SimZone.URBAN_RESIDENTIAL;
    }

    private SimZone EvaluateCommercial(int x, int y, Cell[,] grid, int width, int height)
    {
        int denseR1 = CountDense(x, y, 1, grid, width, height);
        int denseR2 = CountDense(x, y, 2, grid, width, height);
        int urbanR2 = CountUrbanCore(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        float centerFactor = 1f - NormalizeCenterDistance(grid[x, y].distToCenter, width, height);

        if (denseR1 == 0 && denseR2 < 2)
        {
            return SimZone.COMMERCIAL;
        }

        if (urbanDensityR2 < 0.68f)
        {
            return SimZone.COMMERCIAL;
        }

        float score = 0f;
        if (denseR1 >= 1) score += 0.28f;
        if (denseR2 >= 2) score += 0.16f;
        score += urbanDensityR2 * 0.26f;
        score += centerFactor * 0.08f;

        return score >= 0.72f ? SimZone.URBAN_DENSE : SimZone.COMMERCIAL;
    }

    private SimZone EvaluateIndustrial(int x, int y, Cell[,] grid, int width, int height)
    {
        int residentialR1 = CountResidential(x, y, 1, grid, width, height);
        int commercialR1 = CountCommercial(x, y, 1, grid, width, height);
        int industrialR2 = CountIndustrial(x, y, 2, grid, width, height);
        bool nearRoadR1 = NearRoad(x, y, 1, grid, width, height);

        if (residentialR1 >= 3 && commercialR1 >= 1 && industrialR2 == 0 && !nearRoadR1)
        {
            return SimZone.COMMERCIAL;
        }

        return SimZone.INDUSTRIAL;
    }

    private SimZone EvaluateForest(int x, int y, Cell[,] grid, int width, int height)
    {
        int forestR2 = CAUtils.CountType(x, y, 2, SimZone.FOREST, grid, width, height);
        if (CAUtils.GetDensity(forestR2, 2) >= 0.68f)
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
        int urbanR2,
        float urbanDensityR2,
        bool twoSidedUrban,
        bool gapFillUrban,
        bool layerUrban,
        bool nearRoadR1,
        bool nearRoadR2,
        bool thinUrbanSpike)
    {
        if (urbanR1 == 0 || thinUrbanSpike)
        {
            return false;
        }

        if (urbanR1 >= 2)
        {
            return true;
        }

        if (urbanR1 == 1 && urbanR2 >= 6 && (twoSidedUrban || gapFillUrban || layerUrban))
        {
            return true;
        }

        if (urbanDensityR2 >= 0.30f && (gapFillUrban || layerUrban || twoSidedUrban))
        {
            return true;
        }

        return urbanR1 == 1 && nearRoadR2 && (gapFillUrban || layerUrban || (twoSidedUrban && nearRoadR1));
    }

    private static bool CanGrowCommercial(
        int urbanR1,
        float urbanDensityR2,
        bool nearRoadR1,
        bool nearRoadR2,
        bool corridorCommercial,
        bool thinUrbanSpike,
        int commercialR2)
    {
        if (thinUrbanSpike)
        {
            return false;
        }

        if (!(nearRoadR1 || nearRoadR2))
        {
            return false;
        }

        if (commercialR2 >= 1 && corridorCommercial)
        {
            return true;
        }

        if (urbanR1 >= 2 && corridorCommercial)
        {
            return true;
        }

        return urbanDensityR2 >= 0.36f && corridorCommercial;
    }

    private static bool CanGrowIndustrial(
        bool nearRoadR1,
        bool nearRoadR2,
        int industrialR2,
        int industrialR3,
        int urbanR2,
        bool thinUrbanSpike,
        int x,
        int y,
        Cell[,] grid,
        int width,
        int height)
    {
        if (thinUrbanSpike)
        {
            return false;
        }

        if (!(nearRoadR1 || nearRoadR2))
        {
            return false;
        }

        float centerFactor = 1f - NormalizeCenterDistance(grid[x, y].distToCenter, width, height);
        float outerFactor = 1f - centerFactor;
        if (outerFactor < 0.20f)
        {
            return false;
        }

        if (industrialR2 >= 1 || industrialR3 >= 2)
        {
            return true;
        }

        return urbanR2 >= 3 && nearRoadR1;
    }

    private static bool IsTooCloseToIndustry(int x, int y, Cell[,] grid, int width, int height)
    {
        return CountIndustrial(x, y, 1, grid, width, height) > 0 ||
               CountIndustrial(x, y, 2, grid, width, height) >= 3;
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

    private static bool SupportsUrbanGapFill(int x, int y, Cell[,] grid, int width, int height)
    {
        bool left = HasUrbanInDirection(x, y, -1, 0, 3, grid, width, height);
        bool right = HasUrbanInDirection(x, y, 1, 0, 3, grid, width, height);
        bool up = HasUrbanInDirection(x, y, 0, 1, 3, grid, width, height);
        bool down = HasUrbanInDirection(x, y, 0, -1, 3, grid, width, height);

        return (left && right) || (up && down);
    }

    private static bool SupportsUrbanLayerGrowth(int x, int y, Cell[,] grid, int width, int height)
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

        int orthogonalCount = 0;
        if (left) orthogonalCount++;
        if (right) orthogonalCount++;
        if (up) orthogonalCount++;
        if (down) orthogonalCount++;

        return orthogonalCount >= 2;
    }

    private static bool HasDenseLayerSupport(int x, int y, Cell[,] grid, int width, int height)
    {
        bool left = HasDenseInDirection(x, y, -1, 0, 2, grid, width, height);
        bool right = HasDenseInDirection(x, y, 1, 0, 2, grid, width, height);
        bool up = HasDenseInDirection(x, y, 0, 1, 2, grid, width, height);
        bool down = HasDenseInDirection(x, y, 0, -1, 2, grid, width, height);

        return (left && right) || (up && down) || ((left || right) && (up || down));
    }

    private static bool SupportsCommercialCorridor(int x, int y, Cell[,] grid, int width, int height)
    {
        bool roadLeft = HasRoadInDirection(x, y, -1, 0, 3, grid, width, height);
        bool roadRight = HasRoadInDirection(x, y, 1, 0, 3, grid, width, height);
        bool roadUp = HasRoadInDirection(x, y, 0, 1, 3, grid, width, height);
        bool roadDown = HasRoadInDirection(x, y, 0, -1, 3, grid, width, height);

        bool commercialSupport =
            HasCommercialInDirection(x, y, -1, 0, 3, grid, width, height) ||
            HasCommercialInDirection(x, y, 1, 0, 3, grid, width, height) ||
            HasCommercialInDirection(x, y, 0, 1, 3, grid, width, height) ||
            HasCommercialInDirection(x, y, 0, -1, 3, grid, width, height);

        return ((roadLeft && roadRight) || (roadUp && roadDown) || ((roadLeft || roadRight) && (roadUp || roadDown)))
               && commercialSupport;
    }

    private static bool SupportsIndustrialBelt(int x, int y, Cell[,] grid, int width, int height)
    {
        bool roadLeft = HasRoadInDirection(x, y, -1, 0, 3, grid, width, height);
        bool roadRight = HasRoadInDirection(x, y, 1, 0, 3, grid, width, height);
        bool roadUp = HasRoadInDirection(x, y, 0, 1, 3, grid, width, height);
        bool roadDown = HasRoadInDirection(x, y, 0, -1, 3, grid, width, height);

        bool industrialSupport =
            HasIndustrialInDirection(x, y, -1, 0, 3, grid, width, height) ||
            HasIndustrialInDirection(x, y, 1, 0, 3, grid, width, height) ||
            HasIndustrialInDirection(x, y, 0, 1, 3, grid, width, height) ||
            HasIndustrialInDirection(x, y, 0, -1, 3, grid, width, height);

        return ((roadLeft && roadRight) || (roadUp && roadDown) || ((roadLeft || roadRight) && (roadUp || roadDown)))
               && industrialSupport;
    }

    private static bool CreatesThinUrbanSpike(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrbanCore(x, y, 1, grid, width, height);
        if (urbanR1 >= 2)
        {
            return false;
        }

        return !HasTwoSidedUrbanSupport(x, y, grid, width, height)
               && !SupportsUrbanGapFill(x, y, grid, width, height)
               && !SupportsUrbanLayerGrowth(x, y, grid, width, height);
    }

    private static bool PassesUrbanNoiseFilter(
        int urbanR1,
        bool twoSidedUrban,
        bool gapFillUrban,
        bool layerUrban,
        bool nearRoadR1,
        bool nearRoadR2)
    {
        if (urbanR1 == 0)
        {
            return false;
        }

        if (urbanR1 >= 2)
        {
            return true;
        }

        if (twoSidedUrban || gapFillUrban || layerUrban)
        {
            return true;
        }

        return nearRoadR1 || nearRoadR2;
    }

    private float GetResidentialThreshold(SimZone sourceZone)
    {
        float threshold = agricultureToResidentialThreshold;
        float maxThreshold = 0.52f;

        if (sourceZone == SimZone.SHRUBLAND)
        {
            threshold = shrublandToResidentialThreshold;
            maxThreshold = 0.48f;
        }
        else if (sourceZone == SimZone.BARE_OTHER)
        {
            threshold = bareToResidentialThreshold;
            maxThreshold = 0.44f;
        }

        return Mathf.Min(threshold, maxThreshold);
    }

    private float GetCommercialThreshold(SimZone sourceZone)
    {
        float threshold = sourceZone == SimZone.BARE_OTHER ? bareToCommercialThreshold : agricultureToCommercialThreshold;
        float maxThreshold = sourceZone == SimZone.BARE_OTHER ? 0.50f : 0.56f;
        return Mathf.Min(threshold, maxThreshold);
    }

    private float GetIndustrialThreshold(SimZone sourceZone)
    {
        float threshold = sourceZone == SimZone.BARE_OTHER ? bareToIndustrialThreshold : agricultureToIndustrialThreshold;
        float maxThreshold = sourceZone == SimZone.BARE_OTHER ? 0.48f : 0.54f;
        return Mathf.Min(threshold, maxThreshold);
    }

    private float GetDenseThreshold()
    {
        return Mathf.Min(residentialToDenseThreshold, 0.60f);
    }

    private float GetResidentialToCommercialThreshold()
    {
        return Mathf.Min(residentialToCommercialThreshold, 0.54f);
    }

    private static bool HasUrbanInDirection(int x, int y, int stepX, int stepY, int maxDistance, Cell[,] grid, int width, int height)
    {
        for (int d = 1; d <= maxDistance; d++)
        {
            int sx = x + stepX * d;
            int sy = y + stepY * d;

            if ((uint)sx >= (uint)width || (uint)sy >= (uint)height)
            {
                break;
            }

            SimZone zone = grid[sx, sy].currentZone;
            if (zone == SimZone.URBAN_RESIDENTIAL || zone == SimZone.URBAN_DENSE || zone == SimZone.COMMERCIAL)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCommercialInDirection(int x, int y, int stepX, int stepY, int maxDistance, Cell[,] grid, int width, int height)
    {
        for (int d = 1; d <= maxDistance; d++)
        {
            int sx = x + stepX * d;
            int sy = y + stepY * d;

            if ((uint)sx >= (uint)width || (uint)sy >= (uint)height)
            {
                break;
            }

            if (grid[sx, sy].currentZone == SimZone.COMMERCIAL)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasIndustrialInDirection(int x, int y, int stepX, int stepY, int maxDistance, Cell[,] grid, int width, int height)
    {
        for (int d = 1; d <= maxDistance; d++)
        {
            int sx = x + stepX * d;
            int sy = y + stepY * d;

            if ((uint)sx >= (uint)width || (uint)sy >= (uint)height)
            {
                break;
            }

            if (grid[sx, sy].currentZone == SimZone.INDUSTRIAL)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRoadInDirection(int x, int y, int stepX, int stepY, int maxDistance, Cell[,] grid, int width, int height)
    {
        for (int d = 1; d <= maxDistance; d++)
        {
            int sx = x + stepX * d;
            int sy = y + stepY * d;

            if ((uint)sx >= (uint)width || (uint)sy >= (uint)height)
            {
                break;
            }

            if (grid[sx, sy].currentZone == SimZone.TRANSPORT_INFRA)
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

    private static float NormalizeDistance01(float value, int width, int height)
    {
        if (value <= 1f)
        {
            return Mathf.Clamp01(value);
        }

        float maxDistance = Mathf.Sqrt((width * width) + (height * height));
        if (maxDistance <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(value / maxDistance);
    }
    
    private static bool HasDenseInDirection(int x, int y, int stepX, int stepY, int maxDistance, Cell[,] grid, int width, int height)
    {
        for (int d = 1; d <= maxDistance; d++)
        {
            int sx = x + stepX * d;
            int sy = y + stepY * d;

            if ((uint)sx >= (uint)width || (uint)sy >= (uint)height)
            {
                break;
            }

            if (grid[sx, sy].currentZone == SimZone.URBAN_DENSE)
            {
                return true;
            }
        }

        return false;
    }
}
