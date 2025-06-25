using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    public float delay;
    public int sceneTransitionDelay;

    public void ChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}