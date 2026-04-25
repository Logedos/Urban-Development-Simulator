using System.Collections.Generic;

public struct AccuracyReport
{
    public float overallAccuracy;
    public float kappaCoefficient;
    public Dictionary<SimZone, float> producerAccuracy;
    public Dictionary<SimZone, float> userAccuracy;
    public int[,] confusionMatrix;
    public Dictionary<SimZone, int> simulatedCounts;
    public Dictionary<SimZone, int> referenceCounts;
    public int totalCompared;
    public int exactMatches;
    public int sameCategoryMismatches;
    public int fullMismatches;
}
