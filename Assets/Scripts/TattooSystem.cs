/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace TattooSystem
{
    public class TattooDrawingSystem : MonoBehaviour
    {
        public enum BrushSize
        {
            Small = 0,    // 0.05f
            Medium = 1,   // 0.15f
            Large = 2,    // 0.3f
            ExtraLarge = 3 // 0.5f
        }

        private enum DrawingState
        {
            Idle,
            Drawing
        }

        [Header("Drawing Settings")]
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Transform linesParent;
        [SerializeField] private BrushSize brushSize = BrushSize.Medium;
        [SerializeField] private string brushColorHexCode = "#FFFFFF";
        [SerializeField] private float minDistanceThreshold = 0.005f;
        [SerializeField] private Camera drawingCamera;
        [SerializeField] private TattooMachine tattooMachine;

        [Header("Line Renderer Settings")]
        [SerializeField] private int lineCapVertices = 10;
        [SerializeField] private int lineCornerVertices = 10;
        [SerializeField] private LineTextureMode textureMode = LineTextureMode.Tile;
        [SerializeField] private LineAlignment alignment = LineAlignment.View;

        [Header("Mobile Optimization")]
        [SerializeField] private int maxPointsPerLine = 1000;
        [SerializeField] private bool optimizeForMobile = true;
        [SerializeField] private float touchSensitivity = 1f;
        [SerializeField] private float inputDebounceTime = 0.05f;

        [Header("Validation Settings")]
        [SerializeField] private Texture2D referenceTattoo;
        [SerializeField] private Camera validationCamera;
        [SerializeField] private int renderTextureSize = 512;
        [SerializeField] private float shapeWeight = 0.8f;
        [SerializeField] private float colorWeight = 0.2f;
        [SerializeField] private float penaltyWeight = 0.5f;
        [SerializeField] private bool useReducedQuality = true;
        [SerializeField] private int mobileTextureSize = 256;

        [Header("Debug Options")]
        [SerializeField] private bool saveDebugTextures = false;
        [SerializeField] private bool showRealTimeValidation = false;
        [SerializeField] private ValidationResult currentResult;

        // Drawing state
        private LineRenderer currentLine;
        private List<Vector3> currentLinePoints = new List<Vector3>();
        private List<LineRenderer> allLines = new List<LineRenderer>();
        private DrawingState drawingState = DrawingState.Idle;
        private Vector3 lastWorldPosition;
        private Color brushColor = Color.white;
        private RenderTexture playerDrawingTexture;
        private RenderTexture referenceTexture;
        private bool isValidatorInitialized = false;
        private readonly float[] brushSizeValues = { 0.05f, 0.15f, 0.3f, 0.5f };
        private int actualTextureSize;
        private float lastInputTime;

        public System.Action<ValidationResult> OnValidationUpdate;

        void Start()
        {
            InitializeComponents();
            InitializeValidator();
            ParseBrushColor();
        }

        void InitializeComponents()
        {
            if (drawingCamera == null)
                drawingCamera = Camera.main;

            if (linesParent == null)
            {
                GameObject parent = new GameObject("Lines Parent");
                linesParent = parent.transform;
            }

            int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
            if (lineRendererLayer == -1)
            {
                Debug.LogError("LineRenderer layer not found! Please create a layer named 'LineRenderer'.");
                return;
            }

            if ((drawingCamera.cullingMask & (1 << lineRendererLayer)) == 0)
                drawingCamera.cullingMask |= 1 << lineRendererLayer;

            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
                lineMaterial.color = Color.white;
            }
        }

        void InitializeValidator()
        {
            if (referenceTattoo == null || validationCamera == null)
            {
                Debug.LogWarning("Reference tattoo or validation camera not assigned. Validation disabled.");
                return;
            }

            actualTextureSize = Application.platform == RuntimePlatform.Android && useReducedQuality 
                ? mobileTextureSize : renderTextureSize;

            SetupRenderTextures();
            ConfigureValidationCamera();
            isValidatorInitialized = true;

            Debug.Log($"Tattoo Validator initialized for {Application.platform} with texture size: {actualTextureSize}");
        }

        void SetupRenderTextures()
        {
            RenderTextureFormat shapeFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32) 
                ? RenderTextureFormat.ARGB32 : RenderTextureFormat.Default;
            RenderTextureFormat colorFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32) 
                ? RenderTextureFormat.ARGB32 : RenderTextureFormat.Default;

            playerDrawingTexture = new RenderTexture(actualTextureSize, actualTextureSize, 24, colorFormat); // Changed from 0 to 24 for depth buffer
            playerDrawingTexture.name = "PlayerDrawingTexture";
            playerDrawingTexture.filterMode = FilterMode.Point;
            playerDrawingTexture.useMipMap = false;

            referenceTexture = new RenderTexture(actualTextureSize, actualTextureSize, 0, colorFormat); // Reference texture doesn't need depth
            referenceTexture.name = "ReferenceTexture";
            referenceTexture.filterMode = FilterMode.Point;
            referenceTexture.useMipMap = false;
        }

        void ConfigureValidationCamera()
        {
            validationCamera.targetTexture = playerDrawingTexture;
            validationCamera.backgroundColor = Color.black;
            validationCamera.clearFlags = CameraClearFlags.SolidColor;

            validationCamera.orthographic = drawingCamera.orthographic;
            validationCamera.orthographicSize = drawingCamera.orthographicSize;
            validationCamera.transform.position = drawingCamera.transform.position;
            validationCamera.transform.rotation = drawingCamera.transform.rotation;

            int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
            validationCamera.cullingMask = 1 << lineRendererLayer;

            if (Application.platform == RuntimePlatform.Android)
            {
                validationCamera.allowMSAA = false;
                validationCamera.allowHDR = false;
            }
        }

        void ParseBrushColor()
        {
            if (ColorUtility.TryParseHtmlString(brushColorHexCode, out Color newColor))
            {
                brushColor = newColor;
            }
            else
            {
                brushColor = Color.white;
                Debug.LogWarning($"Invalid color hex code: {brushColorHexCode}. Using white.");
            }
        }

        void Update()
        {
            bool isInputDown = false;
            Vector2 inputPosition = Vector2.zero;

            if (Application.isEditor)
            {
                isInputDown = Input.GetMouseButton(0);
                inputPosition = Input.mousePosition;
            }
            else if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                isInputDown = touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
                inputPosition = touch.position;
            }

            if (Time.time - lastInputTime < inputDebounceTime)
                return;

            if (isInputDown && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                if (drawingState == DrawingState.Idle)
                {
                    StartDrawing(inputPosition);
                    drawingState = DrawingState.Drawing;
                    lastInputTime = Time.time;
                }
                else if (drawingState == DrawingState.Drawing)
                {
                    ContinueDrawing(inputPosition);
                }
            }
            else if (drawingState == DrawingState.Drawing)
            {
                FinalizeLine();
                drawingState = DrawingState.Idle;
                lastInputTime = Time.time;
            }
        }

        void StartDrawing(Vector2 screenPosition)
        {
            Vector3 worldPos = ScreenToWorldPosition(screenPosition);
            if (worldPos == Vector3.zero) return;

            if (currentLine != null)
            {
                if (currentLine.positionCount > 0)
                    allLines.Add(currentLine);
                else
                    Destroy(currentLine.gameObject);
                currentLine = null;
            }
            currentLinePoints.Clear();

            GameObject lineObj = new GameObject("TattooLine");
            lineObj.transform.SetParent(linesParent);
            lineObj.layer = LayerMask.NameToLayer("LineRenderer");

            currentLine = lineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(currentLine);

            currentLinePoints.Add(worldPos);
            lastWorldPosition = worldPos;

            UpdateLineRenderer();

            if (tattooMachine != null)
                tattooMachine.MoveTattooMachine(worldPos);
        }

        void ContinueDrawing(Vector2 screenPosition)
        {
            if (drawingState != DrawingState.Drawing || currentLine == null)
                return;

            Vector3 worldPos = ScreenToWorldPosition(screenPosition);
            if (worldPos == Vector3.zero) return;

            if (currentLinePoints.Count >= maxPointsPerLine)
            {
                FinalizeLine();
                StartDrawing(screenPosition);
                drawingState = DrawingState.Drawing;
                return;
            }

            if (Vector3.Distance(worldPos, lastWorldPosition) >= minDistanceThreshold)
            {
                currentLinePoints.Add(worldPos);
                lastWorldPosition = worldPos;
                UpdateLineRenderer();

                if (tattooMachine != null)
                    tattooMachine.MoveTattooMachine(worldPos);

                if (showRealTimeValidation && currentLinePoints.Count > 1)
                    ValidateDrawing();
            }
        }

        void FinalizeLine()
        {
            if (currentLine != null && currentLinePoints.Count > 0)
            {
                allLines.Add(currentLine);
            }
            else if (currentLine != null)
            {
                Destroy(currentLine.gameObject);
            }

            currentLine = null;
            currentLinePoints.Clear();

            if (tattooMachine != null)
                tattooMachine.HideTattooMachine();
        }

        Vector3 ScreenToWorldPosition(Vector2 screenPos)
        {
            if (drawingCamera == null) return Vector3.zero;

            Vector3 worldPos = drawingCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -drawingCamera.transform.position.z));
            worldPos.z = 0;
            return worldPos;
        }

        void UpdateLineRenderer()
        {
            if (currentLine == null || currentLinePoints.Count == 0) return;

            currentLine.positionCount = currentLinePoints.Count;
            for (int i = 0; i < currentLinePoints.Count; i++)
            {
                currentLine.SetPosition(i, currentLinePoints[i]);
            }
        }

        void ConfigureLineRenderer(LineRenderer lr)
        {
            lr.useWorldSpace = true;
            float thickness = brushSizeValues[(int)brushSize];
            lr.startWidth = thickness;
            lr.endWidth = thickness;
            lr.startColor = brushColor;
            lr.endColor = brushColor;
            lr.material = lineMaterial;
            lr.sortingOrder = 10;

            lr.numCapVertices = lineCapVertices;
            lr.numCornerVertices = lineCornerVertices;
            lr.textureMode = textureMode;
            lr.alignment = alignment;

            lr.generateLightingData = false;
            lr.receiveShadows = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void TriggerValidation()
        {
            if (isValidatorInitialized)
            {
                ValidationResult result = ValidateDrawing();
                Debug.Log($"Validation triggered: Success {result.GetFinalScorePercentage():F1}% (Shape: {result.shapeScore * 100:F1}%, Color: {result.colorScore * 100:F1}%)");
            }
            else
            {
                Debug.LogWarning("Validation not initialized!");
            }
        }

        public ValidationResult ValidateDrawing()
        {
            if (!isValidatorInitialized)
            {
                Debug.LogWarning("Validator not initialized!");
                return new ValidationResult();
            }

            RenderPlayerDrawing();
            RenderReferenceTexture();
            currentResult = CompareWithReference();
            OnValidationUpdate?.Invoke(currentResult);

            Debug.Log($"Validation completed: Success {currentResult.GetFinalScorePercentage():F1}% (Shape: {currentResult.shapeScore * 100:F1}%, Color: {currentResult.colorScore * 100:F1}%)");
            return currentResult;
        }

        void RenderPlayerDrawing()
        {
            RenderTexture.active = playerDrawingTexture;
            GL.Clear(true, true, Color.black);
            validationCamera.Render();
            RenderTexture.active = null;
        }

        void RenderReferenceTexture()
        {
            RenderTexture.active = referenceTexture;
            GL.Clear(true, true, Color.black);
            Graphics.Blit(referenceTattoo, referenceTexture);
            RenderTexture.active = null;
        }

        ValidationResult CompareWithReference()
        {
            Texture2D playerTexture = RenderTextureToTexture2D(playerDrawingTexture, TextureFormat.ARGB32);
            Texture2D refTexture = RenderTextureToTexture2D(referenceTexture, TextureFormat.ARGB32);

            int correctShapePixels = 0;
            int incorrectShapePixels = 0;
            int totalReferenceShapePixels = 0;
            int totalPlayerShapePixels = 0;
            float colorSimilaritySum = 0f;
            int coloredPixelCount = 0;

            int sampleStep = actualTextureSize > 512 ? 2 : 1;

            for (int x = 0; x < actualTextureSize; x += sampleStep)
            {
                for (int y = 0; y < actualTextureSize; y += sampleStep)
                {
                    Color refPixel = refTexture.GetPixel(x, y);
                    bool isReferencePath = refPixel.a > 0.5f; // Use alpha for shape detection

                    Color playerPixel = playerTexture.GetPixel(x, y);
                    bool isPlayerDrawn = playerPixel.a > 0.1f;

                    int pixelWeight = sampleStep * sampleStep;

                    if (isReferencePath)
                        totalReferenceShapePixels += pixelWeight;

                    if (isPlayerDrawn)
                    {
                        totalPlayerShapePixels += pixelWeight;
                        if (isReferencePath)
                        {
                            correctShapePixels += pixelWeight;
                            // Compare colors only where shapes overlap and reference is not transparent
                            if (refPixel.a > 0.5f && playerPixel.a > 0.1f)
                            {
                                float colorDistance = ColorDistance(refPixel, playerPixel);
                                colorSimilaritySum += (1f - colorDistance) * pixelWeight;
                                coloredPixelCount += pixelWeight;
                            }
                        }
                        else
                        {
                            incorrectShapePixels += pixelWeight;
                        }
                    }
                }
            }

            float shapeCoverage = totalReferenceShapePixels > 0 ? (float)correctShapePixels / totalReferenceShapePixels : 0f;
            float shapeAccuracy = totalPlayerShapePixels > 0 ? (float)correctShapePixels / totalPlayerShapePixels : 0f;
            float shapePenalty = totalReferenceShapePixels > 0 ? (float)incorrectShapePixels / totalReferenceShapePixels : 0f;
            float shapeScore = Mathf.Clamp01(shapeCoverage - (shapePenalty * penaltyWeight));

            // Explicitly set colorScore to 0 if no pixels are drawn
            float colorScore = totalPlayerShapePixels > 0 && coloredPixelCount > 0 ? colorSimilaritySum / coloredPixelCount : 0f;
            float finalScore = shapeWeight * shapeScore + colorWeight * colorScore;

            if (saveDebugTextures && Application.platform != RuntimePlatform.Android)
            {
                StartCoroutine(SaveDebugTextures(playerTexture, refTexture));
            }
            else
            {
                DestroyImmediate(playerTexture);
                DestroyImmediate(refTexture);
            }

            return new ValidationResult
            {
                shapeScore = shapeScore,
                colorScore = colorScore,
                finalScore = finalScore,
                correctShapePixels = correctShapePixels,
                incorrectShapePixels = incorrectShapePixels,
                totalReferenceShapePixels = totalReferenceShapePixels,
                totalPlayerShapePixels = totalPlayerShapePixels
            };
        }

        float ColorDistance(Color c1, Color c2)
        {
            // Simple Euclidean distance in RGB space, normalized to [0,1]
            float rDiff = c1.r - c2.r;
            float gDiff = c1.g - c2.g;
            float bDiff = c1.b - c2.b;
            return Mathf.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff) / Mathf.Sqrt(3f);
        }

        Texture2D RenderTextureToTexture2D(RenderTexture renderTexture, TextureFormat format)
        {
            RenderTexture.active = renderTexture;
            Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, format, false);
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = null;
            return texture2D;
        }

        IEnumerator SaveDebugTextures(Texture2D playerTexture, Texture2D refTexture)
        {
            yield return new WaitForEndOfFrame();

            string folderPath = Path.Combine(Application.persistentDataPath, "TattooDebug");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            byte[] playerBytes = playerTexture.EncodeToPNG();
            string playerPath = Path.Combine(folderPath, $"player_drawing_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(playerPath, playerBytes);

            byte[] refBytes = refTexture.EncodeToPNG();
            string refPath = Path.Combine(folderPath, $"reference_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(refPath, refBytes);

            Debug.Log($"Debug textures saved to: {playerPath}, {refPath}");
            DestroyImmediate(playerTexture);
            DestroyImmediate(refTexture);
        }

        public void SetBrushSize(BrushSize size)
        {
            brushSize = size;
            if (currentLine != null)
            {
                float thickness = brushSizeValues[(int)brushSize];
                currentLine.startWidth = thickness;
                currentLine.endWidth = thickness;
            }
        }

        public void SetBrushColor(string hexColor)
        {
            brushColorHexCode = hexColor;
            ParseBrushColor();
            if (currentLine != null)
            {
                currentLine.startColor = brushColor;
                currentLine.endColor = brushColor;
            }
        }

        public void SetBrushColor(Color color)
        {
            brushColor = color;
            brushColorHexCode = "#" + ColorUtility.ToHtmlStringRGB(color);
            if (currentLine != null)
            {
                currentLine.startColor = brushColor;
                currentLine.endColor = brushColor;
            }
        }

        public void ClearDrawing()
        {
            foreach (var line in allLines)
            {
                if (line != null)
                    DestroyImmediate(line.gameObject);
            }
            allLines.Clear();
            currentLine = null;
            currentLinePoints.Clear();
            drawingState = DrawingState.Idle;
        }

        public int GetTotalPointCount()
        {
            int totalPoints = 0;
            foreach (var line in allLines)
            {
                if (line != null)
                    totalPoints += line.positionCount;
            }
            return totalPoints;
        }

        public float GetSuccessPercentage()
        {
            return currentResult.GetFinalScorePercentage();
        }

        public string GetFormattedResults()
        {
            return $"Success: {currentResult.GetFinalScorePercentage():F1}% (Shape: {currentResult.shapeScore * 100:F1}%, Color: {currentResult.colorScore * 100:F1}%)";
        }

        public string GetDetailedDebugInfo()
        {
            return currentResult.GetDetailedDebugInfo();
        }

        public ValidationQuality GetCurrentQuality()
        {
            return currentResult.GetQualityLevel();
        }

        public bool IsCurrentScorePassing(float threshold = 0.6f)
        {
            return currentResult.IsPassingScore(threshold);
        }

        void OnDestroy()
        {
            if (playerDrawingTexture != null)
                playerDrawingTexture.Release();
            if (referenceTexture != null)
                referenceTexture.Release();
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Now")]
        void ValidateNow()
        {
            if (Application.isPlaying)
            {
                TriggerValidation();
            }
        }

        [ContextMenu("Clear Drawing")]
        void ClearDrawingDebug()
        {
            if (Application.isPlaying)
                ClearDrawing();
        }
#endif
    }

    [System.Serializable]
    public class ValidationResult
    {
        public float shapeScore;
        public float colorScore;
        public float finalScore;
        public int correctShapePixels;
        public int incorrectShapePixels;
        public int totalReferenceShapePixels;
        public int totalPlayerShapePixels;

        public float GetFinalScorePercentage()
        {
            return finalScore * 100f;
        }

        public string GetFormattedResults()
        {
            return $"Success: {GetFinalScorePercentage():F1}% (Shape: {shapeScore * 100:F1}%, Color: {colorScore * 100:F1}%)";
        }

        public string GetDetailedDebugInfo()
        {
            return $"Shape Score: {shapeScore * 100:F1}%\n" +
                   $"Color Score: {colorScore * 100:F1}%\n" +
                   $"Final Score: {GetFinalScorePercentage():F1}%\n" +
                   $"Correct Shape Pixels: {correctShapePixels}\n" +
                   $"Incorrect Shape Pixels: {incorrectShapePixels}\n" +
                   $"Total Reference Shape Pixels: {totalReferenceShapePixels}\n" +
                   $"Total Player Shape Pixels: {totalPlayerShapePixels}";
        }

        public ValidationQuality GetQualityLevel()
        {
            float score = finalScore;
            if (score >= 0.9f) return ValidationQuality.Excellent;
            if (score >= 0.7f) return ValidationQuality.Good;
            if (score >= 0.5f) return ValidationQuality.Fair;
            return ValidationQuality.Poor;
        }

        public bool IsPassingScore(float threshold)
        {
            return finalScore >= threshold;
        }
    }

    public enum ValidationQuality
    {
        Poor,
        Fair,
        Good,
        Excellent
    }
}*/ // OLD -------------------------------- 1.0

/*
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace TattooSystem
{
    public class TattooDrawingSystem : MonoBehaviour
    {
        public enum BrushSize
        {
            Small = 0,    // 0.05f
            Medium = 1,   // 0.15f
            Large = 2,    // 0.3f
            ExtraLarge = 3 // 0.5f
        }

        private enum DrawingState
        {
            Idle,
            Drawing
        }

        [Header("Drawing Settings")]
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Transform linesParent;
        [SerializeField] private BrushSize brushSize = BrushSize.Medium;
        [SerializeField] private string brushColorHexCode = "#FFFFFF";
        [SerializeField] private float minDistanceThreshold = 0.005f;
        [SerializeField] private Camera drawingCamera;
        [SerializeField] private TattooMachine tattooMachine;

        [Header("Line Renderer Settings")]
        [SerializeField] private int lineCapVertices = 10;
        [SerializeField] private int lineCornerVertices = 10;
        [SerializeField] private LineTextureMode textureMode = LineTextureMode.Tile;
        [SerializeField] private LineAlignment alignment = LineAlignment.View;

        [Header("Mobile Optimization")]
        [SerializeField] private int maxPointsPerLine = 1000;
        [SerializeField] private bool optimizeForMobile = true;
        [SerializeField] private float touchSensitivity = 1f;
        [SerializeField] private float inputDebounceTime = 0.05f;

        [Header("Validation Settings")]
        [SerializeField] private Texture2D referenceTattoo;
        [SerializeField] private Camera validationCamera;
        [SerializeField] private int renderTextureSize = 512;
        [SerializeField] private float shapeWeight = 0.8f;
        [SerializeField] private float colorWeight = 0.2f;
        [SerializeField] private float penaltyWeight = 0.5f;
        [SerializeField] private bool useReducedQuality = true;
        [SerializeField] private int mobileTextureSize = 256;

        [Header("Debug Options")]
        [SerializeField] private bool saveDebugTextures = false;
        [SerializeField] private bool showRealTimeValidation = false;
        [SerializeField] private ValidationResult currentResult;

        // Drawing state
        private LineRenderer currentLine;
        private List<Vector3> currentLinePoints = new List<Vector3>();
        private List<LineRenderer> allLines = new List<LineRenderer>();
        private DrawingState drawingState = DrawingState.Idle;
        private Vector3 lastWorldPosition;
        private Color brushColor = Color.white;
        private RenderTexture playerDrawingTexture;
        private RenderTexture referenceTexture;
        private bool isValidatorInitialized = false;
        private readonly float[] brushSizeValues = { 0.05f, 0.15f, 0.3f, 0.5f };
        private int actualTextureSize;
        private float lastInputTime;

        public System.Action<ValidationResult> OnValidationUpdate;

        void Start()
        {
            InitializeComponents();
            InitializeValidator();
            ParseBrushColor();
        }

        void InitializeComponents()
        {
            if (drawingCamera == null)
                drawingCamera = Camera.main;

            if (linesParent == null)
            {
                GameObject parent = new GameObject("Lines Parent");
                linesParent = parent.transform;
            }

            int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
            if (lineRendererLayer == -1)
            {
                Debug.LogError("LineRenderer layer not found! Please create a layer named 'LineRenderer'.");
                return;
            }

            if ((drawingCamera.cullingMask & (1 << lineRendererLayer)) == 0)
                drawingCamera.cullingMask |= 1 << lineRendererLayer;

            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
                lineMaterial.color = Color.white;
            }
        }

        void InitializeValidator()
        {
            if (referenceTattoo == null || validationCamera == null)
            {
                Debug.LogWarning("Reference tattoo or validation camera not assigned. Validation disabled.");
                return;
            }

            actualTextureSize = Application.platform == RuntimePlatform.Android && useReducedQuality 
                ? mobileTextureSize : renderTextureSize;

            SetupRenderTextures();
            ConfigureValidationCamera();
            isValidatorInitialized = true;

            Debug.Log($"Tattoo Validator initialized for {Application.platform} with texture size: {actualTextureSize}");
        }

        void SetupRenderTextures()
        {
            RenderTextureFormat shapeFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32) 
                ? RenderTextureFormat.ARGB32 : RenderTextureFormat.Default;
            RenderTextureFormat colorFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32) 
                ? RenderTextureFormat.ARGB32 : RenderTextureFormat.Default;

            playerDrawingTexture = new RenderTexture(actualTextureSize, actualTextureSize, 24, colorFormat);
            playerDrawingTexture.name = "PlayerDrawingTexture";
            playerDrawingTexture.filterMode = FilterMode.Point;
            playerDrawingTexture.useMipMap = false;

            referenceTexture = new RenderTexture(actualTextureSize, actualTextureSize, 0, colorFormat);
            referenceTexture.name = "ReferenceTexture";
            referenceTexture.filterMode = FilterMode.Point;
            referenceTexture.useMipMap = false;
        }

        void ConfigureValidationCamera()
        {
            validationCamera.targetTexture = playerDrawingTexture;
            validationCamera.backgroundColor = Color.black;
            validationCamera.clearFlags = CameraClearFlags.SolidColor;

            validationCamera.orthographic = drawingCamera.orthographic;
            validationCamera.orthographicSize = drawingCamera.orthographicSize;
            validationCamera.transform.position = drawingCamera.transform.position;
            validationCamera.transform.rotation = drawingCamera.transform.rotation;

            int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
            validationCamera.cullingMask = 1 << lineRendererLayer;

            if (Application.platform == RuntimePlatform.Android)
            {
                validationCamera.allowMSAA = false;
                validationCamera.allowHDR = false;
            }
        }

        void ParseBrushColor()
        {
            if (ColorUtility.TryParseHtmlString(brushColorHexCode, out Color newColor))
            {
                brushColor = newColor;
            }
            else
            {
                brushColor = Color.white;
                Debug.LogWarning($"Invalid color hex code: {brushColorHexCode}. Using white.");
            }
        }

        void Update()
        {
            bool isInputDown = false;
            Vector2 inputPosition = Vector2.zero;

            if (Application.isEditor)
            {
                isInputDown = Input.GetMouseButton(0);
                inputPosition = Input.mousePosition;
            }
            else if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                isInputDown = touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
                inputPosition = touch.position;
            }

            if (Time.time - lastInputTime < inputDebounceTime)
                return;

            if (isInputDown && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                if (drawingState == DrawingState.Idle)
                {
                    StartDrawing(inputPosition);
                    drawingState = DrawingState.Drawing;
                    lastInputTime = Time.time;
                }
                else if (drawingState == DrawingState.Drawing)
                {
                    ContinueDrawing(inputPosition);
                }
            }
            else if (drawingState == DrawingState.Drawing)
            {
                FinalizeLine();
                drawingState = DrawingState.Idle;
                lastInputTime = Time.time;
            }
        }

        void StartDrawing(Vector2 screenPosition)
        {
            Vector3 worldPos = ScreenToWorldPosition(screenPosition);
            if (worldPos == Vector3.zero) return;

            if (currentLine != null)
            {
                if (currentLine.positionCount > 0)
                    allLines.Add(currentLine);
                else
                    Destroy(currentLine.gameObject);
                currentLine = null;
            }
            currentLinePoints.Clear();

            GameObject lineObj = new GameObject("TattooLine");
            lineObj.transform.SetParent(linesParent);
            lineObj.layer = LayerMask.NameToLayer("LineRenderer");

            currentLine = lineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(currentLine);

            currentLinePoints.Add(worldPos);
            lastWorldPosition = worldPos;

            UpdateLineRenderer();

            if (tattooMachine != null)
                tattooMachine.MoveTattooMachine(worldPos);
        }

        void ContinueDrawing(Vector2 screenPosition)
        {
            if (drawingState != DrawingState.Drawing || currentLine == null)
                return;

            Vector3 worldPos = ScreenToWorldPosition(screenPosition);
            if (worldPos == Vector3.zero) return;

            if (currentLinePoints.Count >= maxPointsPerLine)
            {
                FinalizeLine();
                StartDrawing(screenPosition);
                drawingState = DrawingState.Drawing;
                return;
            }

            if (Vector3.Distance(worldPos, lastWorldPosition) >= minDistanceThreshold)
            {
                currentLinePoints.Add(worldPos);
                lastWorldPosition = worldPos;
                UpdateLineRenderer();

                if (tattooMachine != null)
                    tattooMachine.MoveTattooMachine(worldPos);

                if (showRealTimeValidation && currentLinePoints.Count > 1)
                    ValidateDrawing();
            }
        }

        void FinalizeLine()
        {
            if (currentLine != null && currentLinePoints.Count > 0)
            {
                allLines.Add(currentLine);
            }
            else if (currentLine != null)
            {
                Destroy(currentLine.gameObject);
            }

            currentLine = null;
            currentLinePoints.Clear();

            if (tattooMachine != null)
                tattooMachine.HideTattooMachine();
        }

        Vector3 ScreenToWorldPosition(Vector2 screenPos)
        {
            if (drawingCamera == null) return Vector3.zero;

            Vector3 worldPos = drawingCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -drawingCamera.transform.position.z));
            worldPos.z = 0;
            return worldPos;
        }

        void UpdateLineRenderer()
        {
            if (currentLine == null || currentLinePoints.Count == 0) return;

            currentLine.positionCount = currentLinePoints.Count;
            for (int i = 0; i < currentLinePoints.Count; i++)
            {
                currentLine.SetPosition(i, currentLinePoints[i]);
            }
        }

        void ConfigureLineRenderer(LineRenderer lr)
        {
            lr.useWorldSpace = true;
            float thickness = brushSizeValues[(int)brushSize];
            lr.startWidth = thickness;
            lr.endWidth = thickness;
            lr.startColor = brushColor;
            lr.endColor = brushColor;
            lr.material = lineMaterial;
            lr.sortingOrder = 10;

            lr.numCapVertices = lineCapVertices;
            lr.numCornerVertices = lineCornerVertices;
            lr.textureMode = textureMode;
            lr.alignment = alignment;

            lr.generateLightingData = false;
            lr.receiveShadows = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void TriggerValidation()
        {
            if (isValidatorInitialized)
            {
                ValidationResult result = ValidateDrawing();
                Debug.Log($"Validation triggered: Success {result.GetFinalScorePercentage():F1}% (Shape: {result.shapeScore * 100:F1}%, Color: {result.colorScore * 100:F1}%)");
            }
            else
            {
                Debug.LogWarning("Validation not initialized!");
            }
        }

        public ValidationResult ValidateDrawing()
        {
            if (!isValidatorInitialized)
            {
                Debug.LogWarning("Validator not initialized!");
                return new ValidationResult();
            }

            RenderPlayerDrawing();
            RenderReferenceTexture();
            currentResult = CompareWithReference();
            OnValidationUpdate?.Invoke(currentResult);

            Debug.Log($"Validation completed: Success {currentResult.GetFinalScorePercentage():F1}% (Shape: {currentResult.shapeScore * 100:F1}%, Color: {currentResult.colorScore * 100:F1}%)");
            return currentResult;
        }

        void RenderPlayerDrawing()
        {
            RenderTexture.active = playerDrawingTexture;
            GL.Clear(true, true, Color.black);
            validationCamera.Render();
            RenderTexture.active = null;
        }

        void RenderReferenceTexture()
        {
            RenderTexture.active = referenceTexture;
            GL.Clear(true, true, Color.black);
            Graphics.Blit(referenceTattoo, referenceTexture);
            RenderTexture.active = null;
        }

        ValidationResult CompareWithReference()
        {
            Texture2D playerTexture = RenderTextureToTexture2D(playerDrawingTexture, TextureFormat.ARGB32);
            Texture2D refTexture = RenderTextureToTexture2D(referenceTexture, TextureFormat.ARGB32);

            // Edge detection for shape contours
            Texture2D refEdges = EdgeDetection(refTexture);
            Texture2D playerEdges = EdgeDetection(playerTexture);

            // Count overlapping edge pixels for shape score
            int totalReferenceEdgePixels = 0;
            int overlappingEdgePixels = 0;
            for (int x = 0; x < actualTextureSize; x++)
            {
                for (int y = 0; y < actualTextureSize; y++)
                {
                    Color refEdge = refEdges.GetPixel(x, y);
                    Color playerEdge = playerEdges.GetPixel(x, y);
                    if (refEdge.r > 0.5f) totalReferenceEdgePixels++; // Edge detected
                    if (refEdge.r > 0.5f && playerEdge.r > 0.5f) overlappingEdgePixels++; // Overlap
                }
            }
            float shapeScore = totalReferenceEdgePixels > 0 ? (float)overlappingEdgePixels / totalReferenceEdgePixels : 0f;

            // Color histogram comparison
            float[] refHistogram = ComputeColorHistogram(refTexture);
            float[] playerHistogram = ComputeColorHistogram(playerTexture);
            float colorDistance = ChiSquareDistance(refHistogram, playerHistogram);
            float colorScore = Mathf.Clamp01(1f - colorDistance); // 0 = different, 1 = identical

            float finalScore = shapeWeight * shapeScore + colorWeight * colorScore;

            Debug.Log($"Debug: totalReferenceEdgePixels={totalReferenceEdgePixels}, overlappingEdgePixels={overlappingEdgePixels}, " +
                      $"colorDistance={colorDistance}, colorScore={colorScore}");

            if (saveDebugTextures && Application.platform != RuntimePlatform.Android)
            {
                StartCoroutine(SaveDebugTextures(playerTexture, refTexture, refEdges, playerEdges));
            }
            else
            {
                DestroyImmediate(playerTexture);
                DestroyImmediate(refTexture);
                DestroyImmediate(refEdges);
                DestroyImmediate(playerEdges);
            }

            return new ValidationResult
            {
                shapeScore = shapeScore,
                colorScore = colorScore,
                finalScore = finalScore,
                correctShapePixels = overlappingEdgePixels,
                incorrectShapePixels = totalReferenceEdgePixels - overlappingEdgePixels,
                totalReferenceShapePixels = totalReferenceEdgePixels,
                totalPlayerShapePixels = overlappingEdgePixels // Approx. player edge count
            };
        } 
    
        Texture2D EdgeDetection(Texture2D source)
        {
            Texture2D edges = new Texture2D(actualTextureSize, actualTextureSize, TextureFormat.ARGB32, false);
            Color[] sourcePixels = source.GetPixels(); // Optimize by reading all pixels once

            for (int x = 1; x < actualTextureSize - 1; x++)
            {
                for (int y = 1; y < actualTextureSize - 1; y++)
                {
                    int index = y * actualTextureSize + x;
                    Color center = sourcePixels[index];
                    if (center.a < 0.5f) // Skip transparent areas
                    {
                        edges.SetPixel(x, y, Color.black);
                        continue;
                    }

                    // Sobel kernel for gradient (Gx and Gy)
                    float gx = (
                        -1 * sourcePixels[index - actualTextureSize - 1].r + 1 * sourcePixels[index - actualTextureSize + 1].r + // Top row
                        -2 * sourcePixels[index - 1].r + 2 * sourcePixels[index + 1].r + // Middle row
                        -1 * sourcePixels[index + actualTextureSize - 1].r + 1 * sourcePixels[index + actualTextureSize + 1].r // Bottom row
                    );

                    float gy = (
                        -1 * sourcePixels[index - actualTextureSize - 1].r - 2 * sourcePixels[index - actualTextureSize].r - 1 * sourcePixels[index - actualTextureSize + 1].r + // Top row
                        1 * sourcePixels[index + actualTextureSize - 1].r + 2 * sourcePixels[index + actualTextureSize].r + 1 * sourcePixels[index + actualTextureSize + 1].r // Bottom row
                    );

                    float magnitude = Mathf.Sqrt(gx * gx + gy * gy) / 4f; // Normalize by max kernel sum (4)
                    edges.SetPixel(x, y, magnitude > 0.2f ? Color.white : Color.black); // Adjusted threshold
                }
            }

            // Handle edges by setting to black (no gradient calculation)
            for (int x = 0; x < actualTextureSize; x++)
            {
                edges.SetPixel(x, 0, Color.black);
                edges.SetPixel(x, actualTextureSize - 1, Color.black);
            }
            for (int y = 0; y < actualTextureSize; y++)
            {
                edges.SetPixel(0, y, Color.black);
                edges.SetPixel(actualTextureSize - 1, y, Color.black);
            }

            edges.Apply();
            return edges;
        }

        float[] ComputeColorHistogram(Texture2D texture)
        {
            float[] histogram = new float[8 * 8 * 8]; // 8 bins per RGB channel
            int totalPixels = 0;
            for (int x = 0; x < actualTextureSize; x++)
            {
                for (int y = 0; y < actualTextureSize; y++)
                {
                    Color pixel = texture.GetPixel(x, y);
                    if (pixel.a > 0.5f)
                    {
                        int rBin = Mathf.FloorToInt(pixel.r * 7);
                        int gBin = Mathf.FloorToInt(pixel.g * 7);
                        int bBin = Mathf.FloorToInt(pixel.b * 7);
                        int index = rBin * 64 + gBin * 8 + bBin;
                        histogram[index]++;
                        totalPixels++;
                    }
                }
            }
            for (int i = 0; i < histogram.Length; i++)
                histogram[i] /= totalPixels > 0 ? totalPixels : 1; // Normalize
            return histogram;
        }

        float ChiSquareDistance(float[] h1, float[] h2)
        {
            float distance = 0f;
            for (int i = 0; i < h1.Length; i++)
            {
                if (h1[i] + h2[i] > 0) // Avoid division by zero
                    distance += Mathf.Pow(h1[i] - h2[i], 2) / (h1[i] + h2[i]);
            }
            return Mathf.Sqrt(distance) / 10f; // Scale to [0, 1]
        }

        Texture2D RenderTextureToTexture2D(RenderTexture renderTexture, TextureFormat format)
        {
            RenderTexture.active = renderTexture;
            Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, format, false);
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = null;
            return texture2D;
        }

        IEnumerator SaveDebugTextures(Texture2D playerTexture, Texture2D refTexture, Texture2D refEdges, Texture2D playerEdges)
        {
            yield return new WaitForEndOfFrame();

            string folderPath = Path.Combine(Application.persistentDataPath, "TattooDebug");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            byte[] playerBytes = playerTexture.EncodeToPNG();
            string playerPath = Path.Combine(folderPath, $"player_drawing_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(playerPath, playerBytes);

            byte[] refBytes = refTexture.EncodeToPNG();
            string refPath = Path.Combine(folderPath, $"reference_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(refPath, refBytes);

            byte[] refEdgeBytes = refEdges.EncodeToPNG();
            string refEdgePath = Path.Combine(folderPath, $"ref_edges_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(refEdgePath, refEdgeBytes);

            byte[] playerEdgeBytes = playerEdges.EncodeToPNG();
            string playerEdgePath = Path.Combine(folderPath, $"player_edges_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(playerEdgePath, playerEdgeBytes);

            Debug.Log($"Debug textures saved to: {playerPath}, {refPath}, {refEdgePath}, {playerEdgePath}");
            DestroyImmediate(playerTexture);
            DestroyImmediate(refTexture);
            DestroyImmediate(refEdges);
            DestroyImmediate(playerEdges);
        }

        public void SetBrushSize(BrushSize size)
        {
            brushSize = size;
            if (currentLine != null)
            {
                float thickness = brushSizeValues[(int)brushSize];
                currentLine.startWidth = thickness;
                currentLine.endWidth = thickness;
            }
        }

        public void SetBrushColor(string hexColor)
        {
            brushColorHexCode = hexColor;
            ParseBrushColor();
            if (currentLine != null)
            {
                currentLine.startColor = brushColor;
                currentLine.endColor = brushColor;
            }
        }

        public void SetBrushColor(Color color)
        {
            brushColor = color;
            brushColorHexCode = "#" + ColorUtility.ToHtmlStringRGB(color);
            if (currentLine != null)
            {
                currentLine.startColor = brushColor;
                currentLine.endColor = brushColor;
            }
        }

        public void ClearDrawing()
        {
            foreach (var line in allLines)
            {
                if (line != null)
                    DestroyImmediate(line.gameObject);
            }
            allLines.Clear();
            currentLine = null;
            currentLinePoints.Clear();
            drawingState = DrawingState.Idle;
        }

        public int GetTotalPointCount()
        {
            int totalPoints = 0;
            foreach (var line in allLines)
            {
                if (line != null)
                    totalPoints += line.positionCount;
            }
            return totalPoints;
        }

        public float GetSuccessPercentage()
        {
            return currentResult.GetFinalScorePercentage();
        }

        public string GetFormattedResults()
        {
            return $"Success: {currentResult.GetFinalScorePercentage():F1}% (Shape: {currentResult.shapeScore * 100:F1}%, Color: {currentResult.colorScore * 100:F1}%)";
        }

        public string GetDetailedDebugInfo()
        {
            return currentResult.GetDetailedDebugInfo();
        }

        public ValidationQuality GetCurrentQuality()
        {
            return currentResult.GetQualityLevel();
        }

        public bool IsCurrentScorePassing(float threshold = 0.6f)
        {
            return currentResult.IsPassingScore(threshold);
        }

        void OnDestroy()
        {
            if (playerDrawingTexture != null)
                playerDrawingTexture.Release();
            if (referenceTexture != null)
                referenceTexture.Release();
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Now")]
        void ValidateNow()
        {
            if (Application.isPlaying)
            {
                TriggerValidation();
            }
        }

        [ContextMenu("Clear Drawing")]
        void ClearDrawingDebug()
        {
            if (Application.isPlaying)
                ClearDrawing();
        }
#endif
    }

    [System.Serializable]
    public class ValidationResult
    {
        public float shapeScore;
        public float colorScore;
        public float finalScore;
        public int correctShapePixels;
        public int incorrectShapePixels;
        public int totalReferenceShapePixels;
        public int totalPlayerShapePixels;

        public float GetFinalScorePercentage()
        {
            return finalScore * 100f;
        }

        public string GetFormattedResults()
        {
            return $"Success: {GetFinalScorePercentage():F1}% (Shape: {shapeScore * 100:F1}%, Color: {colorScore * 100:F1}%)";
        }

        public string GetDetailedDebugInfo()
        {
            return $"Shape Score: {shapeScore * 100:F1}%\n" +
                   $"Color Score: {colorScore * 100:F1}%\n" +
                   $"Final Score: {GetFinalScorePercentage():F1}%\n" +
                   $"Correct Shape Pixels: {correctShapePixels}\n" +
                   $"Incorrect Shape Pixels: {incorrectShapePixels}\n" +
                   $"Total Reference Shape Pixels: {totalReferenceShapePixels}\n" +
                   $"Total Player Shape Pixels: {totalPlayerShapePixels}";
        }

        public ValidationQuality GetQualityLevel()
        {
            float score = finalScore;
            if (score >= 0.9f) return ValidationQuality.Excellent;
            if (score >= 0.7f) return ValidationQuality.Good;
            if (score >= 0.5f) return ValidationQuality.Fair;
            return ValidationQuality.Poor;
        }

        public bool IsPassingScore(float threshold)
        {
            return finalScore >= threshold;
        }
    }

    public enum ValidationQuality
    {
        Poor,
        Fair,
        Good,
        Excellent
    }
}*/ //----------------------------1.3

/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace TattooSystem
{
    public class TattooDrawingSystem : MonoBehaviour
    {
        public enum BrushSize
        {
            Small = 0, // 0.05f
            Medium = 1, // 0.15f
            Large = 2, // 0.3f
            ExtraLarge = 3 // 0.5f
        }

        private enum DrawingState
        {
            Idle,
            Drawing
        }

        [Header("Drawing Settings")] [SerializeField]
        private Material lineMaterial;

        [SerializeField] private Transform linesParent;
        [SerializeField] private BrushSize brushSize = BrushSize.Medium;
        [SerializeField] private string brushColorHexCode = "#FFFFFF";
        [SerializeField] private float minDistanceThreshold = 0.005f;
        [SerializeField] private Camera drawingCamera;
        [SerializeField] private TattooMachine tattooMachine;

        [Header("Line Renderer Settings")] [SerializeField]
        private int lineCapVertices = 10;

        [SerializeField] private int lineCornerVertices = 10;
        [SerializeField] private LineTextureMode textureMode = LineTextureMode.Tile;
        [SerializeField] private LineAlignment alignment = LineAlignment.View;

        [Header("Mobile Optimization")] [SerializeField]
        private int maxPointsPerLine = 1000;

        [SerializeField] private bool optimizeForMobile = true;
        [SerializeField] private float touchSensitivity = 1f;
        [SerializeField] private float inputDebounceTime = 0.05f;

        [Header("Validation Settings")] [SerializeField]
        private Texture2D referenceTattoo;

        [SerializeField] private Camera validationCamera;
        [SerializeField] private int renderTextureSize = 512;
        [SerializeField] private float shapeWeight = 0.8f;
        [SerializeField] private float colorWeight = 0.2f;
        [SerializeField] private float penaltyWeight = 0.5f;
        [SerializeField] private bool useReducedQuality = true;
        [SerializeField] private int mobileTextureSize = 256;

        [Header("Debug Options")] [SerializeField]
        private bool saveDebugTextures = false;

        [SerializeField] private bool showRealTimeValidation = false;
        [SerializeField] private ValidationResult currentResult;

        // Drawing state
        private LineRenderer currentLine;
        private List<Vector3> currentLinePoints = new List<Vector3>();
        private List<LineRenderer> allLines = new List<LineRenderer>();
        private DrawingState drawingState = DrawingState.Idle;
        private Vector3 lastWorldPosition;
        private Color brushColor = Color.white;
        private RenderTexture playerDrawingTexture;
        private RenderTexture referenceTexture;
        private bool isValidatorInitialized = false;
        private readonly float[] brushSizeValues = { 0.05f, 0.15f, 0.3f, 0.5f };
        private int actualTextureSize;
        private float lastInputTime;

        public System.Action<ValidationResult> OnValidationUpdate;

        void Start()
        {
            InitializeComponents();
            InitializeValidator();
            ParseBrushColor();
        }

        void InitializeComponents()
        {
            if (drawingCamera == null)
                drawingCamera = Camera.main;

            if (linesParent == null)
            {
                GameObject parent = new GameObject("Lines Parent");
                linesParent = parent.transform;
            }

            int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
            if (lineRendererLayer == -1)
            {
                Debug.LogError("LineRenderer layer not found! Please create a layer named 'LineRenderer'.");
                return;
            }

            if ((drawingCamera.cullingMask & (1 << lineRendererLayer)) == 0)
                drawingCamera.cullingMask |= 1 << lineRendererLayer;

            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
                lineMaterial.color = Color.white;
            }
        }

        void InitializeValidator()
        {
            if (referenceTattoo == null || validationCamera == null)
            {
                Debug.LogWarning("Reference tattoo or validation camera not assigned. Validation disabled.");
                return;
            }

            actualTextureSize = Application.platform == RuntimePlatform.Android && useReducedQuality
                ? mobileTextureSize
                : renderTextureSize;

            SetupRenderTextures();
            ConfigureValidationCamera();
            isValidatorInitialized = true;

            Debug.Log(
                $"Tattoo Validator initialized for {Application.platform} with texture size: {actualTextureSize}");
        }

        void SetupRenderTextures()
        {
            RenderTextureFormat shapeFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32)
                ? RenderTextureFormat.ARGB32
                : RenderTextureFormat.Default;
            RenderTextureFormat colorFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32)
                ? RenderTextureFormat.ARGB32
                : RenderTextureFormat.Default;

            playerDrawingTexture = new RenderTexture(actualTextureSize, actualTextureSize, 24, colorFormat);
            playerDrawingTexture.name = "PlayerDrawingTexture";
            playerDrawingTexture.filterMode = FilterMode.Point;
            playerDrawingTexture.useMipMap = false;

            referenceTexture = new RenderTexture(actualTextureSize, actualTextureSize, 0, colorFormat);
            referenceTexture.name = "ReferenceTexture";
            referenceTexture.filterMode = FilterMode.Point;
            referenceTexture.useMipMap = false;
        }

        void ConfigureValidationCamera()
        {
            validationCamera.targetTexture = playerDrawingTexture;
            validationCamera.backgroundColor = Color.black;
            validationCamera.clearFlags = CameraClearFlags.SolidColor;

            validationCamera.orthographic = drawingCamera.orthographic;
            validationCamera.orthographicSize = drawingCamera.orthographicSize;
            validationCamera.transform.position = drawingCamera.transform.position;
            validationCamera.transform.rotation = drawingCamera.transform.rotation;

            int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
            validationCamera.cullingMask = 1 << lineRendererLayer;

            if (Application.platform == RuntimePlatform.Android)
            {
                validationCamera.allowMSAA = false;
                validationCamera.allowHDR = false;
            }
        }

        void ParseBrushColor()
        {
            if (ColorUtility.TryParseHtmlString(brushColorHexCode, out Color newColor))
            {
                brushColor = newColor;
            }
            else
            {
                brushColor = Color.white;
                Debug.LogWarning($"Invalid color hex code: {brushColorHexCode}. Using white.");
            }
        }

        void Update()
        {
            bool isInputDown = false;
            Vector2 inputPosition = Vector2.zero;

            if (Application.isEditor)
            {
                isInputDown = Input.GetMouseButton(0);
                inputPosition = Input.mousePosition;
            }
            else if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                isInputDown = touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved ||
                              touch.phase == TouchPhase.Stationary;
                inputPosition = touch.position;
            }

            if (Time.time - lastInputTime < inputDebounceTime)
                return;

            if (isInputDown && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                if (drawingState == DrawingState.Idle)
                {
                    StartDrawing(inputPosition);
                    drawingState = DrawingState.Drawing;
                    lastInputTime = Time.time;
                }
                else if (drawingState == DrawingState.Drawing)
                {
                    ContinueDrawing(inputPosition);
                }
            }
            else if (drawingState == DrawingState.Drawing)
            {
                FinalizeLine();
                drawingState = DrawingState.Idle;
                lastInputTime = Time.time;
            }
        }

        void StartDrawing(Vector2 screenPosition)
        {
            Vector3 worldPos = ScreenToWorldPosition(screenPosition);
            if (worldPos == Vector3.zero) return;

            if (currentLine != null)
            {
                if (currentLine.positionCount > 0)
                    allLines.Add(currentLine);
                else
                    Destroy(currentLine.gameObject);
                currentLine = null;
            }

            currentLinePoints.Clear();

            GameObject lineObj = new GameObject("TattooLine");
            lineObj.transform.SetParent(linesParent);
            lineObj.layer = LayerMask.NameToLayer("LineRenderer");

            currentLine = lineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(currentLine);

            currentLinePoints.Add(worldPos);
            lastWorldPosition = worldPos;

            UpdateLineRenderer();

            if (tattooMachine != null)
                tattooMachine.MoveTattooMachine(worldPos);
        }

        void ContinueDrawing(Vector2 screenPosition)
        {
            if (drawingState != DrawingState.Drawing || currentLine == null)
                return;

            Vector3 worldPos = ScreenToWorldPosition(screenPosition);
            if (worldPos == Vector3.zero) return;

            if (currentLinePoints.Count >= maxPointsPerLine)
            {
                FinalizeLine();
                StartDrawing(screenPosition);
                drawingState = DrawingState.Drawing;
                return;
            }

            if (Vector3.Distance(worldPos, lastWorldPosition) >= minDistanceThreshold)
            {
                currentLinePoints.Add(worldPos);
                lastWorldPosition = worldPos;
                UpdateLineRenderer();

                if (tattooMachine != null)
                    tattooMachine.MoveTattooMachine(worldPos);

                if (showRealTimeValidation && currentLinePoints.Count > 1)
                    ValidateDrawing();
            }
        }

        void FinalizeLine()
        {
            if (currentLine != null && currentLinePoints.Count > 0)
            {
                allLines.Add(currentLine);
            }
            else if (currentLine != null)
            {
                Destroy(currentLine.gameObject);
            }

            currentLine = null;
            currentLinePoints.Clear();

            if (tattooMachine != null)
                tattooMachine.HideTattooMachine();
        }

        Vector3 ScreenToWorldPosition(Vector2 screenPos)
        {
            if (drawingCamera == null) return Vector3.zero;

            Vector3 worldPos =
                drawingCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y,
                    -drawingCamera.transform.position.z));
            worldPos.z = 0;
            return worldPos;
        }

        void UpdateLineRenderer()
        {
            if (currentLine == null || currentLinePoints.Count == 0) return;

            currentLine.positionCount = currentLinePoints.Count;
            for (int i = 0; i < currentLinePoints.Count; i++)
            {
                currentLine.SetPosition(i, currentLinePoints[i]);
            }
        }

        void ConfigureLineRenderer(LineRenderer lr)
        {
            lr.useWorldSpace = true;
            float thickness = brushSizeValues[(int)brushSize];
            lr.startWidth = thickness;
            lr.endWidth = thickness;
            lr.startColor = brushColor;
            lr.endColor = brushColor;
            lr.material = lineMaterial;
            lr.sortingOrder = 10;

            lr.numCapVertices = lineCapVertices;
            lr.numCornerVertices = lineCornerVertices;
            lr.textureMode = textureMode;
            lr.alignment = alignment;

            lr.generateLightingData = false;
            lr.receiveShadows = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void TriggerValidation()
        {
            if (isValidatorInitialized)
            {
                ValidationResult result = ValidateDrawing();
                Debug.Log(
                    $"Validation triggered: Success {result.GetFinalScorePercentage():F1}% (Shape: {result.shapeScore * 100:F1}%, Color: {result.colorScore * 100:F1}%)");
            }
            else
            {
                Debug.LogWarning("Validation not initialized!");
            }
        }

        public ValidationResult ValidateDrawing()
        {
            if (!isValidatorInitialized)
            {
                Debug.LogWarning("Validator not initialized!");
                return new ValidationResult();
            }

            RenderPlayerDrawing();
            RenderReferenceTexture();
            currentResult = CompareWithReference();
            OnValidationUpdate?.Invoke(currentResult);

            Debug.Log(
                $"Validation completed: Success {currentResult.GetFinalScorePercentage():F1}% (Shape: {currentResult.shapeScore * 100:F1}%, Color: {currentResult.colorScore * 100:F1}%)");
            return currentResult;
        }

        void RenderPlayerDrawing()
        {
            RenderTexture.active = playerDrawingTexture;
            GL.Clear(true, true, Color.black);
            validationCamera.Render();
            RenderTexture.active = null;
        }

        void RenderReferenceTexture()
        {
            RenderTexture.active = referenceTexture;
            GL.Clear(true, true, Color.black);
            Graphics.Blit(referenceTattoo, referenceTexture);
            RenderTexture.active = null;
        }

        ValidationResult CompareWithReference()
{
    if (GetTotalPointCount() < 10)
    {
        Debug.LogWarning("Too little input to validate!");
        return new ValidationResult();
    }

    Texture2D playerTexture = RenderTextureToTexture2D(playerDrawingTexture, TextureFormat.ARGB32);
    Texture2D refTexture = RenderTextureToTexture2D(referenceTexture, TextureFormat.ARGB32);

    Texture2D refEdges = EdgeDetection(refTexture);
    Texture2D playerEdges = EdgeDetection(playerTexture);

    int totalReferenceEdgePixels = 0;
    int overlappingEdgePixels = 0;
    int totalPlayerEdgePixels = 0;

    Color[] refEdgePixels = refEdges.GetPixels();
    Color[] playerEdgePixels = playerEdges.GetPixels();

    for (int i = 0; i < refEdgePixels.Length; i++)
    {
        if (refEdgePixels[i].r > 0.5f)
        {
            totalReferenceEdgePixels++;
            if (playerEdgePixels[i].r > 0.5f)
                overlappingEdgePixels++;
        }

        if (playerEdgePixels[i].r > 0.5f)
            totalPlayerEdgePixels++;
    }

    float precision = totalPlayerEdgePixels > 0 ? (float)overlappingEdgePixels / totalPlayerEdgePixels : 0f;
    float recall = totalReferenceEdgePixels > 0 ? (float)overlappingEdgePixels / totalReferenceEdgePixels : 0f;
    float shapeScore = (precision + recall) > 0 ? 2 * precision * recall / (precision + recall) : 0f;

    float[] refHistogram = ComputeColorHistogram(refTexture);
    float[] playerHistogram = ComputeColorHistogram(playerTexture);
    float colorDistance = ChiSquareDistance(refHistogram, playerHistogram);
    float colorScore = Mathf.Clamp01(1f - colorDistance * 1.5f);

    float finalScore = shapeWeight * shapeScore + colorWeight * colorScore;

    if (saveDebugTextures && Application.platform != RuntimePlatform.Android)
    {
        StartCoroutine(SaveDebugTextures(playerTexture, refTexture, refEdges, playerEdges));
    }
    else
    {
        DestroyImmediate(playerTexture);
        DestroyImmediate(refTexture);
        DestroyImmediate(refEdges);
        DestroyImmediate(playerEdges);
    }

    return new ValidationResult
    {
        shapeScore = shapeScore,
        colorScore = colorScore,
        finalScore = finalScore,
        correctShapePixels = overlappingEdgePixels,
        incorrectShapePixels = totalReferenceEdgePixels - overlappingEdgePixels,
        totalReferenceShapePixels = totalReferenceEdgePixels,
        totalPlayerShapePixels = totalPlayerEdgePixels
    };
}

        Texture2D EdgeDetection(Texture2D source)
        {
            Texture2D edges = new Texture2D(actualTextureSize, actualTextureSize, TextureFormat.ARGB32, false);
            Color[] sourcePixels = source.GetPixels(); // Optimize by reading all pixels once
            int totalEdgePixels = 0;

            for (int x = 1; x < actualTextureSize - 1; x++)
            {
                for (int y = 1; y < actualTextureSize - 1; y++)
                {
                    int index = y * actualTextureSize + x;
                    Color center = sourcePixels[index];

                    // Skip if fully transparent
                    if (center.a < 0.1f)
                    {
                        edges.SetPixel(x, y, Color.black);
                        continue;
                    }

                    // Sobel kernel for gradient (Gx and Gy)
                    float gx = (
                        -1 * sourcePixels[index - actualTextureSize - 1].r +
                        1 * sourcePixels[index - actualTextureSize + 1].r + // Top row
                        -2 * sourcePixels[index - 1].r + 2 * sourcePixels[index + 1].r + // Middle row
                        -1 * sourcePixels[index + actualTextureSize - 1].r +
                        1 * sourcePixels[index + actualTextureSize + 1].r // Bottom row
                    );

                    float gy = (
                        -1 * sourcePixels[index - actualTextureSize - 1].r -
                        2 * sourcePixels[index - actualTextureSize].r -
                        1 * sourcePixels[index - actualTextureSize + 1].r + // Top row
                        1 * sourcePixels[index + actualTextureSize - 1].r +
                        2 * sourcePixels[index + actualTextureSize].r +
                        1 * sourcePixels[index + actualTextureSize + 1].r // Bottom row
                    );

                    float magnitude = Mathf.Sqrt(gx * gx + gy * gy) / 4f; // Normalize by max kernel sum (4)
                    bool isEdge = magnitude > 0.15f; // Lowered threshold to catch more edges
                    edges.SetPixel(x, y, isEdge ? Color.white : Color.black);
                    if (isEdge) totalEdgePixels++;
                }
            }

            // Handle edges by setting to black
            for (int x = 0; x < actualTextureSize; x++)
            {
                edges.SetPixel(x, 0, Color.black);
                edges.SetPixel(x, actualTextureSize - 1, Color.black);
            }

            for (int y = 0; y < actualTextureSize; y++)
            {
                edges.SetPixel(0, y, Color.black);
                edges.SetPixel(actualTextureSize - 1, y, Color.black);
            }

            edges.Apply();
            Debug.Log($"EdgeDetection: Total edge pixels detected = {totalEdgePixels}");
            return edges;
        }

        float[] ComputeColorHistogram(Texture2D texture)
        {
            float[] histogram = new float[8 * 8 * 8];
            int totalPixels = 0;
            for (int x = 0; x < texture.width; x++)
            {
                for (int y = 0; y < texture.height; y++)
                {
                    Color pixel = texture.GetPixel(x, y);
                    if (pixel.a > 0.5f && pixel.maxColorComponent > 0.1f)
                    {
                        int rBin = Mathf.FloorToInt(pixel.r * 7);
                        int gBin = Mathf.FloorToInt(pixel.g * 7);
                        int bBin = Mathf.FloorToInt(pixel.b * 7);
                        int index = rBin * 64 + gBin * 8 + bBin;
                        histogram[index]++;
                        totalPixels++;
                    }
                }
            }
            for (int i = 0; i < histogram.Length; i++)
                histogram[i] /= totalPixels > 0 ? totalPixels : 1;
            return histogram;
        }

        float ChiSquareDistance(float[] h1, float[] h2)
        {
            float distance = 0f;
            for (int i = 0; i < h1.Length; i++)
            {
                if (h1[i] + h2[i] > 0) // Avoid division by zero
                    distance += Mathf.Pow(h1[i] - h2[i], 2) / (h1[i] + h2[i]);
            }

            return Mathf.Sqrt(distance) / 10f; // Scale to [0, 1]
        }

        Texture2D RenderTextureToTexture2D(RenderTexture renderTexture, TextureFormat format)
        {
            RenderTexture.active = renderTexture;
            Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, format, false);
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = null;
            return texture2D;
        }

        IEnumerator SaveDebugTextures(Texture2D playerTexture, Texture2D refTexture, Texture2D refEdges,
            Texture2D playerEdges)
        {
            yield return new WaitForEndOfFrame();

            string folderPath = Path.Combine(Application.persistentDataPath, "TattooDebug");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            byte[] playerBytes = playerTexture.EncodeToPNG();
            string playerPath = Path.Combine(folderPath, $"player_drawing_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(playerPath, playerBytes);

            byte[] refBytes = refTexture.EncodeToPNG();
            string refPath = Path.Combine(folderPath, $"reference_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(refPath, refBytes);

            byte[] refEdgeBytes = refEdges.EncodeToPNG();
            string refEdgePath = Path.Combine(folderPath, $"ref_edges_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(refEdgePath, refEdgeBytes);

            byte[] playerEdgeBytes = playerEdges.EncodeToPNG();
            string playerEdgePath = Path.Combine(folderPath, $"player_edges_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(playerEdgePath, playerEdgeBytes);

            Debug.Log($"Debug textures saved to: {playerPath}, {refPath}, {refEdgePath}, {playerEdgePath}");
            DestroyImmediate(playerTexture);
            DestroyImmediate(refTexture);
            DestroyImmediate(refEdges);
            DestroyImmediate(playerEdges);
        }

        public void SetBrushSize(BrushSize size)
        {
            brushSize = size;
            if (currentLine != null)
            {
                float thickness = brushSizeValues[(int)brushSize];
                currentLine.startWidth = thickness;
                currentLine.endWidth = thickness;
            }
        }

        public void SetBrushColor(string hexColor)
        {
            brushColorHexCode = hexColor;
            ParseBrushColor();
            if (currentLine != null)
            {
                currentLine.startColor = brushColor;
                currentLine.endColor = brushColor;
            }
        }

        public void SetBrushColor(Color color)
        {
            brushColor = color;
            brushColorHexCode = "#" + ColorUtility.ToHtmlStringRGB(color);
            if (currentLine != null)
            {
                currentLine.startColor = brushColor;
                currentLine.endColor = brushColor;
            }
        }

        public void ClearDrawing()
        {
            foreach (var line in allLines)
            {
                if (line != null)
                    DestroyImmediate(line.gameObject);
            }

            allLines.Clear();
            currentLine = null;
            currentLinePoints.Clear();
            drawingState = DrawingState.Idle;
        }

        public int GetTotalPointCount()
        {
            int totalPoints = 0;
            foreach (var line in allLines)
            {
                if (line != null)
                    totalPoints += line.positionCount;
            }

            return totalPoints;
        }

        public float GetSuccessPercentage()
        {
            return currentResult.GetFinalScorePercentage();
        }

        public string GetFormattedResults()
        {
            return
                $"Success: {currentResult.GetFinalScorePercentage():F1}% (Shape: {currentResult.shapeScore * 100:F1}%, Color: {currentResult.colorScore * 100:F1}%)";
        }

        public string GetDetailedDebugInfo()
        {
            return currentResult.GetDetailedDebugInfo();
        }

        public ValidationQuality GetCurrentQuality()
        {
            return currentResult.GetQualityLevel();
        }

        public bool IsCurrentScorePassing(float threshold = 0.6f)
        {
            return currentResult.IsPassingScore(threshold);
        }

        void OnDestroy()
        {
            if (playerDrawingTexture != null)
                playerDrawingTexture.Release();
            if (referenceTexture != null)
                referenceTexture.Release();
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Now")]
        void ValidateNow()
        {
            if (Application.isPlaying)
            {
                TriggerValidation();
            }
        }

        [ContextMenu("Clear Drawing")]
        void ClearDrawingDebug()
        {
            if (Application.isPlaying)
                ClearDrawing();
        }
#endif
    }

    [System.Serializable]
    public class ValidationResult
    {
        public float shapeScore;
        public float colorScore;
        public float finalScore;
        public int correctShapePixels;
        public int incorrectShapePixels;
        public int totalReferenceShapePixels;
        public int totalPlayerShapePixels;

        public float GetFinalScorePercentage()
        {
            return finalScore * 100f;
        }

        public string GetFormattedResults()
        {
            return
                $"Success: {GetFinalScorePercentage():F1}% (Shape: {shapeScore * 100:F1}%, Color: {colorScore * 100:F1}%)";
        }

        public string GetDetailedDebugInfo()
        {
            return $"Shape Score: {shapeScore * 100:F1}%\n" +
                   $"Color Score: {colorScore * 100:F1}%\n" +
                   $"Final Score: {GetFinalScorePercentage():F1}%\n" +
                   $"Correct Shape Pixels: {correctShapePixels}\n" +
                   $"Incorrect Shape Pixels: {incorrectShapePixels}\n" +
                   $"Total Reference Shape Pixels: {totalReferenceShapePixels}\n" +
                   $"Total Player Shape Pixels: {totalPlayerShapePixels}";
        }

        public ValidationQuality GetQualityLevel()
        {
            float score = finalScore;
            if (score >= 0.9f) return ValidationQuality.Excellent;
            if (score >= 0.7f) return ValidationQuality.Good;
            if (score >= 0.5f) return ValidationQuality.Fair;
            return ValidationQuality.Poor;
        }

        public bool IsPassingScore(float threshold)
        {
            return finalScore >= threshold;
        }
    }

    public enum ValidationQuality
    {
        Poor,
        Fair,
        Good,
        Excellent
    }
}*/ //----------1.5

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.Experimental.Rendering;

namespace TattooSystem
{
    public class TattooDrawingSystem : MonoBehaviour
    {
        public enum BrushSize
        {
            Small = 0,    // 0.05f
            Medium = 1,   // 0.15f
            Large = 2,    // 0.3f
            ExtraLarge = 3 // 0.5f
        }

        private enum DrawingState
        {
            Idle,
            Drawing
        }

        [Header("Drawing Settings")]
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Transform linesParent;
        [SerializeField] private BrushSize brushSize = BrushSize.Medium;
        [SerializeField] private string brushColorHexCode = "#FFFFFF";
        [SerializeField] private float minDistanceThreshold = 0.005f;
        [SerializeField] private Camera drawingCamera;
        [SerializeField] private TattooMachine tattooMachine;

        [Header("Line Renderer Settings")]
        [SerializeField] private int lineCapVertices = 10;
        [SerializeField] private int lineCornerVertices = 10;
        [SerializeField] private LineTextureMode textureMode = LineTextureMode.Tile;
        [SerializeField] private LineAlignment alignment = LineAlignment.View;

        [Header("Mobile Optimization")]
        [SerializeField] private int maxPointsPerLine = 1000;
        [SerializeField] private bool optimizeForMobile = true;
        [SerializeField] private float touchSensitivity = 1f;
        [SerializeField] private float inputDebounceTime = 0.05f;

        [Header("Validation Settings")]
        [SerializeField] private Texture2D referenceTattoo;
        [SerializeField] private Camera validationCamera;
        [SerializeField] private int renderTextureSize = 512;
        [SerializeField] private float shapeWeight = 0.8f; // 80% shape
        [SerializeField] private float colorWeight = 0.2f; // 20% color
        [SerializeField] private bool useReducedQuality = true;
        [SerializeField] private int mobileTextureSize = 256;

        [Header("Debug Options")]
        [SerializeField] private bool saveDebugTextures = false;
        [SerializeField] private bool showRealTimeValidation = false;
        [SerializeField] private ValidationResult currentResult;

        // Drawing state
        private LineRenderer currentLine;
        private List<Vector3> currentLinePoints = new List<Vector3>();
        private List<LineRenderer> allLines = new List<LineRenderer>();
        private DrawingState drawingState = DrawingState.Idle;
        private Vector3 lastWorldPosition;
        private Color brushColor = Color.white;
        private RenderTexture referenceTexture;
        private bool isValidatorInitialized = false;
        private readonly float[] brushSizeValues = { 0.05f, 0.15f, 0.3f, 0.5f };
        private int actualTextureSize;
        private float lastInputTime;

        public System.Action<ValidationResult> OnValidationUpdate;

        void Start()
        {
            InitializeComponents();
            InitializeValidator();
            ParseBrushColor();
        }

        void InitializeComponents()
        {
            if (drawingCamera == null)
                drawingCamera = Camera.main;

            if (linesParent == null)
            {
                GameObject parent = new GameObject("Lines Parent");
                linesParent = parent.transform;
            }

            int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
            if (lineRendererLayer == -1)
            {
                Debug.LogError("LineRenderer layer not found! Please create a layer named 'LineRenderer'.");
                return;
            }

            if ((drawingCamera.cullingMask & (1 << lineRendererLayer)) == 0)
                drawingCamera.cullingMask |= 1 << lineRendererLayer;

            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
                lineMaterial.color = Color.white;
            }
        }

        void InitializeValidator()
        {
            if (referenceTattoo == null || validationCamera == null)
            {
                Debug.LogWarning("Reference tattoo or validation camera not assigned. Validation disabled.");
                return;
            }

            actualTextureSize = Application.platform == RuntimePlatform.Android && useReducedQuality 
                ? mobileTextureSize : renderTextureSize;

            SetupRenderTextures();
            ConfigureValidationCamera();
            isValidatorInitialized = true;

            Debug.Log($"Tattoo Validator initialized for {Application.platform} with texture size: {actualTextureSize}");
        }

        void SetupRenderTextures()
        {
            RenderTextureFormat shapeFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32) 
                ? RenderTextureFormat.ARGB32 : RenderTextureFormat.Default;
            

            referenceTexture = new RenderTexture(actualTextureSize, actualTextureSize, 24);
            referenceTexture.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            referenceTexture.name = "ReferenceTexture";
            referenceTexture.filterMode = FilterMode.Point;
            referenceTexture.useMipMap = false;
        }

        void ConfigureValidationCamera()
        {
            validationCamera.targetTexture = referenceTexture;
            validationCamera.backgroundColor = Color.clear;
            validationCamera.clearFlags = CameraClearFlags.SolidColor;

            validationCamera.orthographic = drawingCamera.orthographic;
            validationCamera.orthographicSize = drawingCamera.orthographicSize*(Screen.width/(float)Screen.height);
            validationCamera.transform.position = drawingCamera.transform.position;
            validationCamera.transform.rotation = drawingCamera.transform.rotation;

            int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
            validationCamera.cullingMask = 1 << lineRendererLayer;

            if (Application.platform == RuntimePlatform.Android)
            {
                validationCamera.allowMSAA = false;
                validationCamera.allowHDR = false;
            }
        }

        void ParseBrushColor()
        {
            if (ColorUtility.TryParseHtmlString(brushColorHexCode, out Color newColor))
            {
                brushColor = newColor;
            }
            else
            {
                brushColor = Color.white;
                Debug.LogWarning($"Invalid color hex code: {brushColorHexCode}. Using white.");
            }
        }

        void Update()
        {
            bool isInputDown = false;
            Vector2 inputPosition = Vector2.zero;

            if (Application.isEditor)
            {
                isInputDown = Input.GetMouseButton(0);
                inputPosition = Input.mousePosition;
            }
            else if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                isInputDown = touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
                inputPosition = touch.position;
            }

            if (Time.time - lastInputTime < inputDebounceTime)
                return;

            if (isInputDown && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                if (drawingState == DrawingState.Idle)
                {
                    StartDrawing(inputPosition);
                    drawingState = DrawingState.Drawing;
                    lastInputTime = Time.time;
                }
                else if (drawingState == DrawingState.Drawing)
                {
                    ContinueDrawing(inputPosition);
                }
            }
            else if (drawingState == DrawingState.Drawing)
            {
                FinalizeLine();
                drawingState = DrawingState.Idle;
                lastInputTime = Time.time;
            }
        }

        void StartDrawing(Vector2 screenPosition)
        {
            Vector3 worldPos = ScreenToWorldPosition(screenPosition);
            if (worldPos == Vector3.zero) return;

            if (currentLine != null)
            {
                if (currentLine.positionCount > 0)
                    allLines.Add(currentLine);
                else
                    Destroy(currentLine.gameObject);
                currentLine = null;
            }
            currentLinePoints.Clear();

            GameObject lineObj = new GameObject("TattooLine");
            lineObj.transform.SetParent(linesParent);
            lineObj.layer = LayerMask.NameToLayer("LineRenderer");

            currentLine = lineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(currentLine);

            currentLinePoints.Add(worldPos);
            lastWorldPosition = worldPos;

            UpdateLineRenderer();

            if (tattooMachine != null)
                tattooMachine.MoveTattooMachine(worldPos);
        }

        void ContinueDrawing(Vector2 screenPosition)
        {
            if (drawingState != DrawingState.Drawing || currentLine == null)
                return;

            Vector3 worldPos = ScreenToWorldPosition(screenPosition);
            if (worldPos == Vector3.zero) return;

            if (currentLinePoints.Count >= maxPointsPerLine)
            {
                FinalizeLine();
                StartDrawing(screenPosition);
                drawingState = DrawingState.Drawing;
                return;
            }

            if (Vector3.Distance(worldPos, lastWorldPosition) >= minDistanceThreshold)
            {
                currentLinePoints.Add(worldPos);
                lastWorldPosition = worldPos;
                UpdateLineRenderer();

                if (tattooMachine != null)
                    tattooMachine.MoveTattooMachine(worldPos);

                if (showRealTimeValidation && currentLinePoints.Count > 1)
                    ValidateDrawing();
            }
        }

        void FinalizeLine()
        {
            if (currentLine != null && currentLine.positionCount > 0)
            {
                allLines.Add(currentLine);
            }
            else if (currentLine != null)
            {
                Destroy(currentLine.gameObject);
            }

            currentLine = null;
            currentLinePoints.Clear();

            if (tattooMachine != null)
                tattooMachine.HideTattooMachine();
        }

        Vector3 ScreenToWorldPosition(Vector2 screenPos)
        {
            if (drawingCamera == null) return Vector3.zero;

            Vector3 worldPos = drawingCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -drawingCamera.transform.position.z));
            worldPos.z = 0;
            return worldPos;
        }

        void UpdateLineRenderer()
        {
            if (currentLine == null || currentLinePoints.Count == 0) return;

            currentLine.positionCount = currentLinePoints.Count;
            for (int i = 0; i < currentLinePoints.Count; i++)
            {
                currentLine.SetPosition(i, currentLinePoints[i]);
            }
        }

        void ConfigureLineRenderer(LineRenderer lr)
        {
            lr.useWorldSpace = true;
            float thickness = brushSizeValues[(int)brushSize];
            lr.startWidth = thickness;
            lr.endWidth = thickness;
            lr.startColor = brushColor;
            lr.endColor = brushColor;
            lr.material = lineMaterial;
            lr.sortingOrder = 10;

            lr.numCapVertices = lineCapVertices;
            lr.numCornerVertices = lineCornerVertices;
            lr.textureMode = textureMode;
            lr.alignment = alignment;

            lr.generateLightingData = false;
            lr.receiveShadows = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void TriggerValidation()
        {
            if (isValidatorInitialized)
            {
                ValidationResult result = ValidateDrawing();
                Debug.Log($"Validation triggered: Success {result.GetFinalScorePercentage():F1}%");
            }
            else
            {
                Debug.LogWarning("Validation not initialized!");
            }
        }

        public ValidationResult ValidateDrawing()
        {
            if (!isValidatorInitialized)
            {
                Debug.LogWarning("Validator not initialized!");
                return new ValidationResult();
            }

            RenderPlayerDrawingAsPng();
            RenderReferenceTexture();
            currentResult = CompareWithReference();
            OnValidationUpdate?.Invoke(currentResult);

            Debug.Log($"Validation completed: Success {currentResult.GetFinalScorePercentage():F1}%");
            return currentResult;
        }

        void RenderPlayerDrawingAsPng()
        {
            RenderTexture.active = referenceTexture;
            GL.Clear(true, true, Color.clear); // Ensure fully transparent
            validationCamera.Render();
            RenderTexture.active = null;

            // Debug check (optional, disable for mobile release)
            if (saveDebugTextures)
            {
                Texture2D debugTexture = RenderTextureToTexture2D(referenceTexture, TextureFormat.ARGB32);
                Color[] pixels = debugTexture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a > 0.1f && i % actualTextureSize < 10) // Check edges
                        Debug.LogWarning($"Non-transparent pixel detected at index {i}");
                }
                DestroyImmediate(debugTexture);
            }
        }

        void RenderReferenceTexture()
        {
            RenderTexture.active = referenceTexture;
            GL.Clear(true, true, Color.black);
            // Render referenceTattoo centered at (0,0,0) using validationCamera
            validationCamera.targetTexture = referenceTexture;
            Graphics.DrawTexture(new Rect(0, 0, actualTextureSize, actualTextureSize), referenceTattoo, new Material(Shader.Find("Unlit/Texture")));
            validationCamera.Render();
            RenderTexture.active = null;
        }

        ValidationResult CompareWithReference()
        {
            // Render player drawing directly to Texture2D
            Texture2D playerTexture = RenderTextureToTexture2D(referenceTexture, TextureFormat.ARGB32);
            Texture2D refTexture = new Texture2D(actualTextureSize, actualTextureSize);
            refTexture.SetPixels(referenceTattoo.GetPixels());
            refTexture.Apply();
            // Get pixel arrays once
            Color[] playerPixels = playerTexture.GetPixels();
            Color[] refPixels = refTexture.GetPixels();

            int totalReferenceOpaquePixels = 0;
            int overlappingOpaquePixels = 0;
            int outsidePixels = 0;
            float totalColorDistance = 0f;
            int colorComparisonCount = 0;

            // Single loop for efficiency
            for (int i = 0; i < playerPixels.Length; i++)
            {
                if (refPixels[i].a > 0.5f)
                {
                    totalReferenceOpaquePixels++;
                    if (playerPixels[i].a > 0.5f)
                    {
                        overlappingOpaquePixels++;
                        float rDiff = playerPixels[i].r - refPixels[i].r;
                        float gDiff = playerPixels[i].g - refPixels[i].g;
                        float bDiff = playerPixels[i].b - refPixels[i].b;
                        float distance = rDiff * rDiff + gDiff * gDiff + bDiff * bDiff; // Avoid sqrt for speed
                        totalColorDistance += distance;
                        colorComparisonCount++;
                    }
                }
                else if (playerPixels[i].a > 0.5f)
                {
                    outsidePixels++;
                }
            }
            
            // Shape score
            float shapeCoverage = totalReferenceOpaquePixels > 0 ? (float)overlappingOpaquePixels / totalReferenceOpaquePixels : 0f;
            float outsidePenalty = (float)outsidePixels / playerPixels.Length;
            float shapeScore = Mathf.Max(0f, shapeCoverage - outsidePenalty);

            // Color score (avoid sqrt in loop, normalize directly)
            float colorScore = 1f;
            if (colorComparisonCount > 0)
            {
                float avgColorDistance = totalColorDistance / colorComparisonCount;
                colorScore = avgColorDistance > 0 ? 1f - Mathf.Min(1f, Mathf.Sqrt(avgColorDistance)) : 1f; // Sqrt only once
            }

            // Final score
            float finalScore = (shapeWeight * shapeScore) + (colorWeight * colorScore);

            Debug.Log($"Debug: totalReferenceOpaquePixels={totalReferenceOpaquePixels}, overlappingOpaquePixels={overlappingOpaquePixels}, " +
                      $"outsidePixels={outsidePixels}, avgColorDistance={Mathf.Sqrt(totalColorDistance / colorComparisonCount):F2}");

            if (saveDebugTextures && Application.platform != RuntimePlatform.Android)
            {
                StartCoroutine(SaveDebugTextures(playerTexture, refTexture, null, null));
            }
            else
            {
                DestroyImmediate(playerTexture);
                DestroyImmediate(refTexture);
            }

            return new ValidationResult
            {
                shapeScore = shapeScore,
                finalScore = finalScore,
                correctShapePixels = overlappingOpaquePixels,
                incorrectShapePixels = totalReferenceOpaquePixels - overlappingOpaquePixels,
                totalReferenceShapePixels = totalReferenceOpaquePixels,
                totalPlayerShapePixels = overlappingOpaquePixels + outsidePixels
            };
        }

        Texture2D RenderTextureToTexture2D(RenderTexture renderTexture, TextureFormat format)
        {
            RenderTexture.active = renderTexture;
            renderTexture.depthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt;
            renderTexture.depth = 32;
            Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, format, false);
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = null;
            return texture2D;
        }

        IEnumerator SaveDebugTextures(Texture2D playerTexture, Texture2D refTexture, Texture2D refEdges, Texture2D playerEdges)
        {
            yield return new WaitForEndOfFrame();

            string folderPath = Path.Combine(Application.persistentDataPath, "TattooDebug");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            byte[] playerBytes = playerTexture.EncodeToPNG();
            string playerPath = Path.Combine(folderPath, $"player_drawing_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(playerPath, playerBytes);

            byte[] refBytes = refTexture.EncodeToPNG();
            string refPath = Path.Combine(folderPath, $"reference_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(refPath, refBytes);

            Debug.Log($"Debug textures saved to: {playerPath}, {refPath}");
            DestroyImmediate(playerTexture);
            DestroyImmediate(refTexture);
        }

        public void SetBrushSize(BrushSize size)
        {
            brushSize = size;
            if (currentLine != null)
            {
                float thickness = brushSizeValues[(int)brushSize];
                currentLine.startWidth = thickness;
                currentLine.endWidth = thickness;
            }
        }

        public void SetBrushColor(string hexColor)
        {
            brushColorHexCode = hexColor;
            ParseBrushColor();
            if (currentLine != null)
            {
                currentLine.startColor = brushColor;
                currentLine.endColor = brushColor;
            }
        }

        public void SetBrushColor(Color color)
        {
            brushColor = color;
            brushColorHexCode = "#" + ColorUtility.ToHtmlStringRGB(color);
            if (currentLine != null)
            {
                currentLine.startColor = brushColor;
                currentLine.endColor = brushColor;
            }
        }

        public void ClearDrawing()
        {
            foreach (var line in allLines)
            {
                if (line != null)
                    DestroyImmediate(line.gameObject);
            }
            allLines.Clear();
            currentLine = null;
            currentLinePoints.Clear();
            drawingState = DrawingState.Idle;
        }

        public int GetTotalPointCount()
        {
            int totalPoints = 0;
            foreach (var line in allLines)
            {
                if (line != null)
                    totalPoints += line.positionCount;
            }
            return totalPoints;
        }

        public float GetSuccessPercentage()
        {
            return currentResult.GetFinalScorePercentage();
        }

        public string GetFormattedResults()
        {
            return $"Success: {currentResult.GetFinalScorePercentage():F1}%";
        }

        public string GetDetailedDebugInfo()
        {
            return currentResult.GetDetailedDebugInfo();
        }

        public ValidationQuality GetCurrentQuality()
        {
            return currentResult.GetQualityLevel();
        }

        public bool IsCurrentScorePassing(float threshold = 0.6f)
        {
            return currentResult.IsPassingScore(threshold);
        }

        void OnDestroy()
        {
            if (referenceTexture != null)
                referenceTexture.Release();
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Now")]
        void ValidateNow()
        {
            if (Application.isPlaying)
            {
                TriggerValidation();
            }
        }

        [ContextMenu("Clear Drawing")]
        void ClearDrawingDebug()
        {
            if (Application.isPlaying)
                ClearDrawing();
        }
#endif
    }

    [System.Serializable]
    public class ValidationResult
    {
        public float shapeScore;
        public float finalScore;
        public int correctShapePixels;
        public int incorrectShapePixels;
        public int totalReferenceShapePixels;
        public int totalPlayerShapePixels;

        public float GetFinalScorePercentage()
        {
            return finalScore * 100f;
        }

        public string GetFormattedResults()
        {
            return $"Success: {GetFinalScorePercentage():F1}%";
        }

        public string GetDetailedDebugInfo()
        {
            return $"Shape Score: {shapeScore * 100:F1}%\n" +
                   $"Final Score: {GetFinalScorePercentage():F1}%\n" +
                   $"Correct Shape Pixels: {correctShapePixels}\n" +
                   $"Incorrect Shape Pixels: {incorrectShapePixels}\n" +
                   $"Total Reference Shape Pixels: {totalReferenceShapePixels}\n" +
                   $"Total Player Shape Pixels: {totalPlayerShapePixels}";
        }

        public ValidationQuality GetQualityLevel()
        {
            float score = finalScore;
            if (score >= 0.9f) return ValidationQuality.Excellent;
            if (score >= 0.7f) return ValidationQuality.Good;
            if (score >= 0.5f) return ValidationQuality.Fair;
            return ValidationQuality.Poor;
        }

        public bool IsPassingScore(float threshold)
        {
            return finalScore >= threshold;
        }
    }

    public enum ValidationQuality
    {
        Poor,
        Fair,
        Good,
        Excellent
    }
}