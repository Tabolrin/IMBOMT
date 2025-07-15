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
        [SerializeField] public Texture2D referenceTattoo;
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