using UnityEngine;

[CreateAssetMenu(menuName = "CA Models/V4 Model")]
public class CA_Model_RealisticGrowth : CA_Model
{
    [SerializeField] private float agricultureResistance = 0.82f;
    [SerializeField] private float shrublandResistance = 0.88f;
    [SerializeField] private float forestResistanceMultiplier = 0.2f;
    [SerializeField] private float centerPullWeight = 0.015f;
    [SerializeField] private float roadWeight = 0.015f;
    [SerializeField] private float industrialWeight = 0.01f;
    [SerializeField] private float waterWeight = 0.005f;
    [SerializeField] private float residentialMaxProbability = 0.18f;
    [SerializeField] private float denseMaxProbability = 0.2f;
    [SerializeField] private float industrialMaxProbability = 0.1f;

    public override SimZone EvaluateCell(int x, int y, Cell[,] grid, int width, int height)
    {
        SimZone currentZone = grid[x, y].currentZone;

        switch (currentZone)
        {
            case SimZone.AGRICULTURE:
                return EvaluateAgriculture(x, y, grid, width, height);
            case SimZone.URBAN_RESIDENTIAL:
                return EvaluateResidential(x, y, grid, width, height);
            case SimZone.SHRUBLAND:
                return EvaluateShrubland(x, y, grid, width, height);
            case SimZone.FOREST:
                return EvaluateForest(x, y, grid, width, height);
            case SimZone.GREEN_PUBLIC:
                return EvaluateGreenPublic(x, y, grid, width, height);
            case SimZone.INDUSTRIAL:
                return EvaluateIndustrial(x, y, grid, width, height);
            case SimZone.WATER:
                return SimZone.WATER;
            case SimZone.TRANSPORT_INFRA:
            case SimZone.COMMERCIAL:
            case SimZone.URBAN_DENSE:
            case SimZone.BARE_OTHER:
            case SimZone.NODATA:
            default:
                return currentZone;
        }
    }

    private SimZone EvaluateAgriculture(int x, int y, Cell[,] grid, int width, int height)
    {
        Cell cell = grid[x, y];
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        bool nearRoadR1 = NearRoad(x, y, 1, grid, width, height);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);
        bool nearIndustrialR2 = NearIndustrial(x, y, 2, grid, width, height);

        if (!FrontierUrbanRule(urbanR1, urbanR2, urbanDensityR2, nearRoadR1, nearRoadR2, nearIndustrialR2))
        {
            return TryAgricultureIndustrial(cell, urbanR2, nearRoadR1, nearRoadR2, nearIndustrialR2, width, height);
        }

        float p = 0f;
        p += urbanDensityR2 * 0.035f;

        if (urbanR1 >= 1)
        {
            p += 0.03f;
        }

        if (urbanR1 >= 2)
        {
            p += 0.04f;
        }

        if (urbanR1 >= 3)
        {
            p += 0.05f;
        }

        if (nearRoadR2)
        {
            p += roadWeight;
        }

        if (nearIndustrialR2)
        {
            p += industrialWeight;
        }

        p += (1f - NormalizeCenterDistance(cell.distToCenter, width, height)) * centerPullWeight;
        p += (1f - NormalizeWaterDistance(cell.distToWater, width, height)) * waterWeight;
        p = Mathf.Min(Mathf.Clamp01(p * agricultureResistance), residentialMaxProbability);

