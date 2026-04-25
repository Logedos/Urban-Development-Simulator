using UnityEngine;
using UnityEngine.UI;

public static class GridRuntimeBootstrap
{
    //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGridPresentation()
    {
        if (GridManager.Instance != null || Object.FindFirstObjectByType<GridManager>() != null)
        {
            return;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("SimulationCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
        }

        GameObject viewObject = new GameObject("GridView", typeof(RectTransform), typeof(RawImage), typeof(GridManager));
        RectTransform rectTransform = viewObject.GetComponent<RectTransform>();
        rectTransform.SetParent(canvas.transform, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        RawImage rawImage = viewObject.GetComponent<RawImage>();
        rawImage.color = Color.white;

        GridManager gridManager = viewObject.GetComponent<GridManager>();
        gridManager.targetImage = rawImage;
    }
}
