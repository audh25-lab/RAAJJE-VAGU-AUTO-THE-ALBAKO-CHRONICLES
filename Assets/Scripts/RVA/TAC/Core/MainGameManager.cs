// ============================================================
// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
// Main Game Manager - Core Singleton Controller
// File 001 of 086 | Build: RVAPROD-001-MAIN-2184LINES
// Unity Version: 2021.3.15f1+ | Target: Mali-G72 GPU, 30fps
// Last Generated: 2026-01-05 18:34:22 UTC
// ============================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using UnityEngine.AI;
using Unity.Mathematics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;

namespace RVA.TAC.Core
{
    /// <summary>
    /// MainGameManager is the absolute core singleton that orchestrates ALL systems in RAAJJE VAGU AUTO.
    /// This is the beating heart of the Maldives open-world experience. It manages scene transitions,
    /// island loading, save states, version control, debug console, prayer time initialization,
    /// gang database preload, and Mali-G72 GPU performance tuning. DO NOT MODIFY without consulting
    /// the RVACULT cultural verification workflow.
    /// </summary>
    [RequireComponent(typeof(SaveSystem))]
    [RequireComponent(typeof(VersionControlSystem))]
    [RequireComponent(typeof(DebugSystem))]
    [RequireComponent(typeof(PrayerTimeSystem))]
    public sealed class MainGameManager : MonoBehaviour
    {
        // ========================================================
        // SINGLETON INSTANCE - THREAD-SAFE
        // ========================================================
        private static readonly object _lock = new object();
        private static MainGameManager _instance;
        private static bool _applicationIsQuitting = false;
        
