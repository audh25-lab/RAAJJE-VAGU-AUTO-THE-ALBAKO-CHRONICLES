using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ShadowSystem : MonoBehaviour
{
    [Header("Mobile Shadow Optimizations")]
    public ShadowResolution shadowResolution = ShadowResolution._1024;
    public float shadowDistance = 50f;
    public float cascadeSplit1 = 0.2f;
    public float cascadeSplit2 = 0.5f;
    public bool softShadows = true;
    
    [Header("Pixel Art Shadow Style")]
    public bool hardPixelShadows = true;
    public float shadowSharpness = 0.8f;
    
    [Header("Performance")]
    public bool cullDynamicShadows = true;
    public int maxShadowCasters = 50; // Limit for mobile
    
    private UniversalRenderPipelineAsset urpAsset;
    private List<Renderer> shadowCasters = new List<Renderer>();
    
    void Start()
    {
        urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        ConfigureShadows();
        TagShadowCasters();
    }
    
    void ConfigureShadows()
    {
        if (urpAsset == null) return;
        
        // Mobile-optimized shadow settings
        urpAsset.shadowDistance = shadowDistance;
        urpAsset.shadowCascadeCount = 2;
        urpAsset.shadowCascadeSplit = new Vector3(cascadeSplit1, cascadeSplit2, 0f);
        urpAsset.supportsSoftShadows = softShadows;
        
        // Shadow resolution based on device tier
        if (SystemInfo.graphicsMemorySize < 2048)
        {
            urpAsset.renderScale = 0.9f; // Slight downscale for low-end
        }
    }
    
    void TagShadowCasters()
    {
        // Find all objects that should cast shadows
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        
        foreach (var renderer in allRenderers)
        {
            // Mobile optimization: only important objects cast shadows
            bool shouldCastShadow = ShouldCastShadows(renderer);
            
            if (shouldCastShadow)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                shadowCasters.Add(renderer);
            }
            else
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            
            // Enable GPU instancing for shadow casters
            if (shouldCastShadow && renderer.sharedMaterial != null)
            {
                renderer.sharedMaterial.enableInstancing = true;
            }
        }
        
        // Limit shadow casters for performance
        if (shadowCasters.Count > maxShadowCasters)
        {
            // Disable shadows on farthest objects
            shadowCasters.Sort((a, b) => 
                Vector3.Distance(Camera.main.transform.position, a.transform.position)
                    .CompareTo(Vector3.Distance(Camera.main.transform.position, b.transform.position))
            );
            
            for (int i = maxShadowCasters; i < shadowCasters.Count; i++)
            {
                shadowCasters[i].shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
    
    bool ShouldCastShadows(Renderer renderer)
    {
        // Only important objects cast shadows on mobile
        string tag = renderer.gameObject.tag;
        return tag switch
        {
            "Player" => true,
            "Vehicle" => true,
            "Building" => true,
            "LargeFlora" => true,
            _ => false
        };
    }
    
    void Update()
    {
        // Dynamic shadow culling based on distance
        if (cullDynamicShadows && Time.frameCount % 30 == 0) // Check every 30 frames
        {
            UpdateShadowCulling();
        }
    }
    
    void UpdateShadowCulling()
    {
        if (Camera.main == null) return;
        
        Vector3 cameraPos = Camera.main.transform.position;
        
        foreach (var caster in shadowCasters)
        {
            if (caster == null) continue;
            
            float distance = Vector3.Distance(cameraPos, caster.transform.position);
            
            // Only cast shadows if within shadow distance and important
            if (distance > shadowDistance || !IsShadowCasterImportant(caster))
            {
                caster.shadowCastingMode = ShadowCastingMode.Off;
            }
            else
            {
                caster.shadowCastingMode = ShadowCastingMode.On;
            }
        }
    }
    
    bool IsShadowCasterImportant(Renderer caster)
    {
        // Player, vehicles, and large buildings always important
        string tag = caster.gameObject.tag;
        return tag == "Player" || tag == "Vehicle" || 
               (tag == "Building" && caster.bounds.size.magnitude > 5f);
    }
    
    public void SetShadowQuality(ShadowQuality quality)
    {
        if (urpAsset == null) return;
        
        switch (quality)
        {
            case ShadowQuality.Low:
                urpAsset.shadowCascadeCount = 1;
                urpAsset.shadowDistance = 30f;
                break;
            case ShadowQuality.Medium:
                urpAsset.shadowCascadeCount = 2;
                urpAsset.shadowDistance = 50f;
                break;
            case ShadowQuality.High:
                urpAsset.shadowCascadeCount = 3;
                urpAsset.shadowDistance = 80f;
                break;
        }
    }
    
    public enum ShadowQuality
    {
        Low,
        Medium,
        High
    }
}
