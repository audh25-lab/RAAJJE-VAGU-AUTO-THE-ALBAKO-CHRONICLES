using UnityEngine;
using System;
using System.Collections.Generic;

public class PrayerTimeSystem : MonoBehaviour
{
    public static PrayerTimeSystem Instance { get; private set; }

    // Event that other systems can subscribe to.
    public event Action<PrayerType> OnPrayerTime;

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
        [Range(0, 1)]
        public float timeOfDay; // 0.0 = midnight, 0.5 = noon, 1.0 = next midnight
        [HideInInspector]
        public bool hasBeenTriggeredToday = false;
    }

    [Header("Prayer Times")]
    public List<Prayer> prayerTimes = new List<Prayer>()
    {
        new Prayer { type = PrayerType.Fajr, timeOfDay = 0.20f },    // Approx 4:48 AM
        new Prayer { type = PrayerType.Dhuhr, timeOfDay = 0.51f },   // Approx 12:14 PM
        new Prayer { type = PrayerType.Asr, timeOfDay = 0.65f },     // Approx 3:36 PM
        new Prayer { type = PrayerType.Maghrib, timeOfDay = 0.76f }, // Approx 6:14 PM
        new Prayer { type = PrayerType.Isha, timeOfDay = 0.81f }     // Approx 7:26 PM
    };

    // To prevent checking every single frame
    private float checkInterval = 1.0f;
    private float lastCheckTime = 0f;
    private float previousTimeOfDay = -1f;

    // Reference to the lighting system to get the time of day
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
        // Attempt to find the LightingSystem in the scene.
        lightingSystem = FindObjectOfType<LightingSystem>();
        if (lightingSystem == null)
        {
            Debug.LogError("PrayerTimeSystem requires a LightingSystem in the scene to function.");
            this.enabled = false;
        }
    }

    private void Update()
    {
        if (lightingSystem == null) return;

        if (Time.time - lastCheckTime > checkInterval)
        {
            float currentTimeOfDay = lightingSystem.GetCurrentTimeOfDay();
            CheckForPrayerTime(currentTimeOfDay);
            CheckForNewDay(currentTimeOfDay);

            previousTimeOfDay = currentTimeOfDay;
            lastCheckTime = Time.time;
        }
    }

    private void CheckForPrayerTime(float currentTime)
    {
        foreach (var prayer in prayerTimes)
        {
            // Check if the time has passed and it hasn't been triggered
            if (!prayer.hasBeenTriggeredToday && previousTimeOfDay < prayer.timeOfDay && currentTime >= prayer.timeOfDay)
            {
                TriggerPrayerTime(prayer);
            }
        }
    }

    private void TriggerPrayerTime(Prayer prayer)
    {
        prayer.hasBeenTriggeredToday = true;
        Debug.Log($"It is time for {prayer.type} prayer.");

        // Fire the event for other systems
        OnPrayerTime?.Invoke(prayer.type);

        // Potential in-game effects can be called from here or from subscribers
        // e.g., PlayAdhan(), TriggerNPCMosqueBehavior(), etc.
    }

    // A new day starts when the time of day loops from >0.9 to <0.1
    private void CheckForNewDay(float currentTime)
    {
        if (previousTimeOfDay > 0.9f && currentTime < 0.1f)
        {
            ResetPrayerTriggers();
            Debug.Log("A new day has begun. Resetting prayer times.");
        }
    }

    private void ResetPrayerTriggers()
    {
        foreach (var prayer in prayerTimes)
        {
            prayer.hasBeenTriggeredToday = false;
        }
    }
}
