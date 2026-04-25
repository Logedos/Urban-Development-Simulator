using UnityEngine;

[CreateAssetMenu(menuName = "CA Models/V1 Model")]
public class CA_Model_v1 : CA_Model
{
    public override SimZone EvaluateCell(int x, int y, Cell[,] grid, int width, int height)
    {
        SimZone currentZone = grid[x, y].currentZone;

        switch (currentZone)
        {
            case SimZone.AGRICULTURE:
                return TryAgricultureTransition(x, y, grid, width, height);
            case SimZone.SHRUBLAND:
                return TryShrublandTransition(x, y, grid, width, height);
            case SimZone.FOREST:
                return TryForestTransition(x, y, grid, width, height);
            case SimZone.URBAN_RESIDENTIAL:
                return TryResidentialTransition(x, y, grid, width, height);
            case SimZone.WATER:
            case SimZone.TRANSPORT_INFRA:
                return currentZone;
            default:
                return currentZone;
        }
    }

    private SimZone TryAgricultureTransition(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CAUtils.CountType(x, y, 1, SimZone.URBAN_DENSE, SimZone.URBAN_RESIDENTIAL, grid, width, height);
        int urbanR2 = CAUtils.CountType(x, y, 2, SimZone.URBAN_DENSE, SimZone.URBAN_RESIDENTIAL, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);

        if (urbanR1 >= 3)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        if (urbanR1 >= 2 && urbanDensityR2 > 0.3f)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        return SimZone.AGRICULTURE;
    }

    private SimZone TryResidentialTransition(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR2 = CAUtils.CountType(x, y, 2, SimZone.URBAN_DENSE, SimZone.URBAN_RESIDENTIAL, grid, width, height);
        float urbanDensityR2 = CAUtils.GetDensity(urbanR2, 2);

        if (urbanDensityR2 >= 0.6f)
        {
            return SimZone.URBAN_DENSE;
        }

        return SimZone.URBAN_RESIDENTIAL;
    }

    private SimZone TryShrublandTransition(int x, int y, Cell[,] grid, int width, int height)
    {
        int agriR1 = CAUtils.CountType(x, y, 1, SimZone.AGRICULTURE, grid, width, height);

        if (agriR1 >= 3)
        {
            return SimZone.AGRICULTURE;
        }

        return SimZone.SHRUBLAND;
    }

    private SimZone TryForestTransition(int x, int y, Cell[,] grid, int width, int height)
    {
        int forestR2 = CAUtils.CountType(x, y, 2, SimZone.FOREST, grid, width, height);
        float forestDensityR2 = CAUtils.GetDensity(forestR2, 2);
        int urbanR3 = CAUtils.CountType(x, y, 3, SimZone.URBAN_DENSE, SimZone.URBAN_RESIDENTIAL, grid, width, height);
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
}
