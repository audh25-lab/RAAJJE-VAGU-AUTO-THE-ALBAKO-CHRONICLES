// AssetBundleManager.cs
// RVACONT-007 - Batch 7: Performance Systems
// Geographic asset streaming: D→C→A island priority

using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RVA.TAC.Performance
{
    public class AssetBundleManager : MonoBehaviour
    {
        [System.Serializable]
        public class IslandAssetBundle
        {
            public string islandName;
            public string bundleName;
            public IslandGenerator.DevelopmentPriority priority;
            public bool isLoaded = false;
            public AssetBundle bundle;
            public int referenceCount = 0;
        }
        
        [Header("Asset Bundle Config")]
        public string baseBundleURL = "https://cdn.rva-tac.mv/bundles/";
        public IslandAssetBundle[] islandBundles;
        
        [Header("Streaming Settings")]
        public int maxConcurrentDownloads = 2;
        public float unloadDelay = 300f; // 5 minutes
        
        // Load queue
        private Queue<IslandAssetBundle> loadQueue = new();
        private int activeDownloads = 0;
        
        // Cache
        private Dictionary<string, AssetBundle> loadedBundles = new();
        
        // References
        private IslandGenerator islandGenerator;
        
        public static AssetBundleManager Instance { get; private set; }
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            // Initialize bundle manifest
            InitializeIslandBundles();
            
            DebugSystem.Log("[AssetBundleManager] Initialized", DebugSystem.LogCategory.PERFORMANCE);
        }
        
        void Start()
        {
            islandGenerator = FindObjectOfType<IslandGenerator>();
        }
        
        private void InitializeIslandBundles()
        {
            // D-phase islands (41 islands) - Load on demand
            // C-phase islands - Preload in background
            // A-phase islands - Force load
            
            foreach (var bundle in islandBundles)
            {
                switch (bundle.priority)
                {
                    case IslandGenerator.DevelopmentPriority.A:
                        LoadBundleImmediate(bundle);
                        break;
                    case IslandGenerator.DevelopmentPriority.C:
                        EnqueueBundle(bundle, loadImmediate: false);
                        break;
                    case IslandGenerator.DevelopmentPriority.D:
                        // On-demand
                        break;
                }
            }
        }
        
        public void LoadIslandAssets(string islandName)
        {
            var bundle = islandBundles.FirstOrDefault(b => b.islandName == islandName);
            if (bundle == null)
            {
                DebugSystem.LogWarning($"[AssetBundleManager] No bundle for island: {islandName}", DebugSystem.LogCategory.PERFORMANCE);
                return;
            }
            
            if (bundle.isLoaded)
            {
                bundle.referenceCount++;
                return;
            }
            
            EnqueueBundle(bundle, loadImmediate: true);
        }
        
        public void UnloadIslandAssets(string islandName)
        {
            var bundle = islandBundles.FirstOrDefault(b => b.islandName == islandName);
            if (bundle == null || !bundle.isLoaded) return;
            
            bundle.referenceCount--;
            
            if (bundle.referenceCount <= 0)
            {
                StartCoroutine(DelayedUnload(bundle));
            }
        }
        
        private void EnqueueBundle(IslandAssetBundle bundle, bool loadImmediate)
        {
            if (loadImmediate)
            {
                loadQueue.Enqueue(bundle);
                ProcessQueue();
            }
            else
            {
                // Background loading
                loadQueue.Enqueue(bundle);
                if (activeDownloads < maxConcurrentDownloads)
                {
                    ProcessQueue();
                }
            }
        }
        
        private void ProcessQueue()
        {
            while (activeDownloads < maxConcurrentDownloads && loadQueue.Count > 0)
            {
                var bundle = loadQueue.Dequeue();
                StartCoroutine(DownloadBundle(bundle));
            }
        }
        
        private IEnumerator DownloadBundle(IslandAssetBundle bundle)
        {
            activeDownloads++;
            
            string url = $"{baseBundleURL}{bundle.bundleName}";
            
            using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(url))
            {
                request.timeout = 30; // Maldives network timeout
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    bundle.bundle = DownloadHandlerAssetBundle.GetContent(request);
                    bundle.isLoaded = true;
                    bundle.referenceCount++;
                    
                    loadedBundles[bundle.islandName] = bundle.bundle;
                    
                    DebugSystem.Log($"[AssetBundleManager] Loaded: {bundle.bundleName}", DebugSystem.LogCategory.PERFORMANCE);
                }
                else
                {
                    DebugSystem.LogError($"[AssetBundleManager] Failed to load {bundle.bundleName}: {request.error}", DebugSystem.LogCategory.PERFORMANCE);
                    
                    // Retry logic for Maldives network instability
                    if (request.responseCode == 0) // Network error
                    {
                        yield return new WaitForSeconds(5f);
                        loadQueue.Enqueue(bundle); // Re-queue
                    }
                }
            }
            
            activeDownloads--;
            ProcessQueue();
        }
        
        private void LoadBundleImmediate(IslandAssetBundle bundle)
        {
            // For A-priority islands, use local resources
            // (In production, this would load from streaming assets)
            bundle.isLoaded = true;
            bundle.referenceCount = 999; // Never unload
            
            DebugSystem.Log($"[AssetBundleManager] Preloaded A-priority: {bundle.islandName}", DebugSystem.LogCategory.PERFORMANCE);
        }
        
        private IEnumerator DelayedUnload(IslandAssetBundle bundle)
        {
            yield return new WaitForSeconds(unloadDelay);
            
            if (bundle.referenceCount <= 0 && bundle.isLoaded)
            {
                bundle.bundle?.Unload(true);
                bundle.isLoaded = false;
                loadedBundles.Remove(bundle.islandName);
                
                DebugSystem.Log($"[AssetBundleManager] Unloaded: {bundle.bundleName}", DebugSystem.LogCategory.PERFORMANCE);
            }
        }
        
        public T LoadAsset<T>(string islandName, string assetName) where T : Object
        {
            if (loadedBundles.TryGetValue(islandName, out AssetBundle bundle))
            {
                return bundle.LoadAsset<T>(assetName);
            }
            
            DebugSystem.LogWarning($"[AssetBundleManager] Bundle not loaded for {islandName}", DebugSystem.LogCategory.PERFORMANCE);
            return null;
        }
        
        public void UnloadAllBundles()
        {
            foreach (var bundle in islandBundles)
            {
                if (bundle.isLoaded && bundle.priority != IslandGenerator.DevelopmentPriority.A)
                {
                    bundle.bundle?.Unload(true);
                    bundle.isLoaded = false;
                }
            }
            
            loadedBundles.Clear();
            DebugSystem.Log("[AssetBundleManager] All non-critical bundles unloaded", DebugSystem.LogCategory.PERFORMANCE);
        }
    }
}
