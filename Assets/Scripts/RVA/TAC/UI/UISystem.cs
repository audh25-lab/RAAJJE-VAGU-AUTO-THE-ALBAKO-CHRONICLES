// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
// RVACONT-006: Batch 6 - UI System
// High-performance UI components for mobile Maldives context

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace RVA.TAC.UI.Components
{
    /// <summary>
    /// Mobile-optimized button with Maldivian cultural feedback
    /// </summary>
    public class TACButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Visual Feedback")]
        public Image buttonImage;
        public TextMeshProUGUI buttonText;
        public Color normalColor = new Color(0.12f, 0.37f, 0.42f); // Ocean blue
        public Color pressedColor = new Color(0.86f, 0.58f, 0.15f); // Gold accent
        
        [Header("Maldivian Style")]
        public bool useWaveAnimation = true;
        public float waveSpeed = 2f;
        
        [Header("Accessibility")]
        public bool supportScreenReaders = true;
        public string accessibilityLabelKey = "";
        
        private RectTransform rectTransform;
        private bool isPressed = false;
        
        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (buttonImage == null)
                buttonImage = GetComponent<Image>();
            if (buttonText == null)
                buttonText = GetComponentInChildren<TextMeshProUGUI>();
            
            // Apply Dhivehi font if needed
            if (LocalizationSystem.Instance?.CurrentLanguage == "dv")
            {
                ApplyDhivehiStyling();
            }
        }
        
        void ApplyDhivehiStyling()
        {
            // Thaana script requires larger line height
            if (buttonText != null)
            {
                buttonText.lineSpacing = -20f; // Compensate for Thaana descenders
                buttonText.alignment = TextAlignmentOptions.Right;
            }
            
            // Mirror icon if present
            Image icon = transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.rectTransform.localScale = new Vector3(-1, 1, 1);
            }
        }
        
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            isPressed = true;
            buttonImage.color = pressedColor;
            
            // Haptic feedback
            #if UNITY_ANDROID || UNITY_IOS
            if (eventData.pointerId == -1) // Primary touch
                Handheld.Vibrate();
            #endif
            
            // Wave animation (Boduberu-inspired)
            if (useWaveAnimation)
                StartCoroutine(WaveAnimation());
        }
        
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
        {
            isPressed = false;
            buttonImage.color = normalColor;
        }
        
        IEnumerator WaveAnimation()
        {
            Vector3 originalScale = rectTransform.localScale;
            float elapsed = 0f;
            
            while (isPressed && elapsed < 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                float wave = 1 + Mathf.Sin(elapsed * waveSpeed * Mathf.PI) * 0.1f;
                rectTransform.localScale = originalScale * wave;
                yield return null;
            }
            
            rectTransform.localScale = originalScale;
        }
    }
    
    /// <summary>
    /// Performance-optimized HUD element for mobile
    /// </summary>
    public class TACHudElement : MonoBehaviour
    {
        [Header("Optimization")]
        public bool useObjectPooling = true;
        public float updateInterval = 0.1f; // 10fps updates for non-critical HUD
        
        [Header("Maldivian Context")]
        public bool showInPrayerTime = true;
        
        private CanvasRenderer canvasRenderer;
        private float lastUpdateTime = 0f;
        
        void Start()
        {
            canvasRenderer = GetComponent<CanvasRenderer>();
            
            // Disable during prayer time if configured
            if (!showInPrayerTime && PrayerTimeSystem.Instance.IsPrayerTimeNow())
            {
                gameObject.SetActive(false);
            }
        }
        
        void Update()
        {
            // Throttled updates for performance
            if (Time.unscaledTime - lastUpdateTime < updateInterval)
                return;
            
            lastUpdateTime = Time.unscaledTime;
            UpdateHudContent();
        }
        
        protected virtual void UpdateHudContent()
        {
            // Override in subclasses
        }
        
        /// <summary>
        /// Force immediate update for critical events
        /// </summary>
        public virtual void ForceUpdate()
        {
            lastUpdateTime = 0f;
        }
    }
    
    /// <summary>
    /// Minimap for island navigation with Maldivian landmarks
    /// </summary>
    public class TACMinimap : TACHudElement
    {
        [Header("Minimap Settings")]
        public RawImage minimapImage;
        public float zoomLevel = 50f;
        public bool rotateWithPlayer = true;
        
        [Header("Maldivian Landmarks")]
        public Sprite mosqueIcon;
        public Sprite harborIcon;
        public Sprite resortIcon;
        
        private RenderTexture minimapTexture;
        private Camera minimapCamera;
        
        void Awake()
        {
            // Create dedicated minimap camera
            GameObject camGO = new GameObject("MinimapCamera");
            minimapCamera = camGO.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.cullingMask = LayerMask.GetMask("Minimap");
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0.2f, 0.6f, 0.8f); // Ocean blue
            
            // Low-res render texture for performance
            minimapTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.RGB565);
            minimapTexture.filterMode = FilterMode.Point; // Pixel art style
            minimapCamera.targetTexture = minimapTexture;
            
            minimapImage.texture = minimapTexture;
        }
        
        void LateUpdate()
        {
            if (PlayerController.Instance != null)
            {
                // Follow player
                Vector3 playerPos = PlayerController.Instance.transform.position;
                minimapCamera.transform.position = new Vector3(
                    playerPos.x, 
                    playerPos.y + 50f, 
                    playerPos.z
                );
                
                // Rotate with player if enabled
                if (rotateWithPlayer)
                {
                    minimapCamera.transform.rotation = Quaternion.Euler(90f, 
                        PlayerController.Instance.transform.eulerAngles.y, 
                        0f);
                }
            }
        }
        
        protected override void UpdateHudContent()
        {
            // Update landmark icons
            UpdateLandmarkIcons();
        }
        
        void UpdateLandmarkIcons()
        {
            // Show mosque locations during prayer time
            if (PrayerTimeSystem.Instance.IsPrayerTimeNow())
            {
                // Highlight nearest mosque
                var mosques = GameObject.FindGameObjectsWithTag("Mosque");
                // Implementation for icon placement
            }
        }
    }
}
