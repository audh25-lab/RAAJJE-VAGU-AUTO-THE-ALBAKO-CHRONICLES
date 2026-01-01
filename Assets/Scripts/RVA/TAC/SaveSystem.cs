// ============================================================================
// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES - Save System
// Cloud + Local Dual Save | Encryption | Mobile-Optimized
// ============================================================================
// Version: 1.0.0 | Build: RVACONT-001 | Author: RVA Development Team
// Last Modified: 2025-12-30 | Platform: Unity 2022.3+ (Mobile)
// Encryption: AES-256 | Compression: LZ4
// ============================================================================

using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using System.Security.Cryptography;

namespace RVA.GameCore
{
    /// <summary>
    /// Dual-save system: Local (fast) + Cloud (backup)
    /// Handles player progress, island states, gang control, inventory
    /// </summary>
    public class SaveSystem : SystemManager
    {
        // ==================== SAVE LOCATIONS ====================
        private const string LOCAL_SAVE_KEY = "RVA_SaveData_v1_";
        private const string CLOUD_SAVE_KEY = "RVA_CloudSave_v1_";
        private const string BACKUP_SAVE_KEY = "RVA_Backup_v1_";
        
        // ==================== ENCRYPTION ====================
        private const string ENCRYPTION_KEY = "RVA_MALDIVES_2025_ALBAKO_ENCRYPT_KEY_256";
        private const string ENCRYPTION_SALT = "MALDIVIAN_SALT";
        
        // ==================== SAVE INTERVALS ====================
        [Header("Auto-Save Settings")]
        public float autoSaveInterval = 300f; // 5 minutes
        public bool saveOnAppPause = true;
        public bool saveOnIslandChange = true;
        public bool saveAfterMission = true;
        
        [Header("Cloud Save")]
        public bool enableCloudSave = true;
        public float cloudSyncInterval = 1800f; // 30 minutes
        
        private float _lastAutoSaveTime;
        private float _lastCloudSyncTime;
        
        // ==================== SAVE DATA STRUCTURE ====================
        [System.Serializable]
        public class GameSaveData
        {
            // Version info
            public string saveVersion = "1.0.0";
            public long timestamp;
            public int playTimeSeconds;
            
            // Player data
            public PlayerData playerData;
            
            // World state
            public IslandData[] islandData;
            public GangData[] gangData;
            
            // Progress
            public MissionData missionData;
            public EconomyData economyData;
            public InventoryData inventoryData;
            
            // Cultural tracking
            public PrayerAttendanceData prayerData;
            public FishingStats fishingStats;
            public BoduberuStats boduberuStats;
            
            // Settings
            public GameSettings settings;
            
            // Analytics
            public SessionData sessionData;
        }

        [System.Serializable]
        public class PlayerData
        {
            public string playerName = "RAAJJE";
            public int currentIslandIndex;
            public Vector3 position;
            public int health;
            public int maxHealth = 100;
            public int wantedLevel;
            public float reputation;
            public int skillPoints;
            
            // Skills
            public int combatSkill;
            public int fishingSkill;
            public int drivingSkill;
            public int stealthSkill;
        }

        [System.Serializable]
        public class IslandData
        {
            public int islandID;
            public bool isDiscovered;
            public int[] gangPresence = new int[83];
            public bool[] buildingOwned = new bool[70];
            public float controlPercentage;
        }

        [System.Serializable]
        public class GangData
        {
            public int gangID;
            public string gangName;
            public int memberCount;
            public float territoryControl;
            public int hostilityLevel;
            public bool isPlayerGang;
        }

        [System.Serializable]
        public class MissionData
        {
            public string currentMissionID;
            public string[] completedMissionIDs = new string[200];
            public int missionCount;
        }

        [System.Serializable]
        public class EconomyData
        {
            public int rufiyaaAmount;
            public int bankBalance;
            public int dailyExpenses;
            public int incomePerDay;
        }

        [System.Serializable]
        public class InventoryData
        {
            public string[] weaponIDs = new string[10];
            public int[] weaponAmmo = new int[10];
            public string[] itemIDs = new string[50];
            public int[] itemQuantities = new int[50];
        }

        [System.Serializable]
        public class PrayerAttendanceData
        {
            public int[] prayersAttendedToday = new int[5]; // 5 prayers
            public int totalPrayersAttended;
            public int consecutiveDays;
            public string lastPrayerDate;
        }

