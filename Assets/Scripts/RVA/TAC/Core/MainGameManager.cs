// ============================================================================
// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES - Main Game Manager
// Mobile-First Production Build | HD Pixel Art Optimized
// ============================================================================
// Version: 1.0.0 | Build: RVACONT-001 | Author: RVA Development Team
// Last Modified: 2025-12-30 | Platform: Unity 2022.3+ (Mobile)
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RVA.GameCore
{
    /// <summary>
    /// Central singleton controlling all game systems lifecycle
    /// Manages initialization, game state, and cross-system coordination
    /// </summary>
    public class MainGameManager : MonoBehaviour
    {
        // ==================== SINGLETON PATTERN ====================
        private static MainGameManager _instance;
        public static MainGameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MainGameManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("MainGameManager");
                        _instance = go.AddComponent<MainGameManager>();
                        DontDestroyOnLoad(go);
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
            RESTARTING
        }

        // ==================== PUBLIC FIELDS ====================
        [Header("Game Configuration")]
        public string gameVersion = "1.0.0";
        public string buildCode = "RVACONT-001";
        
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
        private SystemManager[] _allSystems;

        // ==================== PROPERTIES ====================
        public GameState CurrentState => _currentState;
        public bool IsInitialized => _isInitialized;
        public float GameTime => _gameTime;
        public IslandData CurrentIsland => islandDatabase != null && activeIslandIndex < islandDatabase.Length 
            ? islandDatabase[activeIslandIndex] 
            : null;

        // ==================== SYSTEMS BACKUP ====================
        private SaveSystem _saveSystem;
        private WeatherSystem _weatherSystem;
        private PrayerTimeSystem _prayerSystem;
        private IslamicCalendar _islamicCalendar;
        private EconomySystem _economySystem;
        private UIManager _uiManager;
        private PerformanceProfiler _perfProfiler;

        // ==================== INITIALIZATION ====================
        void Awake()
        {
            // Singleton enforcement
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initialize core utilities
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 1;
            
            // Prevent screen dimming on mobile
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            
            // Load system configuration
            LoadGameConfiguration();
        }

        private void Start()
        {
            Debug.Log($"[RVA] RAAJJE VAGU AUTO v{gameVersion} Booting...");
            SetState(GameState.BOOT);
            StartCoroutine(InitializeAllSystems());
        }

        private IEnumerator InitializeAllSystems()
        {
            Debug.Log("[RVA] Initializing Core Systems...");

            // Phase 1: Critical Systems (must load before anything else)
            yield return InitializeSystem<SaveSystem>("SaveSystem");
            yield return InitializeSystem<VersionControlSystem>("VersionControl");
            yield return InitializeSystem<PerformanceProfiler>("PerformanceProfiler");
            
            // Phase 2: Cultural & Time Systems
            yield return InitializeSystem<TimeSystem>("TimeSystem");
            yield return InitializeSystem<IslamicCalendar>("IslamicCalendar");
            yield return InitializeSystem<PrayerTimeSystem>("PrayerTimeSystem");
            
            // Phase 3: World Generation
            yield return InitializeSystem<IslandGenerator>("IslandGenerator");
            yield return InitializeSystem<OceanSystem>("OceanSystem");
            yield return InitializeSystem<WeatherSystem>("WeatherSystem");
            
            // Phase 4: Player & Input
            yield return InitializeSystem<InputSystem>("InputSystem");
            yield return InitializeSystem<TouchInputSystem>("TouchInputSystem");
            yield return InitializeSystem<PlayerController>("PlayerController");
            
            // Phase 5: Gameplay Systems
            yield return InitializeSystem<EconomySystem>("EconomySystem");
            yield return InitializeSystem<CombatSystem>("CombatSystem");
            yield return InitializeSystem<VehicleSystem>("VehicleSystem");
            
            // Phase 6: UI & Analytics
            yield return InitializeSystem<UIManager>("UIManager");
            yield return InitializeSystem<AnalyticsSystem>("AnalyticsSystem");
            
            // Complete initialization
            _isInitialized = true;
            Debug.Log("[RVA] All Systems Initialized Successfully!");
            
            // Load saved game data
            yield return LoadGameData();
            
            // Transition to main menu
            SetState(GameState.MAIN_MENU);
            SceneManager.LoadScene("MainMenu");
        }

        private IEnumerator InitializeSystem<T>(string systemName) where T : SystemManager
        {
            Debug.Log($"[RVA] Initializing {systemName}...");
            
            // Find or create system
            T system = FindObjectOfType<T>();
            if (system == null)
            {
                GameObject systemObj = new GameObject(systemName);
                system = systemObj.AddComponent<T>();
                systemObj.transform.SetParent(transform);
            }
            
            // Initialize
            system.Initialize();
            
            // Wait for initialization
            float timeout = 5f;
            float elapsed = 0f;
            while (!system.IsInitialized && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (!system.IsInitialized)
            {
                Debug.LogError($"[RVA] CRITICAL: {systemName} failed to initialize!");
            }
            else
            {
                Debug.Log($"[RVA] {systemName} initialized successfully");
            }
        }

        // ==================== GAME LOOP ====================
        void Update()
        {
            if (!_isInitialized) return;
            
            _gameTime += Time.deltaTime;
            
            // Check for prayer time transitions
            if (enablePrayerTimes && _prayerSystem != null)
            {
                if (_prayerSystem.IsPrayerTimeApproaching() && _currentState == GameState.GAMEPLAY)
                {
                    OnPrayerTimeApproaching();
                }
            }
            
            // Mobile-specific input handling
            HandleMobileBackButton();
        }

        // ==================== STATE MANAGEMENT ====================
        public void SetState(GameState newState)
        {
            if (_currentState == newState) return;
            
            _previousState = _currentState;
            _currentState = newState;
            
            Debug.Log($"[RVA] Game State: {_previousState} -> {_currentState}");
            
            // State-specific actions
            switch (_currentState)
            {
                case GameState.MAIN_MENU:
                    Time.timeScale = 1f;
                    PauseAllGameSystems(false);
                    break;
                    
                case GameState.GAMEPLAY:
                    Time.timeScale = 1f;
                    ResumeAllGameSystems();
                    break;
                    
                case GameState.PAUSED:
                    Time.timeScale = 0f;
                    PauseAllGameSystems(true);
                    break;
                    
                case GameState.PRAYER_MODE:
                    EnterPrayerMode();
                    break;
                    
                case GameState.RESTARTING:
                    StartCoroutine(RestartGame());
                    break;
            }
            
            // Notify all systems of state change
            BroadcastStateChange(_currentState);
        }

        private void BroadcastStateChange(GameState state)
        {
            var allSystems = FindObjectsOfType<SystemManager>();
            foreach (var system in allSystems)
            {
                system.OnGameStateChanged(state);
            }
        }

        // ==================== SAVE/LOAD ====================
        private IEnumerator LoadGameData()
        {
            Debug.Log("[RVA] Loading saved game data...");
            
            if (_saveSystem != null)
            {
                yield return _saveSystem.LoadGame();
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
        }

        // ==================== PRAYER TIME HANDLING ====================
        private void OnPrayerTimeApproaching()
        {
            if (_currentState != GameState.GAMEPLAY) return;
            
            Debug.Log("[RVA] Prayer time approaching - showing notification");
            
            // Show subtle notification (player can ignore)
            _uiManager?.ShowPrayerNotification();
            
            // Auto-pause for prayer time if enabled in settings
            if (PlayerPrefs.GetInt("AutoPauseForPrayer", 0) == 1)
            {
                SetState(GameState.PRAYER_MODE);
            }
        }

        private void EnterPrayerMode()
        {
            Debug.Log("[RVA] Entering Prayer Mode");
            
            // Pause gameplay but keep UI active
            Time.timeScale = 0.1f; // Slow motion, not full pause
            
            // Show prayer time UI
            _uiManager?.ShowPrayerTimeInterface();
            
            // Option: Auto-return after prayer duration
            StartCoroutine(PrayerModeTimer());
        }

        private IEnumerator PrayerModeTimer()
        {
            yield return new WaitForSecondsRealtime(300f); // 5 minutes
            
            if (_currentState == GameState.PRAYER_MODE)
            {
                SetState(GameState.GAMEPLAY);
            }
        }

        // ==================== MOBILE-SPECIFIC ====================
        private void HandleMobileBackButton()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                switch (_currentState)
                {
                    case GameState.MAIN_MENU:
                        Application.Quit();
                        break;
                        
                    case GameState.GAMEPLAY:
                        SetState(GameState.PAUSED);
                        break;
                        
                    case GameState.PAUSED:
                        SetState(GameState.GAMEPLAY);
                        break;
                        
                    case GameState.DIALOGUE:
                        // Close dialogue if possible
                        DialogueSystem.Instance?.ForceClose();
                        break;
                }
            }
        }

        // ==================== SYSTEM CONTROL ====================
        private void PauseAllGameSystems(bool pauseUI = false)
        {
            var systems = FindObjectsOfType<MonoBehaviour>();
            foreach (var system in systems)
            {
                if (system is IPausable pausable)
                {
                    pausable.OnPause();
                }
            }
        }

        private void ResumeAllGameSystems()
        {
            var systems = FindObjectsOfType<MonoBehaviour>();
            foreach (var system in systems)
            {
                if (system is IPausable pausable)
                {
                    pausable.OnResume();
                }
            }
        }

        // ==================== RESTART ====================
        private IEnumerator RestartGame()
        {
            Debug.Log("[RVA] Restarting game...");
            
            // Save progress before restart
            SaveGame();
            
            // Reset state
            _gameTime = 0f;
            activeIslandIndex = 0;
            
            // Reload main menu
            SceneManager.LoadScene("MainMenu");
            SetState(GameState.MAIN_MENU);
            
            yield return null;
        }

        // ==================== CONFIGURATION ====================
        private void LoadGameConfiguration()
        {
            // Load from PlayerPrefs or default
            targetFrameRate = PlayerPrefs.GetInt("TargetFrameRate", 60);
            enableAdaptiveQuality = PlayerPrefs.GetInt("AdaptiveQuality", 1) == 1;
            enablePrayerTimes = PlayerPrefs.GetInt("EnablePrayerTimes", 1) == 1;
            
            // Validate island database
            if (islandDatabase == null)
            {
                islandDatabase = new IslandData[totalIslands];
            }
        }

        private void InitializeDefaultIslandData()
        {
            Debug.Log("[RVA] Initializing default island database...");
            
            islandDatabase = new IslandData[totalIslands];
            
            for (int i = 0; i < totalIslands; i++)
            {
                islandDatabase[i] = new IslandData
                {
                    islandID = i,
                    islandName = $"Island_{i:D2}",
                    gangPresence = new int[83], // 83 gangs
                    buildingCount = 0,
                    discovered = false,
                    controlPercentage = 0f
                };
            }
            
            // Mark Male' (Island 0) as discovered (starting island)
            if (islandDatabase.Length > 0)
            {
                islandDatabase[0].islandName = "Male'";
                islandDatabase[0].discovered = true;
            }
        }

        // ==================== UTILITIES ====================
        public void QuitGame()
        {
            Debug.Log("[RVA] Quitting game...");
            SaveGame();
            Application.Quit();
        }

        // ==================== DATA STRUCTURES ====================
        [System.Serializable]
        public class IslandData
        {
            public int islandID;
            public string islandName;
            public int[] gangPresence; // Gang ID -> Member count
            public int buildingCount;
            public bool discovered;
            public float controlPercentage;
            public Vector3 worldPosition;
        }

        // ==================== DEBUGGING ====================
        [ContextMenu("Force Initialize Systems")]
        private void ForceInitialize()
        {
            StartCoroutine(InitializeAllSystems());
        }

        [ContextMenu("Save Game Now")]
        private void DebugSave()
        {
            SaveGame();
        }

        [ContextMenu("Clear All Data")]
        private void ClearAllData()
        {
            PlayerPrefs.DeleteAll();
            Debug.Log("[RVA] All player data cleared!");
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
    public abstract class SystemManager : MonoBehaviour
    {
        protected bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        public abstract void Initialize();
        public abstract void OnGameStateChanged(MainGameManager.GameState newState);
        public abstract void OnPause();
        public abstract void OnResume();
    }
}
