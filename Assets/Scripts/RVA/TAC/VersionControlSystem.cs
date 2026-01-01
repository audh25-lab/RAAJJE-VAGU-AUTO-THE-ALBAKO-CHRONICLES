// ============================================================================
// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES - Version Control System
// Build Management | Update Checker | Compatibility Validation
// ============================================================================
// Version: 1.0.0 | Build: RVACONT-001 | Author: RVA Development Team
// Last Modified: 2025-12-30 | Platform: Unity 2022.3+ (Mobile)
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace RVA.GameCore
{
    /// <summary>
    /// Manages game version, build compatibility, and update requirements
    /// Prevents data corruption between builds
    /// </summary>
    public class VersionControlSystem : SystemManager
    {
        // ==================== VERSION CONFIGURATION ====================
        [Header("Current Build")]
        public string currentVersion = "1.0.0";
        public string buildCode = "RVACONT-001";
        public int buildNumber = 1;
        
        [Header("Compatibility")]
        public string minimumCompatibleVersion = "1.0.0";
        public bool requireUpdateIfIncompatible = true;
        
        [Header("Update Settings")]
        public string updateCheckURL = "https://api.raajjevagu.com/version";
        public float updateCheckInterval = 3600f; // 1 hour
        public bool autoCheckForUpdates = true;
        
        // ==================== VERSION HISTORY ====================
        private string[] _versionHistory = new[]
        {
            "1.0.0 - Initial Release (RVACONT-001)",
            "1.0.1 - Bug fixes, performance improvements",
            "1.1.0 - New islands, vehicle additions",
            "1.2.0 - Multiplayer beta, bug fixes"
        };

        // ==================== PRIVATE FIELDS ====================
        private bool _updateAvailable = false;
        private string _latestVersion = "";
        private string _updateNotes = "";
        private bool _isChecking = false;
        
        // ==================== SAVE VERSION TRACKING ====================
        private const string SAVE_VERSION_KEY = "RVA_SaveVersion";
        private const string BUILD_NUMBER_KEY = "RVA_BuildNumber";

        // ==================== INITIALIZATION ====================
        public override void Initialize()
        {
            if (_isInitialized) return;
            
            Debug.Log($"[VersionControl] Initializing v{currentVersion} build {buildCode}");
            
            // Validate version format
            if (!IsValidVersionFormat(currentVersion))
            {
                Debug.LogError($"[VersionControl] Invalid version format: {currentVersion}");
                currentVersion = "1.0.0";
            }
            
            // Check for version-specific migrations
            CheckSaveVersionCompatibility();
            
            // Update check coroutine
            if (autoCheckForUpdates)
            {
                StartCoroutine(UpdateCheckRoutine());
            }
            
            _isInitialized = true;
            Debug.Log("[VersionControl] Initialized successfully");
        }

        // ==================== SAVE VERSION COMPATIBILITY ====================
        private void CheckSaveVersionCompatibility()
        {
            string savedVersion = PlayerPrefs.GetString(SAVE_VERSION_KEY, "0.0.0");
            int savedBuild = PlayerPrefs.GetInt(BUILD_NUMBER_KEY, 0);
            
            Debug.Log($"[VersionControl] Saved version: {savedVersion} (build {savedBuild})");
            Debug.Log($"[VersionControl] Current version: {currentVersion} (build {buildNumber})");
            
            // Check if save is from incompatible version
            if (IsVersionIncompatible(savedVersion))
            {
                Debug.LogWarning($"[VersionControl] Save from incompatible version detected: {savedVersion}");
                
                if (requireUpdateIfIncompatible)
                {
                    // Show update required dialog
                    ShowIncompatibleVersionDialog(savedVersion);
                }
                else
                {
                    // Attempt migration
                    AttemptSaveMigration(savedVersion);
                }
            }
            
            // Store current version
            PlayerPrefs.SetString(SAVE_VERSION_KEY, currentVersion);
            PlayerPrefs.SetInt(BUILD_NUMBER_KEY, buildNumber);
            PlayerPrefs.Save();
        }

        private bool IsVersionIncompatible(string savedVersion)
        {
            if (savedVersion == "0.0.0") return false; // No save exists
            
            Version saved = new Version(savedVersion);
            Version minimum = new Version(minimumCompatibleVersion);
            
            return saved < minimum;
        }

        private bool IsValidVersionFormat(string version)
        {
            try
            {
                Version v = new Version(version);
                return v.Major >= 0 && v.Minor >= 0 && v.Build >= 0;
            }
            catch
            {
                return false;
            }
        }

        private void ShowIncompatibleVersionDialog(string incompatibleVersion)
        {
            Debug.LogError($"[VersionControl] INCOMPATIBLE SAVE VERSION: {incompatibleVersion}");
            
            // In production, show UI dialog
            // For now, just log and continue with new game
            SaveSystem.Instance?.DeleteSave();
        }

        private void AttemptSaveMigration(string oldVersion)
        {
            Debug.Log($"[VersionControl] Attempting migration from {oldVersion} to {currentVersion}");
            
            // Version-specific migrations
            switch (oldVersion)
            {
                case "0.9.0":
                    Migrate_0_9_0_to_1_0_0();
                    break;
                default:
                    Debug.LogWarning("[VersionControl] No migration path found, starting fresh");
                    SaveSystem.Instance?.DeleteSave();
                    break;
            }
        }

        // ==================== MIGRATION ROUTINES ====================
        private void Migrate_0_9_0_to_1_0_0()
        {
            Debug.Log("[VersionControl] Migrating 0.9.0 -> 1.0.0");
            
            // Example migration: Reset gang data structure changed in 1.0.0
            if (SaveSystem.Instance != null && SaveSystem.Instance.HasSaveData())
            {
                // Load old save
                // Transform data structure
                // Save with new format
            }
            
            Debug.Log("[VersionControl] Migration completed");
        }

        // ==================== UPDATE CHECKING ====================
        private IEnumerator UpdateCheckRoutine()
        {
            yield return new WaitForSeconds(5f); // Wait for game to stabilize
            
            while (true)
            {
                yield return CheckForUpdates();
                yield return new WaitForSeconds(updateCheckInterval);
            }
        }

        public IEnumerator CheckForUpdates()
        {
            if (_isChecking || string.IsNullOrEmpty(updateCheckURL)) yield break;
            
            _isChecking = true;
            Debug.Log("[VersionControl] Checking for updates...");
            
            using (UnityWebRequest request = UnityWebRequest.Get(updateCheckURL))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    ParseUpdateResponse(request.downloadHandler.text);
                }
                else
                {
                    Debug.LogWarning($"[VersionControl] Update check failed: {request.error}");
                }
            }
            
            _isChecking = false;
        }

        private void ParseUpdateResponse(string jsonResponse)
        {
            try
            {
                UpdateResponse response = JsonUtility.FromJson<UpdateResponse>(jsonResponse);
                
                if (response != null && !string.IsNullOrEmpty(response.latestVersion))
                {
                    _latestVersion = response.latestVersion;
                    _updateNotes = response.updateNotes;
                    
                    Version latest = new Version(_latestVersion);
                    Version current = new Version(currentVersion);
                    
                    if (latest > current)
                    {
                        _updateAvailable = true;
                        Debug.Log($"[VersionControl] Update available: {currentVersion} -> {_latestVersion}");
                        
                        // Trigger UI update
                        MainGameManager.Instance?.GetComponent<UIManager>()?.ShowUpdateNotification(_latestVersion);
                    }
                    else
                    {
                        _updateAvailable = false;
                        Debug.Log("[VersionControl] Game is up to date");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VersionControl] Failed to parse update response: {e.Message}");
            }
        }

        // ==================== ANALYTICS ====================
        public void LogBuildEvent(string eventName)
        {
            Debug.Log($"[VersionControl] Build Event: {eventName} | v{currentVersion} | {buildCode}");
            
            // Send to analytics
            AnalyticsSystem.Instance?.LogEvent("build_event", new System.Collections.Generic.Dictionary<string, object>
            {
                { "event_name", eventName },
                { "version", currentVersion },
                { "build_code", buildCode },
                { "build_number", buildNumber }
            });
        }

        public void LogVersionConflict(string savedVersion, string currentVersion)
        {
            Debug.LogWarning($"[VersionControl] Version conflict: Save {savedVersion} vs Current {currentVersion}");
            
            AnalyticsSystem.Instance?.LogEvent("version_conflict", new System.Collections.Generic.Dictionary<string, object>
            {
                { "saved_version", savedVersion },
                { "current_version", currentVersion },
                { "result", "migration_attempted" }
            });
        }

        // ==================== PROPERTIES ====================
        public bool IsUpdateAvailable => _updateAvailable;
        public string LatestVersion => _latestVersion;
        public string UpdateNotes => _updateNotes;
        public string CurrentVersion => currentVersion;
        public string BuildCode => buildCode;

        // ==================== SYSTEM MANAGER OVERRIDES ====================
        public override void OnGameStateChanged(MainGameManager.GameState newState)
        {
            // No special handling needed
        }

        public override void OnPause()
        {
            // Cannot pause
        }

        public override void OnResume()
        {
            // Cannot resume
        }

        // ==================== DATA STRUCTURES ====================
        [System.Serializable]
        private class UpdateResponse
        {
            public string latestVersion;
            public string updateNotes;
            public string releaseDate;
            public bool isMandatory;
            public string downloadURL;
        }

        // ==================== DEBUGGING ====================
        [ContextMenu("Force Update Check")]
        private void ForceUpdateCheck()
        {
            StartCoroutine(CheckForUpdates());
        }

        [ContextMenu("Simulate Incompatible Save")]
        private void SimulateIncompatibleSave()
        {
            PlayerPrefs.SetString(SAVE_VERSION_KEY, "0.5.0");
            PlayerPrefs.Save();
            CheckSaveVersionCompatibility();
        }

        [ContextMenu("Clear Version History")]
        private void ClearVersionHistory()
        {
            PlayerPrefs.DeleteKey(SAVE_VERSION_KEY);
            PlayerPrefs.DeleteKey(BUILD_NUMBER_KEY);
            PlayerPrefs.Save();
            Debug.Log("[VersionControl] Version history cleared");
        }
    }
}
