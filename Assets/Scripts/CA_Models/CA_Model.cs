using UnityEngine;

public abstract class CA_Model : ScriptableObject
{
    public string modelName;

    public abstract SimZone EvaluateCell(int x, int y, Cell[,] grid, int width, int height);
}
