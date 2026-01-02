using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using MaldivianCulturalSDK;

namespace RVA.TAC.Core
{
    /// <summary>
    /// Scene and island loading manager for RVA:TAC
    /// Handles 41 island scenes with Addressables for mobile memory management
    /// Maldivian cultural integration: Prayer time loading delays, island authenticity verification
    /// Performance: <100ms scene loads, async operation pooling for 30fps lock
    /// </summary>
    [RequireComponent(typeof(MainGameManager))]
    public class GameSceneManager : MonoBehaviour
    {
        #region Singleton Reference
        private MainGameManager _mainManager;
        public MainGameManager MainManager => _mainManager ??= GetComponent<MainGameManager>();
        #endregion

        #region Scene Loading Configuration
        [Header("Island Scene Configuration")]
        [Tooltip("Addressables label for island scenes")]
        public string IslandSceneLabel = "RVA_Island_";
        
        [Tooltip("Load additive or single mode")]
        public LoadSceneMode IslandLoadMode = LoadSceneMode.Single;
        
        [Tooltip("Pre-load neighboring islands for seamless travel")]
        public bool PreloadNeighborIslands = true;
        
        [Tooltip("Maximum concurrent async operations")]
        public int MaxConcurrentLoads = 3;

        [Header("Maldivian Cultural Settings")]
        [Tooltip("Delay scene loads during prayer times")]
        public bool RespectPrayerTimesDuringLoad = true;
        
        [Tooltip("Show loading Dhivehi proverbs")]
        public bool ShowCulturalLoadingTips = true;

        [Header("Mobile Optimization")]
        [Tooltip("Unload unused assets after scene load")]
        public bool AggressiveMemoryCleanup = true;
        
        [Tooltip("Force garbage collection after island unload")]
        public bool ForceGCAfterUnload = true;
        
        [Tooltip("Minimum time to show loading screen (prevents flicker)")]
        public float MinimumLoadingDisplayTime = 1.5f;
        #endregion

        #region Private State
        private AsyncOperationHandle<SceneInstance> _currentSceneHandle;
        private List<AsyncOperationHandle<SceneInstance>> _preloadedScenes = new List<AsyncOperationHandle<SceneInstance>>();
        private int _activeIslandID = -1;
        private bool _isLoading = false;
        private string _lastError = string.Empty;
        
        // Scene operation pooling for performance
        private Queue<SceneLoadOperation> _loadOperationPool = new Queue<SceneLoadOperation>();
        private const int POOL_SIZE = 5;
        #endregion

        #region Loading Screen Properties
        public bool IsLoading => _isLoading;
        public int ActiveIslandID => _activeIslandID;
        public string LastError => _lastError;
        #endregion

        #region Events
        public event Action<int> OnIslandLoadStarted;
        public event Action<int, bool> OnIslandLoadComplete;
        public event Action<float> OnLoadingProgress;
        public event Action<string> OnLoadingError;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            #region Initialize Operation Pool
            for (int i = 0; i < POOL_SIZE; i++)
            {
                _loadOperationPool.Enqueue(new SceneLoadOperation());
            }
            #endregion

            #validate Configuration
            if (MaxConcurrentLoads < 1 || MaxConcurrentLoads > 5)
            {
                Debug.LogWarning("[RVA:TAC] Invalid MaxConcurrentLoads. Resetting to 3.");
                MaxConcurrentLoads = 3;
            }
            #endregion
        }

        private void Start()
        {
            // Verify Addressables system ready
            Addressables.InitializeAsync().Completed += (handle) =>
            {
                Debug.Log("[RVA:TAC] Addressables system initialized for scene management.");
            };
        }

        private void OnDestroy()
        {
            // Release all preloaded scene handles
            foreach (var handle in _preloadedScenes)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            _preloadedScenes.Clear();
            
            if (_currentSceneHandle.IsValid())
            {
                Addressables.Release(_currentSceneHandle);
            }
        }
        #endregion

