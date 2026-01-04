using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// CharacterGenerator is a simplified system for creating varied character appearances.
/// Instead of a full procedural generation kernel, it combines pre-made character parts
/// (heads, torsos, legs) to generate a new character prefab.
/// </summary>
public class CharacterGenerator : MonoBehaviour
{
    [Header("Character Parts")]
    [SerializeField] private List<Sprite> headSprites;
    [SerializeField] private List<Sprite> torsoSprites;
    [SerializeField] private List<Sprite> legSprites;

    [Header("Prefab Template")]
    [SerializeField] private GameObject characterPrefabTemplate;

    /// <summary>
    /// Generates a new character GameObject with a randomized appearance.
    /// </summary>
    /// <param name="position">The position where the new character should be spawned.</param>
    /// <returns>The newly created character GameObject.</returns>
    public GameObject GenerateCharacter(Vector3 position)
    {
        if (characterPrefabTemplate == null)
        {
            Debug.LogError("Character prefab template is not assigned.");
            return null;
        }

        GameObject newCharacter = Instantiate(characterPrefabTemplate, position, Quaternion.identity);

        // Find the sprite renderer components for each body part on the prefab.
        // This assumes the prefab has a specific structure with named child objects.
        SpriteRenderer headRenderer = newCharacter.transform.Find("Head")?.GetComponent<SpriteRenderer>();
        SpriteRenderer torsoRenderer = newCharacter.transform.Find("Torso")?.GetComponent<SpriteRenderer>();
        SpriteRenderer legsRenderer = newCharacter.transform.Find("Legs")?.GetComponent<SpriteRenderer>();

        // Assign random sprites to each part.
        if (headRenderer != null && headSprites.Count > 0)
        {
            headRenderer.sprite = headSprites[Random.Range(0, headSprites.Count)];
        }
        if (torsoRenderer != null && torsoSprites.Count > 0)
        {
            torsoRenderer.sprite = torsoSprites[Random.Range(0, torsoSprites.Count)];
        }
        if (legsRenderer != null && legSprites.Count > 0)
        {
            legsRenderer.sprite = legSprites[Random.Range(0, legSprites.Count)];
        }

        return newCharacter;
    }
}
