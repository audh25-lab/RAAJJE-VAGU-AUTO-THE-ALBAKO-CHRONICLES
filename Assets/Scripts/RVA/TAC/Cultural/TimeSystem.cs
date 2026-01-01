// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
// Batch 8: Cultural Systems - RVACONT-008
// TimeSystem.cs - Unified time management with Islamic/Gregorian integration

using UnityEngine;
using Unity.Burst;
using Unity.Mathematics;

namespace RVA.TAC.Cultural
{
    [System.Serializable]
    public struct GameTime
    {
        public float TotalSeconds;
        public int Day;
        public float Hour;
        public int Minute;
        public float Second;

        public GameTime(float totalSeconds)
        {
            TotalSeconds = totalSeconds;
            Day = (int)(totalSeconds / 86400f);
            float daySeconds = totalSeconds % 86400f;
            Hour = daySeconds / 3600f;
            Minute = (int)((daySeconds % 3600f) / 60f);
            Second = daySeconds % 60f;
        }
    }

    [BurstCompile]
    public class TimeSystem : MonoBehaviour
    {
        #region Singleton & State
        public static TimeSystem Instance { get; private set; }
        
        [Header("Time Settings")]
        [Range(0.1f, 100f)]
        public float TimeScale = 24f; // 24 = 1 real second = 1 game hour
        public bool PauseTime = false;
        
        [Header("Start Time")]
        public int StartDay = 0; // Day 0 = 2024/01/01
        public float StartHour = 6f; // 6 AM
        
        [Header("Current Time")]
        public float CurrentDay; // Days since start
        public float CurrentHour; // 0-24
        public float CurrentTimeOfDay; // 0-1
        
        [Header("Cultural Settings")]
        public bool UseRealTimeDate = false; // For debugging
        public float MaldivianTimeOffset = 5f; // UTC+5
        
        private float internalTime = 0f;
        #endregion

