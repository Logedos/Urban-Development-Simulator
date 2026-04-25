using UnityEngine;

[CreateAssetMenu(menuName = "CA Models/V2 Model")]
public class CA_Model_v2 : CA_Model
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
            case SimZone.WATER:
                return SimZone.WATER;
            default:
                return currentZone;
        }
    }

    private SimZone TryAgricultureTransition(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        int industrialR1 = CAUtils.CountType(x, y, 1, SimZone.INDUSTRIAL, grid, width, height);
        int industrialR2 = CAUtils.CountType(x, y, 2, SimZone.INDUSTRIAL, grid, width, height);
        bool hasTransportR1 = CAUtils.HasType(x, y, 1, SimZone.TRANSPORT_INFRA, grid, width, height);

        // Rule A: direct urban edge pressure.
        if (urbanR1 >= 2)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        // Rule B: urban + industrial support.
        if (urbanR2 >= 1 && industrialR2 >= 1)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        // Rule C: road support requires nearby urban.
        if (hasTransportR1 && urbanR2 >= 1)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        // Industrial corridors reinforce urban spread.
        if ((industrialR1 >= 1 || industrialR2 >= 2) && urbanR2 >= 1)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        return SimZone.AGRICULTURE;
    }

    private SimZone TryShrublandTransition(int x, int y, Cell[,] grid, int width, int height)
    {
        int urbanR1 = CountUrban(x, y, 1, grid, width, height);
        int urbanR2 = CountUrban(x, y, 2, grid, width, height);
        int commercialR1 = CAUtils.CountType(x, y, 1, SimZone.COMMERCIAL, grid, width, height);
        int industrialR1 = CAUtils.CountType(x, y, 1, SimZone.INDUSTRIAL, grid, width, height);
        int industrialR2 = CAUtils.CountType(x, y, 2, SimZone.INDUSTRIAL, grid, width, height);
        bool hasTransportR1 = CAUtils.HasType(x, y, 1, SimZone.TRANSPORT_INFRA, grid, width, height);

        // Shrubland can urbanize near mixed urban-commercial edges.
        if (urbanR1 >= 1 && commercialR1 >= 1)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        // Industrial corridors reinforce urban spread.
        if ((industrialR1 >= 1 || industrialR2 >= 2) && urbanR2 >= 1)
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        // Road support requires nearby urban.
        if (hasTransportR1 && (urbanR1 >= 1 || urbanR2 >= 1))
        {
            return SimZone.URBAN_RESIDENTIAL;
        }

        // Construction-site rule omitted because current import maps 133 into URBAN_RESIDENTIAL.
        return SimZone.SHRUBLAND;
    }

    private static int CountUrban(int x, int y, int radius, Cell[,] grid, int width, int height)
    {
        int dense = CAUtils.CountType(x, y, radius, SimZone.URBAN_DENSE, grid, width, height);
        int residential = CAUtils.CountType(x, y, radius, SimZone.URBAN_RESIDENTIAL, grid, width, height);
        int commercial = CAUtils.CountType(x, y, radius, SimZone.COMMERCIAL, grid, width, height);
        return dense + residential + commercial;
    }
}
