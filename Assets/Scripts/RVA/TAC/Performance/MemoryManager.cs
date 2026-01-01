// MemoryManager.cs
// RVACONT-007 - Batch 7: Performance Systems
// Job-based garbage collection scheduling for mobile

using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using System;
using System.Collections.Generic;

namespace RVA.TAC.Performance
{
    [BurstCompile]
    public struct MemoryAnalysisJob : IJob
    {
        [ReadOnly] public NativeArray<long> memorySamples;
        public NativeArray<long> result;
        
        public void Execute()
        {
            long total = 0;
            for (int i = 0; i < memorySamples.Length; i++)
            {
                total += memorySamples[i];
            }
            result[0] = total / memorySamples.Length;
            result[1] = long.MaxValue;
            result[2] = long.MinValue;
            
            for (int i = 0; i < memorySamples.Length; i++)
            {
                result[1] = math.min(result[1], memorySamples[i]);
                result[2] = math.max(result[2], memorySamples[i]);
            }
        }
    }
    
    public class MemoryManager : MonoBehaviour
    {
        [Header("Memory Budgets")]
        public long totalMemoryBudget = 350 * 1024 * 1024; // 350MB mobile budget
        public long textureMemoryBudget = 150 * 1024 * 1024;
        public long audioMemoryBudget = 50 * 1024 * 1024;
        
        [Header("Garbage Collection")]
        public bool enableJobifiedGC = true;
        public int gcFrameInterval = 300; // Every 10 seconds at 30fps
        public bool forceIncrementalGC = true;
        
        // Native collections for burst jobs
        private NativeArray<long> memorySampleBuffer;
        private NativeArray<long> analysisResult;
        private MemoryAnalysisJob analysisJob;
        private JobHandle analysisHandle;
        
        // Object pooling
        private Dictionary<Type, Queue<Component>> objectPools = new();
        
        // Texture streaming
        private Dictionary<string, Texture2D> textureCache = new();
        private long currentTextureMemory;
        
        public static MemoryManager Instance { get; private set; }
        
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
            
            // Initialize native collections
            memorySampleBuffer = new NativeArray<long>(60, Allocator.Persistent); // 60 samples
            analysisResult = new NativeArray<long>(3, Allocator.Persistent);
            
            // Configure Unity GC
            if (forceIncrementalGC)
            {
                UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Enabled;
            }
            
            DebugSystem.Log("[MemoryManager] Initialized with 350MB budget", DebugSystem.LogCategory.PERFORMANCE);
        }
        
        void OnDestroy()
        {
            if (memorySampleBuffer.IsCreated) memorySampleBuffer.Dispose();
            if (analysisResult.IsCreated) analysisResult.Dispose();
        }
        
        void Update()
        {
            // Sample memory usage
            int sampleIndex = Time.frameCount % memorySampleBuffer.Length;
            memorySampleBuffer[sampleIndex] = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            
            // Scheduled GC
            if (enableJobifiedGC && Time.frameCount % gcFrameInterval == 0)
            {
                ScheduleGarbageCollection();
            }
            
            // Complete analysis jobs
            if (analysisHandle.IsCompleted)
            {
                analysisHandle.Complete();
                CheckMemoryBudget();
            }
        }
        
        private void ScheduleGarbageCollection()
        {
            // Incremental GC spread over frames
            UnityEngine.Scripting.GarbageCollector.CollectIncremental(1f / 30f); // 33ms budget
            
            // Unload unused assets asynchronously
            Resources.UnloadUnusedAssets();
            
            DebugSystem.Log("[MemoryManager] Scheduled GC pass", DebugSystem.LogCategory.PERFORMANCE);
        }
        
        public void ForceGarbageCollection()
        {
            // Emergency full GC
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            Resources.UnloadUnusedAssets();
            
            DebugSystem.LogWarning("[MemoryManager] EMERGENCY GC COMPLETED", DebugSystem.LogCategory.PERFORMANCE);
        }
        
        private void CheckMemoryBudget()
        {
            long currentUsage = analysisResult[0];
            long peakUsage = analysisResult[2];
            
            if (peakUsage > totalMemoryBudget * 0.9f)
            {
                DebugSystem.LogWarning($"[MemoryManager] Approaching budget: {peakUsage / (1024f * 1024f):F0}/{totalMemoryBudget / (1024f * 1024f):F0}MB", DebugSystem.LogCategory.PERFORMANCE);
                
                // Aggressive cleanup
                ClearTextureCache();
            }
            
            if (currentTextureMemory > textureMemoryBudget)
            {
                DebugSystem.LogWarning($"[MemoryManager] Texture budget exceeded: {currentTextureMemory / (1024f * 1024f):F0}MB", DebugSystem.LogCategory.PERFORMANCE);
                UnloadRarelyUsedTextures();
            }
        }
        
        // Object pooling API
        public T GetPooledObject<T>(T prefab) where T : Component
        {
            Type type = typeof(T);
            if (!objectPools.ContainsKey(type))
            {
                objectPools[type] = new Queue<Component>();
            }
            
            var pool = objectPools[type];
            if (pool.Count > 0)
            {
                T obj = pool.Dequeue() as T;
                obj.gameObject.SetActive(true);
                return obj;
            }
            
            // Create new
            T newObj = Instantiate(prefab);
            newObj.transform.SetParent(transform);
            return newObj;
        }
        
        public void ReturnToPool<T>(T obj) where T : Component
        {
            obj.gameObject.SetActive(false);
            Type type = typeof(T);
            if (!objectPools.ContainsKey(type))
            {
                objectPools[type] = new Queue<Component>();
            }
            objectPools[type].Enqueue(obj);
        }
        
        // Texture management
        public Texture2D GetCachedTexture(string path)
        {
            if (textureCache.TryGetValue(path, out Texture2D tex))
            {
                return tex;
            }
            return null;
        }
        
        public void CacheTexture(string path, Texture2D texture)
        {
            textureCache[path] = texture;
            currentTextureMemory += TextureUtil.GetTextureMemorySize(texture);
        }
        
        private void ClearTextureCache()
        {
            foreach (var tex in textureCache.Values)
            {
                if (tex != null)
                {
                    Destroy(tex);
                }
            }
            textureCache.Clear();
            currentTextureMemory = 0;
            
            DebugSystem.Log("[MemoryManager] Texture cache cleared", DebugSystem.LogCategory.PERFORMANCE);
        }
        
        private void UnloadRarelyUsedTextures()
        {
            // Simple LRU implementation
            // In production, use proper LRU cache
            if (textureCache.Count > 50)
            {
                var enumerator = textureCache.GetEnumerator();
                enumerator.MoveNext();
                var first = enumerator.Current;
                textureCache.Remove(first.Key);
                Destroy(first.Value);
            }
        }
        
        public long GetCurrentMemoryUsage() => UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        public long GetTextureMemoryUsage() => currentTextureMemory;
    }
    
    public static class TextureUtil
    {
        public static long GetTextureMemorySize(Texture2D tex)
        {
            if (tex == null) return 0;
            return tex.width * tex.height * 4; // Approximate RGBA size
        }
    }
}
