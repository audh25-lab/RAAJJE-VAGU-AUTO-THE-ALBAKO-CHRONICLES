using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using UnityEngine.Audio;
using MaldivianCulturalSDK;

namespace RVA.TAC.Core
{
    /// <summary>
    /// Central game controller for RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES
    /// Mobile-optimized singleton with Maldivian cultural integration
    /// Unity Third-Party Integration: Unity Audio Mixer, Burst Compiler
    /// Cultural Compliance: Islamic prayer time sensitivity, Maldivian business hours
    /// Performance Target: <1ms initialization, 30fps locked on Mali-G72 GPU
    /// </summary>
    [RequireComponent(typeof(PrayerTimeSystem))]
    [RequireComponent(typeof(TimeSystem))]
    [RequireComponent(typeof(SaveSystem))]
    [RequireComponent(typeof(VersionControlSystem))]
    [RequireComponent(typeof(DebugSystem))]
    [RequireComponent(typeof(AudioManager))]
    public class MainGameManager : MonoBehaviour
    {
        #region Singleton Pattern
        private static MainGameManager _instance;
        public static MainGameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.LogError("[RVA:TAC] MainGameManager instance is null. Ensure GameManager prefab exists in initial scene.");
                }
                return _instance;
            }
        }
        #endregion

        #region Unity Inspector Fields
        [Header("Maldivian Game World Configuration")]
        [Tooltip("Total number of islands in the archipelago")]
        public int TotalIslands = 41;
        
        [Tooltip("Main island where player starts (refer to MaldivesData.txt)")]
        public int StartingIslandID = 11; // Male (capital)
        
        [Tooltip("Maximum concurrent gangs active in world")]
        public int MaxActiveGangs = 83;
        
        [Tooltip("Cultural difficulty affects prayer sensitivity")]
        public CulturalDifficultyLevel CulturalDifficulty = CulturalDifficultyLevel.Authentic;

        [Header("Mobile Performance Settings")]
        [Tooltip("Target frame rate for mobile devices")]
        public int TargetFrameRate = 30;
        
        [Tooltip("Master volume control respecting prayer times")]
        [Range(0f, 1f)]
        public float MasterVolume = 0.7f;

        [Header("Cultural Sensitivity")]
        [Tooltip("Automatically pause gameplay during prayer times")]
        public bool RespectPrayerTimes = true;
        
        [Tooltip("Pause during Friday Jumu'ah (12:00-14:00)")]
        public bool RespectFridayPrayer = true;
        
        [Tooltip("Maldivian business hours enforcement (9AM-5PM for missions)")]
        public bool EnforceBusinessHours = true;

        [Header("Debug Configuration")]
        [Tooltip("Enable comprehensive logging for development")]
        public bool EnableDebugLogging = true;
        
        [Tooltip("Skip cultural restrictions for testing")]
        public bool DebugSkipCulturalRules = false;
        #endregion

        #region Private Core Systems
        private PrayerTimeSystem _prayerTimeSystem;
        private TimeSystem _timeSystem;
        private SaveSystem _saveSystem;
        private VersionControlSystem _versionControl;
        private DebugSystem _debugSystem;
        private AudioManager _audioManager;
        private GameSceneManager _sceneManager;
        private InputSystem _inputSystem;
        #endregion

        #region Game State Properties
        public bool IsGameInitialized { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsPrayerTimeActive { get; private set; }
        public bool IsFridayJumuah { get; private set; }
        public GameState CurrentGameState { get; private set; } = GameState.MainMenu;
        public MaldivesDateTime CurrentMaldivianTime { get; private set; }
        #endregion

        #region Cultural Events
        public event Action<PrayerName> OnPrayerTimeBegins;
        public event Action<PrayerName> OnPrayerTimeEnds;
        public event Action OnFridayJumuahBegins;
        public event Action OnFridayJumuahEnds;
        public event Action<float> OnMasterVolumeChanged;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            #region Singleton Enforcement
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[RVA:TAC] Duplicate MainGameManager detected on {gameObject.name}. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            #endregion

            #region System References
            _prayerTimeSystem = GetComponent<PrayerTimeSystem>();
            _timeSystem = GetComponent<TimeSystem>();
            _saveSystem = GetComponent<SaveSystem>();
            _versionControl = GetComponent<VersionControlSystem>();
            _debugSystem = GetComponent<DebugSystem>();
            _audioManager = GetComponent<AudioManager>();
            _sceneManager = GetComponent<GameSceneManager>() ?? gameObject.AddComponent<GameSceneManager>();
            _inputSystem = GetComponent<InputSystem>();
            #endregion

            #region Performance Initialization
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = TargetFrameRate;
            
            // Mali-G72 GPU optimization
            QualitySettings.SetQualityLevel(2, true); // Medium quality for mobile
            Shader.globalMaximumLOD = 200;
            #endregion

            #region Cultural Shielding
            if (RespectPrayerTimes && !DebugSkipCulturalRules)
            {
                Application.lowMemory += HandleLowMemoryDuringPrayer;
            }
            #endregion

            if (EnableDebugLogging)
            {
                Debug.Log($"[RVA:TAC] MainGameManager initialized. Build: {VersionControlSystem.VERSION}");
            }
        }

        private void Start()
        {
            InitializeGameCore();
        }

        private void OnEnable()
        {
            if (_prayerTimeSystem != null)
            {
                _prayerTimeSystem.OnPrayerTimeReached += HandlePrayerTimeReached;
                _prayerTimeSystem.OnPrayerTimeEnded += HandlePrayerTimeEnded;
            }
        }

        private void OnDisable()
        {
            if (_prayerTimeSystem != null)
            {
                _prayerTimeSystem.OnPrayerTimeReached -= HandlePrayerTimeReached;
                _prayerTimeSystem.OnPrayerTimeEnded -= HandlePrayerTimeEnded;
            }
        }

        private void Update()
        {
            if (!IsGameInitialized) return;

            // Cultural time checks
            CheckFridayJumuahPeriod();
            CheckMaldivianBusinessHours();
            
            // Mobile input back button handling
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleMobileBackButton();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!IsGameInitialized) return;
            
            if (RespectPrayerTimes && !DebugSkipCulturalRules && IsPrayerTimeActive)
            {
                // Auto-pause when app loses focus during prayer
                SetPausedState(!hasFocus || IsPaused);
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!IsGameInitialized) return;
            
            // Handle mobile call interruptions during gameplay
            if (pauseStatus && CurrentGameState == GameState.Playing)
            {
                SetPausedState(true);
                _saveSystem?.AutoSave();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        #endregion

        #region Core Initialization
        [ContextMenu("Initialize Game Core")]
        public void InitializeGameCore()
        {
            if (IsGameInitialized)
            {
                Debug.LogWarning("[RVA:TAC] Game core already initialized.");
                return;
            }

            try
            {
                #region Version Verification
                if (!_versionControl.VerifyBuildCompatibility())
                {
                    Debug.LogError("[RVA:TAC] Version compatibility check failed. Aborting initialization.");
                    ShowVersionMismatchDialog();
                    return;
                }
                #endregion

                #region Save System Load
                _saveSystem.InitializeSaveStructure();
                if (_saveSystem.HasExistingSave())
                {
                    var loadResult = _saveSystem.LoadGame();
                    if (loadResult.Success)
                    {
                        Debug.Log("[RVA:TAC] Existing save loaded successfully.");
                        StartingIslandID = loadResult.IslandID;
                    }
                    else
                    {
                        Debug.LogWarning($"[RVA:TAC] Save load failed: {loadResult.ErrorMessage}. Starting fresh.");
                    }
                }
                #endregion

                #initialize Prayer & Time Systems
                _prayerTimeSystem.InitializeForMaldives();
                _timeSystem.InitializeMaldivianTime();
                #endregion

                #initialize Audio & Scene Management
                _audioManager.InitializeAudioMixer();
                _audioManager.SetMasterVolume(MasterVolume);
                _sceneManager.InitializeSceneController();
                #endregion

                #apply Cultural Settings
                ApplyCulturalDifficultySettings();
                #endregion

                IsGameInitialized = true;
                CurrentGameState = GameState.MainMenu;

                Debug.Log($"[RVA:TAC] Game core initialized successfully. Active island: {StartingIslandID}");
                
                // Trigger initialization event for other systems
                OnMasterVolumeChanged?.Invoke(MasterVolume);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RVA:TAC] CRITICAL: Game initialization failed - {ex.Message}\n{ex.StackTrace}");
                _debugSystem.ReportCriticalError("InitializationFailure", ex);
                ShowInitializationErrorDialog();
            }
        }
        #endregion

        #region Cultural Management
        private void HandlePrayerTimeReached(PrayerName prayer)
        {
            IsPrayerTimeActive = true;
            
            if (RespectPrayerTimes && !DebugSkipCulturalRules)
            {
                SetPausedState(true);
                ShowPrayerTimeNotification(prayer);
                
                // Fade audio respectfully
                _audioManager.FadeToVolume(0.1f, 2f);
            }
            
            OnPrayerTimeBegins?.Invoke(prayer);
            
            Debug.Log($"[RVA:TAC] Prayer time reached: {prayer}. Game paused: {IsPaused}");
        }

        private void HandlePrayerTimeEnded(PrayerName prayer)
        {
            IsPrayerTimeActive = false;
            
            // Don't auto-resume if other pause conditions exist
            if (!CheckAnyPauseConditions())
            {
                SetPausedState(false);
                _audioManager.RestoreVolume();
            }
            
            OnPrayerTimeEnds?.Invoke(prayer);
            
            Debug.Log($"[RVA:TAC] Prayer time ended: {prayer}. Game resumed: {!IsPaused}");
        }

        private void CheckFridayJumuahPeriod()
        {
            if (!RespectFridayPrayer || DebugSkipCulturalRules) return;
            
            var now = _timeSystem.GetCurrentMaldivianTime();
            bool wasFridayJumuah = IsFridayJumuah;
            
            // Jumu'ah: Friday 12:00-14:00 Maldives time
            IsFridayJumuah = now.DayOfWeek == 5 && now.Hour >= 12 && now.Hour < 14;
            
            if (IsFridayJumuah != wasFridayJumuah)
            {
                if (IsFridayJumuah)
                {
                    SetPausedState(true);
                    OnFridayJumuahBegins?.Invoke();
                    ShowJumuahNotification();
                }
                else
                {
                    if (!CheckAnyPauseConditions())
                    {
                        SetPausedState(false);
                    }
                    OnFridayJumuahEnds?.Invoke();
                }
            }
        }

        private void CheckMaldivianBusinessHours()
        {
            if (!EnforceBusinessHours || DebugSkipCulturalRules) return;
            
            var now = _timeSystem.GetCurrentMaldivianTime();
            // Maldivian standard business hours: 9AM - 5PM (some variation by island)
            bool isBusinessHours = now.Hour >= 9 && now.Hour < 17;
            
            // This would affect mission availability, shop hours, etc.
            // Implementation would query this from mission/shops systems
        }

        private void ApplyCulturalDifficultySettings()
        {
            switch (CulturalDifficulty)
            {
                case CulturalDifficultyLevel.Authentic:
                    RespectPrayerTimes = true;
                    RespectFridayPrayer = true;
                    EnforceBusinessHours = true;
                    break;
                case CulturalDifficultyLevel.Modern:
                    RespectPrayerTimes = true;
                    RespectFridayPrayer = true;
                    EnforceBusinessHours = false;
                    break;
                case CulturalDifficultyLevel.Relaxed:
                    RespectPrayerTimes = false;
                    RespectFridayPrayer = false;
                    EnforceBusinessHours = false;
                    break;
            }
            
            Debug.Log($"[RVA:TAC] Cultural difficulty applied: {CulturalDifficulty}");
        }
        #endregion

        #region Pause & State Management
        public void SetPausedState(bool paused)
        {
            bool wasPaused = IsPaused;
            IsPaused = paused;
            
            Time.timeScale = paused ? 0f : 1f;
            
            if (_audioManager != null)
            {
                _audioManager.SetMasterVolume(paused ? MasterVolume * 0.3f : MasterVolume);
            }
            
            if (paused != wasPaused)
            {
                Debug.Log($"[RVA:TAC] Game pause state changed: {paused}");
            }
        }

        private bool CheckAnyPauseConditions()
        {
            return IsPrayerTimeActive || IsFridayJumuah || CurrentGameState == GameState.PausedMenu;
        }

        private void HandleMobileBackButton()
        {
            switch (CurrentGameState)
            {
                case GameState.Playing:
                    SetGameState(GameState.PausedMenu);
                    break;
                case GameState.PausedMenu:
                    SetGameState(GameState.Playing);
                    break;
                case GameState.MainMenu:
                    // Confirm quit dialog on mobile
                    ShowQuitConfirmation();
                    break;
            }
        }

        public void SetGameState(GameState newState)
        {
            GameState oldState = CurrentGameState;
            CurrentGameState = newState;
            
            Debug.Log($"[RVA:TAC] Game state: {oldState} -> {newState}");
            
            // Handle state transitions
            switch (newState)
            {
                case GameState.Playing:
                    if (CheckAnyPauseConditions())
                    {
                        SetPausedState(true);
                    }
                    else
                    {
                        SetPausedState(false);
                    }
                    break;
                    
                case GameState.PausedMenu:
                case GameState.MainMenu:
                case GameState.Settings:
                    SetPausedState(true);
                    break;
                    
                case GameState.Loading:
                    SetPausedState(true);
                    break;
            }
        }
        #endregion

        #region Audio Control
        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            _audioManager?.SetMasterVolume(MasterVolume);
            OnMasterVolumeChanged?.Invoke(MasterVolume);
            
            // Auto-save volume preference
            PlayerPrefs.SetFloat("RVA_MasterVolume", MasterVolume);
            PlayerPrefs.Save();
        }

        public float GetMasterVolume()
        {
            return MasterVolume;
        }
        #endregion

        #region Scene & Level Management
        public void LoadIsland(int islandID, bool showLoadingScreen = true)
        {
            if (islandID < 1 || islandID > TotalIslands)
            {
                Debug.LogError($"[RVA:TAC] Invalid island ID: {islandID}. Valid range: 1-{TotalIslands}");
                return;
            }
            
            Debug.Log($"[RVA:TAC] Loading island {islandID}...");
            
            if (showLoadingScreen)
            {
                SetGameState(GameState.Loading);
            }
            
            _sceneManager.LoadIslandScene(islandID, OnIslandLoadComplete);
        }

        private void OnIslandLoadComplete(bool success, int islandID)
        {
            if (success)
            {
                Debug.Log($"[RVA:TAC] Island {islandID} loaded successfully.");
                SetGameState(GameState.Playing);
                _saveSystem?.SaveCurrentIsland(islandID);
            }
            else
            {
                Debug.LogError($"[RVA:TAC] Failed to load island {islandID}.");
                ShowLoadingErrorDialog();
                SetGameState(GameState.MainMenu);
            }
        }
        #endregion

        #region Mobile-Specific
        private void HandleLowMemoryDuringPrayer()
        {
            if (IsPrayerTimeActive)
            {
                Debug.LogWarning("[RVA:TAC] Low memory during prayer time. Aggressive cleanup initiated.");
                
                // Force garbage collection during prayer pause
                Resources.UnloadUnusedAssets();
                GC.Collect();
            }
        }

        public void HandleCallInterruption()
        {
            // Mobile call interruption handler
            if (CurrentGameState == GameState.Playing)
            {
                SetPausedState(true);
                _saveSystem.AutoSave();
                Debug.Log("[RVA:TAC] Call interruption handled - game paused and auto-saved.");
            }
        }
        #endregion

        #region UI Dialog Methods
        private void ShowPrayerTimeNotification(PrayerName prayer)
        {
            string prayerNameDhivehi = GetDhivehiPrayerName(prayer);
            string message = $"🕌 Time for {prayer} ({prayerNameDhivehi})\nThe game will resume after prayer time.";
            
            // UI Manager would show this as overlay
            Debug.Log($"[RVA:TAC] CULTURAL NOTICE: {message}");
        }

        private void ShowJumuahNotification()
        {
            string message = "🕌 Jumu'ah Prayer (Friday Congregational)\n12:00 PM - 2:00 PM\nThe game will resume after prayer time.";
            
            Debug.Log($"[RVA:TAC] CULTURAL NOTICE: {message}");
        }

        private void ShowInitializationErrorDialog()
        {
            string title = "Game Initialization Failed";
            string message = "Unable to start RAAJJE VAGU AUTO. Please restart the application or reinstall if problem persists.";
            
            Debug.LogError($"[RVA:TAC] UI Dialog: {title} - {message}");
        }

        private void ShowVersionMismatchDialog()
        {
            string title = "Version Mismatch";
            string message = $"Your save data is incompatible with this version ({VersionControlSystem.VERSION}). Please update or start a new game.";
            
            Debug.LogError($"[RVA:TAC] UI Dialog: {title} - {message}");
        }

        private void ShowLoadingErrorDialog()
        {
            string title = "Island Loading Error";
            string message = "Failed to load island data. Please check your connection or try again.";
            
            Debug.LogError($"[RVA:TAC] UI Dialog: {title} - {message}");
        }

        private void ShowQuitConfirmation()
        {
            string title = "Exit Game?";
            string message = "Are you sure you want to exit RAAJJE VAGU AUTO?";
            
            Debug.Log($"[RVA:TAC] UI Dialog: {title} - {message}");
        }
        #endregion

        #region Cultural Helper Methods
        private string GetDhivehiPrayerName(PrayerName prayer)
        {
            return prayer switch
            {
                PrayerName.Fajr => "ފަޖްރު",
                PrayerName.Dhuhr => "ދުހުރު",
                PrayerName.Asr => "އަސުރު",
                PrayerName.Maghrib => "މަޣުރިބު",
                PrayerName.Isha => "ޢިޝާ",
                _ => prayer.ToString()
            };
        }
        #endregion

        #region Public API
        public bool IsReady()
        {
            return IsGameInitialized && _saveSystem != null && _prayerTimeSystem != null;
        }

        public string GetBuildInfo()
        {
            return $"RAAJJE VAGU AUTO v{VersionControlSystem.VERSION} | Unity {Application.unityVersion} | Platform: {Application.platform}";
        }

        public void EmergencyShutdown()
        {
            Debug.LogError("[RVA:TAC] EMERGENCY SHUTDOWN initiated.");
            
            _saveSystem?.AutoSave();
            SetPausedState(true);
            CurrentGameState = GameState.MainMenu;
            
            // Force cleanup
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
        #endregion
    }

    #region Enums
    public enum GameState
    {
        MainMenu,
        Playing,
        PausedMenu,
        Settings,
        Loading,
        Cutscene,
        GameOver
    }

    public enum CulturalDifficultyLevel
    {
        Authentic,  // Full cultural compliance
        Modern,     // Prayer times respected, business hours flexible
        Relaxed     // Cultural rules disabled
    }
    #endregion
}
