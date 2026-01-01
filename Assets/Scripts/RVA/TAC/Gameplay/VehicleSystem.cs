using UnityEngine;
using System.Collections.Generic;

// ScriptableObject to define the static data for a vehicle.
[CreateAssetMenu(fileName = "New Vehicle", menuName = "RVA/Vehicle")]
public class VehicleData : ScriptableObject
{
    [Header("Vehicle Information")]
    public string vehicleName;
    public int vehicleID;
    public GameObject vehiclePrefab; // The prefab to instantiate.

    [Header("Performance Attributes")]
    public float topSpeed = 120f; // in km/h
    public float acceleration = 8f;
    public float handling = 0.8f;
    public float braking = 1.2f;
    public float health = 500f;
}

// The main VehicleSystem that manages all vehicles in the game.
public class VehicleSystem : MonoBehaviour
{
    public static VehicleSystem Instance { get; private set; }

    [Header("Vehicle Database")]
    public List<VehicleData> allVehicleData; // All possible vehicles loaded here.

    private Dictionary<int, VehicleData> vehicleDatabase = new Dictionary<int, VehicleData>();
    private List<GameObject> activeVehicles = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            InitializeVehicleDatabase();
        }
    }

    private void InitializeVehicleDatabase()
    {
        foreach (var data in allVehicleData)
        {
            if (!vehicleDatabase.ContainsKey(data.vehicleID))
            {
                vehicleDatabase.Add(data.vehicleID, data);
            }
            else
            {
                Debug.LogWarning($"Duplicate Vehicle ID found: {data.vehicleID}. Skipping.");
            }
        }
    }

    public VehicleData GetVehicleData(int id)
    {
        vehicleDatabase.TryGetValue(id, out VehicleData data);
        return data;
    }

    public GameObject SpawnVehicle(int id, Vector3 position, Quaternion rotation)
    {
        VehicleData data = GetVehicleData(id);
        if (data == null || data.vehiclePrefab == null)
        {
            Debug.LogError($"Vehicle with ID {id} not found or has no prefab in the database.");
            return null;
        }

        GameObject vehicleGO = Instantiate(data.vehiclePrefab, position, rotation);
        vehicleGO.name = data.vehicleName;

        // You might add a component to the vehicle to reference its data, similar to the CharacterSystem
        VehicleComponent vehComponent = vehicleGO.AddComponent<VehicleComponent>();
        vehComponent.vehicleData = data;

        activeVehicles.Add(vehicleGO);

        return vehicleGO;
    }

    public void DespawnVehicle(GameObject vehicleGO)
    {
        if (activeVehicles.Contains(vehicleGO))
        {
            activeVehicles.Remove(vehicleGO);
            Destroy(vehicleGO);
        }
    }
}

// A component to attach to vehicle GameObjects in the scene.
public class VehicleComponent : MonoBehaviour
{
    public VehicleData vehicleData;
}
