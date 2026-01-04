using UnityEngine;

/// <summary>
/// FloraGenerator is a simplified system for creating variations of flora prefabs.
/// Instead of complex ecological simulations, it applies procedural modifications
/// like randomized scale, rotation, and color to a base prefab.
/// </summary>
public class FloraGenerator : MonoBehaviour
{
    [Header("Variation Parameters")]
    [Tooltip("The minimum and maximum scale to apply to the flora.")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.8f, 1.2f);
    [Tooltip("The range of hue shift to apply to the flora's color.")]
    [SerializeField] private float hueVariation = 0.1f;

    /// <summary>
    /// Generates a new flora GameObject with randomized properties.
    /// </summary>
    /// <param name="basePrefab">The base prefab for the flora (e.g., a palm tree model).</param>
    /// <param name="position">The position to spawn the new flora at.</param>
    /// <returns>The newly created and customized flora GameObject.</returns>
    public GameObject GenerateFlora(GameObject basePrefab, Vector3 position)
    {
        if (basePrefab == null)
        {
            Debug.LogError("Base flora prefab is not assigned.");
            return null;
        }

        // --- Instantiate the Prefab ---
        GameObject newFlora = Instantiate(basePrefab, position, Quaternion.identity);

        // --- Apply Random Rotation ---
        newFlora.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

        // --- Apply Random Scale ---
        float scale = Random.Range(scaleRange.x, scaleRange.y);
        newFlora.transform.localScale = Vector3.one * scale;

        // --- Apply Color Variation ---
        // This assumes the prefab has a SpriteRenderer or MeshRenderer to modify.
        Renderer renderer = newFlora.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propBlock);

            // Get the original color and shift its hue
            Color baseColor = renderer.material.color;
            float h, s, v;
            Color.RGBToHSV(baseColor, out h, out s, out v);
            h += Random.Range(-hueVariation, hueVariation);
            h = Mathf.Repeat(h, 1.0f); // Wrap hue value

            propBlock.SetColor("_Color", Color.HSVToRGB(h, s, v));
            renderer.SetPropertyBlock(propBlock);
        }

        return newFlora;
    }
}
