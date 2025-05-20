using UnityEngine;

public class PauseGame : MonoBehaviour
{
    public void Pause(bool pause)
    {
        if (pause)
            Time.timeScale = 0;
        else
            Time.timeScale = 1;
    }
}