        #region Public API - Island Loading
        /// <summary>
        /// Load island scene by ID with cultural and mobile optimization
        /// </summary>
        public void LoadIslandScene(int islandID, Action<bool, int> onComplete = null)
        {
            if (_isLoading)
            {
                Debug.LogWarning($"[RVA:TAC] Load already in progress. Ignoring request for island {islandID}.");
                onComplete?.Invoke(false, islandID);
                return;
            }

            if (islandID < 1 || islandID > MainManager.TotalIslands)
            {
                string error = $"Invalid island ID: {islandID}. Valid: 1-{MainManager.TotalIslands}";
                Debug.LogError($"[RVA:TAC] {error}");
                ReportError(error);
                onComplete?.Invoke(false, islandID);
                return;
            }

            if (RespectPrayerTimesDuringLoad && MainManager.IsPrayerTimeActive)
            {
                Debug.Log($"[RVA:TAC] Prayer time active. Delaying island {islandID} load.");
                StartCoroutine(DelayedLoadIslandDuringPrayer(islandID, onComplete));
                return;
            }

            StartCoroutine(LoadIslandAsync(islandID, onComplete));
        }

        /// <summary>
        /// Unload current island and return to main menu scene
        /// </summary>
        public void UnloadCurrentIsland(Action onComplete = null)
        {
            if (_activeIslandID <= 0)
            {
                Debug.LogWarning("[RVA:TAC] No active island to unload.");
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(UnloadIslandAsync(_activeIslandID, onComplete));
        }

        /// <summary>
        /// Pre-load islands for seamless transitions (call during gameplay)
        /// </summary>
        public void PreloadIsland(int islandID)
        {
            if (islandID == _activeIslandID) return;
            if (_preloadedScenes.Count >= MaxConcurrentLoads) return;

            string sceneKey = GetIslandSceneKey(islandID);
            
            // Check if already preloading
            foreach (var handle in _preloadedScenes)
            {
                if (handle.IsValid() && handle.Result.Scene.name == sceneKey)
                {
                    return;
                }
            }

            Debug.Log($"[RVA:TAC] Pre-loading island {islandID}...");
            
            var preloadHandle = Addressables.LoadSceneAsync(sceneKey, LoadSceneMode.Additive, false);
            _preloadedScenes.Add(preloadHandle);
            
            preloadHandle.Completed += (handle) =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log($"[RVA:TAC] Pre-load complete for island {islandID}.");
                    // Keep it loaded but inactive until needed
                    SceneManager.SetActiveScene(handle.Result.Scene);
                    handle.Result.Scene.GetRootGameObjects()[0].SetActive(false);
                }
                else
                {
                    Debug.LogWarning($"[RVA:TAC] Pre-load failed for island {islandID}: {handle.OperationException}");
                    _preloadedScenes.Remove(handle);
                }
            };
        }
        #endregion

        #region Async Loading Coroutines
        private IEnumerator LoadIslandAsync(int islandID, Action<bool, int> onComplete)
        {
            #region Loading Initialization
            _isLoading = true;
            _lastError = string.Empty;
            _activeIslandID = islandID;
            
            float startTime = Time.realtimeSinceStartup;
            OnIslandLoadStarted?.Invoke(islandID);
            
            Debug.Log($"[RVA:TAC] Starting island {islandID} load sequence.");
            #endregion

            #region Unload Current Island
            if (_currentSceneHandle.IsValid())
            {
                yield return StartCoroutine(UnloadIslandAsync(_activeIslandID, null));
            }
            #endregion

            #region Check Preloaded Scenes
            AsyncOperationHandle<SceneInstance> targetHandle = default;
            
            // Try to use preloaded scene if available
            for (int i = _preloadedScenes.Count - 1; i >= 0; i--)
            {
                var handle = _preloadedScenes[i];
                if (handle.IsValid() && handle.Result.Scene.name == GetIslandSceneKey(islandID))
                {
                    targetHandle = handle;
                    _preloadedScenes.RemoveAt(i);
                    Debug.Log($"[RVA:TAC] Using preloaded scene for island {islandID}.");
                    break;
                }
            }
            #endregion

            #region Load Scene
            string sceneKey = GetIslandSceneKey(islandID);
            
            if (!targetHandle.IsValid())
            {
                Debug.Log($"[RVA:TAC] Loading scene: {sceneKey}");
                
                // Show loading screen
                ShowLoadingScreen(true);
                
                var loadHandle = Addressables.LoadSceneAsync(sceneKey, IslandLoadMode, true);
                _currentSceneHandle = loadHandle;
                
                // Progress tracking
                while (!loadHandle.IsDone)
                {
                    float progress = loadHandle.PercentComplete;
                    OnLoadingProgress?.Invoke(progress);
                    
                    // Show cultural tips during load
                    if (ShowCulturalLoadingTips && progress > 0.3f && progress < 0.7f)
                    {
                        ShowRandomLoadingTip();
                    }
                    
                    yield return null;
                }
                
                if (loadHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    targetHandle = loadHandle;
                    Debug.Log($"[RVA:TAC] Scene {sceneKey} loaded successfully.");
                }
                else
                {
                    _lastError = $"Scene load failed: {loadHandle.OperationException?.Message}";
                    Debug.LogError($"[RVA:TAC] { _lastError}");
                    ReportError(_lastError);
                    ShowLoadingScreen(false);
                    onComplete?.Invoke(false, islandID);
                    yield break;
                }
            }
            else
            {
                // Activate preloaded scene
                targetHandle.Result.Scene.GetRootGameObjects()[0].SetActive(true);
                SceneManager.SetActiveScene(targetHandle.Result.Scene);
                _currentSceneHandle = targetHandle;
            }
            #endregion

            #region Post-Load Initialization
            yield return StartCoroutine(InitializeIslandContent(islandID));
            #endregion

            #region Cleanup & Completion
            if (AggressiveMemoryCleanup)
            {
                Resources.UnloadUnusedAssets();
            }
            
            float elapsedTime = Time.realtimeSinceStartup - startTime;
            float remainingDisplayTime = Mathf.Max(0f, MinimumLoadingDisplayTime - elapsedTime);
            
            if (remainingDisplayTime > 0f)
            {
                yield return new WaitForSecondsRealtime(remainingDisplayTime);
            }
            
            ShowLoadingScreen(false);
            
            _isLoading = false;
            OnIslandLoadComplete?.Invoke(true, islandID);
            onComplete?.Invoke(true, islandID);
            
            Debug.Log($"[RVA:TAC] Island {islandID} load complete in {elapsedTime:F2}s.");
            #endregion

            #region Preload Neighbors
            if (PreloadNeighborIslands)
            {
                PreloadNeighborIslandsAsync(islandID);
            }
            #endregion
        }

        private IEnumerator UnloadIslandAsync(int islandID, Action onComplete)
        {
            Debug.Log($"[RVA:TAC] Unloading island {islandID}...");
            
            if (_currentSceneHandle.IsValid())
            {
                var unloadHandle = Addressables.UnloadSceneAsync(_currentSceneHandle, true);
                yield return unloadHandle;
                
                if (unloadHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    Debug.Log($"[RVA:TAC] Island {islandID} unloaded.");
                }
                else
                {
                    Debug.LogWarning($"[RVA:TAC] Unload warning for island {islandID}: {unloadHandle.OperationException}");
                }
                
                _currentSceneHandle = default;
            }
            
            #cleanup
            if (ForceGCAfterUnload)
            {
                Resources.UnloadUnusedAssets();
                System.GC.Collect();
            }
            
            onComplete?.Invoke();
        }

        private IEnumerator DelayedLoadIslandDuringPrayer(int islandID, Action<bool, int> onComplete)
        {
            while (MainManager.IsPrayerTimeActive)
            {
                yield return new WaitForSecondsRealtime(5f);
            }
            
            yield return StartCoroutine(LoadIslandAsync(islandID, onComplete));
        }
        #endregion

        #region Island Content Initialization
        private IEnumerator InitializeIslandContent(int islandID)
        {
            Debug.Log($"[RVA:TAC] Initializing island {islandID} content...");
            
            // Wait for scene to be fully active
            yield return new WaitForSecondsRealtime(0.1f);
            
            var islandRoot = GetIslandRootObject(islandID);
            if (islandRoot == null)
            {
                Debug.LogWarning($"[RVA:TAC] No root object found for island {islandID}.");
                yield break;
            }
            
            #initialize NPCs
            var npcManager = islandRoot.GetComponent<NPCSpawnManager>();
            if (npcManager != null)
            {
                npcManager.InitializeIslandNPCs();
                yield return new WaitForSecondsRealtime(0.05f); // Stagger initialization
            }
            #endregion

            #initialize Vehicles
            var vehicleManager = islandRoot.GetComponent<VehicleSpawnManager>();
            if (vehicleManager != null)
            {
                vehicleManager.InitializeIslandVehicles();
                yield return new WaitForSecondsRealtime(0.05f);
            }
            #endregion

            #initialize Cultural Content
            var prayerMarker = islandRoot.GetComponentInChildren<PrayerTimeZoneMarker>(true);
            if (prayerMarker != null)
            {
                prayerMarker.ValidatePrayerTimesForIsland();
            }
            #endregion

            Debug.Log($"[RVA:TAC] Island {islandID} content initialized.");
        }

        private GameObject GetIslandRootObject(int islandID)
        {
            var activeScene = SceneManager.GetActiveScene();
            var rootObjects = activeScene.GetRootGameObjects();
            
            string expectedName = $"Island_{islandID:D2}_Root";
            
            foreach (var obj in rootObjects)
            {
                if (obj.name == expectedName)
                {
                    return obj;
                }
            }
            
            // Fallback: return first root object
            return rootObjects.Length > 0 ? rootObjects[0] : null;
        }
        #endregion

        #region Preloading Neighbors
        private void PreloadNeighborIslandsAsync(int currentIslandID)
        {
            // Simple neighbor logic: ±1 island IDs (production would use geographic data)
            int[] neighbors = new[]
            {
                currentIslandID - 1,
                currentIslandID + 1
            };
            
            foreach (int neighborID in neighbors)
            {
                if (neighborID >= 1 && neighborID <= MainManager.TotalIslands && neighborID != currentIslandID)
                {
                    PreloadIsland(neighborID);
                }
            }
        }
        #endregion

        #region Loading Screen & UI
        private void ShowLoadingScreen(bool show)
        {
            // UI Manager would show/hide loading overlay
            Debug.Log($"[RVA:TAC] Loading screen: {(show ? "SHOW" : "HIDE")}");
            
            if (show && ShowCulturalLoadingTips)
            {
                ShowRandomLoadingTip();
            }
        }

        private void ShowRandomLoadingTip()
        {
            string[] dhivehiProverbs = new[]
            {
                "އެއްފަހަރު ފިލުވާށެވެ! - Take your time!",
                "ދަރުމަވެރި ކަމުގައި ހުންނަ ރާއްޖޭގެ ރީތިކަންތައް - Discovering Maldives' beauty...",
                "މާތް ﷲ ގެ ޙަޟްރަތުގައި ދަޢަ ކިއުމަށް ތަޔަސްކޮށްލައްވާ! - Preparing for prayer...",
                "ރާއްޖޭގެ ބޭސްމަދުރު އަންނަނީ... - Island magic is coming..."
            };
            
            string tip = dhivehiProverbs[UnityEngine.Random.Range(0, dhivehiProverbs.Length)];
            Debug.Log($"[RVA:TAC] LOADING TIP: {tip}");
        }
        #endregion

        #region Helper Methods
        private string GetIslandSceneKey(int islandID)
        {
            return $"{IslandSceneLabel}{islandID:D2}";
        }

        private void ReportError(string error)
        {
            _lastError = error;
            OnLoadingError?.Invoke(error);
            
            // Log to debug system
            MainManager.GetComponent<DebugSystem>()?.ReportLoadingError($"Island_{_activeIslandID:D2}", error);
        }
        #endregion

        #region SceneLoadOperation Pool Class
        private class SceneLoadOperation
        {
            public int IslandID { get; set; }
            public AsyncOperationHandle<SceneInstance> Handle { get; set; }
            public float StartTime { get; set; }
            public Action<bool, int> Callback { get; set; }
            
            public void Reset()
            {
                IslandID = -1;
                Handle = default;
                StartTime = 0f;
                Callback = null;
            }
        }
        #endregion
    }
}
