using UnityEngine;

/// <summary>
/// UnityIntegrationSystem acts as the bridge between the procedural generation kernel
/// and the Unity engine. It takes the output from the various generator scripts
/// and handles the creation of GameObjects and Prefabs.
/// </summary>
public class UnityIntegrationSystem : MonoBehaviour
{
    [Header("Generator References")]
    [SerializeField] private CharacterGenerator characterGenerator;
    [SerializeField] private VehicleGenerator vehicleGenerator;
    [SerializeField] private BuildingGenerator buildingGenerator;

    /// <summary>
    /// Generates a character and returns it as a GameObject.
    /// The original method saved a prefab, which is an Editor-only feature.
    /// This version is safe for runtime instantiation.
    /// </summary>
    public GameObject GenerateCharacter(Vector3 position)
    {
        if (characterGenerator == null)
        {
            Debug.LogError("CharacterGenerator is not assigned.");
            return null;
        }

        return characterGenerator.GenerateCharacter(position);
    }

    /// <summary>
    /// Generates a building and instantiates it directly into the scene.
    /// </summary>
    public void GenerateAndPlaceBuilding(string style, int floors, Vector3 position)
    {
        if (buildingGenerator == null)
        {
            Debug.LogError("BuildingGenerator is not assigned.");
            return;
        }

        buildingGenerator.GenerateBuilding(style, floors, position);
    }
}
