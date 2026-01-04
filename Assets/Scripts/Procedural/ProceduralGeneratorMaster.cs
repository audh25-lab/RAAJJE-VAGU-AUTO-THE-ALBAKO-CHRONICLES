using UnityEngine;

/// <summary>
/// ProceduralGeneratorMaster is the high-level coordinator for the entire procedural
/// generation kernel. It serves as a simple interface to run all the individual
/// generator scripts in the correct sequence.
/// </summary>
public class ProceduralGeneratorMaster : MonoBehaviour
{
    [Header("Generator Modules")]
    [SerializeField] private CharacterGenerator characterGenerator;
    [SerializeField] private VehicleGenerator vehicleGenerator;
    [SerializeField] private BuildingGenerator buildingGenerator;
    [SerializeField] private FloraGenerator floraGenerator;
    [SerializeField] private UnityIntegrationSystem integrationSystem;

    [Header("Generation Parameters")]
    [SerializeField] private int numberOfCharactersToGenerate = 50;
    [SerializeField] private int numberOfBuildingsToGenerate = 20;

    /// <summary>
    /// Executes the entire procedural generation process. This can be triggered
    /// from a UI button in a debug menu or called at a specific point in the game.
    /// </summary>
    public void GenerateAllAssets()
    {
        Debug.Log("--- STARTING PROCEDURAL GENERATION KERNEL ---");

        // --- Generate Characters ---
        if (integrationSystem != null && characterGenerator != null)
        {
            Debug.Log($"Generating {numberOfCharactersToGenerate} characters...");
            for (int i = 0; i < numberOfCharactersToGenerate; i++)
            {
                integrationSystem.GenerateAndSaveCharacterPrefab();
            }
        }

        // --- Generate Buildings ---
        if (integrationSystem != null && buildingGenerator != null)
        {
            Debug.Log($"Generating {numberOfBuildingsToGenerate} buildings...");
            for (int i = 0; i < numberOfBuildingsToGenerate; i++)
            {
                // Example: Generate buildings of random heights in a random style
                int floors = Random.Range(3, 10);
                // In a real game, position would be determined by a city layout algorithm
                Vector3 position = new Vector3(i * 10, 0, 0);
                integrationSystem.GenerateAndPlaceBuilding("UrbanStyle1", floors, position);
            }
        }

        // Other generation steps (vehicles, flora, etc.) would be called here.

        Debug.Log("--- PROCEDURAL GENERATION COMPLETE ---");
    }
}
