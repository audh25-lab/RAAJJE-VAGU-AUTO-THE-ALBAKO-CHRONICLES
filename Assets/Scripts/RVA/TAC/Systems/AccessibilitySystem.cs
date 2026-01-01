using UnityEngine;
using System;

public class AccessibilitySystem : MonoBehaviour
{
    public static AccessibilitySystem Instance { get; private set; }

    // --- Events for settings changes ---
    public event Action<SubtitleSettings> OnSubtitleSettingsChanged;
    public event Action<ColorblindMode> OnColorblindModeChanged;

    // --- Data Structures ---
    public enum SubtitleSize { Small, Medium, Large }
    public enum ColorblindMode { None, Protanopia, Deuteranopia, Tritanopia }

    [System.Serializable]
    public struct SubtitleSettings
    {
        public bool isEnabled;
        public SubtitleSize size;
        public Color textColor;
        public bool hasBackground;
        public Color backgroundColor;
    }

    // --- Current Settings ---
    private SubtitleSettings currentSubtitleSettings;
    private ColorblindMode currentColorblindMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
    }

    // --- Public Methods for UI Interaction ---

    public void SetSubtitlesEnabled(bool enabled)
    {
        currentSubtitleSettings.isEnabled = enabled;
        OnSubtitleSettingsChanged?.Invoke(currentSubtitleSettings);
        // SaveSettings(); // In a real implementation
    }

    public void SetSubtitleSize(SubtitleSize size)
    {
        currentSubtitleSettings.size = size;
        OnSubtitleSettingsChanged?.Invoke(currentSubtitleSettings);
        // SaveSettings();
    }

    public void SetColorblindMode(ColorblindMode mode)
    {
        currentColorblindMode = mode;
        OnColorblindModeChanged?.Invoke(currentColorblindMode);
        // SaveSettings();

        // In a real game, you would now apply a color filter or swap shaders.
        Debug.Log($"Colorblind mode set to: {mode}");
    }

    public SubtitleSettings GetSubtitleSettings()
    {
        return currentSubtitleSettings;
    }

    public ColorblindMode GetColorblindMode()
    {
        return currentColorblindMode;
    }

    // --- Save and Load Logic ---

    private void LoadSettings()
    {
        // --- This is how you would load settings in a real Unity game ---
        // currentSubtitleSettings.isEnabled = PlayerPrefs.GetInt("SubtitlesEnabled", 1) == 1;
        // currentSubtitleSettings.size = (SubtitleSize)PlayerPrefs.GetInt("SubtitleSize", (int)SubtitleSize.Medium);
        // currentColorblindMode = (ColorblindMode)PlayerPrefs.GetInt("ColorblindMode", (int)ColorblindMode.None);

        // For now, we'll use default values.
        currentSubtitleSettings = new SubtitleSettings
        {
            isEnabled = true,
            size = SubtitleSize.Medium,
            textColor = Color.white,
            hasBackground = true,
            backgroundColor = new Color(0, 0, 0, 0.5f)
        };
        currentColorblindMode = ColorblindMode.None;

        Debug.Log("Accessibility settings loaded.");
    }

    private void SaveSettings()
    {
        // --- This is how you would save settings ---
        // PlayerPrefs.SetInt("SubtitlesEnabled", currentSubtitleSettings.isEnabled ? 1 : 0);
        // PlayerPrefs.SetInt("SubtitleSize", (int)currentSubtitleSettings.size);
        // PlayerPrefs.SetInt("ColorblindMode", (int)currentColorblindMode);
        // PlayerPrefs.Save();

        Debug.Log("Accessibility settings saved.");
    }
}
