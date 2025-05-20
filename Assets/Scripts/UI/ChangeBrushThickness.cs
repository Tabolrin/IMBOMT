using UnityEngine;

public class ChangeBrushThickness : MonoBehaviour
{
    //[SerializeField] private GameObject brush;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float thickness;

    public void OnAwake()
    {
    }
    public void ChangeThickness()
    {
        lineRenderer.SetWidth(thickness, thickness);
    }
}
