using UnityEngine;
using System;
using System.Collections.Generic;

public class IslamicCalendarSystem : MonoBehaviour
{
    public static IslamicCalendarSystem Instance { get; private set; }

    public event Action<IslamicDate> OnNewDay;
    public event Action<IslamicHoliday> OnHoliday;

    public struct IslamicDate
    {
        public int Day;
        public int Month;
        public int Year;
        public string MonthName;

        public override string ToString()
        {
            return $"{Day} {MonthName}, {Year} AH";
        }
    }

    [System.Serializable]
    public struct IslamicHoliday
    {
        public string Name;
        public int Day;
        public int Month;
    }

    [Header("Calendar Settings")]
    public int startYear = 1445; // AH
    public List<IslamicHoliday> holidays;

    private IslamicDate currentDate;
    private readonly string[] monthNames = { "Muharram", "Safar", "Rabi' al-awwal", "Rabi' al-thani", "Jumada al-awwal", "Jumada al-thani", "Rajab", "Sha'ban", "Ramadan", "Shawwal", "Dhu al-Qi'dah", "Dhu al-Hijjah" };
    // Using a simplified 30/29 day alternating calendar for gameplay purposes.
    private readonly int[] daysInMonth = { 30, 29, 30, 29, 30, 29, 30, 29, 30, 29, 30, 29 };

    private float previousTimeOfDay = -1f;
    private LightingSystem lightingSystem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        lightingSystem = FindObjectOfType<LightingSystem>();
        if (lightingSystem == null)
        {
            Debug.LogError("IslamicCalendarSystem requires a LightingSystem to track the day cycle.");
            this.enabled = false;
            return;
        }

        // Subscribe to SaveSystem events
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnLoad += LoadCalendarData;
        }

        // Initialize with default or loaded data
        if (currentDate.Year == 0) // Check if data has been loaded
        {
            currentDate = new IslamicDate { Day = 1, Month = 1, Year = startYear, MonthName = monthNames[0] };
        }
        Debug.Log($"Calendar Initialized. Today is: {currentDate}");

        // We subscribe to save later to ensure the initial state isn't saving over loaded data.
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnSave += SaveCalendarData;
        }
    }

    private void OnDestroy()
    {
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnSave -= SaveCalendarData;
            SaveSystem.Instance.OnLoad -= LoadCalendarData;
        }
    }

    private void Update()
    {
        if (lightingSystem == null) return;

        float currentTimeOfDay = lightingSystem.GetCurrentTimeOfDay();

        // A new day begins when the time of day loops from >0.9 to <0.1
        if (previousTimeOfDay > 0.9f && currentTimeOfDay < 0.1f)
        {
            AdvanceDay();
        }

        previousTimeOfDay = currentTimeOfDay;
    }

    public void AdvanceDay()
    {
        currentDate.Day++;
        int currentMonthDays = daysInMonth[currentDate.Month - 1];

        if (currentDate.Day > currentMonthDays)
        {
            currentDate.Day = 1;
            currentDate.Month++;

            if (currentDate.Month > 12)
            {
                currentDate.Month = 1;
                currentDate.Year++;
            }
        }
        currentDate.MonthName = monthNames[currentDate.Month - 1];

        Debug.Log($"A new day has begun. Today is: {currentDate}");
        OnNewDay?.Invoke(currentDate);
        CheckForHoliday();
    }

    private void CheckForHoliday()
    {
        foreach (var holiday in holidays)
        {
            if (holiday.Day == currentDate.Day && holiday.Month == currentDate.Month)
            {
                Debug.Log($"Today is a special day: {holiday.Name}!");
                OnHoliday?.Invoke(holiday);
            }
        }
    }

    public IslamicDate GetCurrentDate()
    {
        return currentDate;
    }

    // --- Save and Load Integration ---

    public void SaveCalendarData(SaveData data)
    {
        data.gameDay = currentDate.Day;
        data.gameMonth = currentDate.Month;
        data.gameYear = currentDate.Year;
    }

    public void LoadCalendarData(SaveData data)
    {
        currentDate = new IslamicDate
        {
            Day = data.gameDay,
            Month = data.gameMonth,
            Year = data.gameYear,
            MonthName = monthNames[data.gameMonth - 1]
        };
        Debug.Log("Calendar data loaded.");
    }
}
