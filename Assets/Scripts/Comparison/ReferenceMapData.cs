using System.Collections.Generic;
using UnityEngine;

public struct ReferenceMapData
{
    public string fileName;
    public string fullPath;
    public string yearLabel;
    public int sourceWidth;
    public int sourceHeight;
    public int targetWidth;
    public int targetHeight;
    public bool wasResampled;
    public Vector2 cityCenter;
    public SimZone[,] zoneGrid;
    public int[,] corineIdGrid;
    public Dictionary<SimZone, int> zoneCounts;
}
