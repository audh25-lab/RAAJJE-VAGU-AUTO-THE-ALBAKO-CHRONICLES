// MobilePerformance.cs
// RVACONT-007 - Batch 7: Performance Systems
// Optimized for tropical climate performance degradation (35°C+ ambient)

using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace RVA.TAC.Performance
{
    [BurstCompile]
    public struct ThermalThrottlingJob : IJob
    {
        public float ambientTemperature; // Maldives: 28-35°C typical
        public float deviceTemperature;
        public NativeArray<float> throttleFactor;
        
        public void Execute()
        {
            // Thermal throttle curve: 35°C=1.0, 40°C=0.8, 45°C=0.6
            float tempRatio = deviceTemperature / ambientTemperature;
            throttleFactor[0] = math.clamp(1.0f - (tempRatio - 1f) * 2f, 0.4f, 1.0f);
        }
    }

    public class MobilePerformance : MonoBehaviour
    {
        [Header("Maldives Thermal Profile")]
        public float ambientTemperature = 32f;
        public float thermalThrottleThreshold = 40f;
        public float criticalTempThreshold = 45f;
        
        [Header("Performance Targets")]
        public int targetFrameRate = 30;
        public float targetFrameTime => 1f / targetFrameRate;
        public bool enableDynamicResolution = true;
        
        [Header("Adaptive Quality")]
        public QualityLevel currentQuality;
        public enum QualityLevel { High, Medium, Low, Critical }
        
        // Burst-compiled job system
        private ThermalThrottlingJob thermalJob;
        private NativeArray<float> throttleResult;
        private JobHandle thermalHandle;
        
        // Performance metrics
        private float[] frameTimeBuffer = new float[60];
        private int frameTimeIndex;
        public float AverageFrameTime { get; private set; }
        public float CurrentThrottleFactor { get; private set; } = 1f;
        
        // Component references
        private BatteryOptimizer batteryOptimizer;
        private MemoryManager memoryManager;
        
        private static MobilePerformance _instance;
        public static MobilePerformance Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<MobilePerformance>();
                return _instance;
            }
        }
        
        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            // Initialize native collections
            throttleResult = new NativeArray<float>(1, Allocator.Persistent);
            
            // Set Unity performance baseline
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0; // Mobile-optimized
            
            // Mali-G72 GPU tuning (standard Maldivian device target)
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.masterTextureLimit = 0;
            
            // Get references
            batteryOptimizer = GetComponent<BatteryOptimizer>();
            memoryManager = GetComponent<MemoryManager>();
            
            DebugSystem.Log("[MobilePerformance] Initialized for Maldives thermal profile", DebugSystem.LogCategory.PERFORMANCE);
        }
        
        void OnDestroy()
        {
            if (throttleResult.IsCreated)
                throttleResult.Dispose();
        }
        
        void Start()
        {
            // Register with game manager
            if (MainGameManager.Instance != null)
            {
                MainGameManager.Instance.RegisterSystem(this, MainGameManager.SystemType.PERFORMANCE);
            }
            
            // Initial quality assessment
            AssessDeviceCapability();
        }
        
        void Update()
        {
            // Update frame time metrics
            UpdateFrameTimeMetrics();
            
            // Thermal throttling check every 2 seconds
            if (Time.frameCount % (targetFrameRate * 2) == 0)
            {
                CheckThermalState();
            }
            
            // Adaptive quality management
            if (Time.frameCount % (targetFrameRate * 3) == 0)
            {
                AdjustQualityLevel();
            }
            
            // Complete pending thermal job
            if (thermalHandle.IsCompleted)
            {
                thermalHandle.Complete();
                CurrentThrottleFactor = throttleResult[0];
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateFrameTimeMetrics()
        {
            frameTimeBuffer[frameTimeIndex] = Time.unscaledDeltaTime;
            frameTimeIndex = (frameTimeIndex + 1) % frameTimeBuffer.Length;
            
            // Calculate moving average
            float sum = 0f;
            for (int i = 0; i < frameTimeBuffer.Length; i++)
                sum += frameTimeBuffer[i];
            AverageFrameTime = sum / frameTimeBuffer.Length;
        }
        
        private void CheckThermalState()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject applicationContext = currentActivity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                // Get battery temperature (approximates device temp)
                using (AndroidJavaObject intentFilter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED"))
                using (AndroidJavaObject batteryStatus = applicationContext.Call<AndroidJavaObject>("registerReceiver", null, intentFilter))
                {
                    int temperature = batteryStatus.Call<int>("getIntExtra", "temperature", 0);
                    float deviceTempC = temperature / 10f;
                    
                    // Schedule burst job
                    thermalJob = new ThermalThrottlingJob
                    {
                        ambientTemperature = ambientTemperature,
                        deviceTemperature = deviceTempC,
                        throttleFactor = throttleResult
                    };
                    thermalHandle = thermalJob.Schedule();
                    
                    // Log critical thermal events
                    if (deviceTempC > criticalTempThreshold)
                    {
                        DebugSystem.LogWarning($"[MobilePerformance] CRITICAL THERMAL: {deviceTempC:F1}°C", DebugSystem.LogCategory.PERFORMANCE);
                        EmergencyPerformanceMode();
                    }
                }
            }
            #endif
        }
        
        private void AdjustQualityLevel()
        {
            float targetTime = targetFrameTime * 0.95f; // 5% headroom
            
            if (AverageFrameTime > targetTime * 1.5f || CurrentThrottleFactor < 0.6f)
            {
                SetQualityLevel(QualityLevel.Critical);
            }
            else if (AverageFrameTime > targetTime * 1.2f || CurrentThrottleFactor < 0.75f)
            {
                SetQualityLevel(QualityLevel.Low);
            }
            else if (AverageFrameTime > targetTime * 1.05f || CurrentThrottleFactor < 0.85f)
            {
                SetQualityLevel(QualityLevel.Medium);
            }
            else
            {
                SetQualityLevel(QualityLevel.High);
            }
        }
        
        public void SetQualityLevel(QualityLevel level)
        {
            if (level == currentQuality) return;
            
            currentQuality = level;
            
            switch (level)
            {
                case QualityLevel.High:
                    QualitySettings.SetQualityLevel(3, true);
                    Shader.globalMaximumLOD = 300;
                    break;
                case QualityLevel.Medium:
                    QualitySettings.SetQualityLevel(2, true);
                    Shader.globalMaximumLOD = 250;
                    break;
                case QualityLevel.Low:
                    QualitySettings.SetQualityLevel(1, true);
                    Shader.globalMaximumLOD = 200;
                    break;
                case QualityLevel.Critical:
                    QualitySettings.SetQualityLevel(0, true);
                    Shader.globalMaximumLOD = 150;
                    break;
            }
            
            // Update resolution scale
            if (enableDynamicResolution)
            {
                float scale = level switch
                {
                    QualityLevel.High => 1.0f,
                    QualityLevel.Medium => 0.85f,
                    QualityLevel.Low => 0.7f,
                    QualityLevel.Critical => 0.55f,
                    _ => 0.7f
                };
                ScalableBufferManager.ResizeBuffers(scale, scale);
            }
            
            DebugSystem.Log($"[MobilePerformance] Quality: {level} | Throttle: {CurrentThrottleFactor:F2}", DebugSystem.LogCategory.PERFORMANCE);
            
            // Notify other systems
            PerformanceProfiler.Instance?.OnQualityLevelChanged(level);
        }
        
        private void EmergencyPerformanceMode()
        {
            SetQualityLevel(QualityLevel.Critical);
            
            // Aggressive memory cleanup
            memoryManager?.ForceGarbageCollection();
            
            // Notify battery optimizer
            batteryOptimizer?.EnterEmergencyMode();
            
            // Disable non-essential systems
            if (ParticleSystem.Instance != null)
                ParticleSystem.Instance.SetEmissionEnabled(false);
        }
        
        private void AssessDeviceCapability()
        {
            // Device tier detection for Maldivian market
            int gpuLevel = SystemInfo.graphicsMemorySize;
            int cpuCores = SystemInfo.processorCount;
            
            if (gpuLevel < 1500 || cpuCores < 6)
            {
                SetQualityLevel(QualityLevel.Medium);
                DebugSystem.Log("[MobilePerformance] Device: Mid-tier | Default: Medium", DebugSystem.LogCategory.PERFORMANCE);
            }
            else
            {
                SetQualityLevel(QualityLevel.High);
                DebugSystem.Log("[MobilePerformance] Device: High-tier | Default: High", DebugSystem.LogCategory.PERFORMANCE);
            }
        }
        
        // Public API
        public bool IsPerformanceCritical() => currentQuality == QualityLevel.Critical;
        public float GetCurrentGpuBudget() => CurrentThrottleFactor * (targetFrameTime - AverageFrameTime);
    }
}
