using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// CulturalValidationEngine is a simplified system for ensuring that procedurally
/// generated content adheres to the game's cultural and religious guidelines. It acts
/// as a "cultural linter" by checking generated assets against a predefined set of rules.
/// </summary>
public class CulturalValidationEngine : MonoBehaviour
{
    public static CulturalValidationEngine Instance;

    // In a real system, these rules would be loaded from an external data file.
    private readonly List<string> validLocationPrefixes = new List<string> { "Masjid", "Hukuru", "Fannu" };
    private readonly List<string> invalidItemNames = new List<string> { "Pork", "Alcohol", "Idol" };

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Validates a procedurally generated asset based on its name and category.
    /// </summary>
    /// <param name="assetName">The name of the generated asset (e.g., "Pork Sandwich", "Masjid Al-Bahar").</param>
    /// <param name="assetCategory">The category of the asset (e.g., "FoodItem", "BuildingName").</param>
    /// <returns>True if the asset passes validation, otherwise false.</returns>
    public bool ValidateAsset(string assetName, string assetCategory)
    {
        bool isValid = true;

        switch (assetCategory)
        {
            case "FoodItem":
                // Check if the item name contains any invalid terms.
                foreach (var invalidName in invalidItemNames)
                {
                    if (assetName.ToLower().Contains(invalidName.ToLower()))
                    {
                        Debug.LogWarning($"Cultural Validation Failed: Food item '{assetName}' contains a forbidden term ('{invalidName}').");
                        isValid = false;
                        break;
                    }
                }
                break;

            case "BuildingName":
                // Example: Ensure religious building names are appropriate.
                if (assetName.StartsWith("Masjid"))
                {
                    // Further checks could go here.
                }
                break;
        }

        if(isValid)
        {
            Debug.Log($"Cultural Validation Passed for: {assetName}");
        }

        return isValid;
    }
}
