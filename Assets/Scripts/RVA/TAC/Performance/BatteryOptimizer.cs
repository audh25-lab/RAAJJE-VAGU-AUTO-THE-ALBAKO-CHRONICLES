// BatteryOptimizer.cs
// RVACONT-007 - Batch 7: Performance Systems
// Tropical climate battery degradation compensation

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RVA.TAC.Performance
{
    public class BatteryOptimizer : MonoBehaviour
    {
        [System.Serializable]
        public class BatteryProfile
        {
            public float lowPowerThreshold = 0.2f;
            public float criticalThreshold = 0.1f;
            public bool enableSolarChargingDetection = true;
            
            // Maldives solar pattern: 6AM-6PM peak charging
            public Vector2 solarPeakHours = new Vector2(6f, 18f);
            public bool isDuringSolarHours => IsDuringSolarHours();
        }
        
        [Header("Battery Profile")]
        public BatteryProfile profile = new BatteryProfile();
        
        [Header("Conservation Settings")]
        public bool autoReduceFrameRate = true;
        public bool disableVibrationsOnLow = true;
        public bool reduceAudioQuality = true;
        
        private float currentBatteryLevel = 1f;
        private bool isCharging = false;
        private bool isInEmergencyMode = false;
        
        // System references
        private MobilePerformance mobilePerformance;
        private AudioSystem audioSystem;
        private ParticleSystem particleSystem;
        
        public static BatteryOptimizer Instance { get; private set; }
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            DebugSystem.Log("[BatteryOptimizer] Initialized", DebugSystem.LogCategory.PERFORMANCE);
        }
        
        void Start()
        {
            mobilePerformance = MobilePerformance.Instance;
            audioSystem = FindObjectOfType<AudioSystem>();
            particleSystem = FindObjectOfType<ParticleSystem>();
            
            StartCoroutine(BatteryMonitoringRoutine());
            StartCoroutine(SolarPatternRoutine());
        }
        
        private IEnumerator BatteryMonitoringRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(10f); // Check every 10 seconds
                
                #if UNITY_ANDROID && !UNITY_EDITOR
                UpdateAndroidBatteryStatus();
                #endif
                
                ApplyBatteryConservation();
                
                // Log battery state changes
                if (currentBatteryLevel < profile.criticalThreshold && !isInEmergencyMode)
                {
                    EnterEmergencyMode();
                }
            }
        }
        
        private IEnumerator SolarPatternRoutine()
        {
            // Maldives-specific: encourage play during solar hours
            while (true)
            {
                yield return new WaitForSeconds(60f);
                
                if (profile.enableSolarChargingDetection && profile.isDuringSolarHours && !isCharging)
                {
                    // Subtle UI hint for solar charging opportunity
                    if (currentBatteryLevel < 0.5f)
                    {
                        DebugSystem.Log($"[BatteryOptimizer] Solar charging window active (6AM-6PM)", DebugSystem.LogCategory.PERFORMANCE);
                    }
                }
            }
        }
        
        #if UNITY_ANDROID && !UNITY_EDITOR
        private void UpdateAndroidBatteryStatus()
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject applicationContext = currentActivity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                using (AndroidJavaObject intentFilter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED"))
                using (AndroidJavaObject batteryStatus = applicationContext.Call<AndroidJavaObject>("registerReceiver", null, intentFilter))
                {
                    int level = batteryStatus.Call<int>("getIntExtra", "level", -1);
                    int scale = batteryStatus.Call<int>("getIntExtra", "scale", -1);
                    int status = batteryStatus.Call<int>("getIntExtra", "status", -1);
                    
                    currentBatteryLevel = (level / (float)scale);
                    isCharging = status == 2 || status == 5; // Charging or full
                    
                    // Log critical transitions
                    if (currentBatteryLevel < 0.15f && !isCharging)
                    {
                        DebugSystem.LogWarning($"[BatteryOptimizer] CRITICAL: {currentBatteryLevel:P0} battery", DebugSystem.LogCategory.PERFORMANCE);
                    }
                }
            }
        }
        #endif
        
        private void ApplyBatteryConservation()
        {
            if (currentBatteryLevel < profile.lowPowerThreshold)
            {
                // Low power mode
                if (autoReduceFrameRate && Application.targetFrameRate > 20)
                {
                    Application.targetFrameRate = 20;
                }
                
                if (disableVibrationsOnLow)
                {
                    Handheld.Vibrate = delegate { }; // Disable vibration
                }
                
                if (reduceAudioQuality && audioSystem != null)
                {
                    audioSystem.SetAudioQuality(0.5f);
                }
                
                // Reduce particle density
                particleSystem?.SetEmissionMultiplier(0.3f);
            }
            else if (!isInEmergencyMode)
            {
                // Normal mode
                if (autoReduceFrameRate && Application.targetFrameRate != mobilePerformance?.targetFrameRate)
                {
                    Application.targetFrameRate = mobilePerformance.targetFrameRate;
                }
                
                audioSystem?.SetAudioQuality(1.0f);
                particleSystem?.SetEmissionMultiplier(1.0f);
            }
        }
        
        public void EnterEmergencyMode()
        {
            isInEmergencyMode = true;
            
            // Aggressive battery conservation
            Application.targetFrameRate = 15;
            QualitySettings.SetQualityLevel(0, true);
            
            // Disable all non-essential systems
            FindObjectOfType<BoduberuSystem>()?.SetVolume(0f);
            particleSystem?.SetEmissionEnabled(false);
            
            // Show emergency UI
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowBatteryWarning();
            }
            
            DebugSystem.LogWarning("[BatteryOptimizer] EMERGENCY MODE ACTIVATED", DebugSystem.LogCategory.PERFORMANCE);
            
            // Save game state automatically
            SaveSystem.Instance?.AutoSave();
        }
        
        private bool IsDuringSolarHours()
        {
            float hour = TimeSystem.Instance?.GetCurrentHour() ?? 12f;
            return hour >= profile.solarPeakHours.x && hour <= profile.solarPeakHours.y;
        }
        
        public float GetBatteryLevel() => currentBatteryLevel;
        public bool IsCharging() => isCharging;
        public bool IsEmergencyMode() => isInEmergencyMode;
    }
}
