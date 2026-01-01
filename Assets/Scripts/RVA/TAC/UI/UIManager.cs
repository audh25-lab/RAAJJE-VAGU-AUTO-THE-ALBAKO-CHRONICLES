// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
// RVACONT-006: Batch 6 - UI Manager
// Mobile-native UI controller with Maldivian cultural integration

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace RVA.TAC.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("Core Canvas References")]
        public Canvas mainCanvas;
        public Canvas hudCanvas;
        public Canvas menuCanvas;
        public Canvas popupCanvas;
        
        [Header("Maldivian Cultural UI")]
        public Image prayerTimeIndicator;
        public TextMeshProUGUI dhivehiClockText;
        public Image boduberuRhythmVisualizer;
        
        [Header("Mobile Optimization")]
        public float safeAreaPadding = 20f;
        public float thumbZoneOffset = 100f;
        
        private static UIManager _instance;
        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                    Debug.LogError("UIManager not initialized!");
                return _instance;
            }
        }
        
        private Stack<UIWindow> windowStack = new Stack<UIWindow>();
        private RectTransform safeAreaRect;
        
        // Maldivian prayer time states
        private bool isPrayerTimeActive = false;
        private string currentPrayerName = "";
        
        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeMobileUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void InitializeMobileUI()
        {
            // Apply safe area for notched devices (iPhone X+, Android notch)
            ApplySafeArea();
            
            // Set up Dhivehi font fallback
            SetupDhivehiTypography();
            
            // Configure canvas for mobile performance
            ConfigureCanvasForMobile();
        }
        
        void ApplySafeArea()
        {
            var safeArea = Screen.safeArea;
            var canvasRect = mainCanvas.GetComponent<RectTransform>();
            
            // Convert to local coordinates
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;
            
            canvasRect.anchorMin = anchorMin;
            canvasRect.anchorMax = anchorMax;
            
            // Add cultural padding for prayer time banner
            if (isPrayerTimeActive)
            {
                anchorMax.y -= 0.08f; // Reserve top 8% for prayer notification
            }
        }
        
        void SetupDhivehiTypography()
        {
            // Maldivian language support with Noto Sans Thaana fallback
            TMP_FontAsset thaanaFont = Resources.Load<TMP_FontAsset>("Fonts/NotoSansThaana");
            if (thaanaFont != null)
            {
                TextMeshProUGUI[] allTexts = GetComponentsInChildren<TextMeshProUGUI>();
                foreach (var text in allTexts)
                {
                    if (LocalizationSystem.Instance.CurrentLanguage == "dv")
                    {
                        text.font = thaanaFont;
                        text.isRightToLeftText = true; // Thaana script RTL
                    }
                }
            }
        }
        
        void ConfigureCanvasForMobile()
        {
            // Optimize for Mali-G72 and similar mobile GPUs
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.pixelPerfect = true; // Crucial for pixel art style
            
            // Reduce overdraw in dense island environments
            CanvasScaler scaler = mainCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // Mobile portrait reference
            scaler.matchWidthOrHeight = 0.5f; // Balance between width/height
        }
        
        /// <summary>
        /// Shows UI window with Maldivian cultural context
        /// </summary>
        public void ShowWindow(UIWindow window, bool addToStack = true)
        {
            if (window == null) return;
            
            // Check prayer time restrictions
            if (PrayerTimeSystem.Instance.IsPrayerTimeNow() && window.blockDuringPrayer)
            {
                ShowPrayerTimeAlert();
                return;
            }
            
            window.Show();
            if (addToStack) windowStack.Push(window);
            
            // Apply Dhivehi localization if needed
            if (LocalizationSystem.Instance.CurrentLanguage == "dv")
            {
                window.ApplyRTLLayout();
            }
        }
        
        void ShowPrayerTimeAlert()
        {
            ShowPopup(
                titleKey: "prayer_time_active",
                messageKey: "please_return_after_prayer",
                icon: prayerTimeIndicator.sprite
            );
        }
        
        /// <summary>
        /// Mobile-optimized popup with haptic feedback
        /// </summary>
        public void ShowPopup(string titleKey, string messageKey, Sprite icon = null)
        {
            GameObject popup = ObjectPool.Instance.GetPooledObject("UI Popup");
            popup.transform.SetParent(popupCanvas.transform, false);
            
            // Set content
            var titleText = popup.transform.Find("Title").GetComponent<TextMeshProUGUI>();
            var messageText = popup.transform.Find("Message").GetComponent<TextMeshProUGUI>();
            
            titleText.text = LocalizationSystem.Instance.GetLocalizedString(titleKey);
            messageText.text = LocalizationSystem.Instance.GetLocalizedString(messageKey);
            
            // Maldivian styling
            titleText.color = new Color(0.86f, 0.58f, 0.15f); // Gold accent (#D99526)
            
            // Haptic feedback for mobile
            #if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
            #endif
            
            // Auto-dismiss for prayer time notifications
            if (titleKey == "prayer_time_active")
            {
                StartCoroutine(AutoDismissPopup(popup, 3f));
            }
        }
        
        System.Collections.IEnumerator AutoDismissPopup(GameObject popup, float delay)
        {
            yield return new WaitForSeconds(delay);
            ObjectPool.Instance.ReturnToPool(popup);
        }
        
        /// <summary>
        /// Updates HUD during Boduberu performances
        /// </summary>
        public void UpdateRhythmHUD(float rhythmAccuracy)
        {
            if (boduberuRhythmVisualizer != null)
            {
                // Visual feedback for rhythm mini-game
                boduberuRhythmVisualizer.fillAmount = rhythmAccuracy;
                boduberuRhythmVisualizer.color = Color.Lerp(
                    Color.red, 
                    Color.green, 
                    rhythmAccuracy
                );
            }
        }
        
        void Update()
        {
            // Update Dhivehi clock display
            if (dhivehiClockText != null && TimeSystem.Instance != null)
            {
                dhivehiClockText.text = TimeSystem.Instance.GetDhivehiTimeString();
            }
        }
        
        public void OnPrayerTimeStarted(string prayerName)
        {
            isPrayerTimeActive = true;
            currentPrayerName = prayerName;
            ApplySafeArea(); // Re-adjust UI
            
            // Show subtle prayer notification (not intrusive)
            ShowPopup(
                titleKey: $"prayer_{prayerName}",
                messageKey: "prayer_time_reminder"
            );
        }
        
        public void OnPrayerTimeEnded()
        {
            isPrayerTimeActive = false;
            ApplySafeArea(); // Restore normal layout
        }
    }
    
    [System.Serializable]
    public class UIWindow : MonoBehaviour
    {
        public bool blockDuringPrayer = false;
        public AnimationCurve showCurve = AnimationCurve.EaseInOut(0, 0, 0.3f, 1);
        
        public virtual void Show()
        {
            gameObject.SetActive(true);
            // Animate in
            RectTransform rt = GetComponent<RectTransform>();
            rt.localScale = Vector3.zero;
            StartCoroutine(AnimateShow(rt));
        }
        
        public virtual void Hide()
        {
            StartCoroutine(AnimateHide(GetComponent<RectTransform>()));
        }
        
        public virtual void ApplyRTLLayout()
        {
            // Mirror layout for Dhivehi (Thaana script)
            RectTransform rt = GetComponent<RectTransform>();
            rt.pivot = new Vector2(1 - rt.pivot.x, rt.pivot.y);
            rt.anchorMin = new Vector2(1 - rt.anchorMax.x, rt.anchorMin.y);
            rt.anchorMax = new Vector2(1 - rt.anchorMin.x, rt.anchorMax.y);
        }
        
        System.Collections.IEnumerator AnimateShow(RectTransform rt)
        {
            float elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = showCurve.Evaluate(elapsed / 0.3f);
                rt.localScale = Vector3.one * t;
                yield return null;
            }
            rt.localScale = Vector3.one;
        }
        
        System.Collections.IEnumerator AnimateHide(RectTransform rt)
        {
            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = 1 - (elapsed / 0.2f);
                rt.localScale = Vector3.one * t;
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
