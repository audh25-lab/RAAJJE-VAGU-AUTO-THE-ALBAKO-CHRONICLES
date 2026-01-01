using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

// A container for all data that needs to be saved.
[System.Serializable]
public class SaveData
{
    // --- Player Data ---
    public Vector3 playerPosition;
    public float playerHealth;
    public float playerCurrency;

    // --- World State ---
    public int gameDay;
    public int gameMonth;
    public int gameYear;
    public float timeOfDay;

    // --- Mission Progress ---
    public int activeMissionID;
    public int currentObjectiveIndex;
    public List<int> completedMissionIDs;

    public SaveData()
    {
        completedMissionIDs = new List<int>();
    }
}

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    // These events allow other systems to register their data to be saved or loaded.
    public event Action<SaveData> OnSave;
    public event Action<SaveData> OnLoad;

    private string saveFilePath;
    private const string saveFileName = "raajje_vagu_save.json";

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        saveFilePath = Path.Combine(Application.persistentDataPath, saveFileName);
    }

    private void Update()
    {
        // Example of how to trigger save/load with keyboard shortcuts.
        if (Input.GetKeyDown(KeyCode.F5))
        {
            SaveGame();
        }
        if (Input.GetKeyDown(KeyCode.F9))
        {
            LoadGame();
        }
    }

    public void SaveGame()
    {
        Debug.Log("Saving game...");
        SaveData data = new SaveData();

        // Fire the OnSave event, allowing other systems to populate the SaveData object.
        OnSave?.Invoke(data);

        string json = JsonUtility.ToJson(data, true); // `true` for pretty print
        File.WriteAllText(saveFilePath, json);

        Debug.Log($"Game saved to {saveFilePath}");
    }

    public void LoadGame()
    {
        if (File.Exists(saveFilePath))
        {
            Debug.Log("Loading game...");
            string json = File.ReadAllText(saveFilePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // Fire the OnLoad event, allowing other systems to retrieve their data.
            OnLoad?.Invoke(data);

            Debug.Log("Game loaded successfully.");
        }
        else
        {
            Debug.LogWarning("No save file found. Starting a new game.");
            // You might want to initialize a new game state here.
        }
    }

    public void DeleteSaveFile()
    {
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
            Debug.Log("Save file deleted.");
        }
    }
}
