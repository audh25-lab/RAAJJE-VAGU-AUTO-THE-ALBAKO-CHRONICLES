using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
// MainGameManager.cs - Core Game Controller & System Orchestrator
// Optimized for Mali-G72 GPU, 30fps locked mobile performance
// Maldivian cultural integration with prayer time awareness
// Version: 1.0.0 (Production Build)

namespace RVA.TAC.Core
{
    /// <summary>
    /// Central game state enumerator reflecting Maldives-appropriate game flow
    /// </summary>
    public enum GameState
    {
        BOOT = 0,           // Initial application startup
        MAIN_MENU = 1,      // Main menu (culturally appropriate music/theme)
        LOADING = 2,        // Scene loading with Maldivian trivia
        PLAYING = 3,        // Active gameplay (prayer-aware)
        PAUSED = 4,         // Game paused (prayer time auto-pause)
        PRAYER_PAUSE = 5,   // Auto-pause during prayer times
        SAVING = 6,         // Save in progress
        QUITTING = 7        // Graceful shutdown
    }

    /// <summary>
    /// Critical system initialization priority for deterministic startup order
    /// </summary>
    public enum SystemPriority
    {
        CRITICAL = 0,   // SaveSystem, EventManager, PoolManager
        HIGH = 1,       // InputSystem, AudioManager, PrayerTimeSystem
        MEDIUM = 2,     // WeatherSystem, TimeSystem, IslandManager
        LOW = 3,        // UI, NPC, Vehicles, Missions
        BACKGROUND = 4  // Analytics, Achievements, Non-critical
    }

    [BurstCompile]
    public struct PerformanceMetrics : IDisposable
    {
        public float DeltaTimeAvg;
        public float FrameTimeMS;
        public float MemoryUsageMB;
        public int ActiveObjectCount;
        public int DrawCalls;
        public int SystemLoadPercent;

        public void Dispose() { }
    }

