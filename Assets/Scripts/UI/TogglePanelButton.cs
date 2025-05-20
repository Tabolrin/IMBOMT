using UnityEngine;

public class TogglePanelButton : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    public void OpenSetPanel()
    {
        panel.SetActive(true);
    }

    public void CloseSetPanel()
    {
        panel.SetActive(false);
    }
}