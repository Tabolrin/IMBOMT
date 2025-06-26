//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.InputSystem;

//namespace DrawingSystem
//{
//    public class Draw : MonoBehaviour
//    {
//        [Header("Drawing Settings")]
//        [SerializeField] private LineRenderer lineRenderer;
//        [SerializeField] private float minDistanceThreshold = 0.01f; // Reduced for smoother drawing
//        [SerializeField] private Camera drawingCamera;
//        [SerializeField] private float touchSensitivity = 1f;
//        [SerializeField] public float brushThickness = 0.15f; // Increased for visibility
//        [SerializeField] public string brushColorHexCode = "#FFFFFF"; // White for visibility

//        [Header("Android Optimization")]
//        [SerializeField] private int maxPointsPerLine = 1000; // Limit points for performance
//        [SerializeField] private bool optimizeForMobile = true;
//        [SerializeField] private float smoothingFactor = 0.2f; // Touch smoothing

//        [Header("Validation")]
//        [SerializeField] private TattooValidator validator;

//        [Header("Input Actions")]
//        [SerializeField] private InputActionAsset inputActions;

//        // Input Actions
//        private InputAction touchPositionAction;
//        private InputAction touchPressAction;
//        private InputAction touchContactAction;

//        // Drawing state
//        private List<Vector3> linePoints = new List<Vector3>();
//        private bool isDrawing = false;
//        private Vector2 lastTouchPosition;
//        private Vector3 lastWorldPosition;

//        // Touch smoothing
//        private Queue<Vector2> touchHistory = new Queue<Vector2>();
//        private int maxTouchHistorySize = 3;

//        void Awake()
//        {
//            if (inputActions == null)
//            {
//                Debug.LogError("Input Actions asset not assigned!");
//                return;
//            }
//            var drawingMap = inputActions.FindActionMap("Drawing", true);
//            touchPositionAction = drawingMap.FindAction("TouchPosition", true);
//            touchPressAction = drawingMap.FindAction("TouchPress", true);
//            touchContactAction = drawingMap.FindAction("TouchContact", true);
//        }

//        void Start()
//        {
//            InitializeComponents();
//            SetupInputActions();
//            ConfigureLineRenderer();
//        }

//        void InitializeComponents()
//        {
//            if (lineRenderer == null)
//            {
//                lineRenderer = GetComponent<LineRenderer>();
//                if (lineRenderer == null)
//                {
//                    Debug.LogError("LineRenderer component not found on this GameObject!");
//                    return;
//                }
//            }

//            if (drawingCamera == null)
//            {
//                drawingCamera = Camera.main;
//                if (drawingCamera == null)
//                {
//                    Debug.LogError("Main Camera not found!");
//                    return;
//                }
//            }

//            if (validator == null)
//            {
//                validator = FindFirstObjectByType<TattooValidator>();
//                if (validator == null)
//                {
//                    Debug.LogWarning("TattooValidator not found in scene!");
//                }
//            }

//            // Set the LineRenderer and its GameObject to the "LineRenderer" layer
//            int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
//            if (lineRendererLayer == -1)
//            {
//                Debug.LogError("LineRenderer layer not found! Please create a layer named 'LineRenderer' in Tags and Layers.");
//                return;
//            }
//            gameObject.layer = lineRendererLayer;
//            lineRenderer.gameObject.layer = lineRendererLayer;

//            // Ensure main camera renders the LineRenderer layer
//            if ((drawingCamera.cullingMask & (1 << lineRendererLayer)) == 0)
//            {
//                Debug.LogWarning("Main Camera Culling Mask does not include LineRenderer layer! Fixing...");
//                drawingCamera.cullingMask |= 1 << lineRendererLayer;
//            }

//            Debug.Log($"TouchDrawingSystem initialized for {Application.platform}. Camera Position: {drawingCamera.transform.position}, Orthographic Size: {drawingCamera.orthographicSize}");
//        }

//        public void ConfigureLineRenderer()
//        {
//            lineRenderer.positionCount = 0;
//            lineRenderer.useWorldSpace = true;
//            lineRenderer.startWidth = brushThickness;
//            lineRenderer.endWidth = brushThickness;

//            // Ensure a valid material
//            if (lineRenderer.material == null)
//            {
//                Debug.LogWarning("LineRenderer material is null! Assigning default material.");
//                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
//            }

//            if (ColorUtility.TryParseHtmlString(brushColorHexCode, out Color newColor))
//            {
//                lineRenderer.startColor = newColor;
//                lineRenderer.endColor = newColor;
//            }
//            else
//            {
//                Debug.LogError("Invalid brush color hex code: " + brushColorHexCode);
//                lineRenderer.startColor = Color.white;
//                lineRenderer.endColor = Color.white;
//            }

