using UnityEngine;
using System.IO;

/// <summary>
/// BackupSystem handles the automatic backup of the player's save file to prevent data loss.
/// It creates copies of the save file and provides a method to restore it if the
/// primary save becomes corrupt or is deleted.
/// </summary>
public class BackupSystem : MonoBehaviour
{
    public static BackupSystem Instance;

    [Header("Backup Settings")]
    [Tooltip("How often (in seconds) to create a backup. Set to a high value like 900 for every 15 minutes.")]
    [SerializeField] private float backupInterval = 900f;

    // --- File Naming ---
    private readonly string saveFileName = "rvatac_save.json";
    private readonly string hashFileName = "rvatac_save.hash";
    private readonly string backupSaveFileName = "rvatac_save.json.bak";
    private readonly string backupHashFileName = "rvatac_save.hash.bak";

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

    void Start()
    {
        // Start the periodic backup routine.
        InvokeRepeating(nameof(CreateBackup), backupInterval, backupInterval);
    }

    /// <summary>
    /// Creates a backup of the current save file and its hash.
    /// </summary>
    public void CreateBackup()
    {
        string savePath = Path.Combine(Application.persistentDataPath, saveFileName);
        string hashPath = Path.Combine(Application.persistentDataPath, hashFileName);

        if (File.Exists(savePath) && File.Exists(hashPath))
        {
            try
            {
                string backupSavePath = Path.Combine(Application.persistentDataPath, backupSaveFileName);
                string backupHashPath = Path.Combine(Application.persistentDataPath, backupHashFileName);

                // Copy the current save and hash files to the backup location, overwriting any old backup.
                File.Copy(savePath, backupSavePath, true);
                File.Copy(hashPath, backupHashPath, true);

                Debug.Log("Save game backup created successfully.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create save game backup: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Restores the game state from the backup files.
    /// This should be called by the SaveSystem if it detects a corrupt or missing save file.
    /// </summary>
    /// <returns>True if the restore was successful, otherwise false.</returns>
    public bool RestoreBackup()
    {
        string backupSavePath = Path.Combine(Application.persistentDataPath, backupSaveFileName);
        string backupHashPath = Path.Combine(Application.persistentDataPath, backupHashFileName);

        if (File.Exists(backupSavePath) && File.Exists(backupHashPath))
        {
            try
            {
                string savePath = Path.Combine(Application.persistentDataPath, saveFileName);
                string hashPath = Path.Combine(Application.persistentDataPath, hashFileName);

                // Copy the backup files back to the primary save location.
                File.Copy(backupSavePath, savePath, true);
                File.Copy(backupHashPath, hashPath, true);

                Debug.Log("Save game restored from backup.");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore save game from backup: {e.Message}");
                return false;
            }
        }
        else
        {
            Debug.LogWarning("No backup file found to restore.");
            return false;
        }
    }
}
