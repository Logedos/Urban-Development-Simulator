using UnityEngine;

[CreateAssetMenu(menuName = "CA Models/V3 Model")]
public class CA_Model_v3 : CA_Model
{
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
            case SimZone.WATER:
            case SimZone.TRANSPORT_INFRA:
            case SimZone.INDUSTRIAL:
            case SimZone.COMMERCIAL:
            case SimZone.BARE_OTHER:
                return currentZone;
            default:
                return currentZone;
        }
    }

    private static SimZone EvaluateAgriculture(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);

        if (urbanR1 >= 3)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        if (urbanR1 >= 2 && urbanDensityR2 >= 0.3f)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        return SimZone.AGRICULTURE;
    }

    private static SimZone EvaluateResidential(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);

        if (urbanDensityR2 >= 0.6f)
        {
            return SimZone.URBAN_DENSE;
        }

        return SimZone.URBAN_RESIDENTIAL;
    }

    private static SimZone EvaluateShrubland(int x, int y, Cell[,] grid, int width, int height)
    {
        int agriR1 = CAUtils.CountType(x, y, 1, SimZone.AGRICULTURE, grid, width, height);

        if (agriR1 >= 3)
        {
            return SimZone.AGRICULTURE;
        }

        return SimZone.SHRUBLAND;
    }

    private static SimZone EvaluateForest(int x, int y, Cell[,] grid, int width, int height)
    {
        int forestR2 = CAUtils.CountType(x, y, 2, SimZone.FOREST, grid, width, height);
        float forestDensityR2 = CAUtils.GetDensity(forestR2, 2);
        int urbanR3 = CountUrban(x, y, 3, grid, width, height);
        float urbanDensityR3 = CAUtils.GetDensity(urbanR3, 3);

        if (forestDensityR2 >= 0.6f)
        {
            return SimZone.FOREST;
        }

        if (urbanDensityR3 >= 0.5f)
        {
            return SimZone.SHRUBLAND;
        }

        return SimZone.FOREST;
    }

    private static SimZone EvaluateGreenPublic(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);

        if (urbanDensityR2 >= 0.7f)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        return SimZone.GREEN_PUBLIC;
    }

    private static int CountUrban(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        int denseCount = CAUtils.CountType(x, y, radius, SimZone.URBAN_DENSE, grid, width, height);
        int residentialCount = CAUtils.CountType(x, y, radius, SimZone.URBAN_RESIDENTIAL, grid, width, height);
        int commercialCount = CAUtils.CountType(x, y, radius, SimZone.COMMERCIAL, grid, width, height);
        return denseCount + residentialCount + commercialCount;
    }
}
