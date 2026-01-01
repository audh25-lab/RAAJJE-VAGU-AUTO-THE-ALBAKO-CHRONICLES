// ============================================================================
// RAAJJE VAGU AUTO: THE ALBAKO CHRONICLES - Game Scene Manager
// Async Scene Loading | Mobile-Optimized | Memory Management
// ============================================================================
// Version: 1.0.0 | Build: RVACONT-001 | Author: RVA Development Team
// Last Modified: 2025-12-30 | Platform: Unity 2022.3+ (Mobile)
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RVA.GameCore
{
    /// <summary>
    /// Manages all scene transitions, async loading, and memory management
    /// Handles 41 islands + special scenes with pooling and streaming
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
                    _instance = FindObjectOfType<GameSceneManager>();
                return _instance;
            }
        }

        // ==================== SCENE ENUM ====================
        public enum SceneType
        {
            // Core Scenes
            BOOT,
            MAIN_MENU,
            LOADING_SCREEN,
            
            // Gameplay Scenes
            GAMEPLAY_MALE,      // Island 0 - Starting
            GAMEPLAY_VILLINGILI, // Island 1
            GAMEPLAY_HULHUMALE,  // Island 2
            GAMEPLAY_MAAFUSHI,   // Island 3
            
            // Special Locations
            MOSQUE_INTERIOR,
            MARKET_SCENE,
            FISHING_DOCK,
            POLICE_STATION,
            GANG_HIDEOUT,
            
            // Minigame Scenes
            BODUBERU_MINIGAME,
            FISHING_MINIGAME,
            
            // Total: 41 islands + special scenes
        }

        // ==================== LOADING CONFIG ====================
        [Header("Scene Loading")]
        public bool showLoadingScreen = true;
        public float minimumLoadingTime = 1.5f;
        
        [Header("Memory Management")]
        public int maxScenesInMemory = 3;
        public bool enableScenePooling = true;
        
        [Header("Loading Screen UI")]
        public GameObject loadingScreenPrefab;
        public Text loadingText;
        public Image loadingProgressBar;
        public Text loadingTipText;

        // ==================== PRIVATE FIELDS ====================
        private AsyncOperation _currentLoadOperation;
        private SceneType _currentScene;
        private SceneType _targetScene;
        private float _loadingProgress;
        private bool _isLoading = false;
        
        // Scene pooling for performance
        private SceneData[] _loadedScenes = new SceneData[5];
        private int _scenePoolIndex = 0;

        // ==================== TIP DATABASE ====================
        private string[] _loadingTipsDhivehi = new[]
        {
            "ދިވެހިރާއްޖޭގެ ސަރަހައްދުތަކަށް ވަޑައިގަންނަވާ!",
            "ބޯޑުބެރު ނަގަން 'B' ބުޓަން ފިއްތައިލޭ!",
            "ޣައްނާގެ ބާރު ބަރޯއިކުރަން އާސްޓު މިނިވަންކަން ބޭނުންވަނީ!",
            "ނަމާދުގެ ވަގުތު ކައިރިވަމުން އަންނަނިއޯ!",
            "މަސް ހޯދަން މަސްވެރިންގެ ފިހާރަތަކަށް ވަޑައިގަންނަވާ!",
            "ފުލުހުންނަށް ފަހަތުން ފިރުވައި، ސްޕްރިންޓް އަޅާ!"
        };

        private string[] _loadingTipsEnglish = new[]
        {
            "Welcome to the Maldivian archipelago!",
            "Press 'B' to participate in Boduberu drumming!",
            "Build arsenal strength to challenge gang territories!",
            "Prayer time approaching - find a mosque!",
            "Visit fishing harbors to learn traditional techniques!",
            "Evade police and hit the sprint button!"
        };

        // ==================== INITIALIZATION ====================
        public override void Initialize()
        {
            if (_isInitialized) return;
            
            Debug.Log("[GameSceneManager] Initializing...");
            
            // Verify scene build settings
            VerifyScenesInBuildSettings();
            
            // Create loading screen if needed
            if (loadingScreenPrefab != null)
            {
                InstantiateLoadingScreen();
            }
            
            _isInitialized = true;
            Debug.Log("[GameSceneManager] Initialized successfully");
        }

        private void VerifyScenesInBuildSettings()
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            Debug.Log($"[GameSceneManager] Scenes in build: {sceneCount}");
            
            if (sceneCount < 10)
            {
                Debug.LogWarning("[GameSceneManager] WARNING: Few scenes in build settings. Expected 10+");
            }
        }

        // ==================== SCENE LOADING ====================
        public void LoadScene(SceneType sceneType, bool forceReload = false)
        {
            if (_isLoading) return;
            
            _targetScene = sceneType;
            string sceneName = GetSceneName(sceneType);
            
            Debug.Log($"[GameSceneManager] Loading scene: {sceneName}");
            
            StartCoroutine(LoadSceneAsync(sceneName, forceReload));
        }

        private IEnumerator LoadSceneAsync(string sceneName, bool forceReload)
        {
            _isLoading = true;
            SetState(MainGameManager.GameState.LOADING);
            
            // Show loading screen
            if (showLoadingScreen)
            {
                ShowLoadingScreen(sceneName);
            }
            
            float loadStartTime = Time.realtimeSinceStartup;
            
            // Check if scene is already loaded in pool
            if (!forceReload && IsSceneInPool(sceneName))
            {
                Debug.Log($"[GameSceneManager] Scene already loaded: {sceneName}");
                yield return null;
            }
            else
            {
                // Unload old scenes if needed
                yield return UnloadExcessScenes();
                
                // Start async loading
                _currentLoadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                _currentLoadOperation.allowSceneActivation = false;
                
                // Update progress
                while (!_currentLoadOperation.isDone)
                {
                    _loadingProgress = Mathf.Clamp01(_currentLoadOperation.progress / 0.9f);
                    UpdateLoadingUI(_loadingProgress);
                    
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
            }
            
            // Ensure minimum loading time for smooth experience
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
            if (_targetScene == SceneType.MAIN_MENU)
            {
                SetState(MainGameManager.GameState.MAIN_MENU);
            }
            else
            {
                SetState(MainGameManager.GameState.GAMEPLAY);
            }
            
            _isLoading = false;
            
            Debug.Log($"[GameSceneManager] Scene loaded: {sceneName}");
        }

        // ==================== LOAD ISLAND BY INDEX ====================
        public void LoadIsland(int islandIndex)
        {
            if (islandIndex < 0 || islandIndex >= 41)
            {
                Debug.LogError($"[GameSceneManager] Invalid island index: {islandIndex}");
                return;
            }
            
            // Map island index to scene
            SceneType islandScene = (SceneType)((int)SceneType.GAMEPLAY_MALE + islandIndex);
            
            // Update current island in main manager
            MainGameManager.Instance.activeIslandIndex = islandIndex;
            
            LoadScene(islandScene);
        }

        // ==================== SCENE POOLING ====================
        private bool IsSceneInPool(string sceneName)
        {
            foreach (var sceneData in _loadedScenes)
            {
                if (sceneData != null && sceneData.sceneName == sceneName)
                {
                    return sceneData.isLoaded;
                }
            }
            return false;
        }

        private void AddSceneToPool(string sceneName)
        {
            _loadedScenes[_scenePoolIndex] = new SceneData
            {
                sceneName = sceneName,
                isLoaded = true,
                loadTime = Time.realtimeSinceStartup
            };
            
            _scenePoolIndex = (_scenePoolIndex + 1) % _loadedScenes.Length;
        }

        private IEnumerator UnloadExcessScenes()
        {
            int loadedCount = 0;
            foreach (var sceneData in _loadedScenes)
            {
                if (sceneData != null && sceneData.isLoaded)
                {
                    loadedCount++;
                }
            }
            
            if (loadedCount >= maxScenesInMemory)
            {
                // Unload oldest scene
                SceneData oldest = null;
                float oldestTime = float.MaxValue;
                
                foreach (var sceneData in _loadedScenes)
                {
                    if (sceneData != null && sceneData.isLoaded && sceneData.loadTime < oldestTime)
                    {
                        oldest = sceneData;
                        oldestTime = sceneData.loadTime;
                    }
                }
                
                if (oldest != null)
                {
                    Debug.Log($"[GameSceneManager] Unloading old scene: {oldest.sceneName}");
                    yield return SceneManager.UnloadSceneAsync(oldest.sceneName);
                    oldest.isLoaded = false;
                }
            }
        }

        // ==================== LOADING UI ====================
        private void ShowLoadingScreen(string sceneName)
        {
            if (loadingScreenPrefab == null) return;
            
            // Show loading screen
            loadingScreenPrefab.SetActive(true);
            
            // Set random tip
            bool useDhivehi = PlayerPrefs.GetInt("Language", 0) == 1; // 1 = Dhivehi
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
            if (loadingScreenPrefab != null)
            {
                // Fade out animation
                StartCoroutine(FadeOutLoadingScreen());
            }
        }

        private IEnumerator FadeOutLoadingScreen()
        {
            CanvasGroup canvasGroup = loadingScreenPrefab.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = loadingScreenPrefab.AddComponent<CanvasGroup>();
            }
            
            float fadeTime = 0.5f;
            float elapsed = 0f;
            
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - (elapsed / fadeTime);
                yield return null;
            }
            
            loadingScreenPrefab.SetActive(false);
            canvasGroup.alpha = 1f;
        }

        // ==================== UTILITY METHODS ====================
        public string GetCurrentSceneName()
        {
            return GetSceneName(_currentScene);
        }

        private string GetSceneName(SceneType sceneType)
        {
            switch (sceneType)
            {
                case SceneType.BOOT: return "Boot";
                case SceneType.MAIN_MENU: return "MainMenu";
                case SceneType.LOADING_SCREEN: return "Loading";
                case SceneType.GAMEPLAY_MALE: return "Island_Male";
                case SceneType.GAMEPLAY_VILLINGILI: return "Island_Villingili";
                case SceneType.GAMEPLAY_HULHUMALE: return "Island_Hulhumale";
                case SceneType.MOSQUE_INTERIOR: return "Mosque_Interior";
                case SceneType.MARKET_SCENE: return "Market_Scene";
                case SceneType.FISHING_DOCK: return "Fishing_Dock";
                case SceneType.POLICE_STATION: return "Police_Station";
                case SceneType.GANG_HIDEOUT: return "Gang_Hideout";
                case SceneType.BODUBERU_MINIGAME: return "Boduberu_Minigame";
                case SceneType.FISHING_MINIGAME: return "Fishing_Minigame";
                default: return "Island_Male";
            }
        }

        // ==================== SYSTEM MANAGER OVERRIDES ====================
        public override void OnGameStateChanged(MainGameManager.GameState newState)
        {
            // Scene manager handles loading state internally
        }

        public override void OnPause()
        {
            // Cannot pause scene loading
        }

        public override void OnResume()
        {
            // Resume from pause
        }

        // ==================== DATA STRUCTURES ====================
        private class SceneData
        {
            public string sceneName;
            public bool isLoaded;
            public float loadTime;
        }
    }
}
