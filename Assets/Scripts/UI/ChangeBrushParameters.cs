using UnityEngine;

public class ChangeBrushParameters : MonoBehaviour
{
    //[SerializeField] private GameObject brush;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float thickness;
    [SerializeField] private string colorName;
    [SerializeField] private string colorHexCode;

    public void OnAwake()
    {
    }
    public void ChangeThickness()
    {
        Draw.currentBrushThickness = thickness;
        //Draw.currentLineRenderer.SetWidth(thickness, thickness);
    }

    public void ChangeColor()
    {
        Draw.currentBrushcolorHexCode = colorHexCode;
    }
}
