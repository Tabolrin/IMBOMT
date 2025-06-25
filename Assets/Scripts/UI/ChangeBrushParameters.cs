using DrawingSystem;
using UnityEngine;

public class ChangeBrushParameters : MonoBehaviour
{
    [SerializeField] private Draw drawScript; // Reference to Draw component
    [SerializeField] private float thickness;
    [SerializeField] private string colorHexCode;

    public void ChangeThickness()
    {
        Debug.Log("Changing brush thickness to: " + thickness);
        if (drawScript != null)
        {
            drawScript.brushThickness = thickness; // Update instance variable
            drawScript.ConfigureLineRenderer(); // Reapply settings
        }
    }

    public void ChangeColor()
    {
        Debug.Log("Changing brush color to: " + colorHexCode);
        if (drawScript != null)
        {
            drawScript.brushColorHexCode = colorHexCode;
            drawScript.ConfigureLineRenderer();
        }
    }
}