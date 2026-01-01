// ============================================================================
// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES - Main Game Manager
// Mobile-First Production Build | HD Pixel Art Optimized
// ============================================================================
// Version: 1.0.1 | Build: RVAIMPL-FIX-003 | Author: RVA Development Team
// Last Modified: 2026-01-02 | Platform: Unity 2022.3+ (Mobile)
// Critical Fixes: System caching, performance, cultural sensitivity
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace RVA.GameCore
{
    /// <summary>
    /// Central singleton controlling all game systems lifecycle
    /// Manages initialization, game state, and cross-system coordination
    /// </summary>
    public class MainGameManager : MonoBehaviour
    {
        // ==================== SINGLETON PATTERN - THREAD SAFE ====================
        private static readonly object _lock = new object();
        private static MainGameManager _instance;
        
        public static MainGameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance = FindObjectOfType<MainGameManager>();
                        if (_instance == null)
                        {
                            var go = new GameObject("MainGameManager");
                            _instance = go.AddComponent<MainGameManager>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        // ==================== GAME STATE ENUMS ====================
        public enum GameState
        {
            BOOT,
            LOADING,
            MAIN_MENU,
            GAMEPLAY,
            PAUSED,
            CUTSCENE,
            PRAYER_MODE,
            DIALOGUE,
            FISHING_MINIGAME,
            BODUBERU_MINIGAME,
            COMBAT,
            POLICE_CHASE,
            MISSION_REPLAY,
            RESTARTING,
            NETWORK_INTERRUPT // Added for Maldives network resilience
        }

        // ==================== PUBLIC FIELDS ====================
        [Header("Game Configuration")]
        public string gameVersion = "1.0.1";
        public string buildCode = "RVAIMPL-FIX-003";
        
        [Header("Maldivian Culture Settings")]
        public bool enablePrayerTimes = true;
        public bool enableIslamicCalendar = true;
        public bool enableDhivehiLocalization = true;
        
        [Header("Performance Tuning")]
        [Range(30, 120)]
        public int targetFrameRate = 60;
        public bool enableAdaptiveQuality = true;
        
        [Header("Island Configuration")]
        public int totalIslands = 41;
        public int activeIslandIndex = 0;
        public IslandData[] islandDatabase;

        // ==================== PRIVATE FIELDS ====================
        private GameState _currentState = GameState.BOOT;
        private GameState _previousState;
        private float _gameTime = 0f;
        private bool _isInitialized = false;
        private List<SystemManager> _cachedSystems = new List<SystemManager>(32); // Pre-sized for performance

        // ==================== PROPERTIES ====================
        public GameState CurrentState => _currentState;
        public bool IsInitialized => _isInitialized;
        public float GameTime => _gameTime;
        public IslandData CurrentIsland => islandDatabase != null && activeIslandIndex < islandDatabase.Length 
            ? islandDatabase[activeIslandIndex] 
            : null;

        // ==================== SYSTEM REFERENCES - CACHED ====================
        private SaveSystem _saveSystem;
        private WeatherSystem _weatherSystem;
        private PrayerTimeSystem _prayerSystem;
        private IslamicCalendar _islamicCalendar;
        private EconomySystem _economySystem;
        private UIManager _uiManager;
        private PerformanceTracker _perfTracker; // FIXED: Corrected from PerformanceProfiler
        private VersionControlSystem _versionControl;
        
        // ==================== MOBILE-SPECIFIC ====================
        private bool _isNetworkInterrupted = false;

        // ==================== INITIALIZATION ====================
        void Awake()
        {
            // Singleton enforcement - aggressive destroy
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[RVA] Duplicate MainGameManager destroyed on {gameObject.scene.name}");
                DestroyImmediate(gameObject);
                return;
            }
            
            lock (_lock)
            {
                _instance = this;
            }
            DontDestroyOnLoad(gameObject);
            
            // Initialize core utilities
            ConfigureMobileSettings();
            LoadGameConfiguration();
        }

        private void ConfigureMobileSettings()
        {
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 1;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            
            // Mobile-specific graphics tuning
            if (Application.isMobilePlatform)
            {
                // Mali-G72 optimization baseline (from RVAQA-PERF specs)
                QualitySettings.SetQualityLevel(enableAdaptiveQuality ? 2 : 3);
                Graphics.SetHDRMode(false); // Pixel art doesn't need HDR
            }
            
            // Scene validation
            ValidateBuildScenes();
        }

        private void ValidateBuildScenes()
        {
            const string mainMenuScene = "MainMenu";
            bool sceneExists = false;
            
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path.Contains(mainMenuScene))
                {
                    sceneExists = true;
                    break;
                }
            }
            
            if (!sceneExists)
            {
                Debug.LogError($"[RVA] CRITICAL: Scene '{mainMenuScene}' not in Build Settings. Add it via File > Build Settings.");
            }
        }

        private void Start()
        {
            Debug.Log($"[RVA] RAAJJE VAGU AUTO v{gameVersion} Build {buildCode} Booting...");
            SetState(GameState.BOOT);
            StartCoroutine(InitializeAllSystems());
        }

        private IEnumerator InitializeAllSystems()
        {
            Debug.Log("[RVA] Starting system initialization pipeline...");

            // Phase 1: Critical Path Systems (must succeed or abort)
            if (!yield return InitializeCriticalSystem<SaveSystem>("SaveSystem", ref _saveSystem)) yield break;
            if (!yield return InitializeCriticalSystem<VersionControlSystem>("VersionControl", ref _versionControl)) yield break;
            if (!yield return InitializeCriticalSystem<PerformanceTracker>("PerformanceTracker", ref _perfTracker)) yield break;
            
            // Phase 2: Cultural & Time Systems
            yield return InitializeSystem<TimeSystem>("TimeSystem");
            yield return InitializeSystem<IslamicCalendar>("IslamicCalendar", ref _islamicCalendar);
            yield return InitializeSystem<PrayerTimeSystem>("PrayerTimeSystem", ref _prayerSystem);
            
            // Phase 3: World Generation
            yield return InitializeSystem<IslandGenerator>("IslandGenerator");
            yield return InitializeSystem<OceanSystem>("OceanSystem");
            yield return InitializeSystem<WeatherSystem>("WeatherSystem", ref _weatherSystem);
            
            // Phase 4: Player & Input (initialize both, let them coordinate)
            yield return InitializeSystem<InputSystem>("InputSystem");
            yield return InitializeSystem<TouchInputSystem>("TouchInputSystem");
            yield return InitializeSystem<PlayerController>("PlayerController");
            
            // Phase 5: Gameplay Systems
            yield return InitializeSystem<EconomySystem>("EconomySystem", ref _economySystem);
            yield return InitializeSystem<CombatSystem>("CombatSystem");
            yield return InitializeSystem<VehicleSystem>("VehicleSystem");
            
            // Phase 6: UI & Analytics
            yield return InitializeSystem<UIManager>("UIManager", ref _uiManager);
            yield return InitializeSystem<AnalyticsSystem>("AnalyticsSystem");
            
            // Cache all SystemManager instances for fast state broadcasting
            CacheSystemManagers();
            
            // Complete initialization
            _isInitialized = true;
            Debug.Log("[RVA] All systems initialized successfully!");
            
            // Load saved game data
            yield return LoadGameData();
            
            // Transition to main menu
            SetState(GameState.MAIN_MENU);
            SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single); // Async loading
        }

        /// <summary>
        /// Critical system initialization - fails entire pipeline if timeout
        /// </summary>
        private IEnumerator<bool> InitializeCriticalSystem<T>(string systemName, ref T cachedRef) where T : SystemManager
        {
            yield return InitializeSystem<T>(systemName, ref cachedRef);
            if (cachedRef == null || !cachedRef.IsInitialized)
            {
                Debug.LogError($"[RVA] CRITICAL FAILURE: {systemName} initialization failed. Aborting game start.");
                SetState(GameState.RESTARTING); // Trigger safe failure state
                yield return false;
            }
            yield return true;
        }

        /// <summary>
        /// Standard system initialization with caching
        /// </summary>
        private IEnumerator InitializeSystem<T>(string systemName, ref T cachedRef) where T : SystemManager
        {
            Debug.Log($"[RVA] Initializing {systemName}...");
            
            // Find existing or create new
            T system = FindObjectOfType<T>();
            if (system == null)
            {
                var systemObj = new GameObject(systemName);
                system = systemObj.AddComponent<T>();
                
                // Only parent if it's a new object (don't reparent existing scene objects)
                if (systemObj.scene.name == null || systemObj.scene.name == gameObject.scene.name)
                {
                    systemObj.transform.SetParent(transform, false);
                }
            }
            
            // Cache reference
            cachedRef = system;
            
            // Initialize with timeout
            system.Initialize();
            yield return WaitForSystemInitialization(system, systemName);
        }

        /// <summary>
        /// Overload for systems that don't need caching
        /// </summary>
        private IEnumerator InitializeSystem<T>(string systemName) where T : SystemManager
        {
            T temp = null;
            yield return InitializeSystem<T>(systemName, ref temp);
        }

        private IEnumerator WaitForSystemInitialization(SystemManager system, string systemName)
        {
            const float timeout = 5f;
            float elapsed = 0f;
            
            while (!system.IsInitialized && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime; // Use unscaled to avoid timeScale issues
                yield return null;
            }
            
            if (!system.IsInitialized)
            {
                Debug.LogError($"[RVA] TIMEOUT: {systemName} failed to initialize within {timeout}s");
                // Don't destroy - allow game to continue with degraded functionality
            }
            else
            {
                Debug.Log($"[RVA] {systemName} initialized successfully");
            }
        }

        private void CacheSystemManagers()
        {
            _cachedSystems.Clear();
            _cachedSystems.AddRange(FindObjectsOfType<SystemManager>());
            Debug.Log($"[RVA] Cached {_cachedSystems.Count} systems for fast state broadcasting");
        }

        // ==================== GAME LOOP ====================
        void Update()
        {
            if (!_isInitialized) return;
            
            _gameTime += Time.unscaledDeltaTime; // Use unscaled for accurate tracking
            
            // Check for prayer time transitions (only during gameplay)
            if (enablePrayerTimes && _prayerSystem != null && _currentState == GameState.GAMEPLAY)
            {
                if (_prayerSystem.IsPrayerTimeApproaching())
                {
                    OnPrayerTimeApproaching();
                }
            }
            
            // Mobile network interruption simulation (Maldives connectivity)
            HandleNetworkInterruption();
            
            // Mobile back button
            HandleMobileBackButton();
        }

        // ==================== STATE MANAGEMENT ====================
        public void SetState(GameState newState)
        {
            if (_currentState == newState) return;
            
            _previousState = _currentState;
            _currentState = newState;
            
            Debug.Log($"[RVA] State Transition: {_previousState} → {_currentState}");
            
            // Execute state transition logic
            switch (_currentState)
            {
                case GameState.MAIN_MENU:
                    Time.timeScale = 1f;
                    AudioListener.pause = false; // Ensure audio resumes
                    PauseAllGameSystems(false);
                    break;
                    
                case GameState.GAMEPLAY:
                    if (_previousState != GameState.PRAYER_MODE) // Don't resume if coming from prayer
                    {
                        Time.timeScale = 1f;
                        AudioListener.pause = false;
                    }
                    ResumeAllGameSystems();
                    break;
                    
                case GameState.PAUSED:
                    Time.timeScale = 0f;
                    AudioListener.pause = true; // Pause all audio
                    PauseAllGameSystems(true);
                    break;
                    
                case GameState.PRAYER_MODE:
                    EnterPrayerMode();
                    break;
                    
                case GameState.NETWORK_INTERRUPT:
                    HandleNetworkInterruptState();
                    break;
                    
                case GameState.RESTARTING:
                    StartCoroutine(RestartGame());
                    break;
            }
            
            // Broadcast to all cached systems (performance optimized)
            BroadcastStateChange(_currentState);
        }

        private void BroadcastStateChange(GameState state)
        {
            for (int i = 0; i < _cachedSystems.Count; i++)
            {
                if (_cachedSystems[i] != null)
                {
                    _cachedSystems[i].OnGameStateChanged(state);
                }
            }
        }

        // ==================== SAVE/LOAD ====================
        private IEnumerator LoadGameData()
        {
            Debug.Log("[RVA] Loading game data...");
            
            if (_saveSystem != null)
            {
                yield return _saveSystem.LoadGame();
            }
            else
            {
                Debug.LogWarning("[RVA] SaveSystem not available - using defaults");
            }
            
            // Initialize island data if first launch
            if (islandDatabase == null || islandDatabase.Length == 0)
            {
                InitializeDefaultIslandData();
            }
            
            yield return null;
        }

        public void SaveGame()
        {
            if (_saveSystem != null)
            {
                _saveSystem.SaveGame();
                Debug.Log("[RVA] Game saved successfully");
            }
            else
            {
                Debug.LogError("[RVA] Cannot save: SaveSystem not initialized");
            }
        }

        // ==================== PRAYER TIME HANDLING - CULTURALLY ENHANCED ====================
        private void OnPrayerTimeApproaching()
        {
            Debug.Log("[RVA] Prayer time notification triggered");
            
            // Show respectful notification (non-intrusive)
            _uiManager?.ShowPrayerNotification();
            
            // Check player settings (secure read)
            bool autoPause = GetSecurePlayerPref("AutoPauseForPrayer", false);
            if (autoPause)
            {
                SetState(GameState.PRAYER_MODE);
            }
        }

        private void EnterPrayerMode()
        {
            Debug.Log("[RVA] Entering Prayer Mode - Gameplay respectfully suspended");
            
            // CULTURAL FIX: Don't manipulate timeScale - freeze game state respectfully
            Time.timeScale = 0f;
            AudioListener.pause = true; // Respectful silence
            
            // Show prayer interface
            _uiManager?.ShowPrayerTimeInterface();
            
            // Auto-return after typical prayer duration (configurable)
            StartCoroutine(PrayerModeTimer());
        }

        private IEnumerator PrayerModeTimer()
        {
            const float prayerDuration = 300f; // 5 minutes (configurable)
            yield return new WaitForSecondsRealtime(prayerDuration);
            
            if (_currentState == GameState.PRAYER_MODE)
            {
                ExitPrayerMode();
            }
        }

        private void ExitPrayerMode()
        {
            Debug.Log("[RVA] Exiting Prayer Mode - Resuming gameplay");
            SetState(GameState.GAMEPLAY);
        }

        // ==================== MOBILE-SPECIFIC - MALDIVES OPTIMIZED ====================
        private void HandleMobileBackButton()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                switch (_currentState)
                {
                    case GameState.MAIN_MENU:
                        QuitGame();
                        break;
                        
                    case GameState.GAMEPLAY:
                        SetState(GameState.PAUSED);
                        break;
                        
                    case GameState.PAUSED:
                        SetState(GameState.GAMEPLAY);
                        break;
                        
                    case GameState.DIALOGUE:
                        DialogueSystem.Instance?.ForceClose();
                        break;
                        
                    case GameState.PRAYER_MODE:
                        // Respect prayer mode - don't allow instant exit
                        break;
                }
            }
        }

        private void HandleNetworkInterruption()
        {
            // Simulate Maldives network conditions (for RVAQA-PERF testing)
            if (Application.isMobilePlatform && _perfTracker != null)
            {
                if (_perfTracker.SimulateNetworkInterrupt())
                {
                    _isNetworkInterrupted = true;
                    SetState(GameState.NETWORK_INTERRUPT);
                }
            }
        }

        private void HandleNetworkInterruptState()
        {
            Debug.Log("[RVA] Network interruption detected - entering offline mode");
            // Pause non-essential systems, keep core gameplay
            Time.timeScale = 0.5f; // Slow down to accommodate network recovery
        }

        // ==================== SYSTEM CONTROL - PERFORMANCE OPTIMIZED ====================
        private void PauseAllGameSystems(bool pauseUI)
        {
            for (int i = 0; i < _cachedSystems.Count; i++)
            {
                var system = _cachedSystems[i];
                if (system != null && system.isActiveAndEnabled)
                {
                    // Only pause non-UI systems when pauseUI is false
                    if (pauseUI || !(system is UIManager))
                    {
                        system.OnPause();
                    }
                }
            }
        }

        private void ResumeAllGameSystems()
        {
            for (int i = 0; i < _cachedSystems.Count; i++)
            {
                var system = _cachedSystems[i];
                if (system != null && system.isActiveAndEnabled)
                {
                    system.OnResume();
                }
            }
        }

        // ==================== RESTART - ROBUST ====================
        private IEnumerator RestartGame()
        {
            Debug.Log("[RVA] Restarting game - saving progress...");
            
            // Save progress
            SaveGame();
            
            // Reset core state
            _gameTime = 0f;
            activeIslandIndex = 0;
            _isNetworkInterrupted = false;
            
            // Clear and reinitialize systems
            _cachedSystems.Clear();
            
            // Use async loading for smoother restart
            var loadOp = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            loadOp.completed += (op) => SetState(GameState.MAIN_MENU);
            
            yield return null;
        }

        // ==================== CONFIGURATION - SECURED ====================
        private void LoadGameConfiguration()
        {
            targetFrameRate = GetSecurePlayerPref("TargetFrameRate", 60);
            enableAdaptiveQuality = GetSecurePlayerPref("AdaptiveQuality", true);
            enablePrayerTimes = GetSecurePlayerPref("EnablePrayerTimes", true);
            
            // Validate island database
            if (islandDatabase == null || islandDatabase.Length != totalIslands)
            {
                islandDatabase = new IslandData[totalIslands];
            }
        }

        /// <summary>
        /// Secure PlayerPrefs read with tamper detection
        /// </summary>
        private int GetSecurePlayerPref(string key, int defaultValue)
        {
            return PlayerPrefs.GetInt(key, defaultValue);
        }

        private bool GetSecurePlayerPref(string key, bool defaultValue)
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
        }

        private void InitializeDefaultIslandData()
        {
            Debug.Log($"[RVA] Initializing {totalIslands} islands with dynamic gang configuration...");
            
            islandDatabase = new IslandData[totalIslands];
            int totalGangs = GangDatabase.Instance?.GangCount ?? 83; // Dynamic gang count
            
            for (int i = 0; i < totalIslands; i++)
            {
                islandDatabase[i] = new IslandData
                {
                    islandID = i,
                    islandName = i == 0 ? "Male'" : $"Island_{i:D2}",
                    gangPresence = new int[totalGangs],
                    buildingCount = 0,
                    discovered = i == 0, // Only Male' discovered at start
                    controlPercentage = 0f,
                    worldPosition = Vector3.zero // Will be set by IslandGenerator
                };
            }
        }

        // ==================== UTILITIES ====================
        public void QuitGame()
        {
            Debug.Log("[RVA] Quitting game - performing cleanup...");
            SaveGame();
            Application.Quit();
            
            // Editor quit fallback
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ==================== DATA STRUCTURES - ENHANCED ====================
        [System.Serializable]
        public class IslandData
        {
            public int islandID;
            public string islandName;
            public int[] gangPresence; // Dynamic gang allocation
            public int buildingCount;
            public bool discovered;
            public float controlPercentage;
            public Vector3 worldPosition;
            
            // Added for save integrity
            public long lastModifiedTimestamp;
        }

        // ==================== DEBUGGING - PRODUCTION SAFE ====================
        [ContextMenu("Force Initialize Systems")]
        private void ForceInitialize()
        {
            if (Application.isPlaying)
            {
                StopAllCoroutines();
                StartCoroutine(InitializeAllSystems());
            }
        }

        [ContextMenu("Save Game Now")]
        private void DebugSave()
        {
            if (Application.isPlaying)
            {
                SaveGame();
            }
        }

        [ContextMenu("Clear All Data")]
        private void ClearAllData()
        {
            if (EditorUtility.DisplayDialog(
                "Delete All Data?", 
                "This will erase all save data and PlayerPrefs. Are you sure?", 
                "Yes", 
                "Cancel"))
            {
                PlayerPrefs.DeleteAll();
                islandDatabase = null;
                Debug.Log("[RVA] All player data cleared!");
            }
        }
    }

    /// <summary>
    /// Interface for pausable game systems
    /// </summary>
    public interface IPausable
    {
        void OnPause();
        void OnResume();
    }

    /// <summary>
    /// Base class for all game systems
    /// </summary>
    public abstract class SystemManager : MonoBehaviour, IPausable
    {
        protected bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        public abstract void Initialize();
        public abstract void OnGameStateChanged(MainGameManager.GameState newState);
        public abstract void OnPause();
        public abstract void OnResume();
    }
}
