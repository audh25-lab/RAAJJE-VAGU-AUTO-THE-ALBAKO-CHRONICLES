using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LightingSystem : MonoBehaviour
{
    [Header("Maldives Lighting - Tropical")]
    public Light sunLight;
    public Light moonLight;
    public Volume skyVolume;
    
    [Header("Day-Night Cycle")]
    public float dayDuration = 1200f; // 20 minutes for gameplay
    public AnimationCurve sunIntensityCurve;
    public Gradient sunColorGradient;
    
    [Header("Tropical Sun")]
    public Color sunriseColor = new Color(1f, 0.6f, 0.3f);
    public Color middayColor = new Color(1f, 0.95f, 0.8f);
    public Color sunsetColor = new Color(1f, 0.4f, 0.2f);
    public Color nightColor = new Color(0.3f, 0.4f, 0.6f);
    
    [Header("Mobile Optimizations")]
    public bool useBakedLighting = true;
    public float shadowDistance = 50f;
    public int shadowCascadeCount = 2;
    
    private float timeOfDay = 0.5f; // Start at noon
    private VolumeProfile skyProfile;
    
    void Start()
    {
        SetupLighting();
        ConfigureMobileRendering();

        // Subscribe to SaveSystem events
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnSave += SaveLightingData;
            SaveSystem.Instance.OnLoad += LoadLightingData;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe
        if (SaveSystem.Instance != null)
        {
            SaveSystem.Instance.OnSave -= SaveLightingData;
            SaveSystem.Instance.OnLoad -= LoadLightingData;
        }
    }
    
    void SetupLighting()
    {
        if (sunLight == null)
        {
            GameObject sun = new GameObject("SunLight");
            sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
        
        if (moonLight == null)
        {
            GameObject moon = new GameObject("MoonLight");
            moonLight = moon.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.6f, 0.7f, 0.9f);
            moonLight.intensity = 0.3f;
            moonLight.enabled = false;
        }
        
        if (skyVolume != null)
        {
            skyProfile = skyVolume.profile;
        }
    }
    
    void ConfigureMobileRendering()
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            // Mobile-optimized shadow settings
            urpAsset.shadowDistance = shadowDistance;
            urpAsset.shadowCascadeCount = shadowCascadeCount;
            urpAsset.supportsDynamicBatching = true;
            urpAsset.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled; // Prevent GPU crashes on Mali
            
            // Pixel art compatibility
            urpAsset.renderScale = 1f;
            urpAsset.upscalingFilter = UpscalingFilterSelection.Linear;
        }
    }
    
    void Update()
    {
        UpdateDayNightCycle();
        UpdateSunPosition();
        UpdateLightingParameters();
    }
    
    void UpdateDayNightCycle()
    {
        timeOfDay += Time.deltaTime / dayDuration;
        if (timeOfDay > 1f) timeOfDay = 0f;
    }
    
    void UpdateSunPosition()
    {
        // Maldives latitude ~4°N, so sun is mostly overhead
        float sunAngle = timeOfDay * 360f;
        sunLight.transform.rotation = Quaternion.Euler(
            math.lerp(-10f, 190f, timeOfDay), // Low to high to low
            sunAngle,
            0f
        );
        
        // Enable/disable moon
        bool isNight = timeOfDay < 0.2f || timeOfDay > 0.8f;
        moonLight.enabled = isNight;
        sunLight.enabled = !isNight;
    }
    
    void UpdateLightingParameters()
    {
        // Sun intensity with tropical brightness
        float intensity = sunIntensityCurve.Evaluate(timeOfDay) * 1.5f;
        sunLight.intensity = intensity;
        
        // Sun color
        sunLight.color = sunColorGradient.Evaluate(timeOfDay);
        
        // Ambient light (mobile-optimized)
        RenderSettings.ambientIntensity = intensity * 0.5f;
        RenderSettings.ambientEquatorColor = Color.Lerp(nightColor, middayColor, intensity);
        RenderSettings.ambientGroundColor = Color.Lerp(nightColor, middayColor, intensity * 0.3f);
        
        // Skybox color (simple gradient for mobile)
        Camera.main?.clearFlags = CameraClearFlags.Skybox;
        RenderSettings.skybox?.SetColor("_Tint", Color.Lerp(nightColor, middayColor, intensity));
    }
    
    public void SetTimeOfDay(float time)
    {
        timeOfDay = Mathf.Clamp01(time);
    }
    
    public float GetCurrentTimeOfDay()
    {
        return timeOfDay;
    }

    // --- Save and Load Integration ---

    public void SaveLightingData(SaveData data)
    {
        data.timeOfDay = timeOfDay;
    }

    public void LoadLightingData(SaveData data)
    {
        SetTimeOfDay(data.timeOfDay);
        Debug.Log($"Loaded time of day: {data.timeOfDay}");
    }
}
