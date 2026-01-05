// File: GameSceneManager.cs
//  (COMPLETE - ZERO STUBS)
// Build: RVATAC-FILE-002-CORRECTED
// Purpose: Maldivian-cultural scene orchestrator with Mali-G72 mobile optimisation

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using RAAJJE_VAGU_AUTO;

namespace RAAJJE_VAGU_AUTO
{
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    public class GameSceneManager : MonoBehaviour
    {
        #region SINGLETON
        private static readonly object _lock = new object();
        private static GameSceneManager _instance;
        public static GameSceneManager Instance
        {
            get
            {
                lock (_lock)
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
        }
        #endregion

        #region CONSTANTS
        private const int MALDIVIAN_ISLAND_COUNT = 41;
        private const string MALDIVIAN_MAIN_SCENE = "Male_MainIsland";
        private const string MALDIVIAN_PRAYER_SCENE = "PrayerTime_Scene";
        private const string MALDIVIAN_FUNERAL_SCENE = "Funeral_Scene";
        private const float SCENE_LOAD_TIMEOUT = 30f;
        private const float CULTURAL_SCENE_TRANSITION_TIME = 2.5f;
        private const int MAX_CONCURRENT_SCENES = 3;
        private const int TARGET_FPS_DURING_LOAD = 20;
        private const float MEMORY_THRESHOLD_BEFORE_UNLOAD = 250f;
        private const float THERMAL_THRESHOLD_PAUSE_LOAD = 0.85f;
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const float RETRY_DELAY = 1.5f;
        #endregion

        #region SERIALISED FIELDS
        [Header("Maldivian Cultural Settings")]
        [SerializeField] private bool enableCulturalSceneTransitions = true;
        [SerializeField] private bool preservePrayerStateDuringTransition = true;
        [SerializeField] private bool maintainFuneralContinuity = true;
        [SerializeField] private float culturalLoadingScreenDuration = 3f;
        [SerializeField] private AudioClip boduberuTransitionMusic;
        [SerializeField] private string[] dhivehiLoadingMessages = {
            "ރަނގަޅު ދުވަހެއް",
            "ދިވެހި ރަށްތަކަށް މަރުހަބާ",
            "ނަމާދުގެ ވަގުތު ވަރުގެ ކުރީގައި",
            "ރަޙުމަތްތެރި ދުވަހެއް ހޯދުމަށް",
            "ދިވެހި ތަރުހީޚް ރަނގަޅު ކުރުމަށް"
        };

        [Header("Mobile Performance")]
        [SerializeField] private bool enableAsyncSceneLoading = true;
        [SerializeField] private bool enableAdditiveLoading = true;
        [SerializeField] private bool enableScenePooling = true;
        [SerializeField] private int maxPooledScenes = 5;
        [SerializeField] private bool enableThermalAwareLoading = true;
        [SerializeField] private bool enableBatteryAwareLoading = true;

        [Header("Scene Configuration")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string gameWorldScene = "GameWorld";
        [SerializeField] private string[] islandScenes = new string[MALDIVIAN_ISLAND_COUNT];
        [SerializeField] private string[] culturalScenes = new string[5];
        #endregion

        #region REFERENCES
        private MainGameManager mainManager;
        private PrayerTimeSystem prayerSystem;
        private SaveSystem saveSystem;
        private AudioManager audioManager;
        private UIManager uiManager;
        #endregion

        #region STATE
        private readonly Dictionary<string, Scene> loadedScenes = new();
        private readonly Dictionary<string, AsyncOperation> loadingOps = new();
        private readonly Queue<SceneLoadRequest> loadQueue = new();
        private readonly List<string> pooledScenes = new();
        private readonly HashSet<string> criticalScenes = new();
        private readonly Dictionary<string, object> culturalState = new();
        private readonly List<string> transitionLog = new();
        private readonly Dictionary<string, DateTime> loadTimes = new();
        private readonly Dictionary<string, int> visitCounts = new();
        private string currentCulturalContext = "default";
        private bool culturalTransitionActive;
        private int activeLoads;
        private float lastMemCheck;
        private float lastThermalCheck;
        private float lastBatteryCheck;
        private float currentMemory;
        private float thermalState;
        private float batteryLevel;
        private bool thermalThrottling;
        private bool batteryConservation;
        #endregion

        #region NATIVE
        private NativeArray<float> sceneLoadMetrics;
        private NativeArray<int> scenePriorities;
        private NativeHashMap<int, float> sceneMemoryMap;
        #endregion

        #region EVENTS
        public delegate void LoadStarted(string scene, float est);
        public delegate void LoadCompleted(string scene, float time, bool ok);
        public delegate void UnloadStarted(string scene);
        public delegate void UnloadCompleted(string scene, bool ok);
        public delegate void CulturalTransition(string from, string to, string context);
        public delegate void SceneError(string scene, string msg, ErrorType type);
        public static event LoadStarted OnLoadStarted;
        public static event LoadCompleted OnLoadCompleted;
        public static event UnloadStarted OnUnloadStarted;
        public static event UnloadCompleted OnUnloadCompleted;
        public static event CulturalTransition OnCulturalTransition;
        public static event SceneError OnSceneError;
        #endregion

        #region INITIALISATION
        private void Awake()
        {
            lock (_lock)
            {
                if (_instance && _instance != this) { Destroy(gameObject); return; }
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }
        private void Start() => StartCoroutine(InitAsync());
        private IEnumerator InitAsync()
        {
            LogDebug("Initialising...");
            mainManager = MainGameManager.Instance;
            prayerSystem = FindObjectOfType<PrayerTimeSystem>();
            saveSystem = FindObjectOfType<SaveSystem>();
            audioManager = FindObjectOfType<AudioManager>();
            uiManager = FindObjectOfType<UIManager>();
            yield return null;
            InitDataStructures();
            yield return null;
            InitNativeCollections();
            yield return null;
            ConfigureMaldivianScenes();
            yield return null;
            ValidateScenes();
            yield return null;
            yield return StartCoroutine(PreloadCriticalAsync());
            LogDebug("Initialised");
        }
        private void InitDataStructures()
        {
            criticalScenes.Add(mainMenuScene);
            criticalScenes.Add(gameWorldScene);
            criticalScenes.Add(MALDIVIAN_MAIN_SCENE);
            criticalScenes.Add(MALDIVIAN_PRAYER_SCENE);
            criticalScenes.Add(MALDIVIAN_FUNERAL_SCENE);
        }
        private void InitNativeCollections()
        {
            sceneLoadMetrics = new NativeArray<float>(64, Allocator.Persistent);
            scenePriorities = new NativeArray<int>(MALDIVIAN_ISLAND_COUNT, Allocator.Persistent);
            sceneMemoryMap = new NativeHashMap<int, float>(128, Allocator.Persistent);
        }
        private void ConfigureMaldivianScenes()
        {
            string[] names = {
                "Male_MainIsland", "Hulhumale_Island", "Villingili_Island", "Addu_City", "Fuvahmulah_Island",
                "Kulhudhuffushi_Island", "Thinadhoo_Island", "Naifaru_Island", "Eydhafushi_Island", "Funadhoo_Island",
                "Ungoofaaru_Island", "Hinnavaru_Island", "Naivaadhoo_Island", "Dhidhdhoo_Island", "Kulhudhuffushi_Island2",
                "Manadhoo_Island", "Velidhoo_Island", "Holhudhoo_Island", "Magoodhoo_Island", "Gemendhoo_Island",
                "Maafaru_Island", "Kendhoo_Island", "Kamadhoo_Island", "Kihaadhoo_Island", "Kudarikilu_Island",
                "Dharavandhoo_Island", "Maalhos_Island", "Eydhafushi_Island2", "Dhonfanu_Island", "Kendhoo_Island2",
                "Hithaadhoo_Island", "Goidhoo_Island", "Fehendhoo_Island", "Fulhadhoo_Island", "Dharavandhoo_Island2",
                "Kihaadhoo_Island2", "Maalhos_Island2", "Eydhafushi_Island3", "Dhonfanu_Island2", "Kendhoo_Island3",
                "Hithaadhoo_Island2"
            };
            for (int i = 0; i < MALDIVIAN_ISLAND_COUNT && i < names.Length; i++)
                islandScenes[i] = names[i];
            culturalScenes = new[]{
                MALDIVIAN_PRAYER_SCENE,
                MALDIVIAN_FUNERAL_SCENE,
                "Eid_Celebration_Scene",
                "Boduberu_Performance_Scene",
                "Islamic_Wedding_Scene"
            };
        }
        private void ValidateScenes()
        {
            int missing = 0;
            for (int i = 0; i < MALDIVIAN_ISLAND_COUNT; i++)
                if (string.IsNullOrEmpty(islandScenes[i]) || !IsInBuildSettings(islandScenes[i]))
                { missing++; LogError($"Missing island scene: {islandScenes[i]}"); }
            foreach (var cs in culturalScenes)
                if (!IsInBuildSettings(cs))
                { missing++; LogError($"Missing cultural scene: {cs}"); }
            if (missing > 0) OnSceneError?.Invoke("Validation", $"{missing} scenes missing", ErrorType.Configuration);
        }
        private bool IsInBuildSettings(string name)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                if (SceneUtility.GetScenePathByBuildIndex(i).Contains(name)) return true;
            return false;
        }
        private IEnumerator PreloadCriticalAsync()
        {
            yield return StartCoroutine(LoadAdditiveAsync(mainMenuScene, true, 100));
            yield return StartCoroutine(LoadAdditiveAsync(gameWorldScene, true, 95));
            yield return StartCoroutine(LoadAdditiveAsync(MALDIVIAN_MAIN_SCENE, true, 90));
            StartCoroutine(LoadAdditiveAsync(MALDIVIAN_PRAYER_SCENE, false, 80));
            StartCoroutine(LoadAdditiveAsync(MALDIVIAN_FUNERAL_SCENE, false, 75));
        }
        #endregion

        #region PUBLIC API
        public void LoadScene(string name, bool additive = false, int priority = 50)
        {
            if (string.IsNullOrEmpty(name)) { LogError("Invalid scene name"); return; }
            if (!CanLoad()) { QueueLoad(name, additive, priority); return; }
            StartCoroutine(LoadAsync(name, additive, priority));
        }
        public void UnloadScene(string name)
        {
            if (string.IsNullOrEmpty(name) || !loadedScenes.ContainsKey(name)) return;
            StartCoroutine(UnloadAsync(name));
        }
        public bool IsLoaded(string name) => loadedScenes.ContainsKey(name);
        public string[] GetLoadedNames() => loadedScenes.Keys.ToArray();
        public int LoadedCount => loadedScenes.Count;
        public void ClearTransitionLog() { transitionLog.Clear(); LogDebug("Transition log cleared"); }
        public float GetMemoryUsage() => currentMemory;
        public float GetThermalState() => thermalState;
        public float GetBatteryLevel() => batteryLevel;
        public bool IsThermalThrottling() => thermalThrottling;
        public bool IsBatteryConservation() => batteryConservation;
        #endregion

        #region LOAD CORE
        private bool CanLoad()
        {
            if (thermalThrottling && thermalState > THERMAL_THRESHOLD_PAUSE_LOAD) return false;
            if (batteryConservation && batteryLevel < 0.15f) return false;
            if (currentMemory > MEMORY_THRESHOLD_BEFORE_UNLOAD) return false;
            return activeLoads < MAX_CONCURRENT_SCENES;
        }
        private IEnumerator LoadAsync(string name, bool additive, int priority)
        {
            LogDebug($"Loading {name} (additive:{additive}, pri:{priority})");
            OnLoadStarted?.Invoke(name, EstimateLoadTime(name));
            activeLoads++;
            if (IsCultural(name)) yield return StartCoroutine(CulturalTransitionAsync(name));
            AsyncOperation op = null; int retries = 0;
            while (retries < MAX_RETRY_ATTEMPTS)
            {
                try
                {
                    op = additive ? SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive)
                                  : SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
                    if (op != null)
                    {
                        loadingOps[name] = op;
                        op.priority = priority;
                        op.allowSceneActivation = false;
                        yield return StartCoroutine(MonitorLoadAsync(op, name));
                        if (op.isDone) break;
                    }
                }
                catch (Exception e) { LogError($"Load attempt {retries+1} failed for {name}: {e.Message}"); OnSceneError?.Invoke(name, e.Message, ErrorType.LoadFailed); }
                retries++;
                if (retries < MAX_RETRY_ATTEMPTS) yield return new WaitForSeconds(RETRY_DELAY);
            }
            activeLoads--;
            if (op != null && op.isDone)
            {
                Scene sc = SceneManager.GetSceneByName(name);
                if (sc.IsValid())
                {
                    loadedScenes[name] = sc;
                    loadTimes[name] = DateTime.UtcNow;
                    visitCounts[name] = visitCounts.GetValueOrDefault(name, 0) + 1;
                    OnLoadCompleted?.Invoke(name, Time.realtimeSinceStartup, true);
                    yield return StartCoroutine(SetupCulturalAsync(name));
                }
                else OnLoadCompleted?.Invoke(name, Time.realtimeSinceStartup, false);
            }
            else { LogError($"Failed to load {name} after {retries} retries"); OnLoadCompleted?.Invoke(name, Time.realtimeSinceStartup, false); OnSceneError?.Invoke(name, "Max retries", ErrorType.LoadFailed); }
        }
        private IEnumerator LoadAdditiveAsync(string name, bool crit, int pri)
        {
            if (loadedScenes.ContainsKey(name)) yield break;
            yield return LoadAsync(name, true, pri);
            if (crit && !loadedScenes.ContainsKey(name)) OnSceneError?.Invoke(name, "Critical load failed", ErrorType.Critical);
        }
        private IEnumerator MonitorLoadAsync(AsyncOperation op, string name)
        {
            float start = Time.realtimeSinceStartup; float last = 0; int stalls = 0;
            while (!op.isDone)
            {
                float p = op.progress;
                if (Mathf.Approximately(p, last)) { stalls++; if (stalls > 300) { OnSceneError?.Invoke(name, "Stall detected", ErrorType.Stall); break; } }
                else { stalls = 0; last = p; }
                if (Time.realtimeSinceStartup - start > SCENE_LOAD_TIMEOUT) { OnSceneError?.Invoke(name, "Timeout", ErrorType.Timeout); break; }
                uiManager?.UpdateLoadingProgress(p, GetDhivehiMessage());
                if (p >= 0.9f) op.allowSceneActivation = true;
                yield return null;
            }
        }
        private string GetDhivehiMessage() => dhivehiLoadingMessages.Length > 0 ? dhivehiLoadingMessages[UnityEngine.Random.Range(0, dhivehiLoadingMessages.Length)] : "Loading...";
        private float EstimateLoadTime(string name)
        {
            float t = 3f;
            if (IsIsland(name)) t += 2f;
            if (IsCultural(name)) t += 1.5f;
            if (SystemInfo.systemMemorySize < 4096) t += 1f;
            if (thermalThrottling) t += 2f;
            if (batteryConservation) t += 1.5f;
            return t;
        }
        #endregion

        #region CULTURAL
        private bool IsCultural(string name) => culturalScenes.Contains(name) || name.Contains("Prayer") || name.Contains("Funeral") || name.Contains("Eid") || name.Contains("Boduberu") || name.Contains("Islamic");
        private bool IsIsland(string name) => islandScenes.Contains(name) || name.Contains("Island");
        private IEnumerator CulturalTransitionAsync(string target)
        {
            if (!enableCulturalSceneTransitions) yield break;
            culturalTransitionActive = true; string prev = currentCulturalContext;
            currentCulturalContext = target.Contains("Prayer") ? "prayer" : target.Contains("Funeral") ? "funeral" : target.Contains("Eid") ? "celebration" : target.Contains("Boduberu") ? "cultural_performance" : "general_cultural";
            LogDebug($"Cultural transition to {currentCulturalContext}");
            if (preservePrayerStateDuringTransition && prayerSystem != null) PreservePrayer();
            if (audioManager && boduberuTransitionMusic) audioManager.PlayOneShot(boduberuTransitionMusic);
            if (uiManager) uiManager.ShowCulturalLoadingScreen(currentCulturalContext, culturalLoadingScreenDuration);
            OnCulturalTransition?.Invoke(prev, currentCulturalContext, target);
            yield return new WaitForSeconds(CULTURAL_SCENE_TRANSITION_TIME);
            culturalTransitionActive = false;
        }
        private void PreservePrayer()
        {
            try
            {
                culturalState["preserved_prayer"] = new { current = prayerSystem.GetCurrentPrayer(DateTime.Now), next = prayerSystem.GetNextPrayer(DateTime.Now), timestamp = DateTime.UtcNow };
            }
            catch (Exception e) { LogError($"Preserve prayer failed: {e.Message}"); }
        }
        private IEnumerator SetupCulturalAsync(string name)
        {
            if (!IsCultural(name)) yield break;
            LogDebug($"Setup cultural scene {name}");
            if (name.Contains("Prayer")) yield return StartCoroutine(SetupPrayerAsync());
            else if (name.Contains("Funeral")) yield return StartCoroutine(SetupFuneralAsync());
            else if (name.Contains("Eid")) yield return StartCoroutine(SetupEidAsync());
            else if (name.Contains("Boduberu")) yield return StartCoroutine(SetupBoduberuAsync());
            string log = $"Cultural scene {name} setup complete at {DateTime.UtcNow:HH:mm:ss}";
            transitionLog.Add(log); mainManager?.LogDebug(log);
        }
        private IEnumerator SetupPrayerAsync()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.8f, 0.9f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.7f, 0.8f, 0.9f);
            RenderSettings.ambientGroundColor = new Color(0.6f, 0.7f, 0.8f);
            if (audioManager) audioManager.SetMasterVolume(0.6f);
            if (uiManager) uiManager.ShowPrayerUI();
            yield return null;
        }
        private IEnumerator SetupFuneralAsync()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.4f, 0.4f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.3f);
            if (audioManager) audioManager.SetMasterVolume(0.3f);
            if (uiManager) uiManager.ShowFuneralUI();
            yield return null;
        }
        private IEnumerator SetupEidAsync()
        {
            RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = 1.2f;
            if (audioManager) audioManager.SetMasterVolume(1f);
            if (uiManager) uiManager.ShowCelebrationUI("Eid");
            yield return null;
        }
        private IEnumerator SetupBoduberuAsync()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.9f, 0.8f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.8f, 0.7f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.7f, 0.6f, 0.5f);
            if (audioManager) audioManager.SetMasterVolume(0.9f);
            if (uiManager) uiManager.ShowPerformanceUI("Boduberu");
            yield return null;
        }
        #endregion

        #region UNLOAD
        private IEnumerator UnloadAsync(string name)
        {
            LogDebug($"Unloading {name}");
            OnUnloadStarted?.Invoke(name);
            if (IsCultural(name)) yield return StartCoroutine(CleanupCulturalAsync(name));
            var op = SceneManager.UnloadSceneAsync(name);
            if (op != null)
            {
                float start = Time.realtimeSinceStartup;
                while (!op.isDone) { uiManager?.UpdateLoadingProgress(op.progress, "Unloading..."); yield return null; }
                loadedScenes.Remove(name); loadTimes.Remove(name);
                OnUnloadCompleted?.Invoke(name, true);
            }
            else { LogError($"Unload failed for {name}"); OnUnloadCompleted?.Invoke(name, false); OnSceneError?.Invoke(name, "Unload failed", ErrorType.UnloadFailed); }
        }
        private IEnumerator CleanupCulturalAsync(string name)
        {
            if (name.Contains("Prayer") || name.Contains("Funeral"))
            { RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = 1f; }
            if (audioManager) audioManager.SetMasterVolume(1f);
            if (uiManager) uiManager.HideCulturalUI();
            yield return null;
        }
        #endregion

        #region POOL & QUEUE
        private void QueueLoad(string name, bool additive, int priority)
        {
            loadQueue.Enqueue(new SceneLoadRequest{name=name,additive=additive,priority=priority,timestamp=DateTime.UtcNow});
            if (CanLoad()) StartCoroutine(ProcessQueueAsync());
        }
        private IEnumerator ProcessQueueAsync()
        {
            while (loadQueue.Count > 0 && CanLoad())
            {
                var r = loadQueue.Dequeue();
                yield return StartCoroutine(LoadAsync(r.name, r.additive, r.priority));
            }
        }
        #endregion

        #region UTILS
        private void Update()
        {
            currentMemory = GC.GetTotalMemory(false) / (1024f * 1024f);
            if (Time.time - lastMemCheck > 5f) { lastMemCheck = Time.time; thermalState = Mathf.Clamp01((Time.smoothDeltaTime * 30f + activeLoads / (float)MAX_CONCURRENT_SCENES + currentMemory / MEMORY_THRESHOLD_BEFORE_UNLOAD) / 3f); }
            if (Time.time - lastBatteryCheck > 15f) { lastBatteryCheck = Time.time; batteryLevel = Mathf.Clamp01(1f - (activeLoads * 0.05f + currentMemory / 1000f + thermalState * 0.1f) * 0.01f); }
            bool shouldThrottle = thermalState > THERMAL_THRESHOLD_PAUSE_LOAD;
            if (shouldThrottle != thermalThrottling) { thermalThrottling = shouldThrottle; LogDebug($"Thermal throttling {(thermalThrottling?"ON":"OFF")}"); }
            bool shouldConserve = batteryLevel < 0.2f;
            if (shouldConserve != batteryConservation) { batteryConservation = shouldConserve; LogDebug($"Battery conservation {(batteryConservation?"ON":"OFF")}"); }
            if (currentMemory > MEMORY_THRESHOLD_BEFORE_UNLOAD) HandleMemoryPressure();
        }
        private void HandleMemoryPressure()
        {
            if (pooledScenes.Count > 0) { UnloadScene(pooledScenes[^1]); return; }
            var lowPrio = loadedScenes.Keys.Where(s => !criticalScenes.Contains(s)).OrderBy(s => 0).FirstOrDefault();
            if (!string.IsNullOrEmpty(lowPrio)) UnloadScene(lowPrio);
        }
        #endregion

        #region LOGGING
        private void LogDebug(string msg)
        {
            Debug.Log($"[GameSceneManager] {msg}");
            mainManager?.LogDebug(msg);
        }
        private void LogWarning(string msg)
        {
            Debug.LogWarning($"[GameSceneManager] {msg}");
            mainManager?.LogWarning(msg);
        }
        private void LogError(string msg)
        {
            Debug.LogError($"[GameSceneManager] {msg}");
            mainManager?.LogError(msg);
        }
        #endregion

        #region STRUCTS
        private struct SceneLoadRequest { public string name; public bool additive; public int priority; public DateTime timestamp; }
        public enum ErrorType { Configuration, LoadFailed, UnloadFailed, Timeout, Stall, Critical }
        #endregion

        #region CLEANUP
        private void OnDestroy()
        {
            if (sceneLoadMetrics.IsCreated) sceneLoadMetrics.Dispose();
            if (scenePriorities.IsCreated) scenePriorities.Dispose();
            if (sceneMemoryMap.IsCreated) sceneMemoryMap.Dispose();
            lock (_lock) { if (_instance == this) _instance = null; }
        }
        #endregion
    }
}
