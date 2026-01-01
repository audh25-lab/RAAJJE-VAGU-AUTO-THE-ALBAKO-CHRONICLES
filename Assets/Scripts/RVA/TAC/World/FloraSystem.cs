using UnityEngine;
using System.Collections.Generic;

public class FloraSystem : MonoBehaviour
{
    [Header("Maldivian Flora - 12 Types")]
    public GameObject[] coconutPalms;
    public GameObject[] breadfruitTrees;
    public GameObject[] tropicalFlowers;
    public GameObject[] shrubs;
    public GameObject[] seagrass;
    public GameObject[] mangroves;
    
    [Header("Distribution Settings")]
    public int floraDensity = 50; // Per island
    public float minFloraDistance = 5f;
    public AnimationCurve heightDistribution;
    
    [Header("Coconut Palm Priority")]
    public float coconutProbability = 0.4f; // Most common
    
    private List<FloraInstance> floraInstances = new List<FloraInstance>();
    
    public struct FloraInstance
    {
        public string species;
        public Vector3 position;
        public float health;
        public bool isHarvestable;
    }
    
    void Start()
    {
        DistributeFlora();
    }
    
    void DistributeFlora()
    {
        Collider islandCollider = GetComponent<Collider>();
        if (islandCollider == null) return;
        
        Bounds bounds = islandCollider.bounds;
        
        for (int i = 0; i < floraDensity; i++)
        {
            Vector3 position = GetValidFloraPosition(bounds);
            if (position != Vector3.zero)
            {
                PlaceFlora(position, i);
            }
        }
    }
    
    Vector3 GetValidFloraPosition(Bounds bounds)
    {
        for (int attempt = 0; attempt < 15; attempt++)
        {
            Vector3 point = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.max.y + 5f,
                Random.Range(bounds.min.z, bounds.max.z)
            );
            
            if (Physics.Raycast(point, Vector3.down, out RaycastHit hit, 10f, LayerMask.GetMask("Ground")))
            {
                // Check distance from existing flora
                bool tooClose = false;
                foreach (var flora in floraInstances)
                {
                    if (Vector3.Distance(hit.point, flora.position) < minFloraDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                // Don't place on buildings/paths
                if (!tooClose && IsValidTerrain(hit.point))
                {
                    return hit.point;
                }
            }
        }
        return Vector3.zero;
    }
    
    bool IsValidTerrain(Vector3 pos)
    {
        // Avoid beaches (too sandy), avoid building areas
        float height = pos.y;
        return height > 0.5f && height < 3f; // Typical island elevation
    }
    
    void PlaceFlora(Vector3 position, int index)
    {
        // Weighted random selection (coconut palms most common)
        float rand = Random.value;
        GameObject prefab;
        string speciesName;
        
        if (rand < coconutProbability)
        {
            prefab = coconutPalms[Random.Range(0, coconutPalms.Length)];
            speciesName = "Coconut Palm (ލާމޫ)";
        }
        else if (rand < 0.6f)
        {
            prefab = breadfruitTrees[Random.Range(0, breadfruitTrees.Length)];
            speciesName = "Breadfruit (ބަނބު)";
        }
        else if (rand < 0.75f)
        {
            prefab = tropicalFlowers[Random.Range(0, tropicalFlowers.Length)];
            speciesName = "Tropical Flower (ގުދަންތަ)";
        }
        else if (rand < 0.85f)
        {
            prefab = shrubs[Random.Range(0, shrubs.Length)];
            speciesName = "Coastal Shrub (ލޭން)";
        }
        else if (rand < 0.95f)
        {
            prefab = seagrass[Random.Range(0, seagrass.Length)];
            speciesName = "Seagrass (އުމާ)";
        }
        else
        {
            prefab = mangroves[Random.Range(0, mangroves.Length)];
            speciesName = "Mangrove (ކަނދުރި)";
        }
        
        if (prefab != null)
        {
            GameObject flora = Instantiate(prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            
            // Random scale variation
            float scale = Random.Range(0.8f, 1.3f);
            flora.transform.localScale *= scale;
            
            // Add wind sway (mobile-optimized)
            AddWindSway(flora);
            
            // Track instance
            FloraInstance instance = new FloraInstance
            {
                species = speciesName,
                position = position,
                health = Random.Range(0.7f, 1f),
                isHarvestable = speciesName.Contains("Coconut") || speciesName.Contains("Breadfruit")
            };
            
            floraInstances.Add(instance);
            flora.AddComponent<FloraDataHolder>().SetData(instance);
        }
    }
    
    void AddWindSway(GameObject flora)
    {
        // Add simple wind animation component (mobile-friendly)
        var sway = flora.AddComponent<WindSway>();
        sway.swayAmount = 0.1f;
        sway.swaySpeed = Random.Range(0.5f, 1.5f);
    }
    
    public FloraInstance[] GetHarvestableFlora()
    {
        return floraInstances.FindAll(f => f.isHarvestable && f.health > 0.5f).ToArray();
    }
}

public class WindSway : MonoBehaviour
{
    public float swayAmount = 0.1f;
    public float swaySpeed = 1f;
    private Quaternion originalRotation;
    
    void Start()
    {
        originalRotation = transform.rotation;
    }
    
    void Update()
    {
        float angle = Mathf.Sin(Time.time * swaySpeed) * swayAmount;
        transform.rotation = originalRotation * Quaternion.Euler(0f, 0f, angle);
    }
}