        #region Events
        public System.Action<float> OnHourChanged;
        public System.Action<int> OnDayChanged;
        public System.Action<float> OnTimeOfDayChanged;
        #endregion

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
            }
        }

        void Start()
        {
            internalTime = StartDay * 86400f + StartHour * 3600f;
            SetTime(internalTime);
        }

        void Update()
        {
            if (!PauseTime)
            {
                float deltaTime = Time.deltaTime * TimeScale;
                internalTime += deltaTime;
                SetTime(internalTime);
            }
        }

        #region Time Management
        private void SetTime(float totalSeconds)
        {
            GameTime newTime = new GameTime(totalSeconds);
            
            bool hourChanged = (int)CurrentHour != (int)newTime.Hour;
            bool dayChanged = (int)CurrentDay != newTime.Day;
            
            CurrentDay = newTime.Day;
            CurrentHour = newTime.Hour;
            CurrentTimeOfDay = newTime.Hour / 24f;
            
            if (hourChanged)
            {
                OnHourChanged?.Invoke(CurrentHour);
            }
            
            if (dayChanged)
            {
                OnDayChanged?.Invoke(newTime.Day);
            }
            
            OnTimeOfDayChanged?.Invoke(CurrentTimeOfDay);
        }

        public void SetTimeOfDay(float hour)
        {
            float daySeconds = CurrentDay * 86400f + hour * 3600f;
            internalTime = daySeconds;
            SetTime(internalTime);
        }

        public void AddHours(float hours)
        {
            internalTime += hours * 3600f;
            SetTime(internalTime);
        }

        public void AddDays(int days)
        {
            internalTime += days * 86400f;
            SetTime(internalTime);
        }

        public GameTime GetGameTime()
        {
            return new GameTime(internalTime);
        }

        public void ResetTime()
        {
            internalTime = StartDay * 86400f + StartHour * 3600f;
            SetTime(internalTime);
        }
        #endregion

        #region Real-World Integration
        public System.DateTime GetRealWorldDateTime()
        {
            if (UseRealTimeDate)
            {
                return System.DateTime.UtcNow.AddHours(MaldivianTimeOffset);
            }
            
            // Base date: 2024/01/01 (AVOIDING SENSITIVE)
            System.DateTime baseDate = new System.DateTime(2024, 1, 1);
            return baseDate.AddDays(CurrentDay).AddHours(CurrentHour);
        }

        public string GetFormattedDateTime()
        {
            var dateTime = GetRealWorldDateTime();
            return $"{dateTime:yyyy-MM-dd HH:mm:ss}";
        }

        public string GetMaldivianTimeString()
        {
            // Maldivians often use 12-hour format with local terms
            int hour12 = ((int)CurrentHour % 12);
            if (hour12 == 0) hour12 = 12;
            string period = CurrentHour < 12 ? "AM" : "PM";
            
            // Maldivian time references (simplified)
            if (CurrentHour >= 5 && CurrentHour < 6) return "Faajuru";
            if (CurrentHour >= 6 && CurrentHour < 12) return "Hen'dhun";
            if (CurrentHour >= 12 && CurrentHour < 16) return "Re'ndi";
            if (CurrentHour >= 16 && CurrentHour < 18) return "Handhaan";
            if (CurrentHour >= 18 && CurrentHour < 19) return "Maghrib";
            if (CurrentHour >= 19 || CurrentHour < 5) return "Kandu";
            
            return $"{hour12:00}:{CurrentMinute:00} {period}";
        }
        #endregion

        #region Time Utilities
        public int CurrentMinute => (int)((CurrentHour % 1f) * 60f);
        public int CurrentSecond => (int)(((CurrentHour * 3600f) % 60f));

        public bool IsNightTime()
        {
            // Night: 7 PM to 5 AM (Maldivian culture)
            return CurrentHour >= 19f || CurrentHour < 5f;
        }

        public bool IsDayTime()
        {
            return !IsNightTime();
        }

        public bool IsMorning()
        {
            return CurrentHour >= 5f && CurrentHour < 12f;
        }

        public bool IsAfternoon()
        {
            return CurrentHour >= 12f && CurrentHour < 17f;
        }

        public bool IsEvening()
        {
            return CurrentHour >= 17f && CurrentHour < 21f;
        }

        public float GetTimeOfDayLerp()
        {
            // Smooth 0-1 curve for day/night transitions
            return math.smoothstep(0f, 1f, CurrentTimeOfDay);
        }
        #endregion

        #region Burst-Optimized Operations
        [BurstCompile]
        public static float CalculateSunAltitude(float hour, int dayOfYear, float latitude = MALDIVES_LATITUDE)
        {
            float declination = 23.45f * math.sin(math.radians(360f * (284f + dayOfYear) / 365f));
            float hourAngle = 15f * (hour - 12f);
            
            float altitude = math.degrees(math.asin(
                math.sin(math.radians(latitude)) * math.sin(math.radians(declination)) +
                math.cos(math.radians(latitude)) * math.cos(math.radians(declination)) *
                math.cos(math.radians(hourAngle))
            ));
            
            return altitude;
        }

        [BurstCompile]
        public static float CalculateDayNightRatio(int dayOfYear, float latitude = MALDIVES_LATITUDE)
        {
            float declination = 23.45f * math.sin(math.radians(360f * (284f + dayOfYear) / 365f));
            
            float cosH = -math.tan(math.radians(latitude)) * math.tan(math.radians(declination));
            float H = math.degrees(math.acos(math.clamp(cosH, -1f, 1f)));
            
            return H / 180f; // Ratio of daylight (0.5 = equal day/night)
        }
        #endregion

        #region Pause & Resume
        public void Pause()
        {
            PauseTime = true;
        }

        public void Resume()
        {
            PauseTime = false;
        }

        public void TogglePause()
        {
            PauseTime = !PauseTime;
        }
        #endregion

        #region Constants
        private const float MALDIVES_LATITUDE = 3.2028f;
        #endregion
    }
}
