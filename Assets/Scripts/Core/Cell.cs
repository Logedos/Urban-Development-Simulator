using System;

[Serializable]
public struct Cell
{
    public SimZone currentZone;
    public SimZone nextZone;
    public bool isActive;
    public float urbanPressure;
    public float distToWater;
    public float distToCenter;
}
