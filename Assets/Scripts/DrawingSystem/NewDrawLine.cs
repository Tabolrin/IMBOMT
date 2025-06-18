using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DrawingSystem
{
    public class Draw : MonoBehaviour
    {
        [Header("Drawing Settings")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private float minDistanceThreshold = 0.05f; // Smaller for touch precision
        [SerializeField] private Camera drawingCamera;
        [SerializeField] private float touchSensitivity = 1f;
    
        [Header("Android Optimization")]
        [SerializeField] private int maxPointsPerLine = 1000; // Limit points for performance
        [SerializeField] private bool optimizeForMobile = true;
        [SerializeField] private float smoothingFactor = 0.1f; // Touch smoothing
    
        [Header("Validation")]
        [SerializeField] private TattooValidator validator;
    
        [Header("Input Actions")]
        [SerializeField] private InputActionAsset inputActions;
    
        [Header("Brush Settings")]
        [SerializeField] public float brushThickness = 0.08f;
        [SerializeField] public string brushColorHexCode = "#000000";
    
        // Input Actions
        private InputAction touchPositionAction;
        private InputAction touchPressAction;
        private InputAction touchContactAction;
    
        // Drawing state
        private List<Vector3> linePoints = new List<Vector3>();
        private bool isDrawing = false;
        private Vector2 lastTouchPosition;
        private Vector3 lastWorldPosition;
    
        // Touch smoothing
        private Queue<Vector2> touchHistory = new Queue<Vector2>();
        private int maxTouchHistorySize = 3;
    
        void Awake()
        {
            // Initialize input actions
            if (inputActions != null)
            {
                var drawingMap = inputActions.FindActionMap("Drawing");
                touchPositionAction = drawingMap.FindAction("TouchPosition");
                touchPressAction = drawingMap.FindAction("TouchPress");
                touchContactAction = drawingMap.FindAction("TouchContact");
            }
            else
            {
                Debug.LogError("Input Actions asset not assigned!");
            }
        }
    
        void Start()
        {
            InitializeComponents();
            SetupInputActions();
            ConfigureLineRenderer();
        }
    
        void InitializeComponents()
        {
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();
            
            if (drawingCamera == null)
                drawingCamera = Camera.main;
            
            if (validator == null)
                validator = FindFirstObjectByType<TattooValidator>();
            
            // Set the LineRenderer to the correct layer for validation
            gameObject.layer = LayerMask.NameToLayer("LineRenderer");
        
            Debug.Log("TouchDrawingSystem initialized for " + Application.platform);
        }
    
        public void ConfigureLineRenderer()
        {
            lineRenderer.positionCount = 0;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = brushThickness;
            lineRenderer.endWidth = brushThickness;
            
            if (ColorUtility.TryParseHtmlString(brushColorHexCode, out Color newColor))
            {
                lineRenderer.startColor = newColor;
                lineRenderer.endColor = newColor;
            }
            
            lineRenderer.positionCount = 0;
            lineRenderer.useWorldSpace = true;
        
            // Mobile optimization
            if (optimizeForMobile && Application.platform == RuntimePlatform.Android)
            {
                // Reduce line renderer quality for better performance
                lineRenderer.numCornerVertices = 2;
                lineRenderer.numCapVertices = 2;
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;
            
                // Adjust distance threshold for touch
                minDistanceThreshold = Mathf.Max(minDistanceThreshold, 0.03f);
            }
        }
    
        void SetupInputActions()
        {
            if (touchPressAction != null)
            {
                touchPressAction.performed += OnTouchPress;
                touchPressAction.canceled += OnTouchRelease;
            }
        
            // Enable actions
            inputActions?.Enable();
        }
    
        void Update()
        {
            if (isDrawing)
            {
                HandleContinuousDrawing();
            }
        
            // Handle validation input (optional - can be triggered by UI)
            HandleDebugInput();
        }
    
        void OnTouchPress(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                StartDrawing();
            }
        }
    
        void OnTouchRelease(InputAction.CallbackContext context)
        {
            if (context.canceled && isDrawing)
            {
                StopDrawing();
            }
        }
    
        void StartDrawing()
        {
            if (!touchPositionAction.enabled) return;
        
            isDrawing = true;
            linePoints.Clear();
            touchHistory.Clear();
        
            Vector2 touchPos = touchPositionAction.ReadValue<Vector2>();
            Vector3 worldPos = GetTouchWorldPosition(touchPos);
        
            if (worldPos != Vector3.zero)
            {
                linePoints.Add(worldPos);
                lastTouchPosition = touchPos;
                lastWorldPosition = worldPos;
                touchHistory.Enqueue(touchPos);
                UpdateLineRenderer();
            
                Debug.Log("Started drawing at: " + worldPos);
            }
        }
    
        void HandleContinuousDrawing()
        {
            if (!touchContactAction.ReadValue<float>().Equals(1f)) // Touch not active
            {
                StopDrawing();
                return;
            }
        
            Vector2 currentTouchPos = touchPositionAction.ReadValue<Vector2>();
        
            // Apply smoothing for better touch experience
            Vector2 smoothedTouchPos = ApplyTouchSmoothing(currentTouchPos);
        
            Vector3 worldPos = GetTouchWorldPosition(smoothedTouchPos);
            if (worldPos == Vector3.zero) return;
        
            // Check distance threshold
            float distance = Vector3.Distance(worldPos, lastWorldPosition);
            if (distance >= minDistanceThreshold)
            {
                // Performance limit check
                if (linePoints.Count >= maxPointsPerLine)
                {
                    Debug.LogWarning("Maximum points reached, stopping drawing");
                    StopDrawing();
                    return;
                }
            
                linePoints.Add(worldPos);
                lastTouchPosition = smoothedTouchPos;
                lastWorldPosition = worldPos;
                UpdateLineRenderer();
            }
        }
    
        Vector2 ApplyTouchSmoothing(Vector2 currentPos)
        {
            touchHistory.Enqueue(currentPos);
        
            if (touchHistory.Count > maxTouchHistorySize)
                touchHistory.Dequeue();
        
            // Simple moving average
            Vector2 smoothed = Vector2.zero;
            foreach (Vector2 pos in touchHistory)
                smoothed += pos;
            smoothed /= touchHistory.Count;
        
            // Blend with current position
            return Vector2.Lerp(currentPos, smoothed, smoothingFactor);
        }
    
        void StopDrawing()
        {
            isDrawing = false;
            touchHistory.Clear();
        
            Debug.Log($"Stopped drawing. Total points: {linePoints.Count}");
        
            // Auto-validate when drawing stops
            if (validator != null && linePoints.Count > 1)
            {
                // Small delay to ensure rendering is complete
                Invoke(nameof(ValidateDrawing), 0.15f);
            }
        }
    
        Vector3 GetTouchWorldPosition(Vector2 touchScreenPos)
        {
            // Convert screen position to world position
            Vector3 screenPos = new Vector3(touchScreenPos.x, touchScreenPos.y, 
                Mathf.Abs(drawingCamera.transform.position.z));
        
            Vector3 worldPos = drawingCamera.ScreenToWorldPoint(screenPos);
            worldPos.z = 0; // Keep drawing on Z=0 plane
        
            return worldPos;
        }
    
        void UpdateLineRenderer()
        {
            lineRenderer.positionCount = linePoints.Count;
            for (int i = 0; i < linePoints.Count; i++)
            {
                lineRenderer.SetPosition(i, linePoints[i]);
            }
        }
    
        public void ValidateDrawing()
        {
            if (validator != null)
            {
                ValidationResult result = validator.ValidateCurrentDrawing();
                DisplayValidationResults(result);
            }
            else
            {
                Debug.LogWarning("No TattooValidator found!");
            }
        }
    
        void DisplayValidationResults(ValidationResult result)
        {
            string resultText = $"TATTOO VALIDATION RESULTS:\n" +
                                $"Coverage: {result.coverage * 100f:F1}%\n" +
                                $"Accuracy: {result.accuracy * 100f:F1}%\n" +
                                $"Outside Penalty: {result.outsidePenalty * 100f:F1}%\n" +
                                $"Final Score: {result.finalScore * 100f:F1}%";
                          
            Debug.Log(resultText);
        
            // Trigger haptic feedback on mobile
            if (Application.platform == RuntimePlatform.Android)
            {
                // Simple vibration feedback
#if UNITY_ANDROID
                Handheld.Vibrate();
#endif
            }
        }
    
        public void ClearDrawing()
        {
            linePoints.Clear();
            lineRenderer.positionCount = 0;
            isDrawing = false;
            touchHistory.Clear();
            Debug.Log("Drawing cleared");
        }
    
        void HandleDebugInput()
        {
            // For testing in editor - remove in production
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.Space))
                ValidateDrawing();
            if (Input.GetKeyDown(KeyCode.C))
                ClearDrawing();
#endif
        }
    
        // Public methods for UI buttons
        public void OnValidateButtonPressed()
        {
            ValidateDrawing();
        }
    
        public void OnClearButtonPressed()
        {
            ClearDrawing();
        }
    
        // Input system lifecycle
        void OnEnable()
        {
            inputActions?.Enable();
        }
    
        void OnDisable()
        {
            inputActions?.Disable();
        }
    
        void OnDestroy()
        {
            if (touchPressAction != null)
            {
                touchPressAction.performed -= OnTouchPress;
                touchPressAction.canceled -= OnTouchRelease;
            }
        
            inputActions?.Disable();
        }
    }
}