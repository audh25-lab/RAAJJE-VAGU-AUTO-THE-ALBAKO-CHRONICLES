// ============================================================================
// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES - Game Scene Manager
// Async Scene Loading | Mobile-Optimized | Memory Management | Prayer-Aware
// ============================================================================
// Version: 1.1.0 | Build: RVAIMPL-FIX-002 | Author: RVA Development Team
// Last Modified: 2026-01-02 | Platform: Unity 2022.3+ (Mobile)
// Unity Analyzers Rule Set: FORG0003 (Scene Management), MOPR0001 (Mobile Performance)
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RVA.GameCore
{
    /// <summary>
    /// Manages all scene transitions, async loading, and memory management
    /// Handles 41 islands + special scenes with pooling and streaming
    /// Prayer-time aware - delays transitions during prayer times
    /// </summary>
    public class GameSceneManager : SystemManager
    {
        // ==================== SINGLETON ====================
        private static GameSceneManager _instance;
        public static GameSceneManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameSceneManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("GameSceneManager");
                        _instance = go.AddComponent<GameSceneManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        // ==================== SCENE ENUM ====================
        public enum SceneType
        {
            // Core Scenes
            BOOT = 0,
            MAIN_MENU = 1,
            LOADING_SCREEN = 2,
            
            // Gameplay Scenes (41 Islands: ID 3-43)
            GAMEPLAY_ISLAND_0 = 3,   // Malé (Starting)
            GAMEPLAY_ISLAND_1 = 4,   // Villingili
            GAMEPLAY_ISLAND_2 = 5,   // Hulhumalé
            GAMEPLAY_ISLAND_3 = 6,   // Maafushi
            // ... Add islands 4-40 here (44 total enum values)
            GAMEPLAY_ISLAND_40 = 43, // Last island
            
            // Special Locations (44+)
            MOSQUE_INTERIOR = 44,
            MARKET_SCENE = 45,
            FISHING_DOCK = 46,
            POLICE_STATION = 47,
            GANG_HIDEOUT = 48,
            
            // Minigame Scenes
            BODUBERU_MINIGAME = 49,
            FISHING_MINIGAME = 50,
            
            // Total: 51 scene types (41 islands + 10 special)
        }

        // ==================== LOADING CONFIG ====================
        [Header("Scene Loading")]
        public bool showLoadingScreen = true;
        public float minimumLoadingTime = 1.5f;
        [Tooltip("Delay scene loads during prayer times (Maldivian cultural respect)")]
        public bool respectPrayerTimes = true;
        
        [Header("Memory Management")]
        public int maxScenesInMemory = 3;
        public bool enableScenePooling = true;
        
        [Header("Loading Screen UI")]
        public GameObject loadingScreenPrefab;
        [HideInInspector] public GameObject loadingScreenInstance;
        public Text loadingText;
        public Image loadingProgressBar;
        public Text loadingTipText;
        public Image prayerWarningIcon; // New: Show prayer time warning

        // ==================== PRIVATE FIELDS ====================
        private AsyncOperation _currentLoadOperation;
        private SceneType _currentScene;
        private SceneType _targetScene;
        private float _loadingProgress;
        private bool _isLoading = false;
        private bool _isPaused = false;
        
        // Scene tracking
        private readonly List<SceneData> _scenePool = new List<SceneData>(5);
        private Coroutine _loadCoroutine;
        
        // Prayer time cache
        private PrayerTimeSystem _prayerSystem;
        private bool _isPrayerTime = false;

        // ==================== TIP DATABASE ====================
        private string[] _loadingTipsDhivehi = new[]
        {
            "ދިވެހިރާއްޖޭގެ ސަރަހައްދުތަކަށް ވަޑައިގަންނަވާ!",
            "ބޯޑުބެރު ނަގަން 'B' ބުޓަން ފިއްތައިލޭ!",
            "ޣައްނާގެ ބާރު ބަރޯއިކުރަން އާސްޓު މިނިވަންކަން ބޭނުންވަނީ!",
            "ނަމާދުގެ ވަގުތު ކައިރިވަމުން އަންނަނިއޯ!",
            "މަސް ހޯދަން މަސްވެރިންގެ ފިހާރަތަކަށް ވަޑައިގަންނަވާ!",
            "ފުލުހުންނަށް ފަހަތުން ފިރުވައި، ސްޕްރިންޓް އަޅާ!",
            "ރާޅުގެ ޖޯޝު ބޭނުންވަނީ ކުޅުދުއްފުއްޓާ ހަމައަށް!",
            "ޑްރައިވިން ލައިސެންސް ހޯދަން ޕޮލިސް ސްޓޭޝަނުން!"
        };

        private string[] _loadingTipsEnglish = new[]
        {
            "Welcome to the Maldivian archipelago!",
            "Press 'B' to participate in Boduberu drumming!",
            "Build arsenal strength to challenge gang territories!",
            "Prayer time approaching - find a mosque!",
            "Visit fishing harbors to learn traditional techniques!",
            "Evade police and hit the sprint button!",
            "Need a boat? Visit the Villingili ferry terminal!",
            "Get your driving license from the police station!"
        };

        // ==================== INITIALIZATION ====================
        public override void Initialize()
        {
            if (_isInitialized) return;
            
            Debug.Log($"[RVA:GSM] Initializing... Build: RVAIMPL-FIX-002");
            
            // Get prayer system reference
            _prayerSystem = PrayerTimeSystem.Instance;
            
            // Verify scene build settings
            VerifyScenesInBuildSettings();
            
            // Create loading screen instance
            if (loadingScreenPrefab != null && loadingScreenInstance == null)
            {
                loadingScreenInstance = Instantiate(loadingScreenPrefab);
                DontDestroyOnLoad(loadingScreenInstance);
                loadingScreenInstance.SetActive(false);
                
                // Cache UI references from instance
                CacheLoadingUI();
            }
            
            _isInitialized = true;
            Debug.Log($"[RVA:GSM] Initialized. Max scenes: {maxScenesInMemory}");
        }

        private void CacheLoadingUI()
        {
            if (loadingScreenInstance == null) return;
            
            loadingText = loadingScreenInstance.GetComponentInChildren<Text>();
            loadingProgressBar = loadingScreenInstance.GetComponentInChildren<Image>();
            // Find specific components by name or tag in real implementation
        }

        private void VerifyScenesInBuildSettings()
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            Debug.Log($"[RVA:GSM] Scenes in build: {sceneCount}");
            
            // Verify required core scenes exist
            if (sceneCount < 51) // 41 islands + 10 special
            {
                Debug.LogWarning($"[RVA:GSM] WARNING: Only {sceneCount} scenes in build. Expected 51+ for full Maldives archipelago");
            }
        }

        // ==================== SCENE LOADING ====================
        public void LoadScene(SceneType sceneType, bool forceReload = false)
        {
            if (_isLoading && !forceReload) 
            {
                Debug.LogWarning($"[RVA:GSM] Load already in progress: {_targetScene}");
                return;
            }
            
            // Check prayer time (cultural respect)
            if (respectPrayerTimes && IsPrayerTimeApproaching())
            {
                StartCoroutine(WaitForPrayerTimeEndThenLoad(sceneType, forceReload));
                return;
            }
            
            _targetScene = sceneType;
            string sceneName = GetSceneName(sceneType);
            
            Debug.Log($"[RVA:GSM] Loading scene: {sceneName} (Pool: {_scenePool.Count}/{maxScenesInMemory})");
            
            // Stop existing load if forcing
            if (_loadCoroutine != null)
            {
                StopCoroutine(_loadCoroutine);
            }
            
            _loadCoroutine = StartCoroutine(LoadSceneAsync(sceneName, forceReload));
        }

        private IEnumerator WaitForPrayerTimeEndThenLoad(SceneType sceneType, bool forceReload)
        {
            Debug.Log("[RVA:GSM] Prayer time detected - delaying scene load");
            ShowPrayerTimeWarning();
            
            while (IsPrayerTimeApproaching())
            {
                yield return new WaitForSeconds(1f);
            }
            
            HidePrayerTimeWarning();
            LoadScene(sceneType, forceReload);
        }

        private bool IsPrayerTimeApproaching()
        {
            if (_prayerSystem == null) return false;
            
            // Check if within 5 minutes of prayer time
            DateTime nextPrayer = _prayerSystem.GetNextPrayerTime();
            TimeSpan timeUntil = nextPrayer - DateTime.Now;
            
            return timeUntil.TotalMinutes <= 5 && timeUntil.TotalMinutes > 0;
        }

        private IEnumerator LoadSceneAsync(string sceneName, bool forceReload)
        {
            _isLoading = true;
            SetState(MainGameManager.GameState.LOADING);
            
            // Show loading screen
            if (showLoadingScreen && loadingScreenInstance != null)
            {
                ShowLoadingScreen(sceneName);
            }
            
            float loadStartTime = Time.realtimeSinceStartup;
            
            // Check if scene is already loaded
            Scene existingScene = SceneManager.GetSceneByName(sceneName);
            if (!forceReload && existingScene.IsValid() && existingScene.isLoaded)
            {
                Debug.Log($"[RVA:GSM] Scene already active: {sceneName}");
            }
            else
            {
                // Unload old scenes if needed
                yield return UnloadExcessScenes();
                
                // Start async loading
                _currentLoadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (_currentLoadOperation == null)
                {
                    Debug.LogError($"[RVA:GSM] FAILED to load scene: {sceneName}. Check Build Settings.");
                    yield break;
                }
                
                _currentLoadOperation.allowSceneActivation = false;
                
                // Update progress
                while (!_currentLoadOperation.isDone)
                {
                    _loadingProgress = Mathf.Clamp01(_currentLoadOperation.progress / 0.9f);
                    UpdateLoadingUI(_loadingProgress);
                    
                    // Check for mobile pause
                    if (_isPaused)
                    {
                        yield return new WaitUntil(() => !_isPaused);
                    }
                    
                    if (_currentLoadOperation.progress >= 0.9f)
                    {
                        break;
                    }
                    
                    yield return null;
                }
                
                _currentLoadOperation.allowSceneActivation = true;
                
                // Wait for scene to activate
                while (!_currentLoadOperation.isDone)
                {
                    yield return null;
                }
                
                // Set newly loaded scene as active
                Scene newlyLoadedScene = SceneManager.GetSceneByName(sceneName);
                if (newlyLoadedScene.IsValid())
                {
                    SceneManager.SetActiveScene(newlyLoadedScene);
                }
            }
            
            // Ensure minimum loading time
            float elapsed = Time.realtimeSinceStartup - loadStartTime;
            while (elapsed < minimumLoadingTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Finalize loading
            _currentScene = _targetScene;
            AddSceneToPool(sceneName);
            
            HideLoadingScreen();
            
            // Transition to appropriate state
            SetState(_targetScene == SceneType.MAIN_MENU ? 
                MainGameManager.GameState.MAIN_MENU : 
                MainGameManager.GameState.GAMEPLAY);
            
            _isLoading = false;
            _loadCoroutine = null;
            
            Debug.Log($"[RVA:GSM] Scene loaded successfully: {sceneName}");
        }

        // ==================== LOAD ISLAND BY INDEX ====================
        public void LoadIsland(int islandIndex)
        {
            if (islandIndex < 0 || islandIndex >= 41)
            {
                Debug.LogError($"[RVA:GSM] Invalid island index: {islandIndex}. Must be 0-40.");
                return;
            }
            
            // Calculate enum value (Island 0 = enum value 3)
            SceneType islandScene = (SceneType)((int)SceneType.GAMEPLAY_ISLAND_0 + islandIndex);
            
            // Update current island in main manager
            MainGameManager.Instance.activeIslandIndex = islandIndex;
            
            LoadScene(islandScene);
        }

        // ==================== SCENE POOLING ====================
        private void AddSceneToPool(string sceneName)
        {
            _scenePool.Add(new SceneData
            {
                sceneName = sceneName,
                loadTime = Time.realtimeSinceStartup
            });
            
            Debug.Log($"[RVA:GSM] Scene added to pool: {sceneName} (Count: {_scenePool.Count})");
        }

        private IEnumerator UnloadExcessScenes()
        {
            if (_scenePool.Count <= maxScenesInMemory) yield break;
            
            // Sort by load time (oldest first)
            _scenePool.Sort((a, b) => a.loadTime.CompareTo(b.loadTime));
            
            // Unload oldest scenes
            while (_scenePool.Count > maxScenesInMemory)
            {
                SceneData oldest = _scenePool[0];
                if (oldest.sceneName == GetSceneName(_currentScene))
                {
                    _scenePool.RemoveAt(0);
                    continue; // Don't unload current scene
                }
                
                Debug.Log($"[RVA:GSM] Unloading old scene: {oldest.sceneName}");
                yield return SceneManager.UnloadSceneAsync(oldest.sceneName);
                _scenePool.RemoveAt(0);
            }
        }

        // ==================== LOADING UI ====================
        private void ShowLoadingScreen(string sceneName)
        {
            if (loadingScreenInstance == null) return;
            
            loadingScreenInstance.SetActive(true);
            
            // Set random tip
            bool useDhivehi = PlayerPrefs.GetInt("Language", 0) == 1;
            string[] tips = useDhivehi ? _loadingTipsDhivehi : _loadingTipsEnglish;
            string randomTip = tips[UnityEngine.Random.Range(0, tips.Length)];
            
            if (loadingTipText != null)
            {
                loadingTipText.text = randomTip;
            }
            
            // Update loading text
            if (loadingText != null)
            {
                loadingText.text = useDhivehi ? "ލޯޑުކުރަންވޭ..." : "Loading...";
            }
            
            // Reset progress bar
            if (loadingProgressBar != null)
            {
                loadingProgressBar.fillAmount = 0f;
            }
            
            // Hide prayer warning by default
            if (prayerWarningIcon != null)
            {
                prayerWarningIcon.gameObject.SetActive(false);
            }
        }
        
        private void ShowPrayerTimeWarning()
        {
            if (prayerWarningIcon == null) return;
            prayerWarningIcon.gameObject.SetActive(true);
        }
        
        private void HidePrayerTimeWarning()
        {
            if (prayerWarningIcon == null) return;
            prayerWarningIcon.gameObject.SetActive(false);
        }

        private void UpdateLoadingUI(float progress)
        {
            if (loadingProgressBar != null)
            {
                loadingProgressBar.fillAmount = progress;
            }
        }

        private void HideLoadingScreen()
        {
            if (loadingScreenInstance != null)
            {
                StartCoroutine(FadeOutLoadingScreen());
            }
        }

        private IEnumerator FadeOutLoadingScreen()
        {
            CanvasGroup canvasGroup = loadingScreenInstance.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = loadingScreenInstance.AddComponent<CanvasGroup>();
            }
            
            canvasGroup.alpha = 1f;
            float fadeTime = 0.5f;
            float elapsed = 0f;
            
            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - (elapsed / fadeTime);
                yield return null;
            }
            
            loadingScreenInstance.SetActive(false);
            canvasGroup.alpha = 1f;
        }

        // ==================== UTILITY METHODS ====================
        public string GetCurrentSceneName()
        {
            return GetSceneName(_currentScene);
        }

        private string GetSceneName(SceneType sceneType)
        {
            // Use enum name parsing for islands (more maintainable)
            if (sceneType >= SceneType.GAMEPLAY_ISLAND_0 && sceneType <= SceneType.GAMEPLAY_ISLAND_40)
            {
                int islandIndex = (int)sceneType - (int)SceneType.GAMEPLAY_ISLAND_0;
                return $"Island_{islandIndex:D2}"; // Format: Island_00, Island_01, etc.
            }
            
            switch (sceneType)
            {
                case SceneType.BOOT: return "Boot";
                case SceneType.MAIN_MENU: return "MainMenu";
                case SceneType.LOADING_SCREEN: return "Loading";
                case SceneType.MOSQUE_INTERIOR: return "Mosque_Interior";
                case SceneType.MARKET_SCENE: return "Market_Scene";
                case SceneType.FISHING_DOCK: return "Fishing_Dock";
                case SceneType.POLICE_STATION: return "Police_Station";
                case SceneType.GANG_HIDEOUT: return "Gang_Hideout";
                case SceneType.BODUBERU_MINIGAME: return "Boduberu_Minigame";
                case SceneType.FISHING_MINIGAME: return "Fishing_Minigame";
                default: return "Island_00"; // Default to Malé
            }
        }

        // ==================== MOBILE LIFECYCLE ====================
        private void OnApplicationPause(bool pauseStatus)
        {
            _isPaused = pauseStatus;
            
            if (pauseStatus && _isLoading)
            {
                Debug.Log($"[RVA:GSM] Loading paused due to app interruption");
            }
            else if (!pauseStatus && _isLoading)
            {
                Debug.Log($"[RVA:GSM] Loading resumed after interruption");
            }
        }

        // ==================== SYSTEM MANAGER OVERRIDES ====================
        public override void OnGameStateChanged(MainGameManager.GameState newState)
        {
            // Scene manager handles loading state internally
            // Stop loading if exiting loading state unexpectedly
            if (newState != MainGameManager.GameState.LOADING && _isLoading)
            {
                if (_loadCoroutine != null)
                {
                    StopCoroutine(_loadCoroutine);
                    _loadCoroutine = null;
                }
                _isLoading = false;
                HideLoadingScreen();
            }
        }

        public override void OnPause()
        {
            _isPaused = true;
        }

        public override void OnResume()
        {
            _isPaused = false;
        }

        // ==================== DATA STRUCTURES ====================
        private class SceneData
        {
            public string sceneName;
            public float loadTime;
        }
    }
}