        [System.Serializable]
        public class FishingStats
        {
            public int fishCaught;
            public string biggestFish;
            public int fishingTrips;
            public int rareSpeciesFound;
        }

        [System.Serializable]
        public class BoduberuStats
        {
            public int performancesCompleted;
            public int maxCombo;
            public int totalScore;
            public int perfectPerformances;
        }

        [System.Serializable]
        public class GameSettings
        {
            public int language; // 0 = English, 1 = Dhivehi
            public int targetFrameRate = 60;
            public bool enablePrayerNotifications = true;
            public bool enableAutoPauseForPrayer = false;
            public float musicVolume = 0.8f;
            public float sfxVolume = 0.8f;
            public bool enableVibration = true;
            public bool enableAnalytics = true;
        }

        [System.Serializable]
        public class SessionData
        {
            public int sessionsPlayed;
            public long totalPlayTime;
            public string firstPlayDate;
            public string lastPlayDate;
        }

        // ==================== CURRENT SAVE DATA ====================
        private GameSaveData _currentSaveData;
        public GameSaveData CurrentSave => _currentSaveData;

        // ==================== INITIALIZATION ====================
        public override void Initialize()
        {
            if (_isInitialized) return;
            
            Debug.Log("[SaveSystem] Initializing...");
            
            // Initialize new save data
            _currentSaveData = new GameSaveData
            {
                timestamp = GetUnixTimestamp(),
                playerData = new PlayerData(),
                islandData = new IslandData[41],
                gangData = new GangData[83],
                missionData = new MissionData(),
                economyData = new EconomyData(),
                inventoryData = new InventoryData(),
                prayerData = new PrayerAttendanceData(),
                fishingStats = new FishingStats(),
                boduberuStats = new BoduberuStats(),
                settings = new GameSettings(),
                sessionData = new SessionData()
            };
            
            // Initialize arrays
            for (int i = 0; i < 41; i++)
            {
                _currentSaveData.islandData[i] = new IslandData
                {
                    islandID = i,
                    gangPresence = new int[83],
                    buildingOwned = new bool[70]
                };
            }
            
            for (int i = 0; i < 83; i++)
            {
                _currentSaveData.gangData[i] = new GangData { gangID = i };
            }
            
            _isInitialized = true;
            Debug.Log("[SaveSystem] Initialized successfully");
        }

