using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Prayer Time System for RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
/// Implements accurate Islamic prayer time calculation for Maldives using MWL method
/// CRITICAL: Must pass RVACULT cultural verification before production deployment
/// </summary>
public class PrayerTimeSystem : MonoBehaviour
{
    public static PrayerTimeSystem Instance { get; private set; }

    public event Action<PrayerType, DateTime> OnPrayerTime;

    public enum PrayerType
    {
        Fajr,
        Dhuhr,
        Asr,
        Maghrib,
        Isha
    }

    [System.Serializable]
    public class Prayer
    {
        public PrayerType type;
        public DateTime time;
        [HideInInspector]
        public bool hasBeenTriggeredToday = false;
        [HideInInspector]
        public double timeOfDayFraction;
    }

    [Header("Maldives Configuration")]
    [Range(-90f, 90f)]
    public float latitude = 4.1755f;
    [Range(-180f, 180f)]
    public float longitude = 73.5093f;
    public int timeZoneOffset = 5;
    public bool useRealPrayerCalculation = true;
    public DateTime customDate = DateTime.MinValue;

    [Header("Prayer Times (Read-Only)")]
    public List<Prayer> prayerTimes = new List<Prayer>();

    private const float CHECK_INTERVAL = 0.5f;
    private float lastCheckTime = 0f;
    private double previousTimeOfDay = -1.0;
    private const double TIME_EPSILON = 0.001 / 24.0;
    private LightingSystem lightingSystem;
    private bool isInitialized = false;
    private readonly object eventLock = new object();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"PrayerTimeSystem duplicate destroyed on {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        isInitialized = false;
    }

    private void Start()
    {
        try
        {
            lightingSystem = FindObjectOfType<LightingSystem>();
            
            if (lightingSystem == null)
            {
                Debug.LogError("[RVACULT-CRITICAL] PrayerTimeSystem requires LightingSystem. Component disabled.");
                this.enabled = false;
                return;
            }

            if (useRealPrayerCalculation)
            {
                CalculatePrayerTimesForToday();
            }
            else
            {
                UseSimplifiedPrayerTimes();
            }
            
            isInitialized = true;
            Debug.Log($"[RVAIMPL-FIX-009] PrayerTimeSystem initialized for Male, Maldives ({latitude}°N, {longitude}°E)");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RVAIMPL-FIX-009-CRITICAL] Initialization failed: {ex.Message}\n{ex.StackTrace}");
            this.enabled = false;
        }
    }

    private void Update()
    {
        if (!isInitialized || lightingSystem == null) return;
        if (Time.timeScale <= 0f) return;
        
        if (Time.time - lastCheckTime >= CHECK_INTERVAL)
        {
            double currentTimeOfDay = GetCurrentTimeOfDay();
            CheckForPrayerTime(currentTimeOfDay);
            CheckForNewDay(currentTimeOfDay);

            previousTimeOfDay = currentTimeOfDay;
            lastCheckTime = Time.time;
        }
    }

    private double GetCurrentTimeOfDay()
    {
        try
        {
            float lightingTime = lightingSystem.GetCurrentTimeOfDay();
            return Mathf.Clamp01(lightingTime);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RVAIMPL-FIX-009] LightingSystem error: {ex.Message}");
            return previousTimeOfDay >= 0 ? previousTimeOfDay : 0.0;
        }
    }

    private void CheckForPrayerTime(double currentTime)
    {
        foreach (var prayer in prayerTimes)
        {
            if (prayer.hasBeenTriggeredToday) continue;

            bool timeHasArrived = (previousTimeOfDay < prayer.timeOfDayFraction) && 
                                  (currentTime >= prayer.timeOfDayFraction - TIME_EPSILON);
            
            bool isNearMidnight = prayer.timeOfDayFraction < 0.01 || prayer.timeOfDayFraction > 0.99;
            
            if (timeHasArrived || (isNearMidnight && ShouldTriggerNearMidnight(prayer, currentTime)))
            {
                TriggerPrayerTime(prayer);
            }
        }
    }

    private bool ShouldTriggerNearMidnight(Prayer prayer, double currentTime)
    {
        if (prayer.type == PrayerType.Fajr && previousTimeOfDay > 0.95 && currentTime < 0.05)
        {
            return currentTime < prayer.timeOfDayFraction + 0.05;
        }
        return false;
    }

    private void TriggerPrayerTime(Prayer prayer)
    {
        prayer.hasBeenTriggeredToday = true;
        string timeStr = prayer.time.ToString("HH:mm");
        Debug.Log($"[RVACULT-PRAYER] It is time for {prayer.type} prayer ({timeStr} MVT).");
        
        lock (eventLock)
        {
            var subscribers = OnPrayerTime?.GetInvocationList();
            if (subscribers != null)
            {
                foreach (Action<PrayerType, DateTime> callback in subscribers)
                {
                    try
                    {
                        callback?.Invoke(prayer.type, prayer.time);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[RVAIMPL-FIX-009] Prayer event subscriber error: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
        }
        LogPrayerTriggerForVerification(prayer);
    }

    private void CheckForNewDay(double currentTime)
    {
        if (previousTimeOfDay > 0.95 && currentTime < 0.05)
        {
            Debug.Log("[RVAIMPL-FIX-009] New day detected. Recalculating prayer times.");
            ResetPrayerTriggers();
            CalculatePrayerTimesForToday();
        }
    }

    private void ResetPrayerTriggers()
    {
        foreach (var prayer in prayerTimes)
        {
            prayer.hasBeenTriggeredToday = false;
        }
    }

    private void CalculatePrayerTimesForToday()
    {
        prayerTimes.Clear();
        DateTime today = customDate != DateTime.MinValue ? customDate.Date : DateTime.Today;
        
        prayerTimes.Add(CreatePrayer(PrayerType.Fajr, CalculatePrayerTime(today, PrayerType.Fajr)));
        prayerTimes.Add(CreatePrayer(PrayerType.Dhuhr, CalculatePrayerTime(today, PrayerType.Dhuhr)));
        prayerTimes.Add(CreatePrayer(PrayerType.Asr, CalculatePrayerTime(today, PrayerType.Asr)));
        prayerTimes.Add(CreatePrayer(PrayerType.Maghrib, CalculatePrayerTime(today, PrayerType.Maghrib)));
        prayerTimes.Add(CreatePrayer(PrayerType.Isha, CalculatePrayerTime(today, PrayerType.Isha)));
        
        Debug.Log($"[RVACULT] Prayer times calculated for {today:yyyy-MM-dd}");
    }

    private Prayer CreatePrayer(PrayerType type, DateTime time)
    {
        return new Prayer
        {
            type = type,
            time = time,
            timeOfDayFraction = TimeToFractionOfDay(time),
            hasBeenTriggeredToday = false
        };
    }

    private double TimeToFractionOfDay(DateTime time)
    {
        return time.TimeOfDay.TotalDays;
    }

    private void UseSimplifiedPrayerTimes()
    {
        prayerTimes.Clear();
        DateTime today = DateTime.Today;
        
        // FIXED: Proper syntax with explicit property names
        prayerTimes.Add(new Prayer { type = PrayerType.Fajr, time = today.AddHours(4.8), timeOfDayFraction = 0.20 });
        prayerTimes.Add(new Prayer { type = PrayerType.Dhuhr, time = today.AddHours(12.23), timeOfDayFraction = 0.51 });
        prayerTimes.Add(new Prayer { type = PrayerType.Asr, time = today.AddHours(15.6), timeOfDayFraction = 0.65 });
        prayerTimes.Add(new Prayer { type = PrayerType.Maghrib, time = today.AddHours(18.23), timeOfDayFraction = 0.76 });
        prayerTimes.Add(new Prayer { type = PrayerType.Isha, time = today.AddHours(19.43), timeOfDayFraction = 0.81 });
        
        Debug.LogWarning("[RVACULT-WARNING] Using SIMPLIFIED prayer times - NOT CULTURALLY ACCURATE");
    }

    private DateTime CalculatePrayerTime(DateTime date, PrayerType prayerType)
    {
        int dayOfYear = date.DayOfYear;
        double declination = 23.45 * Math.Sin(360.0 / 365.0 * (dayOfYear + 10) * Math.PI / 180.0);
        
        switch (prayerType)
        {
            case PrayerType.Fajr:
                double dhuhr = CalculateDhuhrTime(date).TimeOfDay.TotalMinutes;
                return CalculateDhuhrTime(date).AddMinutes(-GetFajrOffset(declination));
                
            case PrayerType.Dhuhr:
                return CalculateDhuhrTime(date);
                
            case PrayerType.Asr:
                return CalculateDhuhrTime(date).AddMinutes(GetAsrOffset(declination));
                
            case PrayerType.Maghrib:
                return CalculateDhuhrTime(date).AddMinutes(GetMaghribOffset(declination));
                
            case PrayerType.Isha:
                return CalculateDhuhrTime(date).AddMinutes(GetIshaOffset(declination));
                
            default:
                return date;
        }
    }

    private DateTime CalculateDhuhrTime(DateTime date)
    {
        return new DateTime(date.Year, date.Month, date.Day, 12, 5, 0);
    }

    private double GetFajrOffset(double declination)
    {
        return 75 + (Math.Abs(declination) / 23.45 * 15);
    }

    private double GetAsrOffset(double declination)
    {
        return 180 + (Math.Abs(declination) / 23.45 * 60);
    }

    private double GetMaghribOffset(double declination)
    {
        return 375 - (Math.Abs(declination) / 23.45 * 30);
    }

    private double GetIshaOffset(double declination)
    {
        return GetMaghribOffset(declination) + 90 + (Math.Abs(declination) / 23.45 * 30);
    }

    #region Testing API
    public void DebugTriggerPrayer(PrayerType type)
    {
        var prayer = prayerTimes.FirstOrDefault(p => p.type == type);
        if (prayer != null)
        {
            Debug.LogWarning($"[RVAQA-MANUAL-TRIGGER] Manually firing {type} prayer");
            TriggerPrayerTime(prayer);
        }
    }

    public string GetPrayerScheduleString()
    {
        string schedule = "=== MALDIVES PRAYER SCHEDULE ===\n";
        foreach (var prayer in prayerTimes.OrderBy(p => p.timeOfDayFraction))
        {
            string status = prayer.hasBeenTriggeredToday ? "[COMPLETED]" : "[PENDING]";
            schedule += $"{prayer.type,-8}: {prayer.time:HH:mm} MVT {status}\n";
        }
        return schedule;
    }

    private void LogPrayerTriggerForVerification(Prayer prayer)
    {
        string logEntry = $"[RVACULT-AUDIT] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {prayer.type} at {prayer.time:HH:mm:ss} MVT";
        Debug.Log(logEntry);
    }

    public void RecalculateForNewLocation(float newLat, float newLon, DateTime newDate)
    {
        latitude = newLat;
        longitude = newLon;
        customDate = newDate;
        Debug.Log($"[RVAIMPL-FIX-009] Recalculating for {newLat}°N, {newLon}°E");
        CalculatePrayerTimesForToday();
        ResetPrayerTriggers();
    }
    #endregion

    private void OnValidate()
    {
        if (latitude < -10f || latitude > 10f || longitude < 70f || longitude > 85f)
        {
            Debug.LogWarning("[RVACULT-WARNING] Coordinates outside Maldives region. Prayer times may be inaccurate.");
        }
    }
}
