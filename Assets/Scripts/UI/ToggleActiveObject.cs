using UnityEngine;

public class ToggleActiveObject : MonoBehaviour
{
    //[SerializeField] private GameObject Object;

    public void ActivateObject(GameObject gObject)
    {
        gObject.SetActive(true);
    }

    public void DisableObject(GameObject gObject)
    {
        gObject.SetActive(false);
    }

    public void ToggleObject(GameObject gObject)
    {
        gObject.SetActive(!gObject.activeSelf);
    }
}