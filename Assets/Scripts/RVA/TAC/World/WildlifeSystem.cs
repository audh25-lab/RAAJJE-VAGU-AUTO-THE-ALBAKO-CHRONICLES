using UnityEngine;
using System.Collections.Generic;

public class WildlifeSystem : MonoBehaviour
{
    [Header("Maldivian Wildlife")]
    public GameObject[] reefFish; // Tropical fish schools
    public GameObject[] seaTurtles;
    public GameObject[] dolphins;
    public GameObject[] seaBirds; // Terns, gulls
    public GameObject[] crabs;
    public GameObject[] fruitBats;
    
    [Header("Spawn Settings")]
    public int maxSeaCreatures = 30;
    public int maxLandCreatures = 10;
    public float spawnRadius = 100f;
    public float despawnDistance = 150f;
    
    [Header("Behavior")]
    public float schoolMovementSpeed = 2f;
    public float wildlifeAwarenessDistance = 5f;
    
    private List<GameObject> activeWildlife = new List<GameObject>();
    private Transform playerTransform;
    
    public struct WildlifeInstance
    {
        public string species;
        public Vector3 spawnPoint;
        public float spawnTime;
        public bool isProtected; // For conservation awareness
    }
    
    void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        InvokeRepeating(nameof(ManageWildlifeSpawning), 1f, 3f); // Mobile-optimized
    }
    
    void ManageWildlifeSpawning()
    {
        if (playerTransform == null) return;
        
        // Spawn sea creatures in water around player
        SpawnSeaLife();
        
        // Spawn land creatures on islands
        SpawnLandLife();
        
        // Despawn distant creatures
        DespawnDistantWildlife();
    }
    
    void SpawnSeaLife()
    {
        int currentSeaCount = activeWildlife.FindAll(w => w.GetComponent<WildlifeDataHolder>()?.data.species.Contains("Fish") == true).Count;
        
        for (int i = currentSeaCount; i < maxSeaCreatures; i++)
        {
            Vector3 spawnPos = GetRandomOceanPosition();
            GameObject wildlife = SpawnWildlife(spawnPos, GetRandomSeaCreature());
            
            if (wildlife != null)
            {
                SetupSeaCreatureBehavior(wildlife);
            }
        }
    }
    
    void SpawnLandLife()
    {
        int currentLandCount = activeWildlife.FindAll(w => 
            w.GetComponent<WildlifeDataHolder>()?.data.species.Contains("Crab") == true ||
            w.GetComponent<WildlifeDataHolder>()?.data.species.Contains("Bat") == true
        ).Count;
        
        for (int i = currentLandCount; i < maxLandCreatures; i++)
        {
            Vector3 spawnPos = GetRandomIslandPosition();
            GameObject wildlife = SpawnWildlife(spawnPos, GetRandomLandCreature());
            
            if (wildlife != null)
            {
                SetupLandCreatureBehavior(wildlife);
            }
        }
    }
    
    Vector3 GetRandomOceanPosition()
    {
        Vector2 circle = UnityEngine.Random.insideUnitCircle * spawnRadius;
        Vector3 pos = playerTransform.position + new Vector3(circle.x, 0f, circle.y);
        pos.y = -2f; // Sea level
        
        return pos;
    }
    
    Vector3 GetRandomIslandPosition()
    {
        // Find nearby islands
        Collider[] islandColliders = Physics.OverlapSphere(playerTransform.position, spawnRadius, LayerMask.GetMask("Island"));
        
        if (islandColliders.Length > 0)
        {
            Collider island = islandColliders[UnityEngine.Random.Range(0, islandColliders.Length)];
            Vector3 randomPoint = island.bounds.center + new Vector3(
                UnityEngine.Random.Range(-20f, 20f),
                0f,
                UnityEngine.Random.Range(-20f, 20f)
            );
            
            return randomPoint;
        }
        
        return Vector3.zero;
    }
    
    GameObject SpawnWildlife(Vector3 position, GameObject prefab)
    {
        if (prefab != null && position != Vector3.zero)
        {
            GameObject wildlife = Instantiate(prefab, position, Quaternion.identity);
            activeWildlife.Add(wildlife);
            
            WildlifeInstance data = new WildlifeInstance
            {
                species = prefab.name,
                spawnPoint = position,
                spawnTime = Time.time,
                isProtected = prefab.name.Contains("Turtle") || prefab.name.Contains("Dolphin")
            };
            
            wildlife.AddComponent<WildlifeDataHolder>().SetData(data);
            return wildlife;
        }
        return null;
    }
    
    void SetupSeaCreatureBehavior(GameObject creature)
    {
        // Add simple schooling behavior (mobile-optimized)
        var schoolBehavior = creature.AddComponent<SchoolBehavior>();
        schoolBehavior.schoolCenter = creature.transform.position;
        schoolBehavior.schoolRadius = 10f;
        schoolBehavior.movementSpeed = schoolMovementSpeed;
    }
    
    void SetupLandCreatureBehavior(GameObject creature)
    {
        // Add simple wander behavior
        var wander = creature.AddComponent<WanderBehavior>();
        wander.wanderRadius = 15f;
        wander.wanderSpeed = 1f;
    }
    
    void DespawnDistantWildlife()
    {
        for (int i = activeWildlife.Count - 1; i >= 0; i--)
        {
            if (activeWildlife[i] == null)
            {
                activeWildlife.RemoveAt(i);
                continue;
            }
            
            float distance = Vector3.Distance(playerTransform.position, activeWildlife[i].transform.position);
            if (distance > despawnDistance)
            {
                Destroy(activeWildlife[i]);
                activeWildlife.RemoveAt(i);
            }
        }
    }
    
    GameObject GetRandomSeaCreature()
    {
        float rand = UnityEngine.Random.value;
        if (rand < 0.6f) return reefFish[UnityEngine.Random.Range(0, reefFish.Length)];
        else if (rand < 0.8f) return seaTurtles[UnityEngine.Random.Range(0, seaTurtles.Length)];
        else return dolphins[UnityEngine.Random.Range(0, dolphins.Length)];
    }
    
    GameObject GetRandomLandCreature()
    {
        float rand = UnityEngine.Random.value;
        if (rand < 0.5f) return crabs[UnityEngine.Random.Range(0, crabs.Length)];
        else return fruitBats[UnityEngine.Random.Range(0, fruitBats.Length)];
    }
}

