using UnityEngine;
using System.Collections.Generic;

public class BuildingSystem : MonoBehaviour
{
    [Header("Maldivian Architecture")]
    public GameObject[] maldivianHouses; // Traditional coral stone houses
    public GameObject[] modernBuildings; // Concrete structures
    public GameObject[] mosquePrefabs; // Islamic architecture
    public GameObject[] shopBuildings;
    public GameObject[] harborBuildings;
    
    [Header("Building Data")]
    public int totalBuildings = 70;
    public float minBuildingDistance = 10f;
    public float maxBuildingHeight = 15f; // Malé height restriction
    
    [Header("Cultural Rules")]
    public bool respectPrayerDirection = true; // Face Qibla
    public bool maintainIslandCharacter = true;
    
    private List<BuildingData> buildingDatabase = new List<BuildingData>();
    
    public struct BuildingData
    {
        public string id;
        public Vector3 position;
        public BuildingType type;
        public string dhivehiName;
        public bool isEnterable;
        public Rect bounds;
    }
    
    public enum BuildingType
    {
        Residential,
        Mosque,
        Shop,
        Harbor,
        Government,
        Resort
    }
    
    void Start()
    {
        GenerateBuildingsOnIsland();
    }
    
    void GenerateBuildingsOnIsland()
    {
        // Get island boundary
        Collider islandCollider = GetComponent<Collider>();
        if (islandCollider == null) return;
        
        Bounds islandBounds = islandCollider.bounds;
        
        for (int i = 0; i < totalBuildings; i++)
        {
            Vector3 randomPoint = GetValidBuildingPosition(islandBounds);
            if (randomPoint != Vector3.zero)
            {
                PlaceBuilding(randomPoint, i);
            }
        }
    }
    
    Vector3 GetValidBuildingPosition(Bounds bounds)
    {
        // Mobile-optimized: fewer attempts
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector3 point = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.max.y + 10f,
                Random.Range(bounds.min.z, bounds.max.z)
            );
            
            // Raycast down to find ground
            if (Physics.Raycast(point, Vector3.down, out RaycastHit hit, 20f, LayerMask.GetMask("Ground")))
            {
                // Check distance from existing buildings
                bool tooClose = false;
                foreach (var building in buildingDatabase)
                {
                    if (Vector3.Distance(hit.point, building.position) < minBuildingDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                if (!tooClose) return hit.point;
            }
        }
        return Vector3.zero;
    }
    
    void PlaceBuilding(Vector3 position, int index)
    {
        // Determine building type based on location and island type
        BuildingType type = DetermineBuildingType(position);
        GameObject prefab = GetBuildingPrefab(type);
        
        if (prefab != null)
        {
            GameObject building = Instantiate(prefab, position, CalculateBuildingRotation(type));
            
            // Apply random scale variation (within Maldivian architectural norms)
            float scale = Random.Range(0.8f, 1.2f);
            building.transform.localScale = new Vector3(scale, Random.Range(0.9f, 1.1f), scale);
            
            // Set building data
            BuildingData data = new BuildingData
            {
                id = $"BDG_{transform.name}_{index}",
                position = position,
                type = type,
                dhivehiName = GetDhivehiBuildingName(type, index),
                isEnterable = type != BuildingType.Residential || Random.value > 0.7f,
                bounds = new Rect(position.x - 5f, position.z - 5f, 10f, 10f)
            };
            
            building.AddComponent<BuildingDataHolder>().SetData(data);
            buildingDatabase.Add(data);
            
            // Set appropriate layer for occlusion culling
            building.layer = LayerMask.NameToLayer("Building");
        }
    }
    
    BuildingType DetermineBuildingType(Vector3 pos)
    {
        // Harbor buildings near water
        if (IsNearWater(pos, 20f)) return BuildingType.Harbor;
        
        // Mosques distributed evenly
        if (buildingDatabase.FindAll(b => b.type == BuildingType.Mosque).Count < 3)
            return BuildingType.Mosque;
        
        // Shops in clusters
        if (buildingDatabase.FindAll(b => b.type == BuildingType.Shop).Count < 10)
            return BuildingType.Shop;
        
        // Default residential
        return BuildingType.Residential;
    }
    
    bool IsNearWater(Vector3 pos, float distance)
    {
        Collider[] hits = Physics.OverlapSphere(pos, distance, LayerMask.GetMask("Water"));
        return hits.Length > 0;
    }
    
    Quaternion CalculateBuildingRotation(BuildingType type)
    {
        if (type == BuildingType.Mosque && respectPrayerDirection)
        {
            // Face Qibla (approximate for Maldives: ~247° from North)
            return Quaternion.Euler(0f, 247f, 0f);
        }
        return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }
    
    GameObject GetBuildingPrefab(BuildingType type)
    {
        return type switch
        {
            BuildingType.Mosque => mosquePrefabs[Random.Range(0, mosquePrefabs.Length)],
            BuildingType.Shop => shopBuildings[Random.Range(0, shopBuildings.Length)],
            BuildingType.Harbor => harborBuildings[Random.Range(0, harborBuildings.Length)],
            BuildingType.Residential => maldivianHouses[Random.Range(0, maldivianHouses.Length)],
            _ => modernBuildings[Random.Range(0, modernBuildings.Length)]
        };
    }
    
    string GetDhivehiBuildingName(BuildingType type, int index)
    {
        return type switch
        {
            BuildingType.Mosque => $"މިސްކިތް {index + 1}",
            BuildingType.Shop => $"ހިދުމަތް {index + 1}",
            BuildingType.Harbor => "ބަނދޯން",
            _ => $"ގެ {index + 1}"
        };
    }
}