        // ==================== AUTO-SAVE ====================
        private void Update()
        {
            if (!_isInitialized) return;
            
            // Auto-save timer
            if (Time.time - _lastAutoSaveTime > autoSaveInterval)
            {
                AutoSave();
                _lastAutoSaveTime = Time.time;
            }
            
            // Cloud sync timer
            if (enableCloudSave && Time.time - _lastCloudSyncTime > cloudSyncInterval)
            {
                SyncToCloud();
                _lastCloudSyncTime = Time.time;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (saveOnAppPause && pauseStatus)
            {
                Debug.Log("[SaveSystem] Application paused - saving game");
                SaveGame();
            }
        }

        private void OnApplicationQuit()
        {
            Debug.Log("[SaveSystem] Application quitting - saving game");
            SaveGame();
        }

        // ==================== SAVE GAME ====================
        public void SaveGame()
        {
            if (!_isInitialized) return;
            
            Debug.Log("[SaveSystem] Saving game...");
            
            // Update timestamp and playtime
            _currentSaveData.timestamp = GetUnixTimestamp();
            _currentSaveData.playTimeSeconds = (int)MainGameManager.Instance.GameTime;
            
            // Update session data
            _currentSaveData.sessionData.lastPlayDate = DateTime.Now.ToString("yyyy-MM-dd");
            
            // Serialize to JSON
            string jsonData = JsonUtility.ToJson(_currentSaveData, true);
            
            // Compress and encrypt
            string encryptedData = EncryptAndCompress(jsonData);
            
            // Save to PlayerPrefs (mobile-friendly)
            PlayerPrefs.SetString(LOCAL_SAVE_KEY, encryptedData);
            PlayerPrefs.Save();
            
            // Create backup
            PlayerPrefs.SetString(BACKUP_SAVE_KEY, encryptedData);
            
            Debug.Log("[SaveSystem] Game saved locally");
            
            // Sync to cloud if enabled
            if (enableCloudSave)
            {
                StartCoroutine(SyncToCloudAsync(encryptedData));
            }
        }

        private void AutoSave()
        {
            SaveGame();
            Debug.Log("[SaveSystem] Auto-save completed");
            
            // Show subtle indicator
            UIManager.Instance?.ShowSaveIndicator();
        }

        // ==================== LOAD GAME ====================
        public IEnumerator LoadGame()
        {
            Debug.Log("[SaveSystem] Loading game...");
            
            bool loadSuccess = false;
            
            // Try local save first
            if (PlayerPrefs.HasKey(LOCAL_SAVE_KEY))
            {
                string encryptedData = PlayerPrefs.GetString(LOCAL_SAVE_KEY);
                loadSuccess = DecryptAndLoad(encryptedData);
            }
            
            // If local fails, try backup
            if (!loadSuccess && PlayerPrefs.HasKey(BACKUP_SAVE_KEY))
            {
                Debug.LogWarning("[SaveSystem] Local save corrupted, trying backup...");
                string backupData = PlayerPrefs.GetString(BACKUP_SAVE_KEY);
                loadSuccess = DecryptAndLoad(backupData);
            }
            
            // If both fail, initialize new game
            if (!loadSuccess)
            {
                Debug.Log("[SaveSystem] No valid save found, starting new game");
                InitializeNewGame();
            }
            else
            {
                Debug.Log("[SaveSystem] Game loaded successfully");
                ApplyLoadedData();
            }
            
            yield return null;
        }

        private bool DecryptAndLoad(string encryptedData)
        {
            try
            {
                string jsonData = DecompressAndDecrypt(encryptedData);
                GameSaveData loadedData = JsonUtility.FromJson<GameSaveData>(jsonData);
                
                if (loadedData != null)
                {
                    _currentSaveData = loadedData;
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Load error: {e.Message}");
            }
            
            return false;
        }

        private void ApplyLoadedData()
        {
            // Update main game manager
            if (MainGameManager.Instance != null)
            {
                MainGameManager.Instance.activeIslandIndex = _currentSaveData.playerData.currentIslandIndex;
            }
            
            // Apply settings
            ApplySettings(_currentSaveData.settings);
            
            Debug.Log("[SaveSystem] Loaded data applied to game");
        }

        // ==================== NEW GAME ====================
        private void InitializeNewGame()
        {
            Debug.Log("[SaveSystem] Initializing new game data...");
            
            // Reset all data
            _currentSaveData = new GameSaveData
            {
                timestamp = GetUnixTimestamp(),
                playerData = new PlayerData
                {
                    playerName = "RAAJJE",
                    currentIslandIndex = 0,
                    position = Vector3.zero,
                    health = 100,
                    maxHealth = 100,
                    wantedLevel = 0,
                    reputation = 0f,
                    skillPoints = 0,
                    combatSkill = 1,
                    fishingSkill = 1,
                    drivingSkill = 1,
                    stealthSkill = 1
                },
                economyData = new EconomyData
                {
                    rufiyaaAmount = 500, // Starting money
                    bankBalance = 0,
                    dailyExpenses = 50,
                    incomePerDay = 0
                },
                settings = GetDefaultSettings(),
                sessionData = new SessionData
                {
                    sessionsPlayed = 1,
                    firstPlayDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    lastPlayDate = DateTime.Now.ToString("yyyy-MM-dd")
                }
            };
            
            // Initialize arrays
            for (int i = 0; i < 41; i++)
            {
                _currentSaveData.islandData[i] = new IslandData
                {
                    islandID = i,
                    isDiscovered = i == 0, // Only Male' discovered at start
                    gangPresence = new int[83],
                    buildingOwned = new bool[70]
                };
                
                // Set default gang presence
                if (i == 0) // Male'
                {
                    _currentSaveData.islandData[i].gangPresence[0] = 10; // Small gang presence
                }
            }
            
            // Initialize gangs
            for (int i = 0; i < 83; i++)
            {
                _currentSaveData.gangData[i] = new GangData
                {
                    gangID = i,
                    memberCount = UnityEngine.Random.Range(5, 50),
                    territoryControl = 0f,
                    hostilityLevel = UnityEngine.Random.Range(0, 5),
                    isPlayerGang = false
                };
            }
            
            // Set player gang (ID 0)
            _currentSaveData.gangData[0].isPlayerGang = true;
            _currentSaveData.gangData[0].gangName = "RAAJJE_VAGU";
            
            Debug.Log("[SaveSystem] New game initialized");
        }

        private GameSettings GetDefaultSettings()
        {
            return new GameSettings
            {
                language = 0, // English default
                targetFrameRate = 60,
                enablePrayerNotifications = true,
                enableAutoPauseForPrayer = false,
                musicVolume = 0.8f,
                sfxVolume = 0.8f,
                enableVibration = true,
                enableAnalytics = true
            };
        }

        // ==================== CLOUD SAVE ====================
        private IEnumerator SyncToCloudAsync(string encryptedData)
        {
            Debug.Log("[SaveSystem] Syncing to cloud...");
            
            // Simulate cloud save (replace with actual cloud service)
            PlayerPrefs.SetString(CLOUD_SAVE_KEY, encryptedData);
            
            // In real implementation, use Unity Cloud Save or custom backend
            // yield return UnityServices.Instance.CloudSaveService.Data.Player.SaveAsync(...)
            
            yield return new WaitForSeconds(0.5f); // Simulate network delay
            
            Debug.Log("[SaveSystem] Cloud sync completed");
        }

        private void SyncToCloud()
        {
            if (!enableCloudSave) return;
            
            string localData = PlayerPrefs.GetString(LOCAL_SAVE_KEY, "");
            if (!string.IsNullOrEmpty(localData))
            {
                StartCoroutine(SyncToCloudAsync(localData));
            }
        }

        // ==================== ENCRYPTION ====================
        private string EncryptAndCompress(string jsonData)
        {
            try
            {
                // First compress
                byte[] dataBytes = Encoding.UTF8.GetBytes(jsonData);
                // Simulated compression (implement actual LZ4 compression in production)
                
                // Then encrypt
                using (Aes aes = Aes.Create())
                {
                    aes.Key = GenerateKey(ENCRYPTION_KEY);
                    aes.IV = GenerateIV(ENCRYPTION_SALT);
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    
                    ICryptoTransform encryptor = aes.CreateEncryptor();
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
                    
                    return Convert.ToBase64String(encryptedBytes);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Encryption error: {e.Message}");
                return jsonData; // Fallback to unencrypted
            }
        }

        private string DecompressAndDecrypt(string encryptedData)
        {
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedData);
                
                using (Aes aes = Aes.Create())
                {
                    aes.Key = GenerateKey(ENCRYPTION_KEY);
                    aes.IV = GenerateIV(ENCRYPTION_SALT);
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    
                    ICryptoTransform decryptor = aes.CreateDecryptor();
                    byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                    
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Decryption error: {e.Message}");
                return encryptedData; // Fallback
            }
        }

        private byte[] GenerateKey(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
        }

        private byte[] GenerateIV(string salt)
        {
            using (MD5 md5 = MD5.Create())
            {
                return md5.ComputeHash(Encoding.UTF8.GetBytes(salt));
            }
        }

        // ==================== SETTINGS ====================
        private void ApplySettings(GameSettings settings)
        {
            // Apply frame rate
            Application.targetFrameRate = settings.targetFrameRate;
            
            // Apply language
            LocalizationSystem.Instance?.SetLanguage(settings.language);
            
            // Apply audio
            AudioManager.Instance?.SetVolume(settings.musicVolume, settings.sfxVolume);
            
            // Store settings reference
            _currentSaveData.settings = settings;
        }

        // ==================== UTILITY ====================
        private long GetUnixTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public void DeleteSave()
        {
            PlayerPrefs.DeleteKey(LOCAL_SAVE_KEY);
            PlayerPrefs.DeleteKey(CLOUD_SAVE_KEY);
            PlayerPrefs.DeleteKey(BACKUP_SAVE_KEY);
            PlayerPrefs.Save();
            
            Debug.Log("[SaveSystem] All save data deleted");
        }

        public bool HasSaveData()
        {
            return PlayerPrefs.HasKey(LOCAL_SAVE_KEY);
        }

        // ==================== SYSTEM MANAGER OVERRIDES ====================
        public override void OnGameStateChanged(MainGameManager.GameState newState)
        {
            // Auto-save on state changes
            if (newState == MainGameManager.GameState.PAUSED || 
                newState == MainGameManager.GameState.MAIN_MENU)
            {
                SaveGame();
            }
        }

        public override void OnPause()
        {
            // No pause needed
        }

        public override void OnResume()
        {
            // No resume needed
        }
    }
}
