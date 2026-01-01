using UnityEngine;
using System.Collections.Generic;

// A ScriptableObject to define the static data for a character.
// This allows for easy creation and management of character assets in the Unity Editor.
[CreateAssetMenu(fileName = "New Character", menuName = "RVA/Character")]
public class CharacterData : ScriptableObject
{
    [Header("Basic Information")]
    public string characterName;
    public int characterID;
    [TextArea(5, 10)]
    public string backstory;

    [Header("In-Game Attributes")]
    public float maxHealth = 100f;
    public float movementSpeed = 5f;
    public int initialGangAffiliationID; // ID of the gang they belong to, if any.

    [Header("Dialogue & Voice")]
    public AudioClip[] voiceLines;
    // Potentially a reference to a dialogue tree asset
}

// The main CharacterSystem that manages all characters in the game.
public class CharacterSystem : MonoBehaviour
{
    public static CharacterSystem Instance { get; private set; }

    [Header("Character Database")]
    public List<CharacterData> allCharacterData; // All possible characters loaded here.

    private Dictionary<int, CharacterData> characterDatabase = new Dictionary<int, CharacterData>();
    private Dictionary<GameObject, CharacterData> activeCharacters = new Dictionary<GameObject, CharacterData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            InitializeCharacterDatabase();
        }
    }

    // Loads all CharacterData assets into a dictionary for quick access.
    private void InitializeCharacterDatabase()
    {
        foreach (var data in allCharacterData)
        {
            if (!characterDatabase.ContainsKey(data.characterID))
            {
                characterDatabase.Add(data.characterID, data);
            }
            else
            {
                Debug.LogWarning($"Duplicate Character ID found: {data.characterID}. Skipping.");
            }
        }
    }

    // Retrieves character data by its unique ID.
    public CharacterData GetCharacterData(int id)
    {
        characterDatabase.TryGetValue(id, out CharacterData data);
        return data;
    }

    // Spawns a character into the world.
    public GameObject SpawnCharacter(int id, Vector3 position, Quaternion rotation)
    {
        CharacterData data = GetCharacterData(id);
        if (data == null)
        {
            Debug.LogError($"Character with ID {id} not found in the database.");
            return null;
        }

        // For now, we'll just create a primitive. In the full game, this would instantiate a character prefab.
        GameObject characterGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        characterGO.name = data.characterName;
        characterGO.transform.position = position;
        characterGO.transform.rotation = rotation;

        // Add a component to the GameObject to hold its data reference
        CharacterComponent charComponent = characterGO.AddComponent<CharacterComponent>();
        charComponent.characterData = data;

        activeCharacters.Add(characterGO, data);

        return characterGO;
    }

    // Removes a character from the world.
    public void DespawnCharacter(GameObject characterGO)
    {
        if (activeCharacters.ContainsKey(characterGO))
        {
            activeCharacters.Remove(characterGO);
            Destroy(characterGO);
        }
    }
}

// A component to attach to character GameObjects in the scene.
// This holds a reference to their ScriptableObject data.
public class CharacterComponent : MonoBehaviour
{
    public CharacterData characterData;
}
