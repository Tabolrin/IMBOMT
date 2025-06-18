using System.Collections;
using System.IO;
using UnityEngine;

public class TattooValidator : MonoBehaviour
{
    [Header("Validation Settings")]
    [SerializeField] private Texture2D referenceTattoo;
    [SerializeField] private Camera validationCamera;
    [SerializeField] private LineRenderer targetLineRenderer;
    [SerializeField] private int renderTextureSize = 512;
    [SerializeField] private float penaltyWeight = 0.5f;
    
    [Header("Android Optimization")]
    [SerializeField] private bool useReducedQuality = true; // For mobile performance
    [SerializeField] private int mobileTextureSize = 256; // Smaller for mobile
    [SerializeField] private float validationCooldown = 0.2f; // Limit validation frequency
    
    [Header("Debug Options")]
    [SerializeField] private bool saveDebugTextures = false; // Disabled by default on mobile
    [SerializeField] private bool showRealTimeValidation = false;
    
    [Header("Current Results")]
    [SerializeField] private ValidationResult currentResult;
    
    private RenderTexture playerDrawingTexture;
    private bool isInitialized = false;
    private float lastValidationTime = 0f;
    private int actualTextureSize;
    
    // Events for UI updates
    public System.Action<ValidationResult> OnValidationUpdate;
    
    void Start()
    {
        InitializeValidator();
    }
    
    void InitializeValidator()
    {
        if (referenceTattoo == null)
        {
            Debug.LogError("Reference tattoo texture is not assigned!");
            return;
        }
        
        if (validationCamera == null)
        {
            Debug.LogError("Validation camera is not assigned!");
            return;
        }
        
        if (targetLineRenderer == null)
        {
            targetLineRenderer = FindObjectOfType<LineRenderer>();
            if (targetLineRenderer == null)
            {
                Debug.LogError("No LineRenderer found in scene!");
                return;
            }
        }
        
        // Use mobile-optimized settings on Android
        actualTextureSize = Application.platform == RuntimePlatform.Android && useReducedQuality 
            ? mobileTextureSize : renderTextureSize;
            
        SetupRenderTextures();
        ConfigureValidationCamera();
        isInitialized = true;
        
        Debug.Log($"Tattoo Validator initialized for {Application.platform} with texture size: {actualTextureSize}");
    }
    
