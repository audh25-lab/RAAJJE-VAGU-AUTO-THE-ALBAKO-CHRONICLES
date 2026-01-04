using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// BuildingGenerator is a simplified system for creating varied building appearances.
/// It uses a modular, "kit-bashing" approach to stack pre-made building sections
/// (like floors and roofs) to construct buildings of different heights and styles.
/// </summary>
public class BuildingGenerator : MonoBehaviour
{
    [System.Serializable]
    public class BuildingKit
    {
        public string styleName;
        public GameObject groundFloorPrefab;
        public List<GameObject> middleFloorPrefabs;
        public GameObject roofPrefab;
        public float floorHeight = 3.0f;
    }

    [Header("Building Kits")]
    [SerializeField] private List<BuildingKit> buildingKits;

    /// <summary>
    /// Generates a new building prefab by stacking modules from a chosen kit.
    /// </summary>
    /// <param name="styleName">The name of the building style to use.</param>
    /// <param name="numberOfFloors">The total number of floors for the building.</param>
    /// <param name="position">The position to spawn the new building at.</param>
    /// <returns>The root GameObject of the newly created building.</returns>
    public GameObject GenerateBuilding(string styleName, int numberOfFloors, Vector3 position)
    {
        BuildingKit kit = buildingKits.Find(k => k.styleName == styleName);
        if (kit == null)
        {
            Debug.LogError($"Building kit for style '{styleName}' not found.");
            return null;
        }

        // Ensure the building has at least a ground floor and a roof
        numberOfFloors = Mathf.Max(2, numberOfFloors);

        GameObject buildingRoot = new GameObject($"Building_{styleName}_{numberOfFloors}Floors");
        buildingRoot.transform.position = position;

        // --- 1. Instantiate the Ground Floor ---
        if (kit.groundFloorPrefab != null)
        {
            Instantiate(kit.groundFloorPrefab, buildingRoot.transform);
        }

        // --- 2. Instantiate Middle Floors ---
        for (int i = 1; i < numberOfFloors - 1; i++)
        {
            if (kit.middleFloorPrefabs.Count > 0)
            {
                GameObject floorPrefab = kit.middleFloorPrefabs[Random.Range(0, kit.middleFloorPrefabs.Count)];
                Vector3 floorPosition = new Vector3(0, i * kit.floorHeight, 0);
                Instantiate(floorPrefab, buildingRoot.transform.position + floorPosition, Quaternion.identity, buildingRoot.transform);
            }
        }

        // --- 3. Instantiate the Roof ---
        if (kit.roofPrefab != null)
        {
            Vector3 roofPosition = new Vector3(0, (numberOfFloors - 1) * kit.floorHeight, 0);
            Instantiate(kit.roofPrefab, buildingRoot.transform.position + roofPosition, Quaternion.identity, buildingRoot.transform);
        }

        return buildingRoot;
    }
}