//            // Mobile optimization
//            if (optimizeForMobile && Application.platform == RuntimePlatform.Android)
//            {
//                lineRenderer.numCornerVertices = 2;
//                lineRenderer.numCapVertices = 2;
//                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
//                lineRenderer.receiveShadows = false;
//                minDistanceThreshold = Mathf.Max(minDistanceThreshold, 0.03f);
//            }

//            Debug.Log($"LineRenderer configured: Width: {brushThickness}, Material: {lineRenderer.material.name}, Color: {lineRenderer.startColor}");
//        }

//        void SetupInputActions()
//        {
//            if (touchPressAction != null)
//            {
//                touchPressAction.performed += OnTouchPress;
//                touchPressAction.canceled += OnTouchRelease;
//            }

//            inputActions?.Enable();
//        }

//        void Update()
//        {
//            #if UNITY_EDITOR
//            if (Input.GetMouseButtonDown(0))
//            {
//                StartDrawingMouse();
//            }
//            else if (Input.GetMouseButton(0))
//            {
//                HandleContinuousDrawingMouse();
//            }
//            else if (Input.GetMouseButtonUp(0))
//            {
//                StopDrawing();
//            }
//            #endif

//            if (isDrawing)
//            {
//                HandleContinuousDrawing();
//            }

//            HandleDebugInput();
//        }

//        void OnTouchPress(InputAction.CallbackContext context)
//        {
//            if (context.performed)
//            {
//                StartDrawing();
//            }
//        }

//        void OnTouchRelease(InputAction.CallbackContext context)
//        {
//            if (context.canceled && isDrawing)
//            {
//                StopDrawing();
//            }
//        }

//        void StartDrawing()
//        {
//            if (!touchPositionAction.enabled) return;

//            isDrawing = true;
//            linePoints.Clear();
//            touchHistory.Clear();

//            Vector2 touchPos = touchPositionAction.ReadValue<Vector2>();
//            Vector3 worldPos = GetTouchWorldPosition(touchPos);

//            if (worldPos != Vector3.zero)
//            {
//                linePoints.Add(worldPos);
//                lastTouchPosition = touchPos;
//                lastWorldPosition = worldPos;
//                touchHistory.Enqueue(touchPos);
//                UpdateLineRenderer();
//                Debug.Log($"Started drawing at: {worldPos}, Screen Pos: {touchPos}, Points: {linePoints.Count}");
//            }
//        }

//        void StartDrawingMouse()
//        {
//            isDrawing = true;
//            linePoints.Clear();
//            touchHistory.Clear();
//            Vector2 mousePos = Input.mousePosition;
//            Vector3 worldPos = GetTouchWorldPosition(mousePos);
//            if (worldPos != Vector3.zero)
//            {
//                linePoints.Add(worldPos);
//                lastTouchPosition = mousePos;
//                lastWorldPosition = worldPos;
//                touchHistory.Enqueue(mousePos);
//                UpdateLineRenderer();
//                Debug.Log($"Started drawing (mouse) at: {worldPos}, Screen Pos: {mousePos}, Points: {linePoints.Count}");
//            }

//        }

//        void HandleContinuousDrawing()
//        {
//            float contactValue = touchContactAction.ReadValue<float>();
//            if (contactValue < 0.5f) // Relaxed check
//            {
//                Debug.Log($"Stopping drawing due to low contact value: {contactValue}");
//                StopDrawing();
//                return;
//            }

//            Vector2 currentTouchPos = touchPositionAction.ReadValue<Vector2>();
//            Vector2 smoothedTouchPos = ApplyTouchSmoothing(currentTouchPos);
//            Vector3 worldPos = GetTouchWorldPosition(smoothedTouchPos);
//            if (worldPos == Vector3.zero) return;

//            float distance = Vector3.Distance(worldPos, lastWorldPosition);
//            if (distance >= minDistanceThreshold && worldPos != lastWorldPosition) // Prevent duplicate points
//            {
//                if (linePoints.Count >= maxPointsPerLine)
//                {
//                    Debug.LogWarning("Maximum points reached, stopping drawing");
//                    StopDrawing();
//                    return;
//                }
//                linePoints.Add(worldPos);
//                lastTouchPosition = smoothedTouchPos;
//                lastWorldPosition = worldPos;
//                UpdateLineRenderer();
//                Debug.Log($"Added point at: {worldPos}, Distance: {distance}, Points: {linePoints.Count}, Contact: {contactValue}");
//            }
//        }

//        void HandleContinuousDrawingMouse()
//        {
//            Vector2 mousePos = Input.mousePosition;
//            Vector2 smoothedMousePos = ApplyTouchSmoothing(mousePos);
//            Vector3 worldPos = GetTouchWorldPosition(smoothedMousePos);
//            if (worldPos == Vector3.zero) return;
//            float distance = Vector3.Distance(worldPos, lastWorldPosition);
//            if (distance >= minDistanceThreshold && worldPos != lastWorldPosition) // Prevent duplicate points
//            {
//                if (linePoints.Count >= maxPointsPerLine)
//                {
//                    Debug.LogWarning("Maximum points reached, stopping drawing");
//                    StopDrawing();
//                    return;
//                }
//                linePoints.Add(worldPos);
//                lastTouchPosition = smoothedMousePos;
//                lastWorldPosition = worldPos;
//                UpdateLineRenderer();
//                Debug.Log($"Added point (mouse) at: {worldPos}, Distance: {distance}, Points: {linePoints.Count}");
//            }
//        }

