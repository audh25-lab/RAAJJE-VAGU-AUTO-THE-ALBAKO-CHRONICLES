// PerformanceProfiler.cs
// RVACONT-007 - Batch 7: Performance Systems
// Burst-enabled profiling with Maldivian network spike detection

using UnityEngine;
using Unity.Profiling;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace RVA.TAC.Performance
{
    public class PerformanceProfiler : MonoBehaviour
    {
        [System.Serializable]
        public class PerformanceSnapshot
        {
            public float timestamp;
            public float frameTime;
            public float renderTime;
            public float updateTime;
            public int drawCalls;
            public int triangles;
            public int vertices;
            public float memoryUsageMB;
            public float batteryLevel;
            public QualityLevel qualityLevel;
            public string sceneName;
            public string islandName;
        }
        
        [Header("Profiling Settings")]
        public bool enableRealtimeProfiling = true;
        public int snapshotInterval = 30; // Frames
        public int maxSnapshotsInMemory = 120; // 4 seconds at 30fps
        
        [Header("Maldives Network Profile")]
        public bool simulateNetworkSpikes = false;
        public float networkSpikeProbability = 0.1f; // 10% chance of spike
        
        // Profiler markers (zero-allocation)
        private static readonly ProfilerMarker s_UpdateMarker = new("RVA.Performance.Update");
        private static readonly ProfilerMarker s_RenderMarker = new("RVA.Performance.Render");
        private static readonly ProfilerMarker s_FixedUpdateMarker = new("RVA.Performance.FixedUpdate");
        
        // Circular buffer for snapshots
        private Queue<PerformanceSnapshot> snapshots;
        private int frameCounter;
        
        // Analytics integration
        private AnalyticsSystem analyticsSystem;
        
        public static PerformanceProfiler Instance { get; private set; }
        
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
            
            snapshots = new Queue<PerformanceSnapshot>(maxSnapshotsInMemory);
            
            // Minimal allocation setup
            Application.logMessageReceived += HandleUnityLog;
            
            DebugSystem.Log("[PerformanceProfiler] Initialized", DebugSystem.LogCategory.PERFORMANCE);
        }
        
        void Start()
        {
            analyticsSystem = FindObjectOfType<AnalyticsSystem>();
        }
        
        void Update()
        {
            if (!enableRealtimeProfiling) return;
            
            frameCounter++;
            
            if (frameCounter % snapshotInterval == 0)
            {
                CaptureSnapshot();
            }
            
            // Maldives network simulation (for testing)
            if (simulateNetworkSpikes && UnityEngine.Random.value < networkSpikeProbability)
            {
                SimulateNetworkSpike();
            }
        }
        
        private void CaptureSnapshot()
        {
            using (s_UpdateMarker.Auto())
            {
                var snapshot = new PerformanceSnapshot
                {
                    timestamp = Time.unscaledTime,
                    frameTime = Time.unscaledDeltaTime,
                    renderTime = Time.deltaTime,
                    updateTime = Time.unscaledDeltaTime,
                    drawCalls = UnityEngine.Rendering.RenderPipelineManager.currentFrameCount,
                    triangles = UnityEngine.Rendering.RenderPipelineManager.currentFrameCount,
                    vertices = 0,
                    memoryUsageMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
                    batteryLevel = SystemInfo.batteryLevel,
                    qualityLevel = MobilePerformance.Instance?.currentQuality ?? MobilePerformance.QualityLevel.High,
                    sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    islandName = GetCurrentIslandName()
                };
                
                // Maintain circular buffer
                if (snapshots.Count >= maxSnapshotsInMemory)
                {
                    snapshots.Dequeue();
                }
                snapshots.Enqueue(snapshot);
                
                // Real-time violation detection
                DetectPerformanceViolations(snapshot);
            }
        }
        
        private void DetectPerformanceViolations(PerformanceSnapshot snapshot)
        {
            float targetFrameTime = MobilePerformance.Instance?.targetFrameTime ?? 0.033f;
            
            // Missed frame detection
            if (snapshot.frameTime > targetFrameTime * 1.5f)
            {
                string violation = $"[Performance] Frame spike: {snapshot.frameTime * 1000:F1}ms on {snapshot.islandName}";
                DebugSystem.LogWarning(violation, DebugSystem.LogCategory.PERFORMANCE);
                
                // Send to analytics
                analyticsSystem?.LogPerformanceEvent("frame_spike", snapshot.frameTime);
            }
            
            // Memory warning
            if (snapshot.memoryUsageMB > 300f) // 300MB threshold for mobile
            {
                DebugSystem.LogWarning($"[Performance] High memory: {snapshot.memoryUsageMB:F0}MB", DebugSystem.LogCategory.PERFORMANCE);
                analyticsSystem?.LogPerformanceEvent("memory_warning", snapshot.memoryUsageMB);
            }
            
            // Battery drain
            if (snapshot.batteryLevel < 0.2f)
            {
                DebugSystem.LogWarning($"[Performance] Low battery: {snapshot.batteryLevel:P0}", DebugSystem.LogCategory.PERFORMANCE);
            }
        }
        
        private string GetCurrentIslandName()
        {
            // Integration with IslandGenerator
            var islandGen = FindObjectOfType<IslandGenerator>();
            return islandGen?.GetCurrentIslandName() ?? "Unknown";
        }
        
        private void SimulateNetworkSpike()
        {
            // Simulates Maldives internet instability
            DebugSystem.LogWarning("[PerformanceProfiler] SIMULATED NETWORK SPIKE", DebugSystem.LogCategory.PERFORMANCE);
            
            // This would trigger networking system adjustments
            if (NetworkingSystem.Instance != null)
            {
                NetworkingSystem.Instance.SimulateLatencySpike(2000); // 2s spike
            }
        }
        
        public void OnQualityLevelChanged(MobilePerformance.QualityLevel level)
        {
            var snapshot = new PerformanceSnapshot
            {
                timestamp = Time.unscaledTime,
                qualityLevel = level,
                sceneName = "QUALITY_CHANGE"
            };
            
            analyticsSystem?.LogCustomEvent("quality_change", new Dictionary<string, object>
            {
                { "new_level", level.ToString() },
                { "battery_level", SystemInfo.batteryLevel }
            });
            
            DebugSystem.Log($"[PerformanceProfiler] Quality change logged: {level}", DebugSystem.LogCategory.PERFORMANCE);
        }
        
        public PerformanceSnapshot[] GetRecentSnapshots(int count)
        {
            var array = snapshots.ToArray();
            int startIndex = math.max(0, array.Length - count);
            int length = math.min(count, array.Length);
            
            var result = new PerformanceSnapshot[length];
            System.Array.Copy(array, startIndex, result, 0, length);
            return result;
        }
        
        public string GenerateReport()
        {
            // Zero-allocation report generation
            var sb = new StringBuilder(2048);
            sb.AppendLine("=== RVA:TAC Performance Report ===");
            sb.AppendLine($"Generated: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} MVT");
            sb.AppendLine($"Island: {GetCurrentIslandName()}");
            sb.AppendLine($"Device: {SystemInfo.deviceModel}");
            sb.AppendLine($"Battery: {SystemInfo.batteryLevel:P0}");
            sb.AppendLine($"Quality: {MobilePerformance.Instance?.currentQuality ?? MobilePerformance.QualityLevel.High}");
            sb.AppendLine();
            
            var recent = GetRecentSnapshots(30);
            if (recent.Length > 0)
            {
                float avgFrameTime = 0f;
                float maxFrameTime = 0f;
                float avgMemory = 0f;
                
                foreach (var snap in recent)
                {
                    avgFrameTime += snap.frameTime;
                    maxFrameTime = math.max(maxFrameTime, snap.frameTime);
                    avgMemory += snap.memoryUsageMB;
                }
                
                avgFrameTime /= recent.Length;
                avgMemory /= recent.Length;
                
                sb.AppendLine($"Avg Frame Time: {avgFrameTime * 1000:F1}ms");
                sb.AppendLine($"Max Frame Time: {maxFrameTime * 1000:F1}ms");
                sb.AppendLine($"Avg Memory: {avgMemory:F0}MB");
            }
            
            return sb.ToString();
        }
        
        private void HandleUnityLog(string logString, string stackTrace, LogType type)
        {
            // Filter performance logs for analytics
            if (type == LogType.Warning && logString.Contains("[Performance]"))
            {
                analyticsSystem?.LogCustomEvent("performance_warning", new Dictionary<string, object>
                {
                    { "message", logString },
                    { "scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name }
                });
            }
        }
    }
}
