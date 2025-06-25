// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.InputSystem;
//
// namespace DrawingSystem
// {
//     public class Draw : MonoBehaviour
//     {
//         [Header("Drawing Settings")]
//         [SerializeField] private GameObject linePrefab;
//         [SerializeField] private Transform linesParent;
//         [SerializeField] private float minDistanceThreshold = 0.01f;
//         [SerializeField] private Camera drawingCamera;
//         [SerializeField] private float touchSensitivity = 1f;
//         [SerializeField] public float brushThickness = 0.15f;
//         [SerializeField] public string brushColorHexCode = "#FFFFFF";
//
//         [Header("Android Optimization")]
//         [SerializeField] private int maxPointsPerLine = 1000;
//         [SerializeField] private bool optimizeForMobile = true;
//         [SerializeField] private float smoothingFactor = 0.2f;
//
//         [Header("Validation")]
//         [SerializeField] private TattooValidator validator;
//
//         [Header("Input Actions")]
//         [SerializeField] private InputActionAsset inputActions;
//
//         private InputAction touchPositionAction;
//         private InputAction touchPressAction;
//         private InputAction touchContactAction;
//
//         private LineRenderer currentLine;
//         private List<Vector3> linePoints = new List<Vector3>();
//         private List<LineRenderer> allLines = new List<LineRenderer>();
//         private bool isDrawing = false;
//         private Vector2 lastTouchPosition;
//         private Vector3 lastWorldPosition;
//         private Queue<Vector2> touchHistory = new Queue<Vector2>();
//         private int maxTouchHistorySize = 3;
//
//         void Awake()
//         {
//             if (inputActions == null)
//             {
//                 Debug.LogError("Input Actions asset not assigned!");
//                 return;
//             }
//             var drawingMap = inputActions.FindActionMap("Drawing", true);
//             touchPositionAction = drawingMap.FindAction("TouchPosition", true);
//             touchPressAction = drawingMap.FindAction("TouchPress", true);
//             touchContactAction = drawingMap.FindAction("TouchContact", true);
//         }
//
//         void Start()
//         {
//             InitializeComponents();
//             SetupInputActions();
//         }
//
//         void InitializeComponents()
//         {
//             if (drawingCamera == null)
//                 drawingCamera = Camera.main;
//
//             if (validator == null)
//                 validator = FindFirstObjectByType<TattooValidator>();
//
//             int lineRendererLayer = LayerMask.NameToLayer("LineRenderer");
//             if (lineRendererLayer == -1)
//             {
//                 Debug.LogError("LineRenderer layer not found! Please create a layer named 'LineRenderer'.");
//                 return;
//             }
//
//             if ((drawingCamera.cullingMask & (1 << lineRendererLayer)) == 0)
//                 drawingCamera.cullingMask |= 1 << lineRendererLayer;
//         }
//
//         void SetupInputActions()
//         {
//             if (touchPressAction != null)
//             {
//                 touchPressAction.performed += OnTouchPress;
//                 touchPressAction.canceled += OnTouchRelease;
//             }
//             inputActions?.Enable();
//         }
//
//         void Update()
//         {
// #if UNITY_EDITOR
//             if (Input.GetMouseButtonDown(0)) StartDrawingMouse();
//             else if (Input.GetMouseButton(0)) HandleContinuousDrawingMouse();
//             else if (Input.GetMouseButtonUp(0)) StopDrawing();
// #endif
//             if (isDrawing) HandleContinuousDrawing();
//             HandleDebugInput();
//         }
//
//         void OnTouchPress(InputAction.CallbackContext context)
//         {
//             if (context.performed) StartDrawing();
//         }
//
//         void OnTouchRelease(InputAction.CallbackContext context)
//         {
//             if (context.canceled && isDrawing) StopDrawing();
//         }
//
//         void StartDrawing()
//         {
//             GameObject lineObj = Instantiate(linePrefab, linesParent);
//             currentLine = lineObj.GetComponent<LineRenderer>();
//             allLines.Add(currentLine);
//             ConfigureLineRenderer(currentLine);
//
//             isDrawing = true;
//             linePoints.Clear();
//             touchHistory.Clear();
//
//             Vector2 touchPos = touchPositionAction.ReadValue<Vector2>();
//             Vector3 worldPos = GetTouchWorldPosition(touchPos);
//             if (worldPos != Vector3.zero)
//             {
//                 linePoints.Add(worldPos);
//                 lastTouchPosition = touchPos;
//                 lastWorldPosition = worldPos;
//                 touchHistory.Enqueue(touchPos);
//                 UpdateLineRenderer(currentLine);
//             }
//         }
//
//         void StartDrawingMouse()
//         {
//             GameObject lineObj = Instantiate(linePrefab, linesParent);
//             currentLine = lineObj.GetComponent<LineRenderer>();
//             allLines.Add(currentLine);
//             ConfigureLineRenderer(currentLine);
//
//             isDrawing = true;
//             linePoints.Clear();
//             touchHistory.Clear();
//
//             Vector2 mousePos = Input.mousePosition;
//             Vector3 worldPos = GetTouchWorldPosition(mousePos);
//             if (worldPos != Vector3.zero)
//             {
//                 linePoints.Add(worldPos);
//                 lastTouchPosition = mousePos;
//                 lastWorldPosition = worldPos;
//                 touchHistory.Enqueue(mousePos);
//                 UpdateLineRenderer(currentLine);
//             }
//         }
//
//         void HandleContinuousDrawing()
//         {
//             float contactValue = touchContactAction.ReadValue<float>();
//             if (contactValue < 0.5f)
//             {
//                 StopDrawing();
//                 return;
//             }
//
//             Vector2 currentTouchPos = touchPositionAction.ReadValue<Vector2>();
//             Vector2 smoothedTouchPos = ApplyTouchSmoothing(currentTouchPos);
//             Vector3 worldPos = GetTouchWorldPosition(smoothedTouchPos);
//             if (worldPos == Vector3.zero) return;
//
//             float distance = Vector3.Distance(worldPos, lastWorldPosition);
//             if (distance >= minDistanceThreshold && worldPos != lastWorldPosition)
//             {
//                 if (linePoints.Count >= maxPointsPerLine)
//                 {
//                     StopDrawing();
//                     return;
//                 }
//                 linePoints.Add(worldPos);
//                 lastTouchPosition = smoothedTouchPos;
//                 lastWorldPosition = worldPos;
//                 UpdateLineRenderer(currentLine);
//             }
//         }
//
//         void HandleContinuousDrawingMouse()
//         {
//             Vector2 mousePos = Input.mousePosition;
//             Vector2 smoothedMousePos = ApplyTouchSmoothing(mousePos);
//             Vector3 worldPos = GetTouchWorldPosition(smoothedMousePos);
//             if (worldPos == Vector3.zero) return;
//
//             float distance = Vector3.Distance(worldPos, lastWorldPosition);
//             if (distance >= minDistanceThreshold && worldPos != lastWorldPosition)
//             {
//                 if (linePoints.Count >= maxPointsPerLine)
//                 {
//                     StopDrawing();
//                     return;
//                 }
//                 linePoints.Add(worldPos);
//                 lastTouchPosition = smoothedMousePos;
//                 lastWorldPosition = worldPos;
//                 UpdateLineRenderer(currentLine);
//             }
//         }
//
//         Vector2 ApplyTouchSmoothing(Vector2 currentPos)
//         {
//             touchHistory.Enqueue(currentPos);
//             if (touchHistory.Count > maxTouchHistorySize)
//                 touchHistory.Dequeue();
//
//             Vector2 smoothed = Vector2.zero;
//             foreach (Vector2 pos in touchHistory)
//                 smoothed += pos;
//             smoothed /= touchHistory.Count;
//             return Vector2.Lerp(currentPos, smoothed, smoothingFactor);
//         }
//
//         Vector3 GetTouchWorldPosition(Vector2 screenPos)
//         {
//             Vector3 pos = new Vector3(screenPos.x, screenPos.y, 10f);
//             pos = drawingCamera.ScreenToWorldPoint(pos);
//             pos.z = 0;
//             return pos;
//         }
//
//         void UpdateLineRenderer(LineRenderer line)
//         {
//             line.positionCount = linePoints.Count;
//             for (int i = 0; i < linePoints.Count; i++)
//                 line.SetPosition(i, linePoints[i]);
//         }
//
//         public void ConfigureLineRenderer(LineRenderer lr)
//         {
//             lr.useWorldSpace = true;
//             lr.startWidth = brushThickness;
//             lr.endWidth = brushThickness;
//
//             if (ColorUtility.TryParseHtmlString(brushColorHexCode, out Color newColor))
//             {
//                 lr.startColor = newColor;
//                 lr.endColor = newColor;
//             }
//
//             lr.material = new Material(Shader.Find("Sprites/Default"));
//             lr.sortingOrder = 10;
//             lr.gameObject.layer = LayerMask.NameToLayer("LineRenderer");
//         }
//
//         void StopDrawing()
//         {
//             isDrawing = false;
//             touchHistory.Clear();
//             if (validator != null && linePoints.Count > 1)
//                 Invoke(nameof(ValidateDrawing), 0.3f);
//         }
//
//         public void ValidateDrawing()
//         {
//             if (validator != null)
//             {
//                 ValidationResult result = validator.ValidateCurrentDrawing();
//                 DisplayValidationResults(result);
//             }
//         }
//
//         void DisplayValidationResults(ValidationResult result)
//         {
//             string text = $"Coverage: {result.coverage * 100f:F1}%\n" +
//                           $"Accuracy: {result.accuracy * 100f:F1}%\n" +
//                           $"Penalty: {result.outsidePenalty * 100f:F1}%\n" +
//                           $"Final Score: {result.finalScore * 100f:F1}%";
//             Debug.Log(text);
// #if UNITY_ANDROID
//             Handheld.Vibrate();
// #endif
//         }
//
//         public void ClearDrawing()
//         {
//             foreach (var lr in allLines)
//             {
//                 if (lr != null) Destroy(lr.gameObject);
//             }
//             allLines.Clear();
//             currentLine = null;
//             isDrawing = false;
//             touchHistory.Clear();
//         }
//         
//         public void ApplyNewBrushSettings()
//         {
//             Debug.Log($"Updated brush settings: Thickness={brushThickness}, Color={brushColorHexCode}");
//         }
//
//
//         void HandleDebugInput()
//         {
// #if UNITY_EDITOR
//             if (Input.GetKeyDown(KeyCode.Space)) ValidateDrawing();
//             if (Input.GetKeyDown(KeyCode.C)) ClearDrawing();
// #endif
//         }
//
//         void OnEnable() => inputActions?.Enable();
//         void OnDisable() => inputActions?.Disable();
//
//         void OnDestroy()
//         {
//             if (touchPressAction != null)
//             {
//                 touchPressAction.performed -= OnTouchPress;
//                 touchPressAction.canceled -= OnTouchRelease;
//             }
//             inputActions?.Disable();
//         }
//     }
// }
