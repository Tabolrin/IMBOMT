using TattooSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CustomerManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image customerImage;
    [SerializeField] private Image patternImage;
    
    [Header("Data")]
    [SerializeField] private SpriteContainer customerContainer;
    [SerializeField] private SpriteContainer patternContainer;
    
    [Header("Current Customer and Pattern")]
    [SerializeField] public Texture2D currentCustomerImage;
    [SerializeField] public Sprite currentPatternImage;
    
    private SpriteRenderer inGamePatternRenderer;
    private TattooDrawingSystem tattooDrawingSystem;
    
    void Awake() { DontDestroyOnLoad(gameObject); }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        StoreLobbyLogic();
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "StoreLobby")
        {
            StoreLobbyLogic();
        }
        else if (scene.name == "Game")
        {
            GameSceneLogic();
        }
    }

    private void StoreLobbyLogic()
    {
        if (customerImage != null && customerContainer != null)
        {
            customerImage.sprite = customerContainer.GetRandomSprite();
            currentCustomerImage = customerImage.sprite.texture;
        }

        if (patternImage != null && patternContainer != null)
        {
            patternImage.sprite = patternContainer.GetRandomSprite();
            currentPatternImage = patternImage.sprite;
        }
    }

    private void GameSceneLogic()
    {
        tattooDrawingSystem = FindFirstObjectByType<TattooDrawingSystem>();
        inGamePatternRenderer = GameObject.Find("Pattern").GetComponent<SpriteRenderer>();

        if (tattooDrawingSystem != null)
            tattooDrawingSystem.referenceTattoo = currentPatternImage.texture;
        else
        {
            Debug.LogWarning("No Tattoo Drawing System Found");
        }
            
        if (inGamePatternRenderer != null)
            inGamePatternRenderer.sprite = currentPatternImage;
        else
        {
            Debug.LogWarning("No Pattern Renderer Found");
        }
    }
}
