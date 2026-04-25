using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class GridZoomPan : MonoBehaviour
{
    public float zoomSpeed = 0.15f;
    public float minScale = 0.5f;
    public float maxScale = 5f;
    public float panSpeed = 1f;

    private RectTransform rectTransform;
    private Vector2 lastMousePosition;
    private bool isDragging;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        HandleZoom();
        HandlePan();
    }

    private void HandleZoom()
    {
        float scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scrollDelta, 0f))
        {
            return;
        }

        float currentScale = rectTransform.localScale.x;
        currentScale += scrollDelta * zoomSpeed;
        currentScale = Mathf.Clamp(currentScale, minScale, maxScale);
        rectTransform.localScale = new Vector3(currentScale, currentScale, 1f);
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            lastMousePosition = Input.mousePosition;
            return;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            return;
        }

        if (!isDragging)
        {
            return;
        }

        Vector2 currentMousePosition = Input.mousePosition;
        Vector2 delta = (currentMousePosition - lastMousePosition) * panSpeed;
        rectTransform.anchoredPosition += delta;
        lastMousePosition = currentMousePosition;
    }
}
