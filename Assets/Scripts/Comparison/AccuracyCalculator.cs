using System.Collections.Generic;

public enum ZoneCategory
{
    Urban,
    Infrastructure,
    GreenNatural,
    Agriculture,
    Water,
    BareOther,
    Unknown
}

public static class AccuracyCalculator
{
    public static AccuracyReport ComputeAccuracy(Cell[,] simulationGrid, SimZone[,] referenceGrid)
    {
        AccuracyReport report = new AccuracyReport
        {
            producerAccuracy = new Dictionary<SimZone, float>((int)SimZone.NODATA),
            userAccuracy = new Dictionary<SimZone, float>((int)SimZone.NODATA),
            simulatedCounts = new Dictionary<SimZone, int>((int)SimZone.NODATA),
            referenceCounts = new Dictionary<SimZone, int>((int)SimZone.NODATA)
        };

        int zoneCount = (int)SimZone.NODATA + 1;
        int[,] confusionMatrix = new int[zoneCount, zoneCount];
        int[] predictedTotals = new int[zoneCount];
        int[] referenceTotals = new int[zoneCount];
        int totalCompared = 0;
        int exactMatches = 0;
        int sameCategoryMismatches = 0;
        int fullMismatches = 0;

        if (simulationGrid == null || referenceGrid == null)
        {
            report.confusionMatrix = confusionMatrix;
            return report;
        }

        int width = simulationGrid.GetLength(0);
        int height = simulationGrid.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Cell simulatedCell = simulationGrid[x, y];
                SimZone referenceZone = referenceGrid[x, y];

                if (!simulatedCell.isActive || referenceZone == SimZone.NODATA)
                {
                    continue;
                }

                SimZone predictedZone = simulatedCell.currentZone;
                confusionMatrix[(int)predictedZone, (int)referenceZone]++;
                predictedTotals[(int)predictedZone]++;
                referenceTotals[(int)referenceZone]++;
                totalCompared++;

                if (predictedZone == referenceZone)
                {
                    exactMatches++;
                    continue;
                }

                if (GetCategory(predictedZone) == GetCategory(referenceZone))
                {
                    sameCategoryMismatches++;
                }
                else
                {
                    fullMismatches++;
                }
            }
        }

        report.confusionMatrix = confusionMatrix;
        report.totalCompared = totalCompared;
        report.exactMatches = exactMatches;
        report.sameCategoryMismatches = sameCategoryMismatches;
        report.fullMismatches = fullMismatches;
        report.overallAccuracy = totalCompared > 0 ? exactMatches / (float)totalCompared : 0f;

        float expectedAgreement = 0f;
        if (totalCompared > 0)
        {
            float totalSquared = totalCompared * (float)totalCompared;
            for (int i = 0; i < zoneCount; i++)
            {
                expectedAgreement += (predictedTotals[i] * referenceTotals[i]) / totalSquared;
            }
        }

        float denominator = 1f - expectedAgreement;
        report.kappaCoefficient = denominator > 0f ? (report.overallAccuracy - expectedAgreement) / denominator : 0f;

        for (int i = 0; i < (int)SimZone.NODATA; i++)
        {
            SimZone zone = (SimZone)i;
            int correct = confusionMatrix[i, i];
            int predicted = predictedTotals[i];
            int referenced = referenceTotals[i];

            report.simulatedCounts[zone] = predicted;
            report.referenceCounts[zone] = referenced;
            report.userAccuracy[zone] = predicted > 0 ? correct / (float)predicted : 0f;
            report.producerAccuracy[zone] = referenced > 0 ? correct / (float)referenced : 0f;
        }

        return report;
    }

    public static ZoneCategory GetCategory(SimZone zone)
    {
        switch (zone)
        {
            case SimZone.URBAN_DENSE:
            case SimZone.URBAN_RESIDENTIAL:
            case SimZone.COMMERCIAL:
            case SimZone.INDUSTRIAL:
                return ZoneCategory.Urban;
            case SimZone.TRANSPORT_INFRA:
                return ZoneCategory.Infrastructure;
            case SimZone.GREEN_PUBLIC:
            case SimZone.FOREST:
            case SimZone.SHRUBLAND:
                return ZoneCategory.GreenNatural;
            case SimZone.AGRICULTURE:
                return ZoneCategory.Agriculture;
            case SimZone.WATER:
                return ZoneCategory.Water;
            case SimZone.BARE_OTHER:
                return ZoneCategory.BareOther;
            default:
                return ZoneCategory.Unknown;
        }
    }
}
