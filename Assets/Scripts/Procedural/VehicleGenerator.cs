using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// VehicleGenerator is a simplified system for creating varied vehicle appearances.
/// It customizes base vehicle prefabs by applying random colors and attaching
/// simple cosmetic props like spoilers or decals.
/// </summary>
public class VehicleGenerator : MonoBehaviour
{
    [Header("Customization Options")]
    [Tooltip("A list of possible colors to apply to the vehicle's body.")]
    [SerializeField] private List<Color> vehicleColors;
    [Tooltip("A list of optional cosmetic attachments (e.g., spoilers, roof racks).")]
    [SerializeField] private List<GameObject> cosmeticAttachments;

    /// <summary>
    /// Generates a customized vehicle from a base prefab.
    /// </summary>
    /// <param name="basePrefab">The base vehicle prefab to customize.</param>
    /// <param name="position">The position to spawn the new vehicle at.</param>
    /// <returns>The newly created and customized vehicle GameObject.</returns>
    public GameObject GenerateVehicle(GameObject basePrefab, Vector3 position)
    {
        if (basePrefab == null)
        {
            Debug.LogError("Base vehicle prefab is not assigned.");
            return null;
        }

        GameObject newVehicle = Instantiate(basePrefab, position, Quaternion.identity);

        // --- Apply a Random Color ---
        if (vehicleColors != null && vehicleColors.Count > 0)
        {
            // This assumes the prefab has a 'Body' child with a SpriteRenderer to color.
            SpriteRenderer bodyRenderer = newVehicle.transform.Find("Body")?.GetComponent<SpriteRenderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.color = vehicleColors[Random.Range(0, vehicleColors.Count)];
            }
        }

        // --- Add a Random Cosmetic Attachment ---
        if (cosmeticAttachments != null && cosmeticAttachments.Count > 0)
        {
            // 30% chance to add a cosmetic item
            if (Random.value < 0.3f)
            {
                GameObject attachmentPrefab = cosmeticAttachments[Random.Range(0, cosmeticAttachments.Count)];
                // This assumes the prefab has an 'AttachmentPoint' transform to parent the cosmetic to.
                Transform attachmentPoint = newVehicle.transform.Find("AttachmentPoint");
                if (attachmentPoint != null)
                {
                    Instantiate(attachmentPrefab, attachmentPoint.position, attachmentPoint.rotation, newVehicle.transform);
                }
            }
        }

        return newVehicle;
    }
}
