using UnityEngine;

public class SimpleLineRendererTest : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Camera mainCamera;
    private Vector3 lastWorldPosition;
    private bool isDrawing = false;

    void Start()
    {
        // Setup LineRenderer
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = 0.15f;
        lineRenderer.endWidth = 0.15f;
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;

        // Set layer
        int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
        if (lineRendererLayer == -1)
        {
            Debug.LogError("LineRenderer layer not found!");
            return;
        }
        gameObject.layer = lineRendererLayer;

        // Setup camera
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not found!");
            return;
        }

        Debug.Log("SimpleLineRendererTest initialized");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDrawing = true;
            lineRenderer.positionCount = 0;
            Vector2 mousePos = Input.mousePosition;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 10f));
            worldPos.z = 0;
            lineRenderer.positionCount = 1;
            lineRenderer.SetPosition(0, worldPos);
            lastWorldPosition = worldPos;
            Debug.Log($"Started drawing at: {worldPos}");
        }
        else if (Input.GetMouseButton(0) && isDrawing)
        {
            Vector2 mousePos = Input.mousePosition;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 10f));
            worldPos.z = 0;
            if (Vector3.Distance(worldPos, lastWorldPosition) >= 0.01f)
            {
                lineRenderer.positionCount++;
                lineRenderer.SetPosition(lineRenderer.positionCount - 1, worldPos);
                lastWorldPosition = worldPos;
                Debug.Log($"Added point at: {worldPos}, Points: {lineRenderer.positionCount}");
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDrawing = false;
            Debug.Log($"Stopped drawing. Total points: {lineRenderer.positionCount}");
        }
    }
}