        if (Random.value < p)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        return TryAgricultureIndustrial(cell, urbanR2, nearRoadR1, nearRoadR2, nearIndustrialR2, width, height);
    }

    private SimZone EvaluateResidential(int x, int y, Cell[,] grid, int width, int height)
    {
        Cell cell = grid[x, y];
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        bool hasDenseNearby = HasDenseNearby(x, y, grid, width, height);
        int greenR1 = CAUtils.CountType(x, y, 1, SimZone.GREEN_PUBLIC, grid, width, height);

        if (urbanDensityR2 < 0.55f || urbanR1 < 4 || !hasDenseNearby || greenR1 > 1)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        float p = 0f;
        p += urbanDensityR2 * 0.03f;

        if (urbanR1 >= 5)
        {
            p += 0.03f;
        }

        if (hasDenseNearby)
        {
            p += 0.04f;
        }

        p += (1f - NormalizeCenterDistance(cell.distToCenter, width, height)) * centerPullWeight;

        if (greenR1 == 0)
        {
            p += 0.01f;
        }

        p = Mathf.Min(Mathf.Clamp01(p), denseMaxProbability);
        return Random.value < p ? SimZone.URBAN_DENSE : SimZone.URBAN_RESIDENTIAL;
    }

    private SimZone EvaluateShrubland(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);
        bool nearRoadR1 = NearRoad(x, y, 1, grid, width, height);
        bool nearRoadR2 = NearRoad(x, y, 2, grid, width, height);
        bool nearIndustrialR2 = NearIndustrial(x, y, 2, grid, width, height);

        if (!FrontierUrbanRule(urbanR1, urbanR2, urbanDensityR2, nearRoadR1, nearRoadR2, nearIndustrialR2))
        {
            return SimZone.SHRUBLAND;
        }

        float p = 0f;
        p += urbanDensityR2 * 0.03f;

        if (urbanR1 >= 2)
        {
            p += 0.03f;
        }

        if (nearRoadR2)
        {
            p += 0.01f;
        }

        p = Mathf.Min(Mathf.Clamp01(p * shrublandResistance), 0.14f);
        return Random.value < p ? SimZone.URBAN_RESIDENTIAL : SimZone.SHRUBLAND;
    }

    private SimZone EvaluateForest(int x, int y, Cell[,] grid, int width, int height)
    {
        int forestR2 = CAUtils.CountType(x, y, 2, SimZone.FOREST, grid, width, height);
        if (CAUtils.GetDensity(forestR2, 2) >= 0.65f)
        {
            return SimZone.FOREST;
        }

        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        int urbanR4 = CountUrban(x, y, 4, grid, width, height);
        float urbanDensityR4 = CAUtils.GetDensity(urbanR4, 4);
        if (urbanDensityR4 < 0.4f || urbanR2 < 1)
        {
            return SimZone.FOREST;
        }

        float p = urbanDensityR4 * 0.01f;

        if (NearRoad(x, y, 3, grid, width, height))
        {
            p += 0.005f;
        }

        p = Mathf.Clamp01(p * forestResistanceMultiplier);
        return Random.value < p ? SimZone.SHRUBLAND : SimZone.FOREST;
    }

    private SimZone EvaluateGreenPublic(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        float urbanDensityR1 = CAUtils.GetDensity(urbanR1, 1);
        if (urbanDensityR1 < 0.75f || urbanR1 < 6 || !HasDenseNearby(x, y, grid, width, height))
        {
            return SimZone.GREEN_PUBLIC;
        }

        float p = 0.03f;
        return Random.value < p ? SimZone.URBAN_RESIDENTIAL : SimZone.GREEN_PUBLIC;
    }

    private SimZone EvaluateIndustrial(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        float urbanDensityR3 = CAUtils.GetDensity(CountUrban(x, y, 3, grid, width, height), 3);
        if (urbanR2 < 1 || urbanDensityR3 <= 0.8f)
        {
            return SimZone.INDUSTRIAL;
        }

        float p = Mathf.Clamp01(0.02f + ((urbanDensityR3 - 0.8f) * 0.03f));
        return Random.value < p ? SimZone.COMMERCIAL : SimZone.INDUSTRIAL;
    }

    private SimZone TryAgricultureIndustrial(Cell cell, int urbanR2, bool nearRoadR1, bool nearRoadR2, bool nearIndustrialR2, int width, int height)
    {
        float distToCenter = NormalizeCenterDistance(cell.distToCenter, width, height);
        bool hasSupport = nearIndustrialR2 || urbanR2 >= 1;

        if (!(nearRoadR1 || nearRoadR2) || !hasSupport || distToCenter <= 0.25f)
        {
            return SimZone.AGRICULTURE;
        }

        float p = 0f;

        if (nearRoadR2)
        {
            p += 0.03f;
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

    private static bool HasUrbanInR1(int x, int y, Cell[,] grid, int width, int height)
    {
        return CountUrban(x, y, 1, grid, width, height) > 0;
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

    private static bool FrontierUrbanRule(int urbanR1, int urbanR2, float urbanDensityR2, bool nearRoadR1, bool nearRoadR2, bool nearIndustrialR2)
    {
        if (urbanR1 == 0)
        {
            return false;
        }

        if (urbanR1 >= 2)
        {
            return true;
        }

        if (urbanDensityR2 >= 0.2f)
        {
            return true;
        }

        if ((nearRoadR1 || nearRoadR2) && urbanR2 >= 1)
        {
            return true;
        }

        return nearIndustrialR2 && urbanR2 >= 1;
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
