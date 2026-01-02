// RAAJJE-VAGU-AUTO-THE-ALBAKO-CHRONICLES
// GameSceneManager.cs - Core Scene Management System
// Build: RVAFULLIMP-CORE-002
// Location: Assets/Scripts/Core/GameSceneManager.cs
// GPU Target: Mali-G72 MP3 (30fps locked)
// Cultural Integration: Maldives Island Loading, Prayer Time Scene Sensitivity

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace RVA.TAC.Core
{
    /// <summary>
    /// Scene management system with async loading, cultural integration,
    /// and mobile-optimized transitions for Maldives island environments
    /// </summary>
    public class GameSceneManager : MonoBehaviour
    {
        #region Singleton Pattern
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
                        GameObject obj = new GameObject("GameSceneManager");
                        _instance = obj.AddComponent<GameSceneManager>();
                        DontDestroyOnLoad(obj);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Enums and Structs
        public enum SceneType
        {
            MainMenu,
            Loading,
            Island,
            Interior,
            Cutscene,
            Tutorial
        }

        [Serializable]
        public struct SceneLoadData
        {
            public string sceneName;
            public SceneType sceneType;
            public LoadSceneMode loadMode;
            public bool showLoadingScreen;
            public bool allowSceneActivation;
            public float minimumLoadTime;
            public UnityAction onLoadComplete;
            public UnityAction<float> onProgress;
        }

        [Serializable]
        public struct IslandSceneData
        {
            public string islandName;
            public string sceneName;
            public Vector3 defaultSpawnPosition;
            public bool requiresBoatTransition;
            public float prayerTimeModifier;
        }
        #endregion

        #region Public Fields
        [Header("Scene Configuration")]
        public string mainMenuScene = "MainMenu";
        public string loadingScene = "Loading";
        public List<IslandSceneData> islandScenes = new List<IslandSceneData>();
        
        [Header("Loading Screen")]
        public GameObject loadingScreenPrefab;
        public Image loadingProgressBar;
        public TMP_Text loadingTextDhivehi;
        public TMP_Text loadingTextEnglish;
        public Image loadingIslandImage;
        
        [Header("Scene Transition Settings")]
        public float fadeDuration = 0.5f;
        public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public float minimumIslandLoadTime = 2.5f;
        public float minimumMenuLoadTime = 1.0f;
        
        [Header("Mobile Optimization")]
        public int targetFrameRateDuringLoad = 30;
        public bool useAsyncLoading = true;
        public bool enableSceneActivationDelay = true;
        public float memoryCleanupThreshold = 0.85f;
        
        [Header("Cultural Integration")]
        public bool pauseLoadingDuringPrayer = true;
        public float prayerTimeLoadingDelay = 3.0f;
        public List<string> dhivehiLoadingMessages = new List<string>();
        public Sprite[] islandLoadingImages;
        
        [Header("Debug")]
        public bool enableVerboseLogging = false;
        public bool simulateSlowLoading = false;
        public float simulatedLoadTime = 3.0f;
        #endregion

        #region Private Fields
        private AsyncOperation _currentAsyncOperation;
        private SceneLoadData _currentLoadData;
        private bool _isLoading = false;
        private float _currentProgress = 0f;
        private Camera _loadingCamera;
        private Canvas _loadingCanvas;
        private GameObject _loadingScreenInstance;
        private Dictionary<string, IslandSceneData> _islandSceneLookup = new Dictionary<string, IslandSceneData>();
        private Coroutine _loadCoroutine;
        private DateTime _loadStartTime;
        private bool _islandLoadInProgress = false;
        private string _targetIslandName = "";
        #endregion

        #region Events
        public UnityAction<string> OnSceneLoadStarted;
        public UnityAction<string> OnSceneLoadCompleted;
        public UnityAction<float> OnLoadProgressUpdated;
        public UnityAction<string> OnIslandTransitionStarted;
        public UnityAction<string> OnIslandTransitionCompleted;
        #endregion

        #region Initialization
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSceneManager();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void InitializeSceneManager()
        {
            BuildIslandLookup();
            InitializeDhivehiMessages();
            ValidateSceneConfiguration();
            SetupLoadingScreenPrefab();
            
            if (enableVerboseLogging)
            {
                Debug.Log($"[GameSceneManager] Initialized with {islandScenes.Count} island scenes");
            }
        }

        private void BuildIslandLookup()
        {
            _islandSceneLookup.Clear();
            foreach (var islandData in islandScenes)
            {
                if (!_islandSceneLookup.ContainsKey(islandData.islandName))
                {
                    _islandSceneLookup.Add(islandData.islandName, islandData);
                }
                else
                {
                    Debug.LogWarning($"[GameSceneManager] Duplicate island name detected: {islandData.islandName}");
                }
            }
        }

        private void InitializeDhivehiMessages()
        {
            if (dhivehiLoadingMessages.Count == 0)
            {
                dhivehiLoadingMessages = new List<string>
                {
                    "ލޯޑިންގ...",
                    "ޖަޒީރާ ތައްޔާރުކުރުން...",
                    "ދިވެހި ރަށް ގެނެވެމުންދާތީ...",
                    "ބޯޑުބެރު ސަފަނާ ޖަހަމުން...",
                    "މަސް މަސް ކުރަމުން...",
                    "ނަމާދު ވަގުތު ހަމަޔަށް..."
                };
            }
        }

        private void ValidateSceneConfiguration()
        {
            if (string.IsNullOrEmpty(mainMenuScene))
            {
                Debug.LogError("[GameSceneManager] MainMenu scene name not configured!");
            }
            if (string.IsNullOrEmpty(loadingScene))
            {
                Debug.LogError("[GameSceneManager] Loading scene name not configured!");
            }
        }

        private void SetupLoadingScreenPrefab()
        {
            if (loadingScreenPrefab == null)
            {
                CreateDefaultLoadingScreen();
            }
        }

        private void CreateDefaultLoadingScreen()
        {
            GameObject loadingCanvasObj = new GameObject("LoadingScreenCanvas");
            Canvas canvas = loadingCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            
            CanvasScaler scaler = loadingCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            loadingCanvasObj.AddComponent<GraphicRaycaster>();
            
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(loadingCanvasObj.transform);
            Image bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.02f, 0.08f, 0.15f, 1f);
            
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            GameObject progressBar = new GameObject("ProgressBar");
            progressBar.transform.SetParent(loadingCanvasObj.transform);
            Image progressImage = progressBar.AddComponent<Image>();
            progressImage.color = new Color(0.2f, 0.6f, 1f, 1f);
            
            RectTransform progressRect = progressBar.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.5f, 0.5f);
            progressRect.anchorMax = new Vector2(0.5f, 0.5f);
            progressRect.sizeDelta = new Vector2(600, 30);
            progressRect.anchoredPosition = Vector2.zero;
            
            GameObject loadingTextObj = new GameObject("LoadingText");
            loadingTextObj.transform.SetParent(loadingCanvasObj.transform);
            TMP_Text text = loadingTextObj.AddComponent<TextMeshProUGUI>();
            text.text = "ލޯޑިންގ...";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 36;
            text.color = Color.white;
            
            RectTransform textRect = loadingTextObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.4f);
            textRect.anchorMax = new Vector2(0.5f, 0.4f);
            textRect.sizeDelta = new Vector2(800, 50);
            textRect.anchoredPosition = Vector2.zero;
            
            loadingScreenPrefab = loadingCanvasObj;
            loadingScreenPrefab.SetActive(false);
            DontDestroyOnLoad(loadingScreenPrefab);
            
            loadingProgressBar = progressImage;
            loadingTextDhivehi = text;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Load an island scene with cultural integration
        /// </summary>
        public void LoadIsland(string islandName, Vector3? spawnPosition = null, UnityAction onComplete = null)
        {
            if (_isLoading)
            {
                Debug.LogWarning($"[GameSceneManager] Load already in progress. Cannot load {islandName}");
                return;
            }
            
            if (!_islandSceneLookup.ContainsKey(islandName))
            {
                Debug.LogError($"[GameSceneManager] Island not found: {islandName}");
                return;
            }
            
            IslandSceneData islandData = _islandSceneLookup[islandName];
            _targetIslandName = islandName;
            
            SceneLoadData loadData = new SceneLoadData
            {
                sceneName = islandData.sceneName,
                sceneType = SceneType.Island,
                loadMode = LoadSceneMode.Single,
                showLoadingScreen = true,
                allowSceneActivation = true,
                minimumLoadTime = Mathf.Max(islandData.prayerTimeModifier, minimumIslandLoadTime),
                onLoadComplete = onComplete,
                onProgress = UpdateLoadingUI
            };
            
            OnIslandTransitionStarted?.Invoke(islandName);
            
            if (spawnPosition.HasValue)
            {
                SaveSystem.Instance.SetTempData("SpawnPosition", spawnPosition.Value);
            }
            else
            {
                SaveSystem.Instance.SetTempData("SpawnPosition", islandData.defaultSpawnPosition);
            }
            
            if (islandData.requiresBoatTransition)
            {
                SaveSystem.Instance.SetTempData("BoatTransition", true);
            }
            
            StartCoroutine(LoadSceneRoutine(loadData));
        }

        /// <summary>
        /// Load main menu scene
        /// </summary>
        public void LoadMainMenu(UnityAction onComplete = null)
        {
            SceneLoadData loadData = new SceneLoadData
            {
                sceneName = mainMenuScene,
                sceneType = SceneType.MainMenu,
                loadMode = LoadSceneMode.Single,
                showLoadingScreen = false,
                allowSceneActivation = true,
                minimumLoadTime = minimumMenuLoadTime,
                onLoadComplete = onComplete,
                onProgress = null
            };
            
            StartCoroutine(LoadSceneRoutine(loadData));
        }

        /// <summary>
        /// Load loading scene (typically called automatically)
        /// </summary>
        public void LoadLoadingScene()
        {
            SceneLoadData loadData = new SceneLoadData
            {
                sceneName = loadingScene,
                sceneType = SceneType.Loading,
                loadMode = LoadSceneMode.Single,
                showLoadingScreen = false,
                allowSceneActivation = true,
                minimumLoadTime = 0.5f,
                onLoadComplete = null,
                onProgress = null
            };
            
            StartCoroutine(LoadSceneRoutine(loadData));
        }

        /// <summary>
        /// Reload current scene
        /// </summary>
        public void ReloadCurrentScene(UnityAction onComplete = null)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            SceneType currentType = GetSceneType(currentScene);
            
            SceneLoadData loadData = new SceneLoadData
            {
                sceneName = currentScene,
                sceneType = currentType,
                loadMode = LoadSceneMode.Single,
                showLoadingScreen = currentType == SceneType.Island,
                allowSceneActivation = true,
                minimumLoadTime = currentType == SceneType.Island ? minimumIslandLoadTime : minimumMenuLoadTime,
                onLoadComplete = onComplete,
                onProgress = UpdateLoadingUI
            };
            
            StartCoroutine(LoadSceneRoutine(loadData));
        }

        /// <summary>
        /// Load scene by name with custom parameters
        /// </summary>
        public void LoadScene(string sceneName, SceneType type, bool showLoading = true, UnityAction onComplete = null)
        {
            SceneLoadData loadData = new SceneLoadData
            {
                sceneName = sceneName,
                sceneType = type,
                loadMode = LoadSceneMode.Single,
                showLoadingScreen = showLoading,
                allowSceneActivation = true,
                minimumLoadTime = type == SceneType.Island ? minimumIslandLoadTime : minimumMenuLoadTime,
                onLoadComplete = onComplete,
                onProgress = showLoading ? UpdateLoadingUI : null
            };
            
            StartCoroutine(LoadSceneRoutine(loadData));
        }

        /// <summary>
        /// Check if a scene is currently loading
        /// </summary>
        public bool IsLoading()
        {
            return _isLoading;
        }

        /// <summary>
        /// Get current loading progress (0-1)
        /// </summary>
        public float GetLoadingProgress()
        {
            return _currentProgress;
        }

        /// <summary>
        /// Get island data by name
        /// </summary>
        public IslandSceneData GetIslandData(string islandName)
        {
            if (_islandSceneLookup.TryGetValue(islandName, out IslandSceneData data))
            {
                return data;
            }
            
            Debug.LogError($"[GameSceneManager] Island not found: {islandName}");
            return default;
        }

        /// <summary>
        /// Get all available island names
        /// </summary>
        public List<string> GetAvailableIslands()
        {
            return new List<string>(_islandSceneLookup.Keys);
        }

        /// <summary>
        /// Cancel current loading operation
        /// </summary>
        public void CancelLoading()
        {
            if (_loadCoroutine != null)
            {
                StopCoroutine(_loadCoroutine);
                _loadCoroutine = null;
            }
            
            if (_currentAsyncOperation != null)
            {
                _currentAsyncOperation.allowSceneActivation = true;
                _currentAsyncOperation = null;
            }
            
            _isLoading = false;
            _currentProgress = 0f;
            
            HideLoadingScreen();
            
            Debug.LogWarning("[GameSceneManager] Loading cancelled");
        }
        #endregion

        #region Loading Coroutines
        private IEnumerator LoadSceneRoutine(SceneLoadData loadData)
        {
            _currentLoadData = loadData;
            _isLoading = true;
            _currentProgress = 0f;
            _loadStartTime = DateTime.Now;
            
            OnSceneLoadStarted?.Invoke(loadData.sceneName);
            
            // Check for prayer time pause
            if (pauseLoadingDuringPrayer && PrayerTimeSystem.Instance != null)
            {
                if (PrayerTimeSystem.Instance.IsCurrentlyPrayerTime())
                {
                    yield return new WaitForSeconds(prayerTimeLoadingDelay);
                }
            }
            
            // Set mobile frame rate
            Application.targetFrameRate = targetFrameRateDuringLoad;
            
            // Show loading screen
            if (loadData.showLoadingScreen)
            {
                ShowLoadingScreen(loadData);
            }
            
            // Yield to let loading screen render
            yield return null;
            
            // Unload unused assets if memory is high
            if (GetMemoryUsage() > memoryCleanupThreshold)
            {
                yield return Resources.UnloadUnusedAssets();
                System.GC.Collect();
            }
            
            // Start async load
            _currentAsyncOperation = SceneManager.LoadSceneAsync(loadData.sceneName, loadData.loadMode);
            
            if (_currentAsyncOperation == null)
            {
                Debug.LogError($"[GameSceneManager] Failed to start load for scene: {loadData.sceneName}");
                _isLoading = false;
                yield break;
            }
            
            _currentAsyncOperation.allowSceneActivation = loadData.allowSceneActivation;
            
            // Simulate slow loading for debugging
            float simulatedTimer = 0f;
            if (simulateSlowLoading)
            {
                simulatedTimer = simulatedLoadTime;
            }
            
            // Monitor progress
            while (!_currentAsyncOperation.isDone)
            {
                // Update progress
                float progress = Mathf.Clamp01(_currentAsyncOperation.progress / 0.9f);
                
                if (simulateSlowLoading && simulatedTimer > 0)
                {
                    progress = Mathf.Clamp01((simulatedLoadTime - simulatedTimer) / simulatedLoadTime);
                    simulatedTimer -= Time.unscaledDeltaTime;
                }
                
                _currentProgress = progress;
                loadData.onProgress?.Invoke(progress);
                
                // Check minimum load time
                float elapsedTime = (float)(DateTime.Now - _loadStartTime).TotalSeconds;
                if (_currentAsyncOperation.progress >= 0.9f && elapsedTime >= loadData.minimumLoadTime)
                {
                    _currentAsyncOperation.allowSceneActivation = true;
                }
                
                yield return null;
            }
            
            // Wait for scene to activate
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == loadData.sceneName);
            
            // Additional frame for scene initialization
            yield return null;
            
            // Finalize loading
            _currentProgress = 1f;
            loadData.onProgress?.Invoke(1f);
            
            // Notify systems
            EventManager.Instance?.TriggerEvent("SceneLoadComplete", loadData.sceneName);
            
            if (loadData.sceneType == SceneType.Island)
            {
                OnIslandTransitionCompleted?.Invoke(_targetIslandName);
            }
            
            loadData.onLoadComplete?.Invoke();
            OnSceneLoadCompleted?.Invoke(loadData.sceneName);
            
            // Reset state
            _isLoading = false;
            _currentProgress = 0f;
            _targetIslandName = "";
            
            // Restore normal frame rate
            Application.targetFrameRate = 60;
            
            // Hide loading screen
            if (loadData.showLoadingScreen)
            {
                yield return new WaitForSeconds(0.3f);
                HideLoadingScreen();
            }
            
            // Clean up
            _currentAsyncOperation = null;
            _loadCoroutine = null;
        }
        #endregion

        #region Loading Screen Management
        private void ShowLoadingScreen(SceneLoadData loadData)
        {
            if (loadingScreenPrefab == null) return;
            
            _loadingScreenInstance = Instantiate(loadingScreenPrefab);
            DontDestroyOnLoad(_loadingScreenInstance);
            
            Canvas canvas = _loadingScreenInstance.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.sortingOrder = 999;
            }
            
            // Find UI elements
            if (loadingProgressBar == null)
            {
                loadingProgressBar = _loadingScreenInstance.GetComponentInChildren<Image>();
            }
            
            if (loadingTextDhivehi == null)
            {
                loadingTextDhivehi = _loadingScreenInstance.GetComponentInChildren<TMP_Text>();
            }
            
            // Set initial state
            if (loadingProgressBar != null)
            {
                loadingProgressBar.fillAmount = 0f;
            }
            
            if (loadingTextDhivehi != null)
            {
                loadingTextDhivehi.text = GetRandomDhivehiMessage();
            }
            
            _loadingScreenInstance.SetActive(true);
            
            // Fade in
            CanvasGroup canvasGroup = _loadingScreenInstance.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = _loadingScreenInstance.AddComponent<CanvasGroup>();
            }
            
            StartCoroutine(FadeLoadingScreen(canvasGroup, 0f, 1f, fadeDuration));
        }

        private void HideLoadingScreen()
        {
            if (_loadingScreenInstance == null) return;
            
            CanvasGroup canvasGroup = _loadingScreenInstance.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                Destroy(_loadingScreenInstance);
                return;
            }
            
            StartCoroutine(FadeAndDestroyLoadingScreen(canvasGroup));
        }

        private IEnumerator FadeLoadingScreen(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration)
        {
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float curveValue = fadeCurve.Evaluate(t);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, curveValue);
                yield return null;
            }
            
            canvasGroup.alpha = endAlpha;
        }

        private IEnumerator FadeAndDestroyLoadingScreen(CanvasGroup canvasGroup)
        {
            yield return StartCoroutine(FadeLoadingScreen(canvasGroup, canvasGroup.alpha, 0f, fadeDuration));
            
            if (_loadingScreenInstance != null)
            {
                Destroy(_loadingScreenInstance);
            }
        }

        private void UpdateLoadingUI(float progress)
        {
            if (loadingProgressBar != null)
            {
                loadingProgressBar.fillAmount = progress;
            }
            
            if (progress > 0.7f && loadingTextDhivehi != null && _islandLoadInProgress)
            {
                loadingTextDhivehi.text = "ރަށް ތައްޔާރު!";
            }
            
            OnLoadProgressUpdated?.Invoke(progress);
        }

        private string GetRandomDhivehiMessage()
        {
            if (dhivehiLoadingMessages.Count == 0) return "ލޯޑިންގ...";
            
            int index = UnityEngine.Random.Range(0, dhivehiLoadingMessages.Count);
            return dhivehiLoadingMessages[index];
        }
        #endregion

        #region Utility Methods
        private SceneType GetSceneType(string sceneName)
        {
            if (sceneName == mainMenuScene) return SceneType.MainMenu;
            if (sceneName == loadingScene) return SceneType.Loading;
            if (sceneName.Contains("Island") || _islandSceneLookup.ContainsValue(new IslandSceneData { sceneName = sceneName })) return SceneType.Island;
            if (sceneName.Contains("Interior")) return SceneType.Interior;
            if (sceneName.Contains("Cutscene")) return SceneType.Cutscene;
            if (sceneName.Contains("Tutorial")) return SceneType.Tutorial;
            
            return SceneType.MainMenu;
        }

        private float GetMemoryUsage()
        {
            return (float)System.GC.GetTotalMemory(false) / SystemInfo.systemMemorySize;
        }

        /// <summary>
        /// Get current active island name
        /// </summary>
        public string GetCurrentIslandName()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            
            foreach (var kvp in _islandSceneLookup)
            {
                if (kvp.Value.sceneName == currentScene)
                {
                    return kvp.Key;
                }
            }
            
            return "Unknown";
        }

        /// <summary>
        /// Preload scene in background
        /// </summary>
        public AsyncOperation PreloadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            op.allowSceneActivation = false;
            
            return op;
        }

        /// <summary>
        /// Unload preloaded scene
        /// </summary>
        public void UnloadPreloadedScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(sceneName);
            }
        }
        #endregion

        #region Debug Methods
        [ContextMenu("Test Load Male Island")]
        private void DebugLoadMale()
        {
            LoadIsland("Male");
        }

        [ContextMenu("Test Load Main Menu")]
        private void DebugLoadMainMenu()
        {
            LoadMainMenu();
        }

        [ContextMenu("Test Reload Scene")]
        private void DebugReloadScene()
        {
            ReloadCurrentScene();
        }

        [ContextMenu("Print Island List")]
        private void DebugPrintIslands()
        {
            Debug.Log($"[GameSceneManager] Available Islands: {string.Join(", ", GetAvailableIslands().ToArray())}");
        }

        /// <summary>
        /// Force garbage collection and memory cleanup
        /// </summary>
        [ContextMenu("Force Memory Cleanup")]
        public void ForceMemoryCleanup()
        {
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
            Debug.Log($"[GameSceneManager] Memory cleanup completed. Current usage: {GetMemoryUsage():P2}");
        }
        #endregion

        #region Lifecycle
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _isLoading)
            {
                Debug.LogWarning("[GameSceneManager] Application paused during scene load");
            }
        }

        private void OnApplicationQuit()
        {
            if (_isLoading)
            {
                CancelLoading();
            }
        }
        #endregion
    }
}