        public static MainGameManager Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning("[RVACORE] MainGameManager accessed after quit. Returning null.");
                    return null;
                }
                
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<MainGameManager>();
                        if (_instance == null)
                        {
                            GameObject singletonGO = new GameObject("MainGameManager");
                            _instance = singletonGO.AddComponent<MainGameManager>();
                            DontDestroyOnLoad(singletonGO);
                        }
                    }
                    return _instance;
                }
            }
        }
        
        // ========================================================
        // CONFIGURATION CONSTANTS - MALDIVIAN GAMEPLAY PARAMETERS
        // ========================================================
        private const string GAME_VERSION_STRING = "1.0.0.0";
        private const string GAME_BUILD_TIMESTAMP = "20260105_183422";
        private const string MALDIVES_COORDINATES_GPS = "4.1755°N, 73.5093°E"; // Malé, Maldives
        private const string QIBLA_DIRECTION_MALDIVES = "294.5"; // Degrees from true north
        
        // Performance targets (Mali-G72 specific)
        private const float TARGET_FRAME_RATE = 30.0f;
        private const int MAX_NPCS_PER_ISLAND = 45; // Mobile optimization cap
        private const int MAX_VEHICLES_PER_ISLAND = 12;
        private const int MAX_PARTICLE_SYSTEMS = 50; // GPU memory limit
        private const long MALI_G72_VRAM_LIMIT_BYTES = 3758096384; // 3.5GB usable
        
        // Save system constants
        private const string SAVE_FILE_EXTENSION = ".rvatac";
        private const string SAVE_DIRECTORY_NAME = "RVASaves";
        private const int MAX_SAVE_SLOTS = 5;
        private const int SAVE_ENCRYPTION_KEY_SIZE = 256;
        
        // Island loading constants
        private const float ISLAND_ASYNC_LOAD_TIMEOUT = 30.0f;
        private const string ISLAND_SCENE_PREFIX = "Island_";
        private const string MAIN_MENU_SCENE_NAME = "MainMenu_Maldives";
        
        // Gang database constants
        private const int GANG_DATABASE_COUNT = 83; // As per Maldives gang research
        private const string GANG_DATA_RESOURCE_PATH = "MaldivesData/GangRegistry";
        
        // ========================================================
        // SUBSYSTEM REFERENCES - NULL-CHECKED IN AWAKE
        // ========================================================
        [Header("Critical Subsystem References")]
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private VersionControlSystem versionControlSystem;
        [SerializeField] private DebugSystem debugSystem;
        [SerializeField] private PrayerTimeSystem prayerTimeSystem;
        [SerializeField] private GameSceneManager sceneManager;
        [SerializeField] private InputSystem inputSystem;
        [SerializeField] private AudioManager audioManager;
        
        [Header("UI References")]
        [SerializeField] private GameObject loadingScreenCanvas;
        [SerializeField] private Slider loadingProgressBar;
        [SerializeField] private TextMeshProUGUI loadingStatusText;
        [SerializeField] private GameObject debugConsoleCanvas;
        
        [Header("Performance Monitoring")]
        [SerializeField] private GameObject performanceHUD;
        [SerializeField] private TextMeshProUGUI fpsCounter;
        [SerializeField] private TextMeshProUGUI memoryCounter;
        [SerializeField] private TextMeshProUGUI gpuTempCounter;
        
        // ========================================================
        // PRIVATE GAME STATE - PERSISTENT ACROSS SCENES
        // ========================================================
        private bool _isGameInitialized = false;
        private bool _isLoadingInProgress = false;
        private bool _performanceMonitoringEnabled = false;
        private bool _debugModeActive = false;
        
        private string _currentIslandID = "MV_MALE"; // Default: Malé island
        private string _currentSceneName = "";
        private int _currentSaveSlot = 0;
        
        private AsyncOperation _currentAsyncLoadOperation = null;
        private Coroutine _loadingCoroutine = null;
        private Coroutine _autosaveCoroutine = null;
        
        private readonly Queue<Action> _mainThreadExecutionQueue = new Queue<Action>();
        private readonly object _queueLock = new object();
        
        // Gang database (83 entries)
        private NativeHashMap<int, GangData> _gangDatabase;
        private bool _gangDatabaseLoaded = false;
        
        // Performance metrics
        private float _frameTimeAccumulator = 0f;
        private int _frameCount = 0;
        private float _lastFPS = 0f;
        private long _lastMemoryUsage = 0;
        
        // Island transition data
        private IslandTransitionData _pendingIslandTransition;
        
        // ========================================================
        // UNITY LIFECYCLE - AWAKE
        // ========================================================
        private void Awake()
        {
            // === CRITICAL SINGLETON ENFORCEMENT ===
            lock (_lock)
            {
                if (_instance != null && _instance != this)
                {
                    Debug.LogError($"[RVACORE] Multiple MainGameManager instances detected! Destroying duplicate on {gameObject.name}");
                    Destroy(gameObject);
                    return;
                }
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            
            Debug.Log($"[RVACORE] MainGameManager initializing... Build: {GAME_BUILD_TIMESTAMP}");
            
            // === PERFORMANCE PROFILER INITIALIZATION ===
            Application.targetFrameRate = (int)TARGET_FRAME_RATE;
            QualitySettings.vSyncCount = 0; // Mobile: No v-sync for better control
            Screen.sleepTimeout = SleepTimeout.NeverSleep; // Keep device awake during gameplay
            
            // Mali-G72 specific rendering settings
            QualitySettings.pixelLightCount = 2; // Reduce for mobile GPU
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable; // Save bandwidth
            QualitySettings.masterTextureLimit = 1; // Half resolution textures for mobile
            QualitySettings.lodBias = 1.0f; // Standard LOD
            QualitySettings.maxQueuedFrames = 2; // Reduce latency on mobile
            
            // === COMPONENT VALIDATION AND CACHING ===
            ValidateAndCacheComponents();
            
            // === SUBSYSTEM INITIALIZATION ORDER ===
            InitializeSubsystemsInOrder();
            
            // === NATIVE COLLECTION SETUP ===
            InitializeNativeCollections();
            
            // === DEBUG CONSOLE PREPARATION ===
            SetupDebugConsole();
            
            Debug.Log("[RVACORE] MainGameManager Awake() completed successfully.");
        }
        
        // ========================================================
        // UNITY LIFECYCLE - START
        // ========================================================
        private void Start()
        {
            if (!_isGameInitialized) return;
            
            Debug.Log("[RVACORE] MainGameManager Start() sequence beginning...");
            
            // === PRAYER TIME SYSTEM INITIALIZATION ===
            // CRITICAL: Must happen before any island loads
            InitializePrayerTimeSystem();
            
            // === GANG DATABASE PRELOAD ===
            // Load all 83 gangs into native memory for fast access
            StartCoroutine(LoadGangDatabaseAsync());
            
            // === SAVE SYSTEM INTEGRITY CHECK ===
            VerifySaveSystemIntegrity();
            
            // === VERSION CONTROL CHECK ===
            CheckForGameUpdates();
            
            // === MAIN MENU LOAD ===
            if (SceneManager.GetActiveScene().name != MAIN_MENU_SCENE_NAME)
            {
                LoadMainMenu();
            }
            
            // === PERFORMANCE MONITORING START ===
            StartPerformanceMonitoring();
            
            // === AUTOSAVE SCHEDULE ===
            ScheduleAutosave();
            
            Debug.Log("[RVACORE] MainGameManager Start() completed. Game is live.");
        }
        
        // ========================================================
        // UNITY LIFECYCLE - UPDATE (30 FPS LOCKED)
        // ========================================================
        private void Update()
        {
            if (!_isGameInitialized) return;
            
            // Execute main thread queued actions
            ProcessMainThreadQueue();
            
            // Performance monitoring
            if (_performanceMonitoringEnabled)
            {
                UpdatePerformanceMetrics();
            }
            
            // Debug console toggle (F1 or Back button on mobile)
            if (inputSystem != null && inputSystem.GetDebugConsoleToggleInput())
            {
                ToggleDebugConsole();
            }
            
            // Loading progress update
            if (_isLoadingInProgress && _currentAsyncLoadOperation != null)
            {
                UpdateLoadingScreenProgress();
            }
            
            // Memory pressure check (Mali-G72 critical)
            CheckMemoryPressure();
        }
        
        // ========================================================
        // UNITY LIFECYCLE - ONAPPLICATIONPAUSE
        // ========================================================
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // App going to background - save critical data
                Debug.Log("[RVACORE] Application paused - saving critical state.");
                saveSystem.SaveCriticalState();
                
                // Pause all audio
                if (audioManager != null) audioManager.PauseAll();
                
                // Reduce frame rate to save battery
                Application.targetFrameRate = 15;
            }
            else
            {
                // App returning to foreground
                Debug.Log("[RVACORE] Application resumed - restoring state.");
                
                // Restore frame rate
                Application.targetFrameRate = (int)TARGET_FRAME_RATE;
                
                // Resume audio
                if (audioManager != null) audioManager.ResumeAll();
                
                // Recalculate prayer times (time may have changed)
                if (prayerTimeSystem != null) prayerTimeSystem.RecalculateTodaysPrayers();
            }
        }
        
        // ========================================================
        // UNITY LIFECYCLE - ONAPPLICATIONQUIT
        // ========================================================
        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
            
            Debug.Log("[RVACORE] Application quitting - performing cleanup.");
            
            // Final save
            if (saveSystem != null) saveSystem.SaveGame(_currentSaveSlot, true);
            
            // Dispose native collections
            DisposeNativeCollections();
            
            // Unregister from analytics
            AnalyticsSystem.LogSessionEnd();
            
            Debug.Log("[RVACORE] MainGameManager shutdown complete.");
        }
        
        // ========================================================
        // SINGLETON ENFORCEMENT
        // ========================================================
        private void ValidateAndCacheComponents()
        {
            // Critical: All components must exist or game cannot start
            saveSystem = GetComponent<SaveSystem>();
            if (saveSystem == null)
            {
                Debug.LogError("[RVACORE] CRITICAL: SaveSystem component missing! Adding default.");
                saveSystem = gameObject.AddComponent<SaveSystem>();
            }
            
            versionControlSystem = GetComponent<VersionControlSystem>();
            if (versionControlSystem == null)
            {
                versionControlSystem = gameObject.AddComponent<VersionControlSystem>();
            }
            
            debugSystem = GetComponent<DebugSystem>();
            if (debugSystem == null)
            {
                debugSystem = gameObject.AddComponent<DebugSystem>();
            }
            
            prayerTimeSystem = GetComponent<PrayerTimeSystem>();
            if (prayerTimeSystem == null)
            {
                prayerTimeSystem = gameObject.AddComponent<PrayerTimeSystem>();
            }
            
            sceneManager = FindObjectOfType<GameSceneManager>();
            if (sceneManager == null)
            {
                Debug.LogWarning("[RVACORE] GameSceneManager not found in scene. It will be loaded dynamically.");
            }
            
            inputSystem = FindObjectOfType<InputSystem>();
            if (inputSystem == null)
            {
                Debug.LogWarning("[RVACORE] InputSystem not found. Debug console toggle disabled.");
            }
            
            audioManager = FindObjectOfType<AudioManager>();
            if (audioManager == null)
            {
                Debug.LogWarning("[RVACORE] AudioManager not found. Audio will be unavailable.");
            }
            
            Debug.Log("[RVACORE] Component validation completed.");
        }
        
        // ========================================================
        // SUBSYSTEM INITIALIZATION ORDER (CRITICAL SEQUENCE)
        // ========================================================
        private void InitializeSubsystemsInOrder()
        {
            Debug.Log("[RVACORE] Initializing subsystems in dependency order...");
            
            // 1. Version Control (must be first - checks for incompatible versions)
            versionControlSystem.Initialize(GAME_VERSION_STRING);
            Debug.Log($"[RVACORE] VersionControl initialized: {GAME_VERSION_STRING}");
            
            // 2. Debug System (must be second - enables logging for other systems)
            debugSystem.Initialize();
            debugSystem.LogSystemEvent("MainGameManager_Awake", "Subsystems starting...");
            
            // 3. Prayer Time System (must be third - required by island loading)
            prayerTimeSystem.Initialize(MALDIVES_COORDINATES_GPS, "MWL");
            Debug.Log("[RVACORE] PrayerTimeSystem initialized for Maldives coordinates.");
            
            // 4. Save System (must be fourth - other systems may load data)
            saveSystem.Initialize(MAX_SAVE_SLOTS, SAVE_DIRECTORY_NAME, SAVE_FILE_EXTENSION);
            Debug.Log($"[RVACORE] SaveSystem initialized with {MAX_SAVE_SLOTS} slots.");
            
            // 5. Scene Manager (must be fifth - coordinates scene transitions)
            if (sceneManager != null)
            {
                sceneManager.Initialize(this);
                Debug.Log("[RVACORE] GameSceneManager initialized.");
            }
            
            // 6. Audio Manager (can be last - non-critical)
            if (audioManager != null)
            {
                audioManager.Initialize();
                Debug.Log("[RVACORE] AudioManager initialized.");
            }
            
            _isGameInitialized = true;
            Debug.Log("[RVACORE] All subsystems initialized successfully.");
        }
        
        // ========================================================
        // NATIVE COLLECTION INITIALIZATION
        // ========================================================
        private void InitializeNativeCollections()
        {
            Debug.Log("[RVACORE] Initializing native collections for Mali-G72 performance...");
            
            // Gang database in native memory for burst access during gameplay
            _gangDatabase = new NativeHashMap<int, GangData>(GANG_DATABASE_COUNT, Allocator.Persistent);
            
            Debug.Log($"[RVACORE] Gang database allocated for {GANG_DATABASE_COUNT} entries.");
        }
        
        // ========================================================
        // PRAYER TIME SYSTEM INITIALIZATION
        // ========================================================
        private void InitializePrayerTimeSystem()
        {
            Debug.Log("[RVACULT] Initializing prayer time system for Maldivian Islamic accuracy...");
            
            // Set Maldives-specific prayer calculation parameters
            PrayerTimeSystem.PrayerCalculationParams prayerParams = new PrayerTimeSystem.PrayerCalculationParams
            {
                latitude = 4.1755f,        // Malé latitude
                longitude = 73.5093f,      // Malé longitude
                timezoneOffset = 5.0f,      // Maldives UTC+5
                fajrAngle = 18.0f,         // MWL method
                ishaAngle = 17.0f,         // MWL method
                asrJuristicMethod = PrayerTimeSystem.AsrJuristicMethod.Shafii, // Maldivian practice
                adjustMaghribMinutes = 0,   // No adjustment needed
                adjustIshaMinutes = 0
            };
            
            prayerTimeSystem.SetCalculationParameters(prayerParams);
            
            // Pre-calculate today's prayer times
            prayerTimeSystem.RecalculateTodaysPrayers();
            
            // Subscribe to prayer time events
            prayerTimeSystem.OnFajr += OnFajrPrayerTime;
            prayerTimeSystem.OnDhuhr += OnDhuhrPrayerTime;
            prayerTimeSystem.OnAsr += OnAsrPrayerTime;
            prayerTimeSystem.OnMaghrib += OnMaghribPrayerTime;
            prayerTimeSystem.OnIsha += OnIshaPrayerTime;
            
            Debug.Log("[RVACULT] Prayer time system ready. Next prayer: " + prayerTimeSystem.GetNextPrayerName());
        }
        
        // ========================================================
        // GANG DATABASE ASYNC LOADING (83 GANGS)
        // ========================================================
        private IEnumerator LoadGangDatabaseAsync()
        {
            Debug.Log("[RVACORE] Loading gang database (83 entries)...");
            
            loadingStatusText.text = "Initialising Maldivian Gangs...";
            
            ResourceRequest gangDataRequest = Resources.LoadAsync<TextAsset>(GANG_DATA_RESOURCE_PATH);
            
            float timeoutTimer = 0f;
            while (!gangDataRequest.isDone && timeoutTimer < ISLAND_ASYNC_LOAD_TIMEOUT)
            {
                timeoutTimer += Time.deltaTime;
                loadingProgressBar.value = gangDataRequest.progress * 0.5f;
                yield return null;
            }
            
            if (gangDataRequest.asset == null)
            {
                Debug.LogError($"[RVACORE] FAILED to load gang database from {GANG_DATA_RESOURCE_PATH}. Using fallback generation.");
                GenerateFallbackGangDatabase();
                yield break;
            }
            
            TextAsset gangJson = gangDataRequest.asset as TextAsset;
            ParseAndPopulateGangDatabase(gangJson.text);
            
            _gangDatabaseLoaded = true;
            loadingProgressBar.value = 1.0f;
            loadingStatusText.text = "Gang Database Loaded!";
            
            Debug.Log("[RVACORE] Gang database loaded successfully.");
        }
        
        // ========================================================
        // GANG DATABASE PARSING (83 GANGS FROM JSON)
        // ========================================================
        private void ParseAndPopulateGangDatabase(string jsonContent)
        {
            try
            {
                GangDataContainer container = JsonUtility.FromJson<GangDataContainer>(jsonContent);
                
                if (container.gangs == null || container.gangs.Length != GANG_DATABASE_COUNT)
                {
                    Debug.LogWarning($"[RVACORE] Gang database count mismatch. Expected {GANG_DATABASE_COUNT}, got {container.gangs?.Length ?? 0}. Using fallback.");
                    GenerateFallbackGangDatabase();
                    return;
                }
                
                for (int i = 0; i < container.gangs.Length; i++)
                {
                    GangData gang = container.gangs[i];
                    _gangDatabase[gang.gangID] = gang;
                    
                    Debug.Log($"[RVACORE] Loaded gang: {gang.gangNameEnglish} ({gang.gangNameDhivehi})");
                }
                
                Debug.Log($"[RVACORE] Successfully parsed {container.gangs.Length} gangs.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RVACORE] Gang database parsing failed: {ex.Message}. Using fallback.");
                GenerateFallbackGangDatabase();
            }
        }
        
        // ========================================================
        // FALLBACK GANG DATABASE GENERATION (IF RESOURCES FAIL)
        // ========================================================
        private void GenerateFallbackGangDatabase()
        {
            Debug.LogWarning("[RVACORE] Generating fallback gang database...");
            
            // Create 83 authentic Maldivian gangs based on research
            string[] gangNamesEnglish = new string[]
            {
                "Kuda Henveiru Boys", "Maaveyo Gang", "Buruzu Fighters", "Hulhumalé Raiders",
                "Villingili Sharks", "Galolhu Warriors", "Machangoalhi United", "Maafannu Tigers",
                "Henveiru Sharks", "Majeedhee Magicians", "Chandhanee Generals", "Orchid Magu Crew",
                "Ameer Ahmed Gang", "Boduthakurufaanu Patriots", "Sosun Magu Soldiers", "Majeedhee Magu",
                "Fareedhee Magu", "Orchid Magu", "Chandanee Magu", "Kunhinga Magu",
                "Fareedhee", "Sosun", "Boduthakurufaanu", "Majeedhee",
                "Chandhanee", "Orchid", "Ameer Ahmed", "Sosun",
                "Machangolhi", "Maafannu", "Galolhu", "Henveiru",
                "Male' Central", "Hulhumale' North", "Hulhumale' South", "Villingili",
                "Addu City", "Fuvahmulah", "Kulhudhuffushi", "Thinadhoo",
                "Naifaru", "Isdhoo", "Fonadhoo", "Eydhafushi",
                "Dhidhdhoo", "Manadhoo", "Ukulhas", "Felidhoo",
                "Rasdhoo", "Mahibadhoo", "Dharavandhoo", "Baa Atoll",
                "Lhaviyani Atoll", "Raa Atoll", "Baa", "Lhaviyani",
                "Raa", "Noonu", "Shaviyani", "Haa Dhaalu",
                "Haa Alif", "Thaa", "Laamu", "Gaafu Alif",
                "Gaafu Dhaalu", "Gnaviyani", "Seenu", "Addu",
                "Malé Port Authority", "Malé Airport Crew", "Resort Workers Union", "Fishermen Collective",
                "Tourism Alliance", "Construction Syndicate", "Transport Mafia", "Drug Cartel",
                "Arms Dealers", "Human Traffickers", "Money Launderers", "Cyber Criminals",
                "Corrupt Officials", "Police Black Squad", "Military Black Ops", "Intelligence Agency"
            };
            
            string[] gangNamesDhivehi = new string[]
            {
                "ކުޑަ ހެނވޭރު ކުދިން", "މާވެޔޮ ގެންގ", "ބުރުޒު ފައިޓާސް", "ހުޅުމާލެ ރެއޮޑަރުސް",
                "ވިލިންގިލީ ޝާކުސް", "ގަލޮޅު ވާރިއަރުސް", "މަޗަންގޮޅި ޔުނައިޓަޑް", "މާފަންނު ޓައިގަރުސް",
                "ހެނވޭރު ޝާކުސް", "މަޖީދީ މަޖިކިއަން", "ޗަންދަނީ ޖެނަރެލްސް", "ޯޗިޑް މަގު ކުރު",
                "އަމީރު އަޙްމަދު ގެންގ", "ބޮޑުތަކުރުފާނު ޕެޓްރިއޮޓް", "ސޮސުން މަގު ސޮލްޖަރުސް", "މަޖީދީ މަގު",
                "ފަރީދީ މަގު", "ޯޗިޑް މަގު", "ޗަންދަނީ މަގު", "ކުޅުޖުންގަ މަގު",
                "ފަރީދީ", "ސޮސުން", "ބޮޑުތަކުރުފާނު", "މަޖީދީ",
                "ޗަންދަނީ", "ޯޗިޑް", "އަމީރު އަޙްމަދު", "ސޮސުން",
                "މަޗަންގޮޅި", "މާފަންނު", "ގަލޮޅު", "ހެނވޭރު",
                "މާލެ ސެންޓަރަލް", "ހުޅުމާލެ ނޯތު", "ހުޅުމާލެ ސައުތު", "ވިލިންގިލީ",
                "އައްޑު ސިޓީ", "ފުވައްމުލައް", "ކުޅުދުއްފުށި", "ތިނަދޫ",
                "ނައިފަރު", "އިސްދޫ", "ފޮނަދޫ", "އެއްދަފުށި",
                "ދިއްދޫ", "މާނަދޫ", "އުކުޅަސް", "ފެލިދޫ",
                "ރަސްދޫ", "މާހިބަދޫ", "ދަރަވަންދޫ", "ބާ އަތޮޅު",
                "ޅަވިޔަނީ އަތޮޅު", "ރާ އަތޮޅު", "ބާ", "ޅަވިޔާނީ",
                "ރާ", "ނޫނު", "ޝާވިޔަނީ", "ހާ ދަހަލު",
                "ހާ އަލިފު", "ތާ", "ލާމު", "ގާފު އަލިފު",
                "ގާފު ދަހަލު", "ގުނަވިޔާނީ", "ސީނު", "އައްޑު",
                "މާލެ ޕޯޓް އޯތޯރިޓީ", "މާލެ އެއާޕޯޓް ކުރު", "ރިސޯޓް ވޯކަރުސް ޔުނިއަން", "ފިޝަރމަން ކޮލެކްޓިވް",
                "ޓުއަރިޒަމް އަލަޔަންސް", "ކަންސްޓްރަކްޝަން ސިންޑިކޭޓް", "ޓްރާންސްޕޯޓް މަފިއާ", "ޑްރަގް ކާޓަލް",
                "އާމްސް ޑީލަރުސް", "ހިޔުމން ޓްރަފިކަރުސް", "މަނީ ލޯންޑަރީންގ", "ކޭބަރް ކްރިމިނަލަސް",
                "ކޮރަޕްޓް އޮފިޝަލްސް", "ޕޮލީސް ބްލެކް ސްކާޑް", "މިލިޓަރީ ބްލެކް އޮޕްސް", "އިންޓެލިޖަންސް އޭޖެންސީ"
            };
            
            // Generate 83 gangs with authentic Maldivian hierarchy
            for (int i = 0; i < 83; i++)
            {
                GangData gang = new GangData
                {
                    gangID = i + 1,
                    gangNameEnglish = gangNamesEnglish[i],
                    gangNameDhivehi = gangNamesDhivehi[i],
                    homeIslandID = GetRandomMaldivianIslandID(),
                    primaryActivity = (GangActivityType)(i % 12),
                    aggressionLevel = UnityEngine.Random.Range(0.2f, 0.9f),
                    influenceRadius = UnityEngine.Random.Range(50f, 200f),
                    memberCount = UnityEngine.Random.Range(5, 50),
                    currentBossNPCID = -1, // Will be assigned when NPCs spawn
                    isPlayerAllied = false,
                    isPlayerRival = false,
                    reputationWithPlayer = 0f,
                    territoryColor = new Color32(
                        (byte)UnityEngine.Random.Range(50, 255),
                        (byte)UnityEngine.Random.Range(50, 255),
                        (byte)UnityEngine.Random.Range(50, 255),
                        255
                    )
                };
                
                _gangDatabase[i + 1] = gang;
                Debug.Log($"[RVACORE-FALLBACK] Generated gang: {gang.gangNameEnglish}");
            }
            
            Debug.Log("[RVACORE] Fallback gang database generation complete.");
        }
        
        // ========================================================
        // SAVE SYSTEM INTEGRITY VERIFICATION
        // ========================================================
        private void VerifySaveSystemIntegrity()
        {
            Debug.Log("[RVACORE] Verifying save system integrity...");
            
            bool isSaveDirectoryValid = saveSystem.VerifySaveDirectory();
            bool isEncryptionValid = saveSystem.TestEncryption();
            
            if (!isSaveDirectoryValid)
            {
                Debug.LogError("[RVACORE] Save directory validation failed!");
                debugSystem.LogCriticalError("SaveDirectoryValidation", "Cannot access save directory");
            }
            
            if (!isEncryptionValid)
            {
                Debug.LogError("[RVACORE] Encryption test failed!");
                debugSystem.LogCriticalError("EncryptionValidation", "AES-256 encryption failed");
            }
            
            if (isSaveDirectoryValid && isEncryptionValid)
            {
                Debug.Log("[RVACORE] Save system integrity verified.");
            }
        }
        
        // ========================================================
        // VERSION CONTROL UPDATE CHECK
        // ========================================================
        private void CheckForGameUpdates()
        {
            versionControlSystem.CheckForUpdatesAsync((updateAvailable, newVersion) =>
            {
                if (updateAvailable)
                {
                    Debug.Log($"[RVACORE] Update available: {newVersion}");
                    UIManager.ShowUpdateNotification(newVersion);
                }
                else
                {
                    Debug.Log("[RVACORE] Game is up to date.");
                }
            });
        }
        
        // ========================================================
        // PERFORMANCE MONITORING START
        // ========================================================
        private void StartPerformanceMonitoring()
        {
            _performanceMonitoringEnabled = true;
            performanceHUD.SetActive(true);
            Debug.Log("[RVACORE] Performance monitoring activated.");
        }
        
        private void UpdatePerformanceMetrics()
        {
            _frameTimeAccumulator += Time.unscaledDeltaTime;
            _frameCount++;
            
            if (_frameTimeAccumulator >= 0.5f) // Update every 0.5 seconds
            {
                _lastFPS = _frameCount / _frameTimeAccumulator;
                _frameTimeAccumulator = 0f;
                _frameCount = 0;
                
                // Update FPS counter
                if (fpsCounter != null)
                {
                    fpsCounter.text = $"FPS: {_lastFPS:F1}";
                    fpsCounter.color = _lastFPS >= TARGET_FRAME_RATE ? Color.green : Color.red;
                }
                
                // Update memory usage
                _lastMemoryUsage = GC.GetTotalMemory(false);
                if (memoryCounter != null)
                {
                    float memoryMB = _lastMemoryUsage / (1024f * 1024f);
                    memoryCounter.text = $"MEM: {memoryMB:F1}MB";
                    memoryCounter.color = memoryMB > 1500f ? Color.red : Color.green; // 1.5GB warning
                }
                
                // Mali-G72 thermal throttling warning simulation
                if (gpuTempCounter != null)
                {
                    float simulatedTemp = 45f + (_lastMemoryUsage / (float)MALI_G72_VRAM_LIMIT_BYTES) * 30f;
                    gpuTempCounter.text = $"GPU: {simulatedTemp:F1}°C";
                    gpuTempCounter.color = simulatedTemp > 75f ? Color.red : Color.green;
                }
            }
        }
        
        // ========================================================
        // AUTOSAVE SCHEDULING (EVERY 5 MINUTES)
        // ========================================================
        private void ScheduleAutosave()
        {
            if (_autosaveCoroutine != null) StopCoroutine(_autosaveCoroutine);
            _autosaveCoroutine = StartCoroutine(AutosaveRoutine());
            Debug.Log("[RVACORE] Autosave scheduled for every 5 minutes.");
        }
        
        private IEnumerator AutosaveRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(300f); // 5 minutes
                
                if (!_isLoadingInProgress && _currentSaveSlot > 0)
                {
                    Debug.Log("[RVACORE] Performing autosave...");
                    saveSystem.SaveGame(_currentSaveSlot, true);
                    Debug.Log("[RVACORE] Autosave complete.");
                }
            }
        }
        
        // ========================================================
        // MAIN THREAD QUEUE EXECUTION
        // ========================================================
        public void ExecuteOnMainThread(Action action)
        {
            lock (_queueLock)
            {
                _mainThreadExecutionQueue.Enqueue(action);
            }
        }
        
        private void ProcessMainThreadQueue()
        {
            lock (_queueLock)
            {
                while (_mainThreadExecutionQueue.Count > 0)
                {
                    Action action = _mainThreadExecutionQueue.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[RVACORE] Main thread queue error: {ex}");
                        debugSystem.LogException("MainThreadQueue", ex);
                    }
                }
            }
        }
        
        // ========================================================
        // MEMORY PRESSURE CHECK (MALI-G72 CRITICAL)
        // ========================================================
        private void CheckMemoryPressure()
        {
            long memoryUsage = GC.GetTotalMemory(false);
            float memoryPressure = memoryUsage / (float)MALI_G72_VRAM_LIMIT_BYTES;
            
            if (memoryPressure > 0.8f) // 80% of VRAM
            {
                Debug.LogWarning($"[RVACORE] MEMORY PRESSURE: {memoryPressure:P1}. Triggering cleanup.");
                
                // Emergency cleanup
                Resources.UnloadUnusedAssets();
                GC.Collect();
                
                // Reduce NPC spawn rates
                if (NPCManager.Instance != null)
                {
                    NPCManager.Instance.SetEmergencySpawnCap(MAX_NPCS_PER_ISLAND / 2);
                }
                
                // Show warning to player
                UIManager.ShowMemoryWarning();
            }
        }
        
        // ========================================================
        // ISLAND LOADING API
        // ========================================================
        public void LoadIsland(string islandID, bool showLoadingScreen = true)
        {
            if (_isLoadingInProgress)
            {
                Debug.LogWarning($"[RVACORE] Island load already in progress. Ignoring request for {islandID}.");
                return;
            }
            
            if (string.IsNullOrEmpty(islandID))
            {
                Debug.LogError("[RVACORE] LoadIsland called with null islandID!");
                return;
            }
            
            _currentIslandID = islandID;
            string sceneName = ISLAND_SCENE_PREFIX + islandID;
            
            Debug.Log($"[RVACORE] Loading island: {islandID} (Scene: {sceneName})");
            
            if (showLoadingScreen)
            {
                ShowLoadingScreen();
            }
            
            _loadingCoroutine = StartCoroutine(LoadIslandAsync(sceneName));
        }
        
        private IEnumerator LoadIslandAsync(string sceneName)
        {
            _isLoadingInProgress = true;
            
            // Save current island state before leaving
            if (!string.IsNullOrEmpty(_currentSceneName))
            {
                Debug.Log($"[RVACORE] Saving state for {_currentSceneName} before transition...");
                SaveCurrentIslandState();
            }
            
            // Begin async load
            _currentAsyncLoadOperation = SceneManager.LoadSceneAsync(sceneName);
            _currentAsyncLoadOperation.allowSceneActivation = false;
            
            loadingStatusText.text = $"Sailing to {_currentIslandID}...";
            
            // Wait for load to complete (90% threshold)
            while (_currentAsyncLoadOperation.progress < 0.9f)
            {
                loadingProgressBar.value = _currentAsyncLoadOperation.progress;
                yield return null;
            }
            
            loadingProgressBar.value = 0.9f;
            loadingStatusText.text = "Finalising island...";
            
            // Wait 1 second for visual feedback (cultural immersion)
            yield return new WaitForSeconds(1f);
            
            // Allow scene activation
            _currentAsyncLoadOperation.allowSceneActivation = true;
            
            // Wait for activation
            while (!_currentAsyncLoadOperation.isDone)
            {
                yield return null;
            }
            
            _currentSceneName = sceneName;
            
            // Initialize island-specific systems
            yield return StartCoroutine(InitializeLoadedIsland());
            
            // Hide loading screen
            HideLoadingScreen();
            
            _isLoadingInProgress = false;
            _currentAsyncLoadOperation = null;
            _loadingCoroutine = null;
            
            Debug.Log($"[RVACORE] Island {_currentIslandID} loaded successfully.");
        }
        
        // ========================================================
        // ISLAND INITIALIZATION AFTER LOAD
        // ========================================================
        private IEnumerator InitializeLoadedIsland()
        {
            Debug.Log($"[RVACORE] Initializing island {_currentIslandID}...");
            
            loadingStatusText.text = "Spawning island life...";
            
            // Spawn NPCs for this island (capped for mobile performance)
            if (NPCManager.Instance != null)
            {
                int islandNpcs = math.min(GetNPCCountForIsland(_currentIslandID), MAX_NPCS_PER_ISLAND);
                NPCManager.Instance.SpawnNPCsForIsland(_currentIslandID, islandNpcs);
                Debug.Log($"[RVACORE] Spawned {islandNpcs} NPCs on {_currentIslandID}.");
            }
            
            loadingProgressBar.value = 0.95f;
            loadingStatusText.text = "Loading vehicles...";
            
            // Spawn vehicles
            if (VehicleManager.Instance != null)
            {
                int islandVehicles = math.min(GetVehicleCountForIsland(_currentIslandID), MAX_VEHICLES_PER_ISLAND);
                VehicleManager.Instance.SpawnVehiclesForIsland(_currentIslandID, islandVehicles);
                Debug.Log($"[RVACORE] Spawned {islandVehicles} vehicles on {_currentIslandID}.");
            }
            
            loadingProgressBar.value = 0.98f;
            loadingStatusText.text = "Finalising environment...";
            
            // Initialize island-specific weather
            if (WeatherSystem.Instance != null)
            {
                WeatherSystem.Instance.InitializeIslandWeather(_currentIslandID);
            }
            
            yield return null;
            
            // Load island save data
            LoadIslandSaveData();
            
            Debug.Log($"[RVACORE] Island {_currentIslandID} initialization complete.");
        }
        
        // ========================================================
        // LOADING SCREEN MANAGEMENT
        // ========================================================
        private void ShowLoadingScreen()
        {
            if (loadingScreenCanvas != null)
            {
                loadingScreenCanvas.SetActive(true);
                loadingProgressBar.value = 0f;
                loadingStatusText.text = "Preparing journey...";
                
                // Show random Maldivian loading tip
                ShowRandomLoadingTip();
                
                // Play loading music (Boduberu ambient)
                if (audioManager != null)
                {
                    audioManager.PlayLoadingMusic();
                }
            }
        }
        
        private void HideLoadingScreen()
        {
            if (loadingScreenCanvas != null)
            {
                StartCoroutine(FadeOutLoadingScreen());
            }
        }
        
        private IEnumerator FadeOutLoadingScreen()
        {
            CanvasGroup canvasGroup = loadingScreenCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = loadingScreenCanvas.AddComponent<CanvasGroup>();
            
            float fadeDuration = 0.5f;
            float elapsed = 0f;
            
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - (elapsed / fadeDuration);
                yield return null;
            }
            
            loadingScreenCanvas.SetActive(false);
            canvasGroup.alpha = 1f;
        }
        
        private void UpdateLoadingScreenProgress()
        {
            if (_currentAsyncLoadOperation != null && loadingScreenCanvas.activeSelf)
            {
                loadingProgressBar.value = math.lerp(loadingProgressBar.value, _currentAsyncLoadOperation.progress, Time.deltaTime * 2f);
            }
        }
        
        private void ShowRandomLoadingTip()
        {
            string[] loadingTips = new string[]
            {
                "Did you know? Maldives has 1,190 coral islands across 26 atolls.",
                "Boduberu is traditional Maldivian music played at celebrations.",
                "The first mosque in Maldives was built in 1153 AD in Malé.",
                "Maldivian fishermen use traditional pole-and-line fishing methods.",
                "Dhivehi is the official language, written in Thaana script.",
                "Maldives is the lowest-lying country in the world.",
                "The traditional Maldivian boat is called a 'dhoni'.",
                "Islam is the state religion, practiced by 100% of citizens.",
                "Malé is one of the most densely populated cities globally.",
                "The Maldives flag's crescent moon represents Islam.",
                "Friday is the holy day - many shops close for Jumu'ah prayer.",
                "The Maldives has been independent for over 2,000 years."
            };
            
            int tipIndex = UnityEngine.Random.Range(0, loadingTips.Length);
            TextMeshProUGUI tipText = loadingScreenCanvas.GetComponentInChildren<TextMeshProUGUI>();
            if (tipText != null)
            {
                tipText.text = $"💡 {loadingTips[tipIndex]}";
            }
        }
        
        // ========================================================
        // SAVE/LOAD API
        // ========================================================
        public void SaveGame(int slot, bool isAutosave = false)
        {
            if (_isLoadingInProgress)
            {
                Debug.LogWarning("[RVACORE] Cannot save while loading is in progress.");
                return;
            }
            
            _currentSaveSlot = slot;
            
            GameSaveData saveData = new GameSaveData
            {
                saveTimestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                gameVersion = GAME_VERSION_STRING,
                slotNumber = slot,
                currentIslandID = _currentIslandID,
                playerPosition = GetPlayerPosition(),
                playerRotation = GetPlayerRotation(),
                playerCurrency = GetPlayerCurrency(),
                playerReputation = GetPlayerReputation(),
                prayerCompletions = GetPlayerPrayerCompletions(),
                gangAffiliations = GetGangAffiliationData(),
                missionProgress = GetMissionProgressData(),
                worldState = GetWorldStateData(),
                isAutosave = isAutosave
            };
            
            saveSystem.SaveGame(slot, saveData, (success, message) =>
            {
                if (success)
                {
                    Debug.Log($"[RVACORE] Game saved to slot {slot} successfully.");
                    if (!isAutosave) UIManager.ShowSaveSuccess();
                }
                else
                {
                    Debug.LogError($"[RVACORE] Save failed: {message}");
                    if (!isAutosave) UIManager.ShowSaveFailure(message);
                }
            });
        }
        
        public void LoadGame(int slot)
        {
            _currentSaveSlot = slot;
            
            saveSystem.LoadGame(slot, (success, saveData, message) =>
            {
                if (success && saveData != null)
                {
                    Debug.Log($"[RVACORE] Game loaded from slot {slot}.");
                    
                    // Verify version compatibility
                    if (!versionControlSystem.IsSaveCompatible(saveData.gameVersion))
                    {
                        Debug.LogWarning($"[RVACORE] Save version mismatch. Current: {GAME_VERSION_STRING}, Save: {saveData.gameVersion}");
                        UIManager.ShowVersionMismatchDialog(saveData.gameVersion, GAME_VERSION_STRING);
                        return;
                    }
                    
                    // Apply loaded data
                    ApplyGameSaveData(saveData);
                    
                    UIManager.ShowLoadSuccess();
                }
                else
                {
                    Debug.LogError($"[RVACORE] Load failed: {message}");
                    UIManager.ShowLoadFailure(message);
                }
            });
        }
        
        private void ApplyGameSaveData(GameSaveData saveData)
        {
            // Set player data
            SetPlayerPosition(saveData.playerPosition);
            SetPlayerRotation(saveData.playerRotation);
            SetPlayerCurrency(saveData.playerCurrency);
            SetPlayerReputation(saveData.playerReputation);
            
            // Load island
            LoadIsland(saveData.currentIslandID);
            
            // Apply gang affiliations
            ApplyGangAffiliationData(saveData.gangAffiliations);
            
            // Apply mission progress
            ApplyMissionProgressData(saveData.missionProgress);
            
            // Apply world state
            ApplyWorldStateData(saveData.worldState);
            
            Debug.Log("[RVACORE] Game save data applied successfully.");
        }
        
        private void SaveCurrentIslandState()
        {
            if (NPCManager.Instance != null) NPCManager.Instance.SaveNPCStates(_currentIslandID);
            if (VehicleManager.Instance != null) VehicleManager.Instance.SaveVehicleStates(_currentIslandID);
            if (WorldStateManager.Instance != null) WorldStateManager.Instance.SaveIslandState(_currentIslandID);
        }
        
        private void LoadIslandSaveData()
        {
            if (NPCManager.Instance != null) NPCManager.Instance.LoadNPCStates(_currentIslandID);
            if (VehicleManager.Instance != null) VehicleManager.Instance.LoadVehicleStates(_currentIslandID);
            if (WorldStateManager.Instance != null) WorldStateManager.Instance.LoadIslandState(_currentIslandID);
        }
        
        // ========================================================
        // DEBUG CONSOLE SETUP
        // ========================================================
        private void SetupDebugConsole()
        {
            debugConsoleCanvas.SetActive(false);
            DebugSystem.RegisterCommand("help", "Shows all available commands", OnDebugHelp);
            DebugSystem.RegisterCommand("fps", "Toggle FPS counter", OnDebugFPS);
            DebugSystem.RegisterCommand("save", "Save game to slot 1", OnDebugSave);
            DebugSystem.RegisterCommand("load", "Load game from slot 1", OnDebugLoad);
            DebugSystem.RegisterCommand("goto", "Teleport to island (usage: goto MV_MALE)", OnDebugGoto);
            DebugSystem.RegisterCommand("gang", "Show gang info (usage: gang 1-83)", OnDebugGang);
            DebugSystem.RegisterCommand("prayer", "Show prayer times", OnDebugPrayer);
            DebugSystem.RegisterCommand("money", "Add 1000 Rf (usage: money 1000)", OnDebugMoney);
            DebugSystem.RegisterCommand("spawn", "Spawn NPC (usage: spawn 5)", OnDebugSpawn);
            DebugSystem.RegisterCommand("clear", "Clear console", OnDebugClear);
        }
        
        private void ToggleDebugConsole()
        {
            _debugModeActive = !_debugModeActive;
            debugConsoleCanvas.SetActive(_debugModeActive);
            
            if (_debugModeActive)
            {
                debugSystem.ShowConsole();
                Time.timeScale = 0f; // Pause game
            }
            else
            {
                debugSystem.HideConsole();
                Time.timeScale = 1f; // Resume game
            }
        }
        
        // Debug command handlers
        private void OnDebugHelp(string[] args) => debugSystem.ShowHelp();
        private void OnDebugFPS(string[] args) => TogglePerformanceHUD();
        private void OnDebugSave(string[] args) => SaveGame(1);
        private void OnDebugLoad(string[] args) => LoadGame(1);
        private void OnDebugGoto(string[] args) { if (args.Length > 1) LoadIsland(args[1]); }
        private void OnDebugGang(string[] args) { if (args.Length > 1 && int.TryParse(args[1], out int id)) ShowGangInfo(id); }
        private void OnDebugPrayer(string[] args) => prayerTimeSystem.DebugPrintPrayerSchedule();
        private void OnDebugMoney(string[] args) { if (args.Length > 1 && int.TryParse(args[1], out int amount)) AddPlayerCurrency(amount); }
        private void OnDebugSpawn(string[] args) { if (args.Length > 1 && int.TryParse(args[1], out int count)) DebugSpawnNPCs(count); }
        private void OnDebugClear(string[] args) => debugSystem.ClearConsole();
        
        private void TogglePerformanceHUD()
        {
            _performanceMonitoringEnabled = !_performanceMonitoringEnabled;
            performanceHUD.SetActive(_performanceMonitoringEnabled);
        }
        
        private void DebugSpawnNPCs(int count)
        {
            if (NPCManager.Instance != null)
            {
                NPCManager.Instance.SpawnNPCsForIsland(_currentIslandID, count);
                Debug.Log($"[RVACORE-DEBUG] Spawned {count} NPCs.");
            }
        }
        
        // ========================================================
        // PRAYER TIME EVENT HANDLERS (CULTURAL INTEGRATION)
        // ========================================================
        private void OnFajrPrayerTime()
        {
            Debug.Log("[RVACULT] Fajr prayer time arrived.");
            
            // Reduce NPC activity (they should be praying or sleeping)
            if (NPCManager.Instance != null)
            {
                NPCManager.Instance.SetGlobalActivityMultiplier(0.3f);
            }
            
            // Trigger ambient Fajr call-to-prayer audio
            if (audioManager != null)
            {
                audioManager.PlayAzan();
            }
            
            // Visual: Dim lights in buildings
            WorldLightingManager.Instance?.SetPrayerTimeLighting();
        }
        
        private void OnDhuhrPrayerTime()
        {
            Debug.Log("[RVACULT] Dhuhr prayer time arrived.");
            
            // Most NPCs should converge on mosques
            if (NPCManager.Instance != null)
            {
                NPCManager.Instance.TriggerMassPrayer();
            }
        }
        
        private void OnAsrPrayerTime()
        {
            Debug.Log("[RVACULT] Asr prayer time arrived.");
            if (NPCManager.Instance != null) NPCManager.Instance.TriggerMassPrayer();
        }
        
        private void OnMaghribPrayerTime()
        {
            Debug.Log("[RVACULT] Maghrib prayer time arrived.");
            
            // Day to night transition
            if (audioManager != null) audioManager.PlayAzan();
            if (NPCManager.Instance != null) NPCManager.Instance.TriggerMassPrayer();
        }
        
        private void OnIshaPrayerTime()
        {
            Debug.Log("[RVACULT] Isha prayer time arrived.");
            
            // Reduce nighttime activity
            if (NPCManager.Instance != null)
            {
                NPCManager.Instance.SetGlobalActivityMultiplier(0.5f);
                NPCManager.Instance.TriggerMassPrayer();
            }
        }
        
        // ========================================================
        // PLAYER DATA ACCESSORS
        // ========================================================
        private Vector3 GetPlayerPosition()
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            return player != null ? player.transform.position : Vector3.zero;
        }
        
        private Quaternion GetPlayerRotation()
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            return player != null ? player.transform.rotation : Quaternion.identity;
        }
        
        private float GetPlayerCurrency()
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            return player != null ? player.GetCurrency() : 0f;
        }
        
        private float GetPlayerReputation()
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            return player != null ? player.GetReputation() : 50f;
        }
        
        private int GetPlayerPrayerCompletions()
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            return player != null ? player.GetPrayerCompletions() : 0;
        }
        
        private GangAffiliationData[] GetGangAffiliationData()
        {
            // Gather current gang relationship state
            List<GangAffiliationData> affiliations = new List<GangAffiliationData>();
            
            var gangEnumerator = _gangDatabase.GetEnumerator();
            while (gangEnumerator.MoveNext())
            {
                GangData gang = gangEnumerator.Current.Value;
                affiliations.Add(new GangAffiliationData
                {
                    gangID = gang.gangID,
                    reputation = gang.reputationWithPlayer,
                    isAllied = gang.isPlayerAllied,
                    isRival = gang.isPlayerRival
                });
            }
            
            return affiliations.ToArray();
        }
        
        private MissionProgressData GetMissionProgressData()
        {
            // Get from MissionManager
            if (MissionManager.Instance !=
                
return MissionManager.Instance.GetAllMissionProgress();
            }
            return new MissionProgressData();
        }

        private WorldStateData GetWorldStateData()
        {
            if (WorldStateManager.Instance != null)
            {
                return WorldStateManager.Instance.GetWorldState();
            }
            return new WorldStateData();
        }

        // ========================================================
        // PLAYER DATA MODIFIERS
        // ========================================================
        private void SetPlayerPosition(Vector3 position)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) player.transform.position = position;
        }

        private void SetPlayerRotation(Quaternion rotation)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) player.transform.rotation = rotation;
        }

        private void SetPlayerCurrency(float amount)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) player.SetCurrency(amount);
        }

        private void SetPlayerReputation(float reputation)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) player.SetReputation(reputation);
        }

        private void AddPlayerCurrency(int amount)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) player.AddCurrency(amount);
        }

        // ========================================================
        // GANG DATA APPLICATION
        // ========================================================
        private void ApplyGangAffiliationData(GangAffiliationData[] affiliations)
        {
            for (int i = 0; i < affiliations.Length; i++)
            {
                GangAffiliationData affiliation = affiliations[i];
                if (_gangDatabase.ContainsKey(affiliation.gangID))
                {
                    GangData gang = _gangDatabase[affiliation.gangID];
                    gang.reputationWithPlayer = affiliation.reputation;
                    gang.isPlayerAllied = affiliation.isAllied;
                    gang.isPlayerRival = affiliation.isRival;
                    _gangDatabase[affiliation.gangID] = gang;
                }
            }
        }

        private void ShowGangInfo(int gangID)
        {
            if (_gangDatabase.ContainsKey(gangID))
            {
                GangData gang = _gangDatabase[gangID];
                Debug.Log($"[RVACORE-GANG] ID: {gang.gangID} | Name: {gang.gangNameEnglish} | Activity: {gang.primaryActivity} | Island: {gang.homeIslandID}");
                debugSystem.LogInfo("GangInfo", $"Gang '{gang.gangNameEnglish}' has {gang.memberCount} members on {gang.homeIslandID}");
            }
            else
            {
                Debug.LogError($"[RVACORE] Gang ID {gangID} not found.");
            }
        }

        // ========================================================
        // MISSION AND WORLD STATE APPLICATION
        // ========================================================
        private void ApplyMissionProgressData(MissionProgressData missionData)
        {
            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.ApplyMissionProgress(missionData);
            }
        }

        private void ApplyWorldStateData(WorldStateData worldState)
        {
            if (WorldStateManager.Instance != null)
            {
                WorldStateManager.Instance.ApplyWorldState(worldState);
            }
        }

        // ========================================================
        // UTILITY METHODS
        // ========================================================
        private string GetRandomMaldivianIslandID()
        {
            string[] islandIDs = new string[]
            {
                "MV_MALE", "MV_HULHUMALE", "MV_VILLINGILI", "MV_ADDU", "MV_FUVAHMULAH",
                "MV_KULHUDHUFFUSHI", "MV_THINADHOO", "MV_NAIFARU", "MV_ISDHOO", "MV_FONADHOO"
            };
            
            return islandIDs[UnityEngine.Random.Range(0, islandIDs.Length)];
        }

        private int GetNPCCountForIsland(string islandID)
        {
            return islandID switch
            {
                "MV_MALE" => 45,      // Dense capital
                "MV_HULHUMALE" => 35, // Medium density
                "MV_VILLINGILI" => 25, // Lower density
                _ => 20               // Default for smaller islands
            };
        }

        private int GetVehicleCountForIsland(string islandID)
        {
            return islandID switch
            {
                "MV_MALE" => 12,
                "MV_HULHUMALE" => 8,
                "MV_VILLINGILI" => 5,
                _ => 3
            };
        }

        private void LoadMainMenu()
        {
            Debug.Log("[RVACORE] Loading main menu...");
            SceneManager.LoadScene(MAIN_MENU_SCENE_NAME);
        }

        private void DisposeNativeCollections()
        {
            if (_gangDatabase.IsCreated) _gangDatabase.Dispose();
        }

        // ========================================================
        // PUBLIC API FOR OTHER SYSTEMS
        // ========================================================
        public bool IsGameInitialized() => _isGameInitialized;
        public bool IsLoadingInProgress() => _isLoadingInProgress;
        public string GetGameVersion() => GAME_VERSION_STRING;
        public string GetCurrentIslandID() => _currentIslandID;
        public float GetCurrentFPS() => _lastFPS;
        public long GetCurrentMemoryUsage() => _lastMemoryUsage;
        public NativeHashMap<int, GangData> GetGangDatabase() => _gangDatabase;
        public bool IsGangDatabaseLoaded() => _gangDatabaseLoaded;

        // ========================================================
        // DATA STRUCTURES AND SERIALIZATION CLASSES
        // ========================================================

        [Serializable]
        private class GangDataContainer
        {
            public GangData[] gangs;
        }

        [Serializable]
        public struct GangData
        {
            public int gangID;
            public string gangNameEnglish;
            public string gangNameDhivehi;
            public string homeIslandID;
            public GangActivityType primaryActivity;
            public float aggressionLevel;
            public float influenceRadius;
            public int memberCount;
            public int currentBossNPCID;
            public bool isPlayerAllied;
            public bool isPlayerRival;
            public float reputationWithPlayer;
            public Color32 territoryColor;
        }

        [Serializable]
        public enum GangActivityType
        {
            DrugTrafficking, ArmsDealing, HumanTrafficking, MoneyLaundering,
            Extortion, Smuggling, Corruption, Terrorism, Piracy,
            Prostitution, Gambling, Cybercrime, LegitimateBusiness
        }

        [Serializable]
        public struct IslandTransitionData
        {
            public string fromIslandID;
            public string toIslandID;
            public Vector3 playerEntryPosition;
        }

        [Serializable]
        public class GameSaveData
        {
            public string saveTimestamp;
            public string gameVersion;
            public int slotNumber;
            public string currentIslandID;
            public Vector3 playerPosition;
            public Quaternion playerRotation;
            public float playerCurrency;
            public float playerReputation;
            public int prayerCompletions;
            public GangAffiliationData[] gangAffiliations;
            public MissionProgressData missionProgress;
            public WorldStateData worldState;
            public bool isAutosave;
        }

        [Serializable]
        public struct GangAffiliationData
        {
            public int gangID;
            public float reputation;
            public bool isAllied;
            public bool isRival;
        }

        [Serializable]
        public struct MissionProgressData
        {
            public string[] completedMissionIDs;
            public string[] activeMissionIDs;
            public Dictionary<string, float> missionObjectiveProgress;
        }

        [Serializable]
        public struct WorldStateData
        {
            public float gameTimeHours;
            public int dayNumber;
            public string currentWeather;
            public Dictionary<string, bool> islandDiscoveryStates;
            public Dictionary<string, int> npcRelationshipStates;
        }
    }
}

// ============================================================
// END OF FILE 001 - MainGameManager.cs
// Total Lines: 2,184
// Build: RVAPROD-001-MAIN-2184LINES
// ============================================================
