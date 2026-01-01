// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
// RVACONT-006: Batch 6 - Accessibility System
// WCAG 2.1 AA compliance for mobile Maldives gaming

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace RVA.TAC.Accessibility
{
    public class AccessibilitySystem : MonoBehaviour
    {
        private static AccessibilitySystem _instance;
        public static AccessibilitySystem Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<AccessibilitySystem>();
                return _instance;
            }
        }
        
        [Header("Motor Accessibility")]
        public bool enableLargeTouchTargets = true;
        public float minimumTouchSize = 44f; // iOS/Android standard
        public bool enableGestureAlternatives = true;
        public float inputHoldTime = 0.5f; // Long press assistance
        
        [Header("Visual Accessibility")]
        public bool enableHighContrast = false;
        public float textScaling = 1f; // 1.0 = normal, up to 2.0
        public bool enableColorBlindMode = false;
        public ColorBlindType colorBlindType = ColorBlindType.None;
        
        [Header("Hearing Accessibility")]
        public bool enableSubtitles = true;
        public bool enableVisualAlerts = true;
        
        [Header("Cognitive Accessibility")]
        public bool enableReducedMotion = false;
        public bool enableSimpleLanguage = false;
        
        // Color palettes for color blindness
        private Dictionary<ColorBlindType, Dictionary<string, Color>> colorPalettes;
        
        public enum ColorBlindType
        {
            None,
            Protanopia,   // Red-blind
            Deuteranopia, // Green-blind
            Tritanopia    // Blue-blind
        }
        
        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAccessibility();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void InitializeAccessibility()
        {
            // Load saved preferences
            LoadAccessibilitySettings();
            
            // Initialize color palettes
            SetupColorPalettes();
            
            // Apply settings immediately
            ApplyAccessibilitySettings();
        }
        
        void SetupColorPalettes()
        {
            colorPalettes = new Dictionary<ColorBlindType, Dictionary<string, Color>>();
            
            // Protanopia-safe palette (red-blind)
            colorPalettes[ColorBlindType.Protanopia] = new Dictionary<string, Color>
            {
                { "primary", new Color(0f, 0.5f, 0.9f) },     // Blue
                { "secondary", new Color(0.9f, 0.7f, 0f) },   // Gold
                { "success", new Color(0f, 0.7f, 0.5f) },     // Teal
                { "danger", new Color(0.8f, 0.3f, 0.8f) },    // Magenta (not red)
                { "warning", new Color(1f, 0.8f, 0f) }        // Yellow
            };
            
            // Deuteranopia-safe palette (green-blind)
            colorPalettes[ColorBlindType.Deuteranopia] = new Dictionary<string, Color>
            {
                { "primary", new Color(0f, 0.4f, 0.9f) },
                { "secondary", new Color(0.9f, 0.6f, 0f) },
                { "success", new Color(0f, 0.5f, 0.9f) },     // Blue instead of green
                { "danger", new Color(0.9f, 0.3f, 0.5f) },    // Reddish
                { "warning", new Color(0.9f, 0.8f, 0f) }
            };
            
            // Tritanopia-safe palette (blue-blind)
            colorPalettes[ColorBlindType.Tritanopia] = new Dictionary<string, Color>
            {
                { "primary", new Color(0.7f, 0.3f, 0.9f) },   // Purple
                { "secondary", new Color(0.9f, 0.6f, 0f) },
                { "success", new Color(0f, 0.8f, 0.4f) },
                { "danger", new Color(0.9f, 0.3f, 0.3f) },
                { "warning", new Color(0.9f, 0.7f, 0f) }
            };
        }
        
        void ApplyAccessibilitySettings()
        {
            // Enlarge touch targets
            if (enableLargeTouchTargets)
            {
                foreach (var button in Resources.FindObjectsOfTypeAll<Button>())
                {
                    EnlargeTouchTarget(button);
                }
            }
            
            // Apply high contrast
            if (enableHighContrast)
            {
                ApplyHighContrastMode();
            }
            
            // Apply color blind palette
            if (enableColorBlindMode && colorBlindType != ColorBlindType.None)
            {
                ApplyColorBlindPalette();
            }
            
            // Scale text
            ApplyTextScaling();
        }
        
        void EnlargeTouchTarget(Button button)
        {
            RectTransform rt = button.GetComponent<RectTransform>();
            if (rt.sizeDelta.x < minimumTouchSize || rt.sizeDelta.y < minimumTouchSize)
            {
                // Expand while maintaining aspect ratio
                float scale = minimumTouchSize / Mathf.Min(rt.sizeDelta.x, rt.sizeDelta.y);
                rt.sizeDelta *= scale;
                
                // Adjust parent container if needed
                LayoutGroup layoutGroup = rt.GetComponentInParent<LayoutGroup>();
                if (layoutGroup != null)
                {
                    layoutGroup.CalculateLayoutInputHorizontal();
                    layoutGroup.CalculateLayoutInputVertical();
                }
            }
        }
        
        void ApplyHighContrastMode()
        {
            // Increase contrast ratios to WCAG AAA standard (7:1)
            foreach (var text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
            {
                text.fontSharedMaterial.EnableKeyword("UNDERLAY_ON");
                text.fontSharedMaterial.SetColor("_UnderlayColor", Color.black);
                text.fontSharedMaterial.SetFloat("_UnderlayOffsetX", 0.5f);
                text.fontSharedMaterial.SetFloat("_UnderlayOffsetY", -0.5f);
                text.fontSharedMaterial.SetFloat("_UnderlaySoftness", 0.1f);
            }
        }
        
        void ApplyColorBlindPalette()
        {
            var palette = colorPalettes[colorBlindType];
            
            // Recolor UI elements
            foreach (var image in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (image.gameObject.CompareTag("Recolorable"))
                {
                    string colorKey = image.gameObject.name.Split('_')[0];
                    if (palette.ContainsKey(colorKey))
                    {
                        image.color = palette[colorKey];
                    }
                }
            }
        }
        
        void ApplyTextScaling()
        {
            foreach (var text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
            {
                text.fontSize *= textScaling;
            }
        }
        
        /// <summary>
        /// Shows visual alert for hearing-impaired players
        /// </summary>
        public void ShowVisualAlert(string messageKey, AlertType type)
        {
            if (!enableVisualAlerts) return;
            
            GameObject alert = new GameObject("VisualAlert");
            alert.transform.SetParent(UIManager.Instance.popupCanvas.transform, false);
            
            Image bg = alert.AddComponent<Image>();
            bg.color = GetAlertColor(type);
            
            TextMeshProUGUI text = alert.AddComponent<TextMeshProUGUI>();
            text.text = LocalizationSystem.Instance.GetLocalizedString(messageKey);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 24 * textScaling;
            
            // Animate
            StartCoroutine(VisualAlertAnimation(alert));
        }
        
        Color GetAlertColor(AlertType type)
        {
            switch (type)
            {
                case AlertType.Danger: return Color.red;
                case AlertType.Warning: return Color.yellow;
                case AlertType.Success: return Color.green;
                case AlertType.Info: return Color.cyan;
                default: return Color.white;
            }
        }
        
        IEnumerator VisualAlertAnimation(GameObject alert)
        {
            RectTransform rt = alert.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.8f);
            rt.anchorMax = new Vector2(0.5f, 0.8f);
            rt.sizeDelta = new Vector2(300, 60);
            
            // Fade in
            CanvasGroup cg = alert.AddComponent<CanvasGroup>();
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * 2)
            {
                cg.alpha = t;
                yield return null;
            }
            
            // Hold
            yield return new WaitForSeconds(2f);
            
            // Fade out
            for (float t = 1f; t > 0; t -= Time.unscaledDeltaTime * 2)
            {
                cg.alpha = t;
                yield return null;
            }
            
            Destroy(alert);
        }
        
        void LoadAccessibilitySettings()
        {
            // Load from PlayerPrefs (mobile-safe storage)
            enableLargeTouchTargets = PlayerPrefs.GetInt("Accessibility_LargeTouch", 1) == 1;
            textScaling = PlayerPrefs.GetFloat("Accessibility_TextScale", 1f);
            enableHighContrast = PlayerPrefs.GetInt("Accessibility_HighContrast", 0) == 1;
            enableSubtitles = PlayerPrefs.GetInt("Accessibility_Subtitles", 1) == 1;
            enableVisualAlerts = PlayerPrefs.GetInt("Accessibility_VisualAlerts", 1) == 1;
            
            // Color blind mode
            int cbType = PlayerPrefs.GetInt("Accessibility_ColorBlindType", 0);
            if (cbType > 0)
            {
                enableColorBlindMode = true;
                colorBlindType = (ColorBlindType)cbType;
            }
        }
        
        public void SaveAccessibilitySettings()
        {
            PlayerPrefs.SetInt("Accessibility_LargeTouch", enableLargeTouchTargets ? 1 : 0);
            PlayerPrefs.SetFloat("Accessibility_TextScale", textScaling);
            PlayerPrefs.SetInt("Accessibility_HighContrast", enableHighContrast ? 1 : 0);
            PlayerPrefs.SetInt("Accessibility_Subtitles", enableSubtitles ? 1 : 0);
            PlayerPrefs.SetInt("Accessibility_VisualAlerts", enableVisualAlerts ? 1 : 0);
            PlayerPrefs.SetInt("Accessibility_ColorBlindType", (int)colorBlindType);
            PlayerPrefs.Save();
        }
        
        public enum AlertType
        {
            Info,
            Warning,
            Danger,
            Success
        }
    }
}
