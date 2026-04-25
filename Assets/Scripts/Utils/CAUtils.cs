public static class CAUtils
{
    public static int CountType(int x, int y, int radius, SimZone type, Cell[,] grid, int width, int height)
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

        int count = 0;

        for (int neighborY = minY; neighborY <= maxY; neighborY++)
        {
            for (int neighborX = minX; neighborX <= maxX; neighborX++)
            {
                if (neighborX == x && neighborY == y)
                {
                    continue;
                }

                if (grid[neighborX, neighborY].currentZone == type)
                {
                    count++;
                }
            }
        }

        return count;
    }

    public static int CountType(int x, int y, int radius, SimZone typeA, SimZone typeB, Cell[,] grid, int width, int height)
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

        int count = 0;

        for (int neighborY = minY; neighborY <= maxY; neighborY++)
        {
            for (int neighborX = minX; neighborX <= maxX; neighborX++)
            {
                if (neighborX == x && neighborY == y)
                {
                    continue;
                }

                SimZone zone = grid[neighborX, neighborY].currentZone;
                if (zone == typeA || zone == typeB)
                {
                    count++;
                }
            }
        }

        return count;
    }

    public static float GetDensity(int count, int radius)
    {
        int diameter = (radius * 2) + 1;
        int total = (diameter * diameter) - 1;
        return total > 0 ? count / (float)total : 0f;
    }

    public static bool HasType(int x, int y, int radius, SimZone type, Cell[,] grid, int width, int height)
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

                if (grid[neighborX, neighborY].currentZone == type)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
