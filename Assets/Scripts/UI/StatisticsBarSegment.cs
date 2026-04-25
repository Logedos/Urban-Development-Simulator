using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(LayoutElement))]
public class StatisticsBarSegment : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private SimulationStatistics owner;
    private SimZone zone;
    private int cells;
    private float percent;

    public void Initialize(SimulationStatistics statisticsOwner)
    {
        owner = statisticsOwner;
    }

    public void SetData(SimZone segmentZone, int segmentCells, float segmentPercent)
    {
        zone = segmentZone;
        cells = segmentCells;
        percent = segmentPercent;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.ShowTooltip(zone, cells, percent, Input.mousePosition);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.HideTooltip();
        }
    }
}
