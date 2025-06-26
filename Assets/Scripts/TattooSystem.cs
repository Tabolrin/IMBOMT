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
        [SerializeField] private float inputDebounceTime = 0.05f; // Debounce rapid inputs

        [Header("Validation Settings")]
        [SerializeField] private Texture2D referenceTattoo;
        [SerializeField] private Camera validationCamera;
        [SerializeField] private int renderTextureSize = 512;
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
                Debug.LogError("LineRenderer layer not found! Please create a layer named 'LineRenderer' in Tags and Layers.");
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
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB565) 
                ? RenderTextureFormat.RGB565 : RenderTextureFormat.Default;

            playerDrawingTexture = new RenderTexture(actualTextureSize, actualTextureSize, 0, format);
            playerDrawingTexture.name = "PlayerDrawingTexture";
            playerDrawingTexture.filterMode = FilterMode.Point;
            playerDrawingTexture.useMipMap = false;
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
                Debug.LogWarning($"Invalid color hex code: {brushColorHexCode}. Using white instead.");
            }
        }

        void Update()
        {
            // Handle input in a frame-based manner
            bool isInputDown = false;
            Vector2 inputPosition = Vector2.zero;

            // Editor: Use mouse input
            if (Application.isEditor)
            {
                isInputDown = Input.GetMouseButton(0);
                inputPosition = Input.mousePosition;
            }
            // Mobile: Use touch input
            else if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                isInputDown = touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
                inputPosition = touch.position;
            }

            // Debounce input to avoid rapid state changes
            if (Time.time - lastInputTime < inputDebounceTime)
            {
                Debug.Log($"Update: Input debounced, time since last input: {Time.time - lastInputTime}");
                return;
            }

            // Handle state transitions
            if (isInputDown && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                if (drawingState == DrawingState.Idle)
                {
                    StartDrawing(inputPosition);
                    drawingState = DrawingState.Drawing;
                    lastInputTime = Time.time;
                    Debug.Log($"Update: Transition to Drawing, pos: {inputPosition}");
                }
                else if (drawingState == DrawingState.Drawing)
                {
                    ContinueDrawing(inputPosition);
                    Debug.Log($"Update: Continuing drawing, pos: {inputPosition}, points: {currentLinePoints.Count}");
                }
            }
            else if (drawingState == DrawingState.Drawing)
            {
                FinalizeLine();
                drawingState = DrawingState.Idle;
                lastInputTime = Time.time;
                Debug.Log("Update: Transition to Idle, input released");
            }
        }

        void StartDrawing(Vector2 screenPosition)
        {
            Vector3 worldPos = ScreenToWorldPosition(screenPosition);
            if (worldPos == Vector3.zero) return;

            // Force clear any existing line
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
                tattooMachine.MoveTattooMachine(worldPos);////////////////////////

            Debug.Log($"StartDrawing: New line at world pos: {worldPos}, points: {currentLinePoints.Count}");
        }

        void ContinueDrawing(Vector2 screenPosition)
        {
            if (drawingState != DrawingState.Drawing || currentLine == null)
            {
                Debug.LogWarning($"ContinueDrawing: Aborted (state={drawingState}, currentLine={(currentLine == null ? "null" : "not null")})");
                return;
            }

            Vector3 worldPos = ScreenToWorldPosition(screenPosition);
            if (worldPos == Vector3.zero) return;

            if (currentLinePoints.Count >= maxPointsPerLine)
            {
                FinalizeLine();
                StartDrawing(screenPosition);
                drawingState = DrawingState.Drawing;
                Debug.Log("ContinueDrawing: Max points reached, starting new line");
                return;
            }

            if (Vector3.Distance(worldPos, lastWorldPosition) >= minDistanceThreshold)
            {
                currentLinePoints.Add(worldPos);
                lastWorldPosition = worldPos;
                UpdateLineRenderer();

                if (tattooMachine != null)
                    tattooMachine.MoveTattooMachine(worldPos);//////////////////////////////

                if (showRealTimeValidation && currentLinePoints.Count > 1)
                    ValidateDrawing();

                Debug.Log($"ContinueDrawing: Added point at {worldPos}, total points: {currentLinePoints.Count}");
            }
        }

        void FinalizeLine()
        {
            if (currentLine != null && currentLinePoints.Count > 0)
            {
                allLines.Add(currentLine);
                Debug.Log($"FinalizeLine: Line added to allLines, total lines: {allLines.Count}, points: {currentLinePoints.Count}");
            }
            else if (currentLine != null)
            {
                Destroy(currentLine.gameObject);
                Debug.Log("FinalizeLine: Empty line destroyed");
            }

            currentLine = null;
            currentLinePoints.Clear();

            tattooMachine.HideTattooMachine(); ///////////////////
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

        public ValidationResult ValidateDrawing()
        {
            if (!isValidatorInitialized)
            {
                Debug.LogWarning("Validator not initialized!");
                return new ValidationResult();
            }

            Debug.Log($"Validating drawing with {GetTotalPointCount()} total points");
            RenderPlayerDrawing();
            currentResult = CompareWithReference();
            OnValidationUpdate?.Invoke(currentResult);

            Debug.Log($"Validation completed: {currentResult.GetFormattedResults()}");
            return currentResult;
        }

        void RenderPlayerDrawing()
        {
            RenderTexture.active = playerDrawingTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = null;
            validationCamera.Render();
        }

        ValidationResult CompareWithReference()
        {
            Texture2D playerTexture = RenderTextureToTexture2D(playerDrawingTexture);

            int correctPixels = 0;
            int incorrectPixels = 0;
            int totalReferencePixels = 0;
            int totalPlayerPixels = 0;

            int sampleStep = actualTextureSize > 512 ? 2 : 1;

            for (int x = 0; x < actualTextureSize; x += sampleStep)
            {
                for (int y = 0; y < actualTextureSize; y += sampleStep)
                {
                    float refU = (float)x / actualTextureSize;
                    float refV = (float)y / actualTextureSize;
                    Color refPixel = referenceTattoo.GetPixelBilinear(refU, refV);
                    bool isReferencePath = refPixel.grayscale > 0.5f;

                    Color playerPixel = playerTexture.GetPixel(x, y);
                    bool isPlayerDrawn = playerPixel.grayscale > 0.1f;

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

            float coverage = totalReferencePixels > 0 ? (float)correctPixels / totalReferencePixels : 0f;
            float accuracy = totalPlayerPixels > 0 ? (float)correctPixels / totalPlayerPixels : 0f;
            float outsidePenalty = totalReferencePixels > 0 ? (float)incorrectPixels / totalReferencePixels : 0f;
            float finalScore = Mathf.Clamp01(coverage - (outsidePenalty * penaltyWeight));

            if (saveDebugTextures && Application.platform != RuntimePlatform.Android)
            {
                StartCoroutine(SaveDebugTextures(playerTexture));
            }
            else
            {
                DestroyImmediate(playerTexture);
            }

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
            DestroyImmediate(playerTexture);
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
            Debug.Log($"Brush size set to: {brushSize} ({brushSizeValues[(int)brushSize]})");
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
            Debug.Log($"Brush color set to: {hexColor}");
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
            Debug.Log($"Brush color set to: {brushColorHexCode}");
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
            Debug.Log("ClearDrawing: All lines cleared, state set to Idle");
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

        public float GetCoveragePercentage() => currentResult.GetCoveragePercentage();
        public float GetAccuracyPercentage() => currentResult.GetAccuracyPercentage();
        public float GetPenaltyPercentage() => currentResult.GetPenaltyPercentage();
        public float GetFinalScorePercentage() => currentResult.GetFinalScorePercentage();

        public string GetFormattedResults()
        {
            return currentResult.GetFormattedResults();
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
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Now")]
        void ValidateNow()
        {
            if (Application.isPlaying)
            {
                ValidationResult result = ValidateDrawing();
                Debug.Log($"Validation Result: {GetFormattedResults()}");
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
}