//        Vector2 ApplyTouchSmoothing(Vector2 currentPos)
//        {
//            touchHistory.Enqueue(currentPos);
//            if (touchHistory.Count > maxTouchHistorySize)
//                touchHistory.Dequeue();

//            Vector2 smoothed = Vector2.zero;
//            foreach (Vector2 pos in touchHistory)
//                smoothed += pos;
//            smoothed /= touchHistory.Count;
//            return Vector2.Lerp(currentPos, smoothed, smoothingFactor);
//        }

//        void StopDrawing()
//        {
//            isDrawing = false;
//            touchHistory.Clear();
//            Debug.Log($"Stopped drawing. Total points: {linePoints.Count}");
//            if (validator != null && linePoints.Count > 1)
//            {
//                Invoke(nameof(ValidateDrawing), 0.3f);
//            }
//        }

//        Vector3 GetTouchWorldPosition(Vector2 touchScreenPos)
//        {
//            Vector3 screenPos = new Vector3(touchScreenPos.x, touchScreenPos.y, 10f); // Fixed distance
//            Vector3 worldPos = drawingCamera.ScreenToWorldPoint(screenPos);
//            worldPos.z = 0;
//            Debug.Log($"ScreenToWorld: Screen: {touchScreenPos}, World: {worldPos}");
//            return worldPos;
//        }

//        void UpdateLineRenderer()
//        {
//            lineRenderer.positionCount = linePoints.Count;
//            for (int i = 0; i < linePoints.Count; i++)
//            {
//                lineRenderer.SetPosition(i, linePoints[i]);
//            }
//            Debug.Log($"Updated LineRenderer with {linePoints.Count} points. Bounds: {GetLineRendererBounds()}");
//        }

//        private Bounds GetLineRendererBounds()
//        {
//            if (linePoints.Count == 0) return new Bounds(Vector3.zero, Vector3.zero);
//            Bounds bounds = new Bounds(linePoints[0], Vector3.zero);
//            foreach (Vector3 point in linePoints)
//            {
//                bounds.Encapsulate(point);
//            }
//            return bounds;
//        }

//        public void ValidateDrawing()
//        {
//            if (validator != null)
//            {
//                ValidationResult result = validator.ValidateCurrentDrawing();
//                DisplayValidationResults(result);
//            }
//            else
//            {
//                Debug.LogWarning("No TattooValidator found!");
//            }
//        }

//        void DisplayValidationResults(ValidationResult result)
//        {
//            string resultText = $"TATTOO VALIDATION RESULTS:\n" +
//                                $"Coverage: {result.coverage * 100f:F1}%\n" +
//                                $"Accuracy: {result.accuracy * 100f:F1}%\n" +
//                                $"Outside Penalty: {result.outsidePenalty * 100f:F1}%\n" +
//                                $"Final Score: {result.finalScore * 100f:F1}%";
//            Debug.Log(resultText);

//            if (Application.platform == RuntimePlatform.Android)
//            {
//                #if UNITY_ANDROID
//                Handheld.Vibrate();
//                #endif
//            }
//        }

//        public void ClearDrawing()
//        {
//            linePoints.Clear();
//            lineRenderer.positionCount = 0;
//            isDrawing = false;
//            touchHistory.Clear();
//            Debug.Log("Drawing cleared");
//        }

//        void HandleDebugInput()
//        {
//            #if UNITY_EDITOR
//            if (Input.GetKeyDown(KeyCode.Space))
//                ValidateDrawing();
//            if (Input.GetKeyDown(KeyCode.C))
//                ClearDrawing();
//            #endif
//        }

//        public void OnValidateButtonPressed()
//        {
//            ValidateDrawing();
//        }

//        public void OnClearButtonPressed()
//        {
//            ClearDrawing();
//        }

//        void OnEnable()
//        {
//            inputActions?.Enable();
//        }

//        void OnDisable()
//        {
//            inputActions?.Disable();
//        }

//        void OnDestroy()
//        {
//            if (touchPressAction != null)
//            {
//                touchPressAction.performed -= OnTouchPress;
//                touchPressAction.canceled -= OnTouchRelease;
//            }
//            inputActions?.Disable();
//        }

//        // Debug gizmos to visualize LineRenderer points
//        void OnDrawGizmos()
//        {
//            if (linePoints == null || linePoints.Count == 0) return;
//            Gizmos.color = Color.red;
//            foreach (Vector3 point in linePoints)
//            {
//                Gizmos.DrawSphere(point, 0.05f);
//            }
//        }
//    }
//}