    void SetupRenderTextures()
    {
        // Create render texture with mobile optimization
        RenderTextureFormat format = RenderTextureFormat.RGB565; // More efficient for mobile
        if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB565))
            format = RenderTextureFormat.RGB565;
        else
            format = RenderTextureFormat.Default;
            
        playerDrawingTexture = new RenderTexture(actualTextureSize, actualTextureSize, 0, format);
        playerDrawingTexture.name = "PlayerDrawingTexture";
        playerDrawingTexture.filterMode = FilterMode.Point;
        playerDrawingTexture.useMipMap = false; // Disable mipmaps for mobile
        
        Debug.Log($"Render texture created: {actualTextureSize}x{actualTextureSize}, Format: {format}");
    }
    
    void ConfigureValidationCamera()
    {
        validationCamera.targetTexture = playerDrawingTexture;
        validationCamera.backgroundColor = Color.black;
        validationCamera.clearFlags = CameraClearFlags.SolidColor;
        
        // Make sure camera only renders LineRenderer layer
        int lineRendererLayer = targetLineRenderer.gameObject.layer;
        validationCamera.cullingMask = 1 << lineRendererLayer;
        
        // Mobile optimization: reduce camera quality
        if (Application.platform == RuntimePlatform.Android)
        {
            validationCamera.allowMSAA = false;
            validationCamera.allowHDR = false;
        }
        
        Debug.Log($"Validation camera configured for layer: {lineRendererLayer}");
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        // Real-time validation with cooldown for mobile performance
        if (showRealTimeValidation && targetLineRenderer.positionCount > 1)
        {
            if (Time.time - lastValidationTime >= validationCooldown)
            {
                ValidateCurrentDrawing();
            }
        }
    }
    
    public ValidationResult ValidateCurrentDrawing()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Validator not initialized!");
            return new ValidationResult();
        }
        
        // Throttle validation calls for mobile performance
        if (Time.time - lastValidationTime < validationCooldown)
            return currentResult;
            
        lastValidationTime = Time.time;
        
        // Render current line renderer to texture
        RenderPlayerDrawing();
        
        // Compare with reference
        currentResult = CompareWithReference();
        
        // Trigger event for UI updates
        OnValidationUpdate?.Invoke(currentResult);
        
        return currentResult;
    }
    
    void RenderPlayerDrawing()
    {
        // Clear the render texture
        RenderTexture.active = playerDrawingTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = null;
        
        // Render the LineRenderer
        validationCamera.Render();
    }
    
    ValidationResult CompareWithReference()
    {
        // Convert render texture to readable Texture2D
        Texture2D playerTexture = RenderTextureToTexture2D(playerDrawingTexture);
        
        int correctPixels = 0;
        int incorrectPixels = 0;
        int totalReferencePixels = 0;
        int totalPlayerPixels = 0;
        
        // Mobile optimization: use sampling for large textures
        int sampleStep = actualTextureSize > 256 ? 2 : 1; // Skip pixels for performance
        
        for (int x = 0; x < actualTextureSize; x += sampleStep)
        {
            for (int y = 0; y < actualTextureSize; y += sampleStep)
            {
                // Sample reference texture
                float refU = (float)x / actualTextureSize;
                float refV = (float)y / actualTextureSize;
                Color refPixel = referenceTattoo.GetPixelBilinear(refU, refV);
                bool isReferencePath = refPixel.grayscale > 0.5f;
                
                // Sample player texture
                Color playerPixel = playerTexture.GetPixel(x, y);
                bool isPlayerDrawn = playerPixel.grayscale > 0.1f;
                
                // Count pixels (adjust for sampling)
                int pixelWeight = sampleStep * sampleStep;
                
                if (isReferencePath)
                    totalReferencePixels += pixelWeight;
                
                if (isPlayerDrawn)
                {
                    totalPlayerPixels += pixelWeight;
                    if (isReferencePath)
                        correctPixels += pixelWeight;
                    else
                        incorrectPixels += pixelWeight;
                }
            }
        }
        
        // Calculate metrics
        float coverage = totalReferencePixels > 0 ? (float)correctPixels / totalReferencePixels : 0f;
        float accuracy = totalPlayerPixels > 0 ? (float)correctPixels / totalPlayerPixels : 0f;
        float outsidePenalty = totalReferencePixels > 0 ? (float)incorrectPixels / totalReferencePixels : 0f;
        float finalScore = Mathf.Clamp01(coverage - (outsidePenalty * penaltyWeight));
        
        // Save debug texture only if enabled and not on mobile (to save storage)
        if (saveDebugTextures && Application.platform != RuntimePlatform.Android)
        {
            StartCoroutine(SaveDebugTextures(playerTexture));
        }
        
        // Clean up
        DestroyImmediate(playerTexture);
        
        return new ValidationResult
        {
            coverage = coverage,
            accuracy = accuracy,
            outsidePenalty = outsidePenalty,
            finalScore = finalScore,
            correctPixels = correctPixels,
            incorrectPixels = incorrectPixels,
            totalReferencePixels = totalReferencePixels
        };
    }
    
    Texture2D RenderTextureToTexture2D(RenderTexture renderTexture)
    {
        RenderTexture.active = renderTexture;
        
        // Use more efficient format for mobile
        TextureFormat format = Application.platform == RuntimePlatform.Android 
            ? TextureFormat.RGB565 : TextureFormat.RGB24;
            
        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, format, false);
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();
        RenderTexture.active = null;
        return texture2D;
    }
    
    IEnumerator SaveDebugTextures(Texture2D playerTexture)
    {
        yield return new WaitForEndOfFrame();
        
        string folderPath = Path.Combine(Application.persistentDataPath, "TattooDebug");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);
        
        byte[] playerBytes = playerTexture.EncodeToPNG();
        string playerPath = Path.Combine(folderPath, $"player_drawing_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
        File.WriteAllBytes(playerPath, playerBytes);
        
        Debug.Log($"Debug texture saved to: {playerPath}");
    }
    
    // Public methods for external use
    public float GetCoveragePercentage() => currentResult.coverage * 100f;
    public float GetAccuracyPercentage() => currentResult.accuracy * 100f;
    public float GetFinalScore() => currentResult.finalScore;
    
    public string GetDetailedResults()
    {
        return $"Coverage: {GetCoveragePercentage():F1}% | " +
               $"Accuracy: {GetAccuracyPercentage():F1}% | " +
               $"Outside: {currentResult.outsidePenalty * 100f:F1}% | " +
               $"Score: {currentResult.finalScore * 100f:F1}%";
    }
    
    void OnDestroy()
    {
        if (playerDrawingTexture != null)
            playerDrawingTexture.Release();
    }
    
    #if UNITY_EDITOR
    [ContextMenu("Validate Now")]
    void ValidateNow()
    {
        if (Application.isPlaying)
        {
            ValidationResult result = ValidateCurrentDrawing();
            Debug.Log($"Validation Result: {GetDetailedResults()}");
        }
    }
    #endif
}