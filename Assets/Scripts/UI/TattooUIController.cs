using UnityEngine;
using UnityEngine.UI;
using TattooSystem;
using TMPro;

public class TattooUIController : MonoBehaviour
{
    [Header("Brush Settings")]
    [SerializeField] private TattooDrawingSystem drawingSystem;
    [SerializeField] private TattooDrawingSystem.BrushSize brushSize = TattooDrawingSystem.BrushSize.Medium;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private string colorHexCode = "#FFFFFF";

    [Header("UI References")]
    [SerializeField] private Button smallSizeButton;
    [SerializeField] private Button mediumSizeButton;
    [SerializeField] private Button largeSizeButton;
    [SerializeField] private Button extraLargeSizeButton;
    [SerializeField] private Button validateButton;
    [SerializeField] private TMP_Text validationResultText;
    [SerializeField] private Button[] colorButtons;
    [SerializeField] private InputField hexColorInput;
    [SerializeField] private Image colorPreview;

    [Header("Preset Colors")]
    [SerializeField] private Color[] presetColors = new Color[]
    {
        Color.white,
        Color.black,
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.magenta,
        Color.cyan
    };

    void Start()
    {
        InitializeDrawingSystem();
        SetupUI();
        ApplyInitialSettings();
    }

    void InitializeDrawingSystem()
    {
        if (drawingSystem == null)
        {
            drawingSystem = FindFirstObjectByType<TattooDrawingSystem>();
            if (drawingSystem == null)
            {
                Debug.LogError("TattooDrawingSystem not found! Please assign it manually.");
            }
        }
    }

    void SetupUI()
    {
        if (smallSizeButton != null)
            smallSizeButton.onClick.AddListener(() => SetBrushSize(TattooDrawingSystem.BrushSize.Small));
        if (mediumSizeButton != null)
            mediumSizeButton.onClick.AddListener(() => SetBrushSize(TattooDrawingSystem.BrushSize.Medium));
        if (largeSizeButton != null)
            largeSizeButton.onClick.AddListener(() => SetBrushSize(TattooDrawingSystem.BrushSize.Large));
        if (extraLargeSizeButton != null)
            extraLargeSizeButton.onClick.AddListener(() => SetBrushSize(TattooDrawingSystem.BrushSize.ExtraLarge));

        if (validateButton != null)
        {
            validateButton.onClick.AddListener(() =>
            {
                drawingSystem.TriggerValidation();
                if (validationResultText != null)
                    validationResultText.text = drawingSystem.GetFormattedResults();
            });
        }

        if (colorButtons != null && colorButtons.Length > 0)
        {
            for (int i = 0; i < colorButtons.Length && i < presetColors.Length; i++)
            {
                int colorIndex = i;
                colorButtons[i].onClick.AddListener(() => SetPresetColor(colorIndex));
                Image buttonImage = colorButtons[i].GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = presetColors[i];
                }
            }
        }

        if (hexColorInput != null)
        {
            hexColorInput.text = colorHexCode;
            hexColorInput.onEndEdit.AddListener(OnHexColorChanged);
        }

        UpdateColorPreview();
    }

    void ApplyInitialSettings()
    {
        ApplyBrushSize();
        ApplyColor();
    }

    public void ApplyBrushSize()
    {
        if (drawingSystem != null)
        {
            drawingSystem.SetBrushSize(brushSize);
        }
    }

    public void ApplyColor()
    {
        if (drawingSystem != null)
        {
            drawingSystem.SetBrushColor(color);
            UpdateColorHexCode();
            UpdateColorPreview();
        }
    }

    public void ApplyBothSettings()
    {
        ApplyBrushSize();
        ApplyColor();
    }

    public void SetBrushSize(TattooDrawingSystem.BrushSize newSize)
    {
        brushSize = newSize;
        ApplyBrushSize();
    }

    public void OnHexColorChanged(string hexInput)
    {
        if (ColorUtility.TryParseHtmlString(hexInput, out Color newColor))
        {
            color = newColor;
            colorHexCode = hexInput;
            ApplyColor();
        }
        else
        {
            Debug.LogWarning($"Invalid hex color: {hexInput}");
            if (hexColorInput != null)
                hexColorInput.text = colorHexCode;
        }
    }

    public void SetPresetColor(int colorIndex)
    {
        if (colorIndex >= 0 && colorIndex < presetColors.Length)
        {
            color = presetColors[colorIndex];
            ApplyColor();
            if (hexColorInput != null)
                hexColorInput.text = colorHexCode;
        }
    }

    public void SetCustomColor(Color newColor)
    {
        color = newColor;
        ApplyColor();
        if (hexColorInput != null)
            hexColorInput.text = colorHexCode;
    }

    void UpdateColorHexCode()
    {
        colorHexCode = "#" + ColorUtility.ToHtmlStringRGB(color);
    }

    void UpdateColorPreview()
    {
        if (colorPreview != null)
        {
            colorPreview.color = color;
        }
    }

    public TattooDrawingSystem.BrushSize BrushSize
    {
        get => brushSize;
        set
        {
            brushSize = value;
            ApplyBrushSize();
        }
    }

    public Color Color
    {
        get => color;
        set
        {
            color = value;
            UpdateColorHexCode();
            UpdateColorPreview();
            if (hexColorInput != null)
                hexColorInput.text = colorHexCode;
            ApplyColor();
        }
    }

    public string ColorHexCode
    {
        get => colorHexCode;
        set
        {
            if (ColorUtility.TryParseHtmlString(value, out Color newColor))
            {
                color = newColor;
                colorHexCode = value;
                UpdateColorPreview();
                if (hexColorInput != null)
                    hexColorInput.text = colorHexCode;
                ApplyColor();
            }
        }
    }

    public void AddPresetColor(Color newColor)
    {
        System.Array.Resize(ref presetColors, presetColors.Length + 1);
        presetColors[presetColors.Length - 1] = newColor;
    }

    public void ResetToDefaults()
    {
        brushSize = TattooDrawingSystem.BrushSize.Medium;
        color = Color.white; 
        colorHexCode = "#FFFFFF";
    
        if (hexColorInput != null)
            hexColorInput.text = colorHexCode;
    
        UpdateColorPreview();
        ApplyBothSettings();
    }

    void OnDestroy()
    {
        if (smallSizeButton != null)
            smallSizeButton.onClick.RemoveAllListeners();
        if (mediumSizeButton != null)
            mediumSizeButton.onClick.RemoveAllListeners();
        if (largeSizeButton != null)
            largeSizeButton.onClick.RemoveAllListeners();
        if (extraLargeSizeButton != null)
            extraLargeSizeButton.onClick.RemoveAllListeners();
        if (validateButton != null)
            validateButton.onClick.RemoveAllListeners();
        
        if (hexColorInput != null)
            hexColorInput.onEndEdit.RemoveAllListeners();
        
        if (colorButtons != null)
        {
            foreach (var button in colorButtons)
            {
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Apply Settings")]
    void ApplySettingsDebug()
    {
        if (Application.isPlaying)
            ApplyBothSettings();
    }

    [ContextMenu("Reset to Defaults")]
    void ResetToDefaultsDebug()
    {
        if (Application.isPlaying)
            ResetToDefaults();
    }
#endif
}
