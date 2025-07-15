using UnityEngine;

public class ToggleActiveObject : MonoBehaviour
{
    [SerializeField] private GameObject Object;

    public void ActivateObject()
    {
        Object.SetActive(true);
    }

    public void DisableObject()
    {
        Object.SetActive(false);
    }

    public void ToggleObject()
    {
        Object.SetActive(!Object.activeSelf);
    }
}