public class SchoolBehavior : MonoBehaviour
{
    public Vector3 schoolCenter;
    public float schoolRadius = 10f;
    public float movementSpeed = 2f;
    private Vector3 targetPosition;
    
    void Start()
    {
        targetPosition = GetRandomSchoolPosition();
    }
    
    void Update()
    {
        // Move towards target
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, movementSpeed * Time.deltaTime);
        
        // Update target if reached
        if (Vector3.Distance(transform.position, targetPosition) < 1f)
        {
            targetPosition = GetRandomSchoolPosition();
        }
    }
    
    Vector3 GetRandomSchoolPosition()
    {
        return schoolCenter + UnityEngine.Random.insideUnitSphere * schoolRadius;
    }
}

public class WanderBehavior : MonoBehaviour
{
    public float wanderRadius = 15f;
    public float wanderSpeed = 1f;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    
    void Start()
    {
        startPosition = transform.position;
        targetPosition = GetWanderPosition();
    }
    
    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, wanderSpeed * Time.deltaTime);
        
        if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
        {
            targetPosition = GetWanderPosition();
        }
    }
    
    Vector3 GetWanderPosition()
    {
        Vector2 circle = UnityEngine.Random.insideUnitCircle * wanderRadius;
        return startPosition + new Vector3(circle.x, 0f, circle.y);
    }
}
