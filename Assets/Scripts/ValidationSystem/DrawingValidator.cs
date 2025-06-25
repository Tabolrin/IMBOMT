// using System.Collections;
// using System.IO;
// using UnityEngine;
//
// public class TattooValidator : MonoBehaviour
// {
//     [Header("Validation Settings")]
//     [SerializeField] private Texture2D referenceTattoo;
//     [SerializeField] private Camera validationCamera;
//     [SerializeField] private LineRenderer targetLineRenderer;
//     [SerializeField] private int renderTextureSize = 512;
//     [SerializeField] private float penaltyWeight = 0.5f;
//     
//     [Header("Android Optimization")]
//     [SerializeField] private bool useReducedQuality = true; // For mobile performance
//     [SerializeField] private int mobileTextureSize = 256; // Smaller for mobile
//     [SerializeField] private float validationCooldown = 0f; // Set to 0 for debugging
//     
//     [Header("Debug Options")]
//     [SerializeField] private bool saveDebugTextures = false; // Disabled by default
//     [SerializeField] private bool showRealTimeValidation = false;
//     
//     [Header("Current Results")]
//     [SerializeField] private ValidationResult currentResult;
//     
//     private RenderTexture playerDrawingTexture;
//     private bool isInitialized = false;
//     private float lastValidationTime = 0f;
//     private int actualTextureSize;
//     private Camera drawingCamera; // Reference to synchronize with Draw's camera
//     
//     public System.Action<ValidationResult> OnValidationUpdate;
//     
//     void Start()
//     {
//         InitializeValidator();
//     }
//     
//     void InitializeValidator()
//     {
//         bool hasError = false;
//         if (referenceTattoo == null)
//         {
//             Debug.LogError("Reference tattoo texture is not assigned!");
//             hasError = true;
//         }
//         if (validationCamera == null)
//         {
//             Debug.LogError("Validation camera is not assigned!");
//             hasError = true;
//         }
//         if (targetLineRenderer == null)
//         {
//             targetLineRenderer = FindObjectOfType<LineRenderer>();
//             if (targetLineRenderer == null)
//             {
//                 Debug.LogError("No LineRenderer found in scene!");
//                 hasError = true;
//             }
//         }
//         if (hasError)
//         {
//             isInitialized = false;
//             return;
//         }
//         
//         // Find drawing camera for synchronization
//         drawingCamera = Camera.main;
//         if (drawingCamera == null)
//         {
//             Debug.LogError("Main Camera not found for synchronization!");
//             isInitialized = false;
//             return;
//         }
//         
//         actualTextureSize = Application.platform == RuntimePlatform.Android && useReducedQuality 
//             ? mobileTextureSize : renderTextureSize;
//             
//         SetupRenderTextures();
//         ConfigureValidationCamera();
//         isInitialized = true;
//         
//         Debug.Log($"Tattoo Validator initialized for {Application.platform} with texture size: {actualTextureSize}");
//     }
//     
//     void SetupRenderTextures()
//     {
//         RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB565) 
//             ? RenderTextureFormat.RGB565 : RenderTextureFormat.Default;
//             
//         playerDrawingTexture = new RenderTexture(actualTextureSize, actualTextureSize, 0, format);
//         playerDrawingTexture.name = "PlayerDrawingTexture";
//         playerDrawingTexture.filterMode = FilterMode.Point;
//         playerDrawingTexture.useMipMap = false;
//         
//         Debug.Log($"Render texture created: {actualTextureSize}x{actualTextureSize}, Format: {format}");
//     }
//     
//     void ConfigureValidationCamera()
//     {
//         validationCamera.targetTexture = playerDrawingTexture;
//         validationCamera.backgroundColor = Color.black;
//         validationCamera.clearFlags = CameraClearFlags.SolidColor;
//         
//         // Synchronize with drawing camera
//         validationCamera.orthographic = drawingCamera.orthographic;
//         validationCamera.orthographicSize = drawingCamera.orthographicSize;
//         validationCamera.transform.position = drawingCamera.transform.position;
//         validationCamera.transform.rotation = drawingCamera.transform.rotation;
//         
//         int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
//         if (lineRendererLayer == -1)
//         {
//             Debug.LogError("LineRenderer layer not found! Please create it in Tags and Layers.");
//             isInitialized = false;
//             return;
//         }
//         validationCamera.cullingMask = 1 << lineRendererLayer;
//         
//         if (Application.platform == RuntimePlatform.Android)
//         {
//             validationCamera.allowMSAA = false;
//             validationCamera.allowHDR = false;
//         }
//         
//         Debug.Log($"Validation camera configured: Position: {validationCamera.transform.position}, Orthographic Size: {validationCamera.orthographicSize}, Culling Mask: LineRenderer only, Clear Flags: {validationCamera.clearFlags}, Background: {validationCamera.backgroundColor}");
//     }
//     
//     void Update()
//     {
//         if (!isInitialized) return;
//         
//         if (showRealTimeValidation && targetLineRenderer.positionCount > 1)
//         {
//             if (Time.time - lastValidationTime >= validationCooldown)
//             {
//                 ValidateCurrentDrawing();
//             }
//         }
//     }
//     
//     public ValidationResult ValidateCurrentDrawing()
//     {
//         if (!isInitialized)
//         {
//             Debug.LogWarning("Validator not initialized!");
//             return new ValidationResult();
//         }
//         
//         if (Time.time - lastValidationTime < validationCooldown)
//             return currentResult;
//             
//         lastValidationTime = Time.time;
//         
//         Debug.Log($"Validating drawing with {targetLineRenderer.positionCount} points");
//         RenderPlayerDrawing();
//         currentResult = CompareWithReference();
//         OnValidationUpdate?.Invoke(currentResult);
//         
//         Debug.Log($"Validation completed: {GetDetailedResults()}");
//         return currentResult;
//     }
//     
//     void RenderPlayerDrawing()
//     {
//         RenderTexture.active = playerDrawingTexture;
//         GL.Clear(true, true, Color.black);
//         RenderTexture.active = null;
//         validationCamera.Render();
//         Debug.Log("Rendered player drawing to RenderTexture");
//     }
//     
//     ValidationResult CompareWithReference()
//     {
//         Texture2D playerTexture = RenderTextureToTexture2D(playerDrawingTexture);
//         
//         int correctPixels = 0;
//         int incorrectPixels = 0;
//         int totalReferencePixels = 0;
//         int totalPlayerPixels = 0;
//         
//         int sampleStep = actualTextureSize > 512 ? 2 : 1; // Adjusted for accuracy
//         
//         for (int x = 0; x < actualTextureSize; x += sampleStep)
//         {
//             for (int y = 0; y < actualTextureSize; y += sampleStep)
//             {
//                 float refU = (float)x / actualTextureSize;
//                 float refV = (float)y / actualTextureSize;
//                 Color refPixel = referenceTattoo.GetPixelBilinear(refU, refV);
//                 bool isReferencePath = refPixel.grayscale > 0.5f;
//                 
//                 Color playerPixel = playerTexture.GetPixel(x, y);
//                 bool isPlayerDrawn = playerPixel.grayscale > 0.1f;
//                 
//                 int pixelWeight = sampleStep * sampleStep;
//                 
//                 if (isReferencePath)
//                     totalReferencePixels += pixelWeight;
//                 
//                 if (isPlayerDrawn)
//                 {
//                     totalPlayerPixels += pixelWeight;
//                     if (isReferencePath)
//                         correctPixels += pixelWeight;
//                     else
//                         incorrectPixels += pixelWeight;
//                 }
//             }
//         }
//         
//         float coverage = totalReferencePixels > 0 ? (float)correctPixels / totalReferencePixels : 0f;
//         float accuracy = totalPlayerPixels > 0 ? (float)correctPixels / totalPlayerPixels : 0f;
//         float outsidePenalty = totalReferencePixels > 0 ? (float)incorrectPixels / totalReferencePixels : 0f;
//         float finalScore = Mathf.Clamp01(coverage - (outsidePenalty * penaltyWeight));
//         
//         if (saveDebugTextures && Application.platform != RuntimePlatform.Android)
//         {
//             StartCoroutine(SaveDebugTextures(playerTexture));
//         }
//         else
//         {
//             DestroyImmediate(playerTexture);
//         }
//         
//         return new ValidationResult
//         {
//             coverage = coverage,
//             accuracy = accuracy,
//             outsidePenalty = outsidePenalty,
//             finalScore = finalScore,
//             correctPixels = correctPixels,
//             incorrectPixels = incorrectPixels,
//             totalReferencePixels = totalReferencePixels
//         };
//     }
//     
//     Texture2D RenderTextureToTexture2D(RenderTexture renderTexture)
//     {
//         RenderTexture.active = renderTexture;
//         TextureFormat format = Application.platform == RuntimePlatform.Android 
//             ? TextureFormat.RGB565 : TextureFormat.RGB24;
//         Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, format, false);
//         texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
//         texture2D.Apply();
//         RenderTexture.active = null;
//         Debug.Log("Converted RenderTexture to Texture2D");
//         return texture2D;
//     }
//     
//     IEnumerator SaveDebugTextures(Texture2D playerTexture)
//     {
//         yield return new WaitForEndOfFrame();
//         
//         string folderPath = Path.Combine(Application.persistentDataPath, "TattooDebug");
//         if (!Directory.Exists(folderPath))
//             Directory.CreateDirectory(folderPath);
//         
//         byte[] playerBytes = playerTexture.EncodeToPNG();
//         string playerPath = Path.Combine(folderPath, $"player_drawing_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
//         File.WriteAllBytes(playerPath, playerBytes);
//         
//         Debug.Log($"Debug texture saved to: {playerPath}");
//         DestroyImmediate(playerTexture);
//     }
//     
//     public float GetCoveragePercentage() => currentResult.coverage * 100f;
//     public float GetAccuracyPercentage() => currentResult.accuracy * 100f;
//     public float GetFinalScore() => currentResult.finalScore;
//     
//     public string GetDetailedResults()
//     {
//         return $"Coverage: {GetCoveragePercentage():F1}% | " +
//                $"Accuracy: {GetAccuracyPercentage():F1}% | " +
//                $"Outside: {currentResult.outsidePenalty * 100f:F1}% | " +
//                $"Score: {currentResult.finalScore * 100f:F1}%";
//     }
//     
//     void OnDestroy()
//     {
//         if (playerDrawingTexture != null)
//             playerDrawingTexture.Release();
//     }
//     
//     #if UNITY_EDITOR
//     [ContextMenu("Validate Now")]
//     void ValidateNow()
//     {
//         if (Application.isPlaying)
//         {
//             ValidationResult result = ValidateCurrentDrawing();
//             Debug.Log($"Validation Result: {GetDetailedResults()}");
//         }
//     }
//     #endif
// }