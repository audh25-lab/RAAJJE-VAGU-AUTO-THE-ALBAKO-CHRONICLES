// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
// Batch 8: Cultural Systems - RVACONT-008
// AnalyticsSystem.cs - Privacy-focused analytics with cultural sensitivity

using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using System.IO;

namespace RVA.TAC.Cultural
{
    [BurstCompile]
    public class AnalyticsSystem : MonoBehaviour
    {
        #region Singleton & Configuration
        public static AnalyticsSystem Instance { get; private set; }
        
        [Header("Analytics Settings")]
        public bool EnableAnalytics = true;
        public bool RespectDoNotTrack = true; // Cultural privacy respect
        public float FlushInterval = 300f; // 5 minutes
        public int MaxBatchSize = 50;
        
        [Header("Privacy & Culture")]
        public bool AnonymizeIPAddress = true;
        public bool ExcludeRamadanData = false; // Optional religious data exclusion
        
        private float lastFlushTime = 0f;
        private List<AnalyticsEvent> eventQueue = new List<AnalyticsEvent>();
        private object queueLock = new object();
        #endregion

        #region Event Structures
        [System.Serializable]
        public struct AnalyticsEvent
        {
            public string EventName;
            public float Timestamp;
            public int DayOfPlay;
            public float HourOfDay;
            public NativeHashMap<FixedString64Bytes, float> NumericParams;
            public NativeHashMap<FixedString64Bytes, FixedString128Bytes> StringParams;
            
            public static AnalyticsEvent Create(string eventName)
            {
                return new AnalyticsEvent
                {
                    EventName = eventName,
                    Timestamp = Time.realtimeSinceStartup,
                    DayOfPlay = GameState.Instance?.DaysSinceInstall ?? 0,
                    HourOfDay = TimeSystem.Instance?.CurrentHour ?? 0f,
                    NumericParams = new NativeHashMap<FixedString64Bytes, float>(8, Allocator.Temp),
                    StringParams = new NativeHashMap<FixedString64Bytes, FixedString128Bytes>(4, Allocator.Temp)
                };
            }
            
            public void Dispose()
            {
                if (NumericParams.IsCreated) NumericParams.Dispose();
                if (StringParams.IsCreated) StringParams.Dispose();
            }
        }

        [System.Serializable]
        private struct QueuedEvent
        {
            public string EventName;
            public float Timestamp;
            public Dictionary<string, object> Parameters;
        }
        #endregion

        #region Event Categories
        // Cultural sensitivity: Track gameplay, not personal behavior
        public enum EventCategory
        {
            Gameplay,      // Mission completion, fishing success
            Progression,   // Level up, skill unlock
            Economy,       // Money earned/spent
            Social,        // NPC interactions (anonymized)
            Technical,     // Performance, crashes
            Cultural       // Prayer time participation, Boduberu engagement
        }
        #endregion

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // Check for Do Not Track
                if (RespectDoNotTrack && PlayerPrefs.GetInt("DoNotTrack", 0) == 1)
                {
                    EnableAnalytics = false;
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            if (!EnableAnalytics) return;
            
            // Periodic flush
            if (Time.realtimeSinceStartup - lastFlushTime > FlushInterval)
            {
                FlushEvents();
                lastFlushTime = Time.realtimeSinceStartup;
            }
            
            // Emergency flush if queue too large
            if (eventQueue.Count >= MaxBatchSize)
            {
                FlushEvents();
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && EnableAnalytics)
            {
                TrackEvent("app_pause");
                FlushEvents();
            }
        }

        void OnApplicationQuit()
        {
            if (EnableAnalytics)
            {
                TrackEvent("app_quit");
                FlushEvents();
            }
        }

        #region Event Tracking
        public void TrackEvent(string eventName, EventCategory category = EventCategory.Gameplay)
        {
            if (!EnableAnalytics) return;
            
            // Cultural sensitivity filters
            if (ExcludeRamadanData && IslamicCalendar.Instance != null && IslamicCalendar.Instance.IsRamadan())
            {
                if (category == EventCategory.Economy || category == EventCategory.Cultural)
                    return; // Skip sensitive tracking during Ramadan
            }
            
            var job = new CreateEventJob
            {
                eventName = eventName,
                category = category,
                currentTime = Time.realtimeSinceStartup,
                dayOfPlay = GameState.Instance?.DaysSinceInstall ?? 0,
                currentHour = TimeSystem.Instance?.CurrentHour ?? 0f
            };
            
            var jobHandle = job.Schedule();
            jobHandle.Complete();
            
            lock (queueLock)
            {
                eventQueue.Add(job.resultEvent);
            }
        }

        public void TrackEvent(string eventName, Dictionary<string, object> parameters, EventCategory category = EventCategory.Gameplay)
        {
            if (!EnableAnalytics) return;
            
            var @event = AnalyticsEvent.Create(eventName);
            
            // Add parameters with privacy filtering
            foreach (var param in parameters)
            {
                if (IsSensitiveParameter(param.Key)) continue;
                
                switch (param.Value)
                {
                    case float f:
                        @event.NumericParams.TryAdd(new FixedString64Bytes(param.Key), f);
                        break;
                    case int i:
                        @event.NumericParams.TryAdd(new FixedString64Bytes(param.Key), (float)i);
                        break;
                    case string s:
                        @event.StringParams.TryAdd(new FixedString64Bytes(param.Key), new FixedString128Bytes(s));
                        break;
                }
            }
            
            lock (queueLock)
            {
                eventQueue.Add(@event);
            }
        }

        private bool IsSensitiveParameter(string key)
        {
            string lowerKey = key.ToLower();
            return lowerKey.Contains("name") || lowerKey.Contains("location") || 
                   lowerKey.Contains("geo") || lowerKey.Contains("ip") ||
                   lowerKey.Contains("personal") || lowerKey.Contains("ident");
        }
        #endregion

        #region Job System Integration
        [BurstCompile]
        private struct CreateEventJob : IJob
        {
            [ReadOnly] public string eventName;
            [ReadOnly] public EventCategory category;
            [ReadOnly] public float currentTime;
            [ReadOnly] public int dayOfPlay;
            [ReadOnly] public float currentHour;
            
            public AnalyticsEvent resultEvent;

            public void Execute()
            {
                resultEvent = AnalyticsEvent.Create(eventName);
                // Additional burst-optimized processing here
            }
        }

        [BurstCompile]
        private struct ProcessEventsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<AnalyticsEvent> events;
            [WriteOnly] public NativeArray<int> processedCount;

            public void Execute(int index)
            {
                // Process events in parallel (aggregation, filtering)
                processedCount[index] = 1;
            }
        }
        #endregion

        #region Event Flushing & Storage
        private void FlushEvents()
        {
            if (eventQueue.Count == 0) return;
            
            List<AnalyticsEvent> batch;
            lock (queueLock)
            {
                batch = new List<AnalyticsEvent>(eventQueue);
                eventQueue.Clear();
            }
            
            // Process batch
            StartCoroutine(FlushBatch(batch));
        }

        private System.Collections.IEnumerator FlushBatch(List<AnalyticsEvent> batch)
        {
            #if UNITY_WEBGL
            // WebGL: Use localStorage
            string json = JsonUtility.ToJson(new { events = batch });
            PlayerPrefs.SetString($"analytics_backup_{Time.realtimeSinceStartup}", json);
            #else
            // Mobile: Write to persistent storage
            string path = Path.Combine(Application.persistentDataPath, "analytics.dat");
            string json = JsonUtility.ToJson(new { events = batch });
            
            File.AppendAllText(path, json + "\n");
            #endif
            
            // Simulate network send (replace with actual endpoint)
            yield return new WaitForSeconds(0.1f);
            
            // Log for debugging (in production, this would be removed)
            Debug.Log($"[Analytics] Flushed {batch.Count} events");
            
            // Dispose native containers
            foreach (var ev in batch)
            {
                ev.Dispose();
            }
        }
        #endregion

        #region Gameplay-Specific Trackers
        public void TrackMissionComplete(string missionId, float duration, int score)
        {
            TrackEvent("mission_complete", new Dictionary<string, object>
            {
                { "mission_id", missionId },
                { "duration_seconds", duration },
                { "score", score },
                { "fishing_success", missionId.Contains("fish") ? 1 : 0 } // Cultural context
            }, EventCategory.Gameplay);
        }

        public void TrackFishingActivity(string location, string fishType, bool success)
        {
            TrackEvent("fishing_activity", new Dictionary<string, object>
            {
                { "location_hash", location.GetHashCode() }, // Anonymized
                { "fish_type", fishType },
                { "success", success ? 1 : 0 },
                { "time_of_day", TimeSystem.Instance?.CurrentHour ?? 0f }
            }, EventCategory.Cultural);
        }

        public void TrackPrayerParticipation(string prayerName, bool participated)
        {
            TrackEvent("prayer_engagement", new Dictionary<string, object>
            {
                { "prayer", prayerName },
                { "participated", participated ? 1 : 0 },
                { "respect_gained", PrayerTimeSystem.Instance?.PrayerTimeRespectBonus ?? 0f }
            }, EventCategory.Cultural);
        }

        public void TrackBoduberuEngagement(float sessionDuration, int participants)
        {
            TrackEvent("boduberu_session", new Dictionary<string, object>
            {
                { "duration", sessionDuration },
                { "participant_count", participants },
                { "is_ramadan", IslamicCalendar.Instance?.IsRamadan() ?? false }
            }, EventCategory.Cultural);
        }

        public void TrackVehicleUsage(string vehicleType, float distance)
        {
            TrackEvent("vehicle_used", new Dictionary<string, object>
            {
                { "type", vehicleType },
                { "distance_m", distance },
                { "is_boat", vehicleType.Contains("boat") || vehicleType.Contains("dhoni") ? 1 : 0 }
            }, EventCategory.Gameplay);
        }

        public void TrackEconomyTransaction(string type, float amount, string source)
        {
            if (ExcludeRamadanData && IslamicCalendar.Instance?.IsRamadan() == true && 
                source.Contains("gambling")) return; // Cultural filter
            
            TrackEvent("economy_transaction", new Dictionary<string, object>
            {
                { "transaction_type", type },
                { "amount", amount },
                { "source_category", source },
                { "player_balance", EconomySystem.Instance?.PlayerMoney ?? 0f }
            }, EventCategory.Economy);
        }
        #endregion

        #region Performance Tracking
        public void TrackPerformanceMetrics(float fps, float frameTime, float memoryMB)
        {
            TrackEvent("performance_snapshot", new Dictionary<string, object>
            {
                { "fps", fps },
                { "frame_time_ms", frameTime },
                { "memory_mb", memoryMB },
                { "battery_level", SystemInfo.batteryLevel },
                { "device_model", AnonymizeIPAddress ? "redacted" : SystemInfo.deviceModel }
            }, EventCategory.Technical);
        }

        private void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                TrackEvent("error_occurred", new Dictionary<string, object>
                {
                    { "error_type", type.ToString() },
                    { "message_hash", logString.GetHashCode() } // Anonymized
                }, EventCategory.Technical);
            }
        }
        #endregion

        #region User Consent Management
        public void RequestAnalyticsConsent()
        {
            // Show culturally sensitive consent dialog
            // In Maldives, privacy is highly valued
            string message = "Help us improve the game by sharing anonymous gameplay data. " +
                           "No personal information is collected, and you can opt out anytime.";
            
            // This would trigger a UI dialog
            Debug.Log($"[Analytics] Consent requested: {message}");
        }

        public void SetAnalyticsConsent(bool consent)
        {
            EnableAnalytics = consent;
            PlayerPrefs.SetInt("AnalyticsConsent", consent ? 1 : 0);
            
            if (!consent)
            {
                // Clear any stored data
                ClearAnalyticsData();
            }
        }

        public void ClearAnalyticsData()
        {
            lock (queueLock)
            {
                eventQueue.Clear();
            }
            
            #if !UNITY_WEBGL
            string path = Path.Combine(Application.persistentDataPath, "analytics.dat");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            #endif
        }
        #endregion

        #region Memory Management
        void OnDestroy()
        {
            lock (queueLock)
            {
                foreach (var ev in eventQueue)
                {
                    ev.Dispose();
                }
                eventQueue.Clear();
            }
        }
        #endregion
    }

    #region Game State Manager (Required for Analytics)
    [BurstCompile]
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }
        public int DaysSinceInstall { get; private set; }
        public int SessionsCount { get; private set; }
        public float TotalPlayTime { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadState();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            TotalPlayTime += Time.unscaledDeltaTime;
        }

        private void LoadState()
        {
            DaysSinceInstall = PlayerPrefs.GetInt("DaysSinceInstall", 0);
            SessionsCount = PlayerPrefs.GetInt("SessionsCount", 0) + 1;
            
            PlayerPrefs.SetInt("SessionsCount", SessionsCount);
            
            // Track session start
            AnalyticsSystem.Instance?.TrackEvent("session_start", new Dictionary<string, object>
            {
                { "session_number", SessionsCount },
                { "days_since_install", DaysSinceInstall }
            });
        }

        void OnApplicationQuit()
        {
            PlayerPrefs.Save();
        }
    }
    #endregion
}
