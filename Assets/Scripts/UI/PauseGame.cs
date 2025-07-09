using UnityEngine;

public class PauseGame : MonoBehaviour
{
    bool isPaused = false;
    
    public void Pause()
    {
        isPaused = !isPaused;
        
        if (isPaused)
            Time.timeScale = 0;
        else
            Time.timeScale = 1;
    }
}
