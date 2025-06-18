using UnityEngine;
using UnityEngine.EventSystems;

public class Draw : MonoBehaviour
{
    // alter using event\action instead of public static!!!
    public static float currentBrushThickness = 0.08f;
    public static string currentBrushcolorHexCode = "#000000";
    
    [SerializeField] Camera m_camera;
    [SerializeField] GameObject brush;
    [SerializeField] TattooMachine tattooMachine;

    //[SerializeField] private LineRenderer lineRenderer;

    public static LineRenderer currentLineRenderer = null;

    Vector2 lastPos;


    private void Update()
    {
        Drawing();
    }

    void Drawing()
    {
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began && !EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) //Start drawing
        {
            CreateBrush();
        }
        else if (Input.touchCount == 1)
        {
            PointToTouchPos();
        }
        else
        {
            currentLineRenderer = null;
        }
    }

    void CreateBrush()
    {   
        GameObject brushInstance = Instantiate(brush);
        currentLineRenderer = brushInstance.GetComponent <LineRenderer>();
        
        //Setting the brush color and thickness
        currentLineRenderer.SetWidth(currentBrushThickness, currentBrushThickness);
        
        if (ColorUtility.TryParseHtmlString(currentBrushcolorHexCode, out Color newColor))
        {
            currentLineRenderer.startColor = newColor;
            currentLineRenderer.endColor = newColor;
        }
        else
        {
            Debug.LogError("Invalid hex color code");
        }
        
        Vector2 mousePos = m_camera.ScreenToWorldPoint(Input.mousePosition);

        currentLineRenderer.SetPosition(0, mousePos);
        currentLineRenderer.SetPosition(1, mousePos);
        
         
        /*
        GameObject brushInstance = Instantiate(brush);
        currentLineRenderer = brushInstance.GetComponent<LineRenderer>();

        Vector2 touchPosition = m_camera.ScreenToWorldPoint(Input.GetTouch(0).position);

        currentLineRenderer.SetPosition(0, touchPosition);
        currentLineRenderer.SetPosition(1, touchPosition);
        */
    }

    void AddAPoint(Vector2 pointPos)
    {
        currentLineRenderer.positionCount++;
        int positionIndex = currentLineRenderer.positionCount - 1;
        currentLineRenderer.SetPosition(positionIndex, pointPos);

        /*
        currentLineRenderer.positionCount++;
        int positionIndex = currentLineRenderer.positionCount - 1;
        currentLineRenderer.SetPosition(positionIndex, pointPos);
        */
    }

    void PointToTouchPos()
    {
        
        Vector2 mousePos = m_camera.ScreenToWorldPoint(Input.mousePosition);
        if (lastPos != mousePos)
        {
            AddAPoint(mousePos);
            lastPos = mousePos;

            tattooMachine.MoveTattooMachine(mousePos);
        }

        /*
        Vector2 touchPosition = m_camera.ScreenToWorldPoint(Input.GetTouch(0).position);
        if (lastPos != touchPosition)
        {
            AddAPoint(touchPosition);
            lastPos = touchPosition;
        }
        */
    }

}