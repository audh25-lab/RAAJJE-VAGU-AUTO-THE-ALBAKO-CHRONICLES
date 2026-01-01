using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class OceanSystem : MonoBehaviour
{
    [Header("Maldives Indian Ocean")]
    public float waveHeight = 1.5f;
    public float waveSpeed = 0.5f;
    public float waveScale = 0.1f;
    public Color shallowWaterColor = new Color(0.2f, 0.7f, 0.8f, 0.6f);
    public Color deepWaterColor = new Color(0.05f, 0.3f, 0.5f, 0.8f);
    
    [Header("Performance Settings")]
    public int vertexDensity = 64; // Reduced for mobile
    public float updateDistance = 100f; // Only update near player
    public bool useGPUInstancing = true;
    
    private Material oceanMaterial;
    private MeshRenderer oceanRenderer;
    private Transform playerTransform;
    
    void Start()
    {
        SetupOceanPlane();
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
    }
    
    void SetupOceanPlane()
    {
        GameObject ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ocean.name = "IndianOcean";
        ocean.transform.localScale = new Vector3(1000f, 1f, 1000f); // Covers entire world
        
        oceanRenderer = ocean.GetComponent<MeshRenderer>();
        oceanMaterial = new Material(Shader.Find("Mobile/OceanSimple"));
        
        // Configure material for mobile
        oceanMaterial.SetFloat("_WaveHeight", waveHeight);
        oceanMaterial.SetFloat("_WaveSpeed", waveSpeed);
        oceanMaterial.SetFloat("_WaveScale", waveScale);
        oceanMaterial.SetColor("_ShallowColor", shallowWaterColor);
        oceanMaterial.SetColor("_DeepColor", deepWaterColor);
        
        oceanRenderer.material = oceanMaterial;
        oceanRenderer.shadowCastingMode = ShadowCastingMode.Off;
        oceanRenderer.receiveShadows = false;
        
        // Enable GPU instancing for mobile performance
        if (useGPUInstancing)
        {
            oceanRenderer.material.enableInstancing = true;
        }
        
        // Set layer for water detection
        ocean.layer = LayerMask.NameToLayer("Water");
    }
    
    void Update()
    {
        // Mobile optimization: only animate when visible and near player
        if (playerTransform == null) return;
        
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if (distance < updateDistance)
        {
            // Animate waves
            float time = Time.time * waveSpeed;
            oceanMaterial.SetFloat("_TimeOffset", time);
            
            // Dynamic quality adjustment based on distance
            if (distance > updateDistance * 0.5f)
            {
                oceanRenderer.material.SetFloat("_DetailLevel", 0.5f);
            }
            else
            {
                oceanRenderer.material.SetFloat("_DetailLevel", 1f);
            }
        }
    }
    
    void OnDestroy()
    {
        if (oceanMaterial != null)
        {
            DestroyImmediate(oceanMaterial);
        }
    }
}