    /// <summary>
    /// MainGameManager - The beating heart of RVA:TAC
    /// Persistent singleton orchestrating all game systems with cultural sensitivity
    /// </summary>
    [DisallowMultipleComponent]
    public class MainGameManager : MonoBehaviour
    {
        #region SINGLETON_PATTERN
        private static MainGameManager _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static MainGameManager Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning("[MainGameManager] Instance accessed during application quit. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<MainGameManager>();
                        if (_instance == null)
                        {
                            GameObject singleton = new GameObject("MainGameManager");
                            _instance = singleton.AddComponent<MainGameManager>();
                            Debug.Log("[MainGameManager] Singleton created dynamically.");
                        }
                    }
                    return _instance;
                }
            }
        }
        #endregion

        #region SERIALIZED_FIELDS
        [Header("=== RVA:TAC CORE SETTINGS ===")]
        [Tooltip("Enable for production mobile builds (disables verbose logging)")]
        [SerializeField] private bool _isProductionBuild = true;

        [Tooltip("Target frame rate for mobile optimization")]
        [SerializeField] private int _targetFrameRate = 30;

        [Tooltip("Enable burst compilation for performance-critical sections")]
        [SerializeField] private bool _enableBurstCompilation = true;

        [Header("MALDIVIAN CULTURAL SETTINGS")]
        [Tooltip("Automatically pause gameplay during prayer times")]
        [SerializeField] private bool _respectPrayerTimes = true;

        [Tooltip("Prayer reminder UI notification enabled")]
        [SerializeField] private bool _prayerNotifications = true;

        [Tooltip ("Male, Maldives GPS coordinates for accurate prayer calculation")]
        [SerializeField] private Vector2 _maldivesCoordinates = new Vector2(4.1755f, 73.5093f);

        [Header("SYSTEM INITIALIZATION")]
        [Tooltip ("Systems will initialize in priority order (CRITICAL → BACKGROUND)")]
        [SerializeField] private List<SystemInitializer> _systemInitializers = new List<SystemInitializer>
        {
            // CRITICAL PRIORITY (Order matters!)
            new SystemInitializer("SaveSystem", SystemPriority.CRITICAL, true),
            new SystemInitializer("EventManager", SystemPriority.CRITICAL, true),
            new SystemInitializer("PoolManager", SystemPriority.CRITICAL, true),
            new SystemInitializer("VersionControlSystem", SystemPriority.CRITICAL, true),
            
            // HIGH PRIORITY
            new SystemInitializer("PrayerTimeSystem", SystemPriority.HIGH, true),
            new SystemInitializer("AudioManager", SystemPriority.HIGH, true),
            new SystemInitializer("InputSystem", SystemPriority.HIGH, true),
            new SystemInitializer ("TimeSystem", SystemPriority.HIGH, true),
            
            // MEDIUM PRIORITY
            new SystemInitializer("WeatherSystem", SystemPriority.MEDIUM, true),
            new SystemInitializer("IslandManager", SystemPriority.MEDIUM, true),
            new SystemInitializer("IslamicCalendar", SystemPriority.MEDIUM, false), // Optional
            
            // LOW PRIORITY (Gameplay)
            new SystemInitializer("PlayerController", SystemPriority.LOW, true),
            new SystemInitializer("VehicleSpawnManager", SystemPriority.LOW, true),
            new SystemInitializer("NPCSpawner", SystemPriority.LOW, true),
            new SystemInitializer("GangManager", SystemPriority.LOW, true),
            
            // BACKGROUND (Non-critical)
            new SystemInitializer("UIManager", SystemPriority.BACKGROUND, true),
            new SystemInitializer("MissionSystem", SystemPriority.BACKGROUND, true),
            new SystemInitializer("AchievementSystem", SystemPriority.BACKGROUND, false),
            new SystemInitializer("AnalyticsSystem", SystemPriority.BACKGROUND, false)
        };

        [Header("MOBILE PERFORMANCE")]
        [Tooltip("Low memory threshold for mobile devices (MB)")]
        [SerializeField] private int _lowMemoryThresholdMB = 200;

        [Tooltip ("Enable dynamic quality adjustment based on performance")]
        [SerializeField] private bool _adaptiveQuality = true;

        [Tooltip ("Max concurrent audio sources for mobile")]
        [SerializeField] private int _maxAudioSources = 16;
        #endregion

        #region PRIVATE_FIELDS
        private GameState _currentState = GameState.BOOT;
        private GameState _previousState = GameState.BOOT;
        private readonly Dictionary<string, IGameSystem> _registeredSystems = new Dictionary<string, IGameSystem>(64);
        private readonly Dictionary<SystemPriority, List<string>> _systemsByPriority = new Dictionary<SystemPriority, List<string>>
        {
            { SystemPriority.CRITICAL, new List<string>(8) },
            { SystemPriority.HIGH, new List<string>(8) },
            { SystemPriority.MEDIUM, new List<string>(8) },
            { SystemPriority.LOW, new List<string>(16) },
            { SystemPriority.BACKGROUND, new List<string>(16) }
        };

        // Performance tracking
        private readonly Queue<float> _frameTimeHistory = new Queue<float>(60);
        private PerformanceMetrics _currentMetrics;
        private bool _isPerformanceCritical = false;

        // Cultural state
        private bool _isPrayerTimeActive = false;
        private string _nextPrayerName = string.Empty;
        private DateTime _nextPrayerTime;

        // Initialization
        private bool _isInitialized = false;
        private bool _isShuttingDown = false;
        private int _initializedSystemCount = 0;
        private float _initializationStartTime = 0f;

        // Mobile lifecycle
        private bool _isAppPaused = false;
        private float _pauseTime = 0f;
        private const float MAX_PAUSE_DURATION_BEFORE_SAVE = 300f; // 5 minutes
        #endregion

        #region PUBLIC_PROPERTIES
        public GameState CurrentState => _currentState;
        public bool IsInitialized => _isInitialized;
        public bool IsPrayerTimeActive => _isPrayerTimeActive;
        public PerformanceMetrics PerformanceMetrics => _currentMetrics;
        public Vector2 MaldivesCoordinates => _maldivesCoordinates;
        public bool IsProductionBuild => _isProductionBuild;
        #endregion

        #region UNITY_LIFECYCLE
        private void Awake()
        {
            // ENFORCE SINGLETON
            lock (_lock)
            {
                if (_instance != null && _instance != this)
                {
                    Debug.LogWarning($"[MainGameManager] Duplicate instance destroyed on {gameObject.name}");
                    Destroy(gameObject);
                    return;
                }

                _instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            // BOOTSTRAP PERFORMANCE
            Application.targetFrameRate = _targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // MOBILE PERFORMANCE TUNING
            if (Application.isMobilePlatform)
            {
                _maxAudioSources = Mathf .Min(_maxAudioSources, 16);
                QualitySettings.SetQualityLevel(2, false); // Medium quality
                Application.backgroundLoadingPriority = ThreadPriority.Low;
            }

            // BURST COMPILATION
            if (_enableBurstCompilation)
            {
                BurstCompiler.Enable();
            }

            // INITIALIZE STATE
            _currentState = GameState.BOOT;
            _initializationStartTime = Time.realtimeSinceStartup;

            Log("=== RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES ===", LogType.Important);
            Log($"Build: {Application.version} | Platform: {Application.platform} | Unity: {Application.unityVersion}", LogType.Important);
            Log($"Maldives Coordinates: {_maldivesCoordinates.x}°N, {_maldivesCoordinates.y}°E", LogType.Info);
            
            // PRE-ALLOCATE COLLECTIONS
            _registeredSystems.Clear();
            foreach (var priority in _systemsByPriority.Keys)
            {
                _systemsByPriority[priority].Clear();
            }
        }

        private void Start()
        {
            // BEGIN INITIALIZATION PIPELINE
            StartCoroutine(InitializeSystemsAsync());
        }

        private void Update()
        {
            if (!_isInitialized || _isShuttingDown) return;

            // UPDATE ALL ACTIVE SYSTEMS
            foreach (var system in _registeredSystems.Values)
            {
                if (system.IsEnabled && !system.IsPaused)
                {
                    try
                    {
                        system.OnUpdate(Time.deltaTime);
                    }
                    catch (Exception e)
                    {
                        LogError($"[Update] System '{system.SystemName}' failed: {e.Message}", system.SystemName);
                        if (!_isProductionBuild) throw;
                    }
                }
            }

            // PERFORMANCE MONITORING (Every 60 frames)
            if (Time.frameCount % 60 == 0)
            {
                UpdatePerformanceMetrics();
            }

            // PRAYER TIME CHECK (Every 30 seconds)
            if (Time.frameCount % (30 * _targetFrameRate) == 0 && _respectPrayerTimes)
            {
                CheckPrayerTimes();
            }

            // MOBILE MEMORY PRESSURE
            if (Application.isMobilePlatform && Time.frameCount % 120 == 0)
            {
                CheckMemoryPressure();
            }
        }

        private void FixedUpdate()
        {
            if (!_isInitialized || _isShuttingDown) return;

            foreach (var system in _registeredSystems.Values)
            {
                if (system.IsEnabled && !system.IsPaused)
                {
                    try
                    {
                        system.OnFixedUpdate(Time.fixedDeltaTime);
                    }
                    catch (Exception e)
                    {
                        LogError($"[FixedUpdate] System '{system.SystemName}' failed: {e.Message}", system.SystemName);
                        if (!_isProductionBuild) throw;
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized || _isShuttingDown) return;

            foreach (var system in _registeredSystems.Values)
            {
                if (system.IsEnabled && !system.IsPaused)
                {
                    try
                    {
                        system.OnLateUpdate(Time.deltaTime);
                    }
                    catch (Exception e)
                    {
                        LogError($"[LateUpdate] System '{system.SystemName}' failed: {e.Message}", system.SystemName);
                        if (!_isProductionBuild) throw;
                    }
                }
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _isAppPaused = pauseStatus;
            
            if (pauseStatus)
            {
                _pauseTime = Time.realtimeSinceStartup;
                Log("Application paused - entering background mode", LogType.Warning);
                
                // PAUSE ALL SYSTEMS
                SetAllSystemsPause(true);
                
                // AUTO-SAVE IF PAUSED TOO LONG
                StartCoroutine(DelayedSaveOnPause());
            }
            else
            {
                float pauseDuration = Time.realtimeSinceStartup - _pauseTime;
                Log($"Application resumed after {pauseDuration:F1}s", LogType.Info);
                
                // RESUME SYSTEMS
                SetAllSystemsPause(false);
                
                // ADJUST PRAYER TIMES AFTER BACKGROUNDING
                if (_registeredSystems.TryGetValue("PrayerTimeSystem", out var prayerSystem))
                {
                    (prayerSystem as PrayerTimeSystem)?.ForceRecalculation();
                }
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Handle mobile focus loss/gain
            if (!hasFocus && Application.isMobilePlatform)
            {
                Log("Application lost focus", LogType.Warning);
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
            _isShuttingDown = true;
            
            Log("=== APPLICATION QUITTING ===", LogType.Important);
            
            // GRACEFUL SHUTDOWN
            ShutdownAllSystems();
            
            // FINAL SAVE
            if (_registeredSystems.TryGetValue("SaveSystem", out var saveSystem))
            {
                (saveSystem as SaveSystem)?.ForceSave();
            }
            
            _registeredSystems.Clear();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnLowMemory()
        {
            Log("LOW MEMORY WARNING - Clearing non-critical caches", LogType.Critical);
            
            // EMERGENCY MEMORY CLEARING
            Resources.UnloadUnusedAssets();
            
            // NOTIFY ALL SYSTEMS
            foreach (var system in _registeredSystems.Values)
            {
                system.OnLowMemory();
            }
            
            GC.Collect();
        }
        #endregion

        #region INITIALIZATION
        private IEnumerator InitializeSystemsAsync()
        {
            Log("=== INITIALIZING CORE SYSTEMS ===", LogType.Important);
            
            // SORT SYSTEMS BY PRIORITY
            foreach (var initializer in _systemInitializers)
            {
                if (initializer.IsEnabled)
                {
                    _systemsByPriority[initializer.Priority].Add(initializer.SystemName);
                }
            }

            // STEP 1: CRITICAL SYSTEMS (Synchronous - must complete before continuing)
            yield return InitializeSystemBatch(SystemPriority.CRITICAL, waitForCompletion: true);

            // STEP 2: HIGH PRIORITY (Asynchronous - can load in background)
            yield return InitializeSystemBatch(SystemPriority.HIGH, waitForCompletion: false);
            
            // LOADING SCREEN UPDATE
            yield return UpdateLoadingProgress(0.4f, " initializing core environment...");

            // STEP 3: MEDIUM PRIORITY
            yield return InitializeSystemBatch(SystemPriority.MEDIUM, waitForCompletion: false);
            yield return UpdateLoadingProgress(0.6f, "Loading island data...");
            
            // Wait for island data to be ready
            yield return new WaitUntil(() => 
                _registeredSystems.ContainsKey("IslandManager") && 
                (_registeredSystems["IslandManager"] as IslandManager)?.IsDataLoaded == true
            );

            // STEP 4: LOW PRIORITY (Gameplay systems)
            yield return InitializeSystemBatch(SystemPriority.LOW, waitForCompletion: false);
            yield return UpdateLoadingProgress(0.8f, "Spawning NPCs and vehicles...");

            // STEP 5: BACKGROUND SYSTEMS
            yield return InitializeSystemBatch(SystemPriority.BACKGROUND, waitForCompletion: false);
            yield return UpdateLoadingProgress(1.0f, "Finishing up...");

            // FINALIZE
            yield return FinalizeInitialization();
        }

        private IEnumerator InitializeSystemBatch(SystemPriority priority, bool waitForCompletion)
        {
            var systemNames = _systemsByPriority[priority];
            if (systemNames.Count == 0) yield break;

            Log($"Initializing {priority} priority systems...", LogType.Info);
            
            int completed = 0;
            foreach (string systemName in systemNames)
            {
                yield return InitializeSingleSystem(systemName);
                completed++;
                
                if (waitForCompletion)
                {
                    // Wait for system to report ready
                    yield return new WaitUntil(() => 
                        _registeredSystems.ContainsKey(systemName) && 
                        _registeredSystems[systemName].IsInitialized
                    );
                }
                
                float progress = (float)completed / systemNames.Count;
                Log($"  ✓ {systemName} initialized ({progress:P0})", LogType.Info);
            }
            
            if (!waitForCompletion)
            {
                // Give background systems time to breathe
                yield return new WaitForSeconds(0.1f);
            }
        }

        private IEnumerator InitializeSingleSystem(string systemName)
        {
            // FIND OR CREATE SYSTEM COMPONENT
            IGameSystem system = FindObjectOfType<MonoBehaviour>() as IGameSystem;
            if (system == null)
            {
                GameObject systemGO = new GameObject($"[SYSTEM] {systemName}");
                systemGO.transform.SetParent(transform);
                
                // CREATE COMPONENT BASED ON NAME
                system = systemName switch
                {
                    "SaveSystem" => systemGO.AddComponent<SaveSystem>(),
                    "EventManager" => systemGO.AddComponent<EventManager>(),
                    "PoolManager" => systemGO.AddComponent<PoolManager>(),
                    "VersionControlSystem" => systemGO.AddComponent<VersionControlSystem>(),
                    "PrayerTimeSystem" => systemGO.AddComponent<PrayerTimeSystem>(),
                    "AudioManager" => systemGO.AddComponent<AudioManager>(),
                    "InputSystem" => systemGO.AddComponent<InputSystem>(),
                    "TimeSystem" => systemGO.AddComponent<TimeSystem>(),
                    "WeatherSystem" => systemGO.AddComponent<WeatherSystem>(),
                    "IslandManager" => systemGO.AddComponent<IslandManager>(),
                    "IslamicCalendar" => systemGO.AddComponent<IslamicCalendar>(),
                    "PlayerController" => systemGO.AddComponent<PlayerController>(),
                    "VehicleSpawnManager" => systemGO.AddComponent<VehicleSpawnManager>(),
                    "NPCSpawner" => systemGO.AddComponent<NPCSpawner>(),
                    "GangManager" => systemGO.AddComponent<GangManager>(),
                    "UIManager" => systemGO.AddComponent<UIManager>(),
                    "MissionSystem" => systemGO.AddComponent<MissionSystem>(),
                    "AchievementSystem" => systemGO.AddComponent<AchievementSystem>(),
                    "AnalyticsSystem" => systemGO.AddComponent<AnalyticsSystem>(),
                    _ => throw new NotImplementedException($"System '{systemName}' not implemented")
                };

                Assert.IsNotNull(system, $"Failed to create system: {systemName}");
            }

            // REGISTER SYSTEM
            _registeredSystems[systemName] = system;
            system.SystemName = systemName;
            
            // INITIALIZE
            system.Initialize(this);
            
            yield return null; // Wait one frame
        }

        private IEnumerator UpdateLoadingProgress(float progress, string message)
        {
            // UPDATE LOADING UI IF AVAILABLE
            if (_registeredSystems.TryGetValue("UIManager", out var uiManager) && uiManager.IsInitialized)
            {
                (uiManager as UIManager)?.UpdateLoadingProgress(progress, message);
            }
            
            // LOG PROGRESS
            Log($"Loading: {progress:P0} - {message}", LogType.Info);
            
            yield return null;
        }

        private IEnumerator FinalizeInitialization()
        {
            _initializedSystemCount = _registeredSystems.Count;
            _isInitialized = true;
            
            float initDuration = Time.realtimeSinceStartup - _initializationStartTime;
            Log($"=== ALL SYSTEMS INITIALIZED ({_initializedSystemCount}) in {initDuration:F2}s ===", LogType.Important);
            
            // TRANSITION TO MAIN MENU
            yield return new WaitForSeconds(0.5f);
            
            SetGameState(GameState.MAIN_MENU);
            
            // LOAD MAIN MENU SCENE
            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                yield return LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            }
            
            // READY TO PLAY
            Log("=== GAME READY ===", LogType.Important);
            Log("Bismillah - RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES", LogType.Important);
        }
        #endregion

        #region SYSTEM_MANAGEMENT
        public void RegisterSystem(string name, IGameSystem system)
        {
            if (string.IsNullOrEmpty(name) || system == null)
            {
                LogError("Invalid system registration - null name or system", "MainGameManager");
                return;
            }

            if (_registeredSystems.ContainsKey(name))
            {
                LogWarning($"System '{name}' already registered. Overwriting.", "MainGameManager");
            }

            _registeredSystems[name] = system;
            Log($"System registered: {name}", LogType.Info);
        }

        public T GetSystem<T>() where T : class, IGameSystem
        {
            foreach (var system in _registeredSystems.Values)
            {
                if (system is T typedSystem)
                {
                    return typedSystem;
                }
            }
            return null;
        }

        public IGameSystem GetSystem(string name)
        {
            return _registeredSystems.TryGetValue(name, out var system) ? system : null;
        }

        private void SetAllSystemsPause(bool paused)
        {
            foreach (var system in _registeredSystems.Values)
            {
                system.IsPaused = paused;
            }
        }

        private void ShutdownAllSystems()
        {
            Log("Shutting down all systems...", LogType.Warning);
            
            // SHUTDOWN IN REVERSE PRIORITY ORDER
            for (int priority = 4; priority >= 0; priority--)
            {
                var priorityEnum = (SystemPriority)priority;
                if (!_systemsByPriority.ContainsKey(priorityEnum)) continue;
                
                foreach (string systemName in _systemsByPriority[priorityEnum])
                {
                    if (_registeredSystems.TryGetValue(systemName, out var system))
                    {
                        try
                        {
                            system.Shutdown();
                            Log($"  ✓ {systemName} shutdown", LogType.Info);
                        }
                        catch (Exception e)
                        {
                            LogError($"Failed to shutdown {systemName}: {e.Message}", systemName);
                        }
                    }
                }
            }
        }

        private IEnumerator DelayedSaveOnPause()
        {
            yield return new WaitForSecondsRealtime(1f);
            
            if (_isAppPaused && Time.realtimeSinceStartup - _pauseTime > 10f)
            {
                Log("Auto-saving due to extended pause...", LogType.Info);
                if (_registeredSystems.TryGetValue("SaveSystem", out var saveSystem))
                {
                    (saveSystem as SaveSystem)?.ForceSave();
                }
            }
        }
        #endregion

        #region GAME_STATE_MANAGEMENT
        public void SetGameState(GameState newState)
        {
            if (_currentState == newState) return;

            _previousState = _currentState;
            _currentState = newState;

            Log($"GameState: {_previousState} → {newState}", LogType.Important);

            // STATE-SPECIFIC LOGIC
            switch (newState)
            {
                case GameState.MAIN_MENU:
                    Time.timeScale = 1f;
                    AudioListener.pause = false;
                    break;

                case GameState.PLAYING:
                    Time.timeScale = 1f;
                    AudioListener.pause = false;
                    // Resume if not in prayer time
                    if (!_isPrayerTimeActive)
                    {
                        SetAllSystemsPause(false );
                    }
                    break;

                case GameState.PAUSED:
                    Time.timeScale = 0f;
                    // Audio continues for menu sounds
                    SetAllSystemsPause(true);
                    break;

                case GameState.PRAYER_PAUSE:
                    Time.timeScale = 0f;
                    AudioListener.pause = true; // Respectful audio pause
                    SetAllSystemsPause(true);
                    ShowPrayerNotification();
                    break;

                case GameState.LOADING:
                    Time.timeScale = 0f;
                    SetAllSystemsPause(true);
                    break;

                case GameState.SAVING:
                    // Brief time freeze during save
                    Time.timeScale = 0f;
                    break;

                case GameState.QUITTING:
                    StartCoroutine(QuitApplicationAsync());
                    break;
            }

            // BROADCAST STATE CHANGE
            EventManager.TriggerEvent("OnGameStateChanged", new GameStateChangedData
            {
                PreviousState = _previousState,
                NewState = newState
            });
        }

        public void ReturnToPreviousState()
        {
            SetGameState(_previousState);
        }

        public bool IsInGameplayState()
        {
            return _currentState == GameState.PLAYING || _currentState == GameState.PAUSED;
        }
        #endregion

        #region SCENE_MANAGEMENT
        public IEnumerator LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            Log($"Loading scene: {sceneName} ({mode})", LogType.Info);
            
            // VALIDATE SCENE EXISTS
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                LogError($"Scene '{sceneName}' cannot be loaded. Check Build Settings.", "SceneManager");
                yield break;
            }

            // PRE-LOAD SAVE
            if (mode == LoadSceneMode.Single && _registeredSystems.ContainsKey("SaveSystem"))
            {
                (GetSystem<SaveSystem>())?.LoadGameState();
            }

            // START LOADING
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);
            operation.allowSceneActivation = false;

            // SHOW LOADING UI
            SetGameState(GameState.LOADING);
            
            float progress = 0f;
            while (!operation.isDone)
            {
                progress = Mathf.Clamp01(operation.progress / 0.9f);
                yield return UpdateLoadingProgress(progress, $"Loading {sceneName}...");
                
                if (progress >= 0.9f)
                {
                    operation.allowSceneActivation = true;
                }
                
                yield return null;
            }

            // POST-LOAD INITIALIZATION
            yield return new WaitForSeconds(0.1f);
            
            if (mode == LoadSceneMode.Single)
            {
                SetGameState(GameState.PLAYING);
            }
            
            Log($"Scene loaded: {sceneName}", LogType.Info);
        }

        public void LoadIsland(string islandName)
        {
            StartCoroutine(LoadSceneAsync($"Islands/{islandName}", LoadSceneMode.Single));
        }

        public void ReturnToMainMenu()
        {
            StartCoroutine(LoadSceneAsync("MainMenu", LoadSceneMode.Single));
        }

        private IEnumerator QuitApplicationAsync()
        {
            Log("Quitting application...", LogType.Important);
            
            // FINAL SAVE
            if (_registeredSystems.TryGetValue("SaveSystem", out var saveSystem))
            {
                yield return (saveSystem as SaveSystem)?.ForceSaveAsync();
            }
            
            // WAIT FOR OPERATIONS
            yield return new WaitForSeconds(0.5f);
            
            Application.Quit();
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }
        #endregion

        #region CULTURAL_INTEGRATION
        private void CheckPrayerTimes()
        {
            if (!_registeredSystems.TryGetValue("PrayerTimeSystem", out var prayerSystem)) return;

            var prayer = prayerSystem as PrayerTimeSystem;
            if (prayer != null && prayer.IsPrayerTimeNow() && !_isPrayerTimeActive)
            {
                _isPrayerTimeActive = true;
                _nextPrayerName = prayer.GetCurrentPrayerName();
                
                Log($"Prayer time detected: {_nextPrayerName}. Auto-pausing game.", LogType.Important);
                SetGameState(GameState.PRAYER_PAUSE);
            }
            else if (prayer != null && !prayer.IsPrayerTimeNow() && _isPrayerTimeActive)
            {
                _isPrayerTimeActive = false;
                Log($"Prayer time ended. Resuming gameplay.", LogType.Important);
                
                if (_previousState == GameState.PLAYING)
                {
                    ReturnToPreviousState();
                }
            }

            // UPDATE NEXT PRAYER INFO
            var nextPrayer = prayer?.GetNextPrayerTime();
            if (nextPrayer.HasValue)
            {
                _nextPrayerTime = nextPrayer.Value.Time;
                _nextPrayerName = nextPrayer.Value.Name;
            }
        }

        private void ShowPrayerNotification()
        {
            if (!_prayerNotifications) return;
            
            // TRIGGER UI NOTIFICATION
            EventManager.TriggerEvent("OnPrayerTimeNotification", new PrayerNotificationData
            {
                PrayerName = _nextPrayerName,
                PrayerTime = _nextPrayerTime,
                IsAutoPause = true
            });

            // PLAY ADHAN AUDIO (RESPECTFUL VOLUME)
            if (_registeredSystems.TryGetValue("AudioManager", out var audioSystem))
            {
                (audioSystem as AudioManager)?.PlayPrayerCall(_nextPrayerName, volume: 0.3f);
            }
        }

        public PrayerNotificationData GetNextPrayerInfo()
        {
            return new PrayerNotificationData
            {
                PrayerName = _nextPrayerName,
                PrayerTime = _nextPrayerTime,
                IsAutoPause = _respectPrayerTimes
            };
        }
        #endregion

        #region PERFORMANCE_MONITORING
        [BurstCompile]
        private void UpdatePerformanceMetrics()
        {
            // FRAME TIME
            float frameTime = Time.unscaledDeltaTime;
            _frameTimeHistory.Enqueue(frameTime);
            if (_frameTimeHistory.Count > 60) _frameTimeHistory.Dequeue();

            float avgFrameTime = 0f;
            foreach (var ft in _frameTimeHistory)
            {
                avgFrameTime += ft;
            }
            avgFrameTime /= _frameTimeHistory.Count;

            // MEMORY
            long memoryBytes = GC.GetTotalMemory(false);
            float memoryMB = memoryBytes / (1024f * 1024f);

            // OBJECT COUNT
            int objectCount = FindObjectsOfType<Object>().Length;

            // SYSTEM LOAD
            int totalSystems = _registeredSystems.Count;
            int activeSystems = 0;
            foreach (var system in _registeredSystems.Values)
            {
                if (system.IsEnabled && !system.IsPaused) activeSystems++;
            }
            int systemLoadPercent = totalSystems > 0 ? (activeSystems * 100) / totalSystems : 0;

            // CRITICAL PERFORMANCE DETECTION
            bool wasCritical = _isPerformanceCritical;
            _isPerformanceCritical = avgFrameTime > (1f / (_targetFrameRate * 0.8f)); // 20% below target

            if (_isPerformanceCritical && !wasCritical)
            {
                Log($"PERFORMANCE CRITICAL: {avgFrameTime * 1000:F1}ms frame time", LogType.Critical);
                OnPerformanceCritical();
            }

            // UPDATE STRUCT
            _currentMetrics = new PerformanceMetrics
            {
                DeltaTimeAvg = avgFrameTime,
                FrameTimeMS = avgFrameTime * 1000f,
                MemoryUsageMB = memoryMB,
                ActiveObjectCount = objectCount,
                DrawCalls = UnityStats.drawCalls,
                SystemLoadPercent = systemLoadPercent
            };
        }

        private void OnPerformanceCritical()
        {
            if (!_adaptiveQuality) return;

            // EMERGENCY QUALITY REDUCTION
            Log("Applying emergency performance optimizations...", LogType.Warning);
            
            // REDUCE RENDER SCALE
            if (ScalableBufferManager.widthScaleFactor > 0.7f)
            {
                ScalableBufferManager.ResizeBuffers(0.7f, 0.7f);
            }

            // DISABLE NON-CRITICAL SYSTEMS
            foreach (var kvp in _registeredSystems)
            {
                if (kvp.Value.SystemPriority == SystemPriority.BACKGROUND)
                {
                    kvp.Value.IsEnabled = false;
                }
            }

            // CLEAR POOLS
            if (_registeredSystems.TryGetValue("PoolManager", out var poolManager))
            {
                (poolManager as PoolManager)?.ClearAllPools();
            }
        }

        private void CheckMemoryPressure()
        {
            long availableMemory = SystemInfo.systemMemorySize - (long)(_currentMetrics.MemoryUsageMB);
            float availableMB = availableMemory;
            
            if (availableMB < _lowMemoryThresholdMB)
            {
                Log($"Low memory pressure detected: {availableMB:F0}MB available", LogType.Warning);
                OnLowMemory();
            }
        }
        #endregion

        #region LOGGING
        public void Log(string message, LogType type = LogType.Info, [CallerMemberName] string caller = "")
        {
            if (_isProductionBuild && type == LogType.Debug) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logLevel = type switch
            {
                LogType.Debug => "[DEBUG]",
                LogType.Info => "[INFO]",
                LogType.Warning => "[WARN]",
                LogType.Error => "[ERROR]",
                LogType.Critical => "[CRITICAL]",
                LogType.Important => "[===]",
                _ => "[INFO]"
            };

            string logMessage = $"{timestamp} {logLevel} [MainGameManager.{caller}] {message}";
            
            // UNITY DEBUG LOG
            switch (type)
            {
                case LogType.Debug:
                case LogType.Info:
                    Debug.Log(logMessage);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(logMessage);
                    break;
                case LogType.Error:
                case LogType.Critical:
                    Debug.LogError(logMessage);
                    break;
                case LogType.Important:
                    Debug.Log($"<color=cyan><b>{logMessage}</b></color>");
                    break;
            }

            // PERSIST TO FILE IN DEVELOPMENT
            if (!_isProductionBuild && _registeredSystems.TryGetValue("SaveSystem", out var saveSystem))
            {
                (saveSystem as SaveSystem)?.LogToFile(logMessage);
            }
        }

        public void LogError(string message, string systemName = null)
        {
            Log(message, LogType.Error);
            
            // ANALYTICS TRACKING
            if (_registeredSystems.ContainsKey("AnalyticsSystem"))
            {
                EventManager.TriggerEvent("OnErrorLogged", new ErrorData
                {
                    SystemName = systemName ?? "MainGameManager",
                    ErrorMessage = message,
                    Timestamp = DateTime.Now,
                    StackTrace = Environment.StackTrace
                });
            }
        }

        public void LogWarning(string message, string systemName = null)
        {
            Log(message, LogType.Warning);
        }
        #endregion

        #region UTILITY_METHODS
        public bool IsSystemReady(string systemName)
        {
            return _registeredSystems.TryGetValue(systemName, out var system) && system.IsInitialized;
        }

        public T GetSafeComponent<T>(GameObject obj, bool addIfMissing = true) where T : Component
        {
            if (obj == null) return null;
            
            T component = obj.GetComponent<T>();
            if (component == null && addIfMissing)
            {
                component = obj.AddComponent<T>();
            }
            
            return component;
        }

        public Coroutine StartCoroutineSafe(IEnumerator routine, string coroutineName = "")
        {
            if (_isShuttingDown)
            {
                LogWarning($"Cannot start coroutine '{coroutineName}' - application shutting down", "MainGameManager");
                return null;
            }
            return StartCoroutine(routine);
        }

        public void StopAllSystemCoroutines()
        {
            StopAllCoroutines();
        }
        #endregion
    }

    #region SUPPORTING_STRUCTS
    [Serializable]
    public class SystemInitializer
    {
        public string SystemName;
        public SystemPriority Priority;
        public bool IsEnabled;

        public SystemInitializer(string name, SystemPriority priority, bool enabled)
        {
            SystemName = name;
            Priority = priority;
            IsEnabled = enabled;
        }
    }

    public enum LogType
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical,
        Important
    }

    public interface IGameSystem
    {
        string SystemName { get; set; }
        bool IsInitialized { get; }
        bool IsEnabled { get; set; }
        bool IsPaused { get; set; }
        SystemPriority SystemPriority { get; }
        
        void Initialize(MainGameManager gameManager);
        void Shutdown();
        void OnUpdate(float deltaTime);
        void OnFixedUpdate(float fixedDeltaTime);
        void OnLateUpdate(float deltaTime);
        void OnLowMemory();
    }

    [Serializable]
    public struct GameStateChangedData
    {
        public GameState PreviousState;
        public GameState NewState;
    }

    [Serializable]
    public struct PrayerNotificationData
    {
        public string PrayerName;
        public DateTime PrayerTime;
        public bool IsAutoPause;
    }

    [Serializable]
    public struct ErrorData
    {
        public string SystemName;
        public string ErrorMessage;
        public DateTime Timestamp;
        public string StackTrace;
    }
    #endregion
}
