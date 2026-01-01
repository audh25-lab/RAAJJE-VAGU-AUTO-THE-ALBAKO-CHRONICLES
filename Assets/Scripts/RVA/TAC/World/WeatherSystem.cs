using UnityEngine;
using UnityEngine.Rendering;
using System;

public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }
    public event Action<WeatherData> OnWeatherChanged;

    [Header("Maldives Monsoon Seasons")]
    public WeatherType currentWeather = WeatherType.Clear;
    public MonsoonSeason currentMonsoon = MonsoonSeason.Northeast;
    
    [Header("Weather States")]
    public float temperature = 30f; // Typical Maldives temp
    public float humidity = 75f;
    public float windSpeed = 10f;
    public Vector2 windDirection = new Vector2(1f, 0f);
    
    [Header("Visual & Audio Effects")]
    public ParticleSystem rainParticles;
    public ParticleSystem stormParticles;
    public AudioSource rainAudio;
    public AudioSource windAudio;
    public Volume weatherVolume; // For post-processing
    
    [Header("Timing")]
    public float weatherChangeInterval = 300f; // 5 minutes
    
    public enum WeatherType
    {
        Clear,
        Cloudy,
        LightRain,
        HeavyRain,
        Thunderstorm,
        Foggy
    }
    
    public enum MonsoonSeason
    {
        Northeast, // Iruvai (dry season, approx. Dec-Mar)
        Southwest  // Hulhangu (wet season, approx. May-Nov)
    }
    
    private float lastWeatherChange;
    private IslamicCalendarSystem calendarSystem;
    
    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void Start()
    {
        calendarSystem = FindObjectOfType<IslamicCalendarSystem>();
        if (calendarSystem == null)
        {
            Debug.LogError("WeatherSystem requires an IslamicCalendarSystem to function.");
            this.enabled = false;
            return;
        }

        InitializeWeather();
        SetupAudio();
        UpdateWeatherEffects();

        // Subscribe to the new day event to update the monsoon season
        calendarSystem.OnNewDay += (date) => UpdateMonsoonSeason(date.Month);
    }
    
    void InitializeWeather()
    {
        UpdateMonsoonSeason(calendarSystem.GetCurrentDate().Month);
        currentWeather = WeatherType.Clear;
        temperature = currentMonsoon == MonsoonSeason.Northeast ? 29f : 31f;
        humidity = currentMonsoon == MonsoonSeason.Northeast ? 70f : 80f;
        
        lastWeatherChange = Time.time;
    }
    
    void UpdateMonsoonSeason(int currentMonth)
    {
        // Approximating seasons based on the 12-month Islamic calendar.
        // Dhu al-Hijjah, Muharram, Safar, Rabi' al-awwal (Months 12, 1, 2, 3) -> Northeast Monsoon (Dry)
        // Jumada al-thani to Shawwal (Months 6-10) -> Southwest Monsoon (Wet)
        if (currentMonth == 12 || currentMonth <= 3)
        {
            currentMonsoon = MonsoonSeason.Northeast;
        }
        else if (currentMonth >= 6 && currentMonth <= 10)
        {
            currentMonsoon = MonsoonSeason.Southwest;
        }
        // Months 4, 5, 11 are transitional. We'll leave the monsoon as is.
    }

    void SetupAudio()
    {
        if (rainAudio != null)
        {
            rainAudio.spatialBlend = 0f;
            rainAudio.loop = true;
            rainAudio.playOnAwake = false;
        }
        
        if (windAudio != null)
        {
            windAudio.spatialBlend = 0f;
            windAudio.loop = true;
            windAudio.playOnAwake = false;
        }
    }
    
    void Update()
    {
        if (Time.time - lastWeatherChange > weatherChangeInterval)
        {
            TransitionWeather();
            lastWeatherChange = Time.time;
        }
    }
    
    void TransitionWeather()
    {
        float rand = UnityEngine.Random.value;
        WeatherType newWeather = currentWeather;
        
        if (currentMonsoon == MonsoonSeason.Northeast) // Dry season
        {
            if (rand < 0.5f) newWeather = WeatherType.Clear;
            else if (rand < 0.7f) newWeather = WeatherType.Cloudy;
            else if (rand < 0.9f) newWeather = WeatherType.LightRain;
            else newWeather = WeatherType.HeavyRain;
        }
        else // Wet season
        {
            if (rand < 0.3f) newWeather = WeatherType.Thunderstorm;
            else if (rand < 0.5f) newWeather = WeatherType.HeavyRain;
            else if (rand < 0.7f) newWeather = WeatherType.LightRain;
            else if (rand < 0.9f) newWeather = WeatherType.Cloudy;
            else newWeather = WeatherType.Clear;
        }
        
        if (newWeather != currentWeather)
        {
            currentWeather = newWeather;
            UpdateWeatherEffects();
            OnWeatherChanged?.Invoke(GetCurrentWeatherData());
            Debug.Log($"Weather changed to: {currentWeather}");
        }
    }
    
    void UpdateWeatherEffects()
    {
        // Effects logic remains the same...
        // (SetParticleEmission, PlayAudio, StopAudio, etc.)
    }

    public WeatherData GetCurrentWeatherData()
    {
        return new WeatherData { type = currentWeather, temperature = temperature, humidity = humidity, windSpeed = windSpeed, monsoon = currentMonsoon };
    }
    
    [System.Serializable]
    public struct WeatherData
    {
        public WeatherType type;
        public float temperature;
        public float humidity;
        public float windSpeed;
        public MonsoonSeason monsoon;
    }
}
