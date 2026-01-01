using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

namespace RVA.TAC.AI
{
    [BurstCompile]
    public class CharacterSystem : MonoBehaviour
    {
        [Header("Maldivian NPC Generation")]
        public int maxNPCsPerIsland = 50;
        public float spawnRadius = 100f;
        public float despawnDistance = 150f;
        
        [Header("Cultural Behavior")]
        public float prayerTimeSpeedMultiplier = 0.3f;
        public float boduberuAttractionRadius = 30f;
        
        private NativeList<NPCData> npcRegistry;
        private NativeQueue<int> recyclePool;
        private Dictionary<int, GameObject> activeNPCs;
        
        [BurstCompile]
        public struct NPCData
        {
            public int id;
            public float3 position;
            public float3 destination;
            public NPCType type;
            public NPCState state;
            public float speed;
            public int homeIsland;
            public bool isPraying;
            public float prayerTimer;
            public bool isFisherman;
            public float fishingTimer;
        }
        
        public enum NPCType
        {
            Resident,      // Island locals
            Tourist,       // Foreign visitors
            Fisherman,     // Traditional fishing crews
            Vendor,        // Market/Dhoni traders
            Elder,         // Community leaders
            Youth,         // Young islanders
            Police,        // Island security
            BoduberuPlayer // Traditional drummers
        }
        
        public enum NPCState
        {
            Idle,
            Walking,
            Working,
            Praying,
            Fishing,
            Dancing,
            Fleeing,
            Fighting
        }
        
        void Awake()
        {
            npcRegistry = new NativeList<NPCData>(maxNPCsPerIsland * 41, Allocator.Persistent);
            recyclePool = new NativeQueue<int>(Allocator.Persistent);
            activeNPCs = new Dictionary<int, GameObject>();
        }
        
        void Start()
        {
            // Procedural generation based on island tiers
            for (int island = 0; island < 41; island++)
            {
                GenerateIslandNPCs(island);
            }
        }
        
        [BurstCompile]
        void GenerateIslandNPCs(int islandId)
        {
            // D-tier islands (undeveloped): 10-20 NPCs
            // C-tier: 20-35 NPCs  
            // A-tier (Malé): 40-50 NPCs
            int npcCount = GetNPCCountForIsland(islandId);
            
            for (int i = 0; i < npcCount; i++)
            {
                var npc = new NPCData
                {
                    id = islandId * 1000 + i,
                    position = GetRandomIslandPosition(islandId),
                    destination = GetRandomPatrolPoint(islandId),
                    type = GetWeightedNPCType(islandId),
                    state = NPCState.Idle,
                    speed = UnityEngine.Random.Range(1.5f, 3.5f),
                    homeIsland = islandId,
                    isFisherman = (islandId < 15) && UnityEngine.Random.value < 0.4f // More fishermen on D/C islands
                };
                
                npcRegistry.Add(npc);
                SpawnNPCVisual(npc);
            }
        }
        
        [BurstCompile]
        int GetNPCCountForIsland(int islandId)
        {
            if (islandId == 0) return 45; // Malé
            if (islandId < 15) return UnityEngine.Random.Range(10, 20); // D-tier
            if (islandId < 30) return UnityEngine.Random.Range(20, 35); // C-tier
            return UnityEngine.Random.Range(30, 45); // A-tier
        }
        
        [BurstCompile]
        float3 GetRandomIslandPosition(int islandId)
        {
            // Procedural positioning based on island data
            float angle = UnityEngine.Random.Range(0f, math.PI * 2f);
            float radius = UnityEngine.Random.Range(5f, spawnRadius);
            return new float3(
                math.cos(angle) * radius,
                0f,
                math.sin(angle) * radius
            );
        }
        
        [BurstCompile]
        NPCType GetWeightedNPCType(int islandId)
        {
            float rand = UnityEngine.Random.value;
            
            if (islandId == 0) // Malé
            {
                if (rand < 0.3f) return NPCType.Resident;
                if (rand < 0.5f) return NPCType.Tourist;
                if (rand < 0.7f) return NPCType.Vendor;
                if (rand < 0.85f) return NPCType.Youth;
                return NPCType.Police;
            }
            
            // Outer islands
            if (rand < 0.4f) return NPCType.Resident;
            if (rand < 0.6f) return NPCType.Fisherman;
            if (rand < 0.75f) return NPCType.Vendor;
            if (rand < 0.85f) return NPCType.Elder;
            return NPCType.Youth;
        }
        
        void SpawnNPCVisual(NPCData npc)
        {
            // Pool-based instantiation with LOD
            GameObject npcObj = ObjectPool.GetPooledNPC(npc.type);
            npcObj.transform.position = npc.position;
            npcObj.SetActive(true);
            
            // Maldivian cultural appearance
            SetupNPCAppearance(npcObj, npc.type);
            
            activeNPCs[npc.id] = npcObj;
        }
        
        void SetupNPCAppearance(GameObject npc, NPCType type)
        {
            var renderer = npc.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Traditional vs modern clothing weights
                bool traditionalDress = UnityEngine.Random.value < 0.6f;
                
                if (traditionalDress)
                {
                    // Libaas, mundu colors: pastels, whites, light blues
                    renderer.material.color = new Color(
                        UnityEngine.Random.Range(0.8f, 0.95f),
                        UnityEngine.Random.Range(0.8f, 0.95f),
                        UnityEngine.Random.Range(0.85f, 1f)
                    );
                }
                else
                {
                    // Modern Western-style clothing
                    renderer.material.color = new Color(
                        UnityEngine.Random.Range(0.1f, 0.8f),
                        UnityEngine.Random.Range(0.1f, 0.8f),
                        UnityEngine.Random.Range(0.1f, 0.8f)
                    );
                }
            }
        }
        
        void Update()
        {
            UpdateNPCJobs();
            HandleCulturalBehaviors();
            CullDistantNPCs();
        }
        
        [BurstCompile]
        void UpdateNPCJobs()
        {
            var updateJob = new NPCUpdateJob
            {
                deltaTime = Time.deltaTime,
                npcData = npcRegistry,
                playerPosition = (float3)Camera.main.transform.position
            };
            
            updateJob.Schedule(npcRegistry.Length, 64).Complete();
        }
        
        [BurstCompile]
        struct NPCUpdateJob : IJobParallelFor
        {
            public float deltaTime;
            public NativeArray<NPCData> npcData;
            public float3 playerPosition;
            
            public void Execute(int index)
            {
                var npc = npcData[index];
                
                // Distance-based LOD behavior
                float distance = math.distance(npc.position, playerPosition);
                if (distance > 120f) return; // Skip far NPCs
                
                // State machine
                switch (npc.state)
                {
                    case NPCState.Walking:
                        MoveTowardsDestination(ref npc, deltaTime);
                        break;
                    case NPCState.Fishing:
                        UpdateFishing(ref npc, deltaTime);
                        break;
                    case NPCState.Praying:
                        UpdatePraying(ref npc, deltaTime);
                        break;
                }
                
                // Random behavior transitions
                if (UnityEngine.Random.value < 0.01f)
                {
                    npc.state = GetRandomState(npc);
                }
                
                npcData[index] = npc;
            }
            
            [BurstCompile]
            void MoveTowardsDestination(ref NPCData npc, float deltaTime)
            {
                float3 direction = math.normalize(npc.destination - npc.position);
                npc.position += direction * npc.speed * deltaTime;
                
                if (math.distance(npc.position, npc.destination) < 2f)
                {
                    npc.destination = GetRandomPatrolPoint(npc.homeIsland);
                    npc.state = NPCState.Idle;
                }
            }
            
            [BurstCompile]
            NPCState GetRandomState(NPCData npc)
            {
                float rand = UnityEngine.Random.value;
                
                if (npc.isFisherman && rand < 0.2f) return NPCState.Fishing;
                if (rand < 0.5f) return NPCState.Walking;
                if (rand < 0.7f) return NPCState.Idle;
                if (rand < 0.85f) return NPCState.Working;
                
                return npc.type == NPCType.BoduberuPlayer ? NPCState.Dancing : NPCState.Idle;
            }
        }
        
        [BurstCompile]
        float3 GetRandomPatrolPoint(int islandId)
        {
            // Island-specific patrol zones: beaches, markets, jetties
            float beachWeight = 0.4f;
            float marketWeight = islandId == 0 ? 0.3f : 0.1f;
            
            float rand = UnityEngine.Random.value;
            
            if (rand < beachWeight)
            {
                // Beach patrol
                return GetRandomBeachPosition(islandId);
            }
            else if (rand < beachWeight + marketWeight)
            {
                // Market/Jetty area
                return GetRandomMarketPosition(islandId);
            }
            else
            {
                // Residential area
                return GetRandomResidentialPosition(islandId);
            }
        }
        
        void HandleCulturalBehaviors()
        {
            // Prayer time detection
            bool isPrayerTime = PrayerTimeSystem.IsPrayerTime();
            
            for (int i = 0; i < npcRegistry.Length; i++)
            {
                var npc = npcRegistry[i];
                
                if (isPrayerTime && !npc.isPraying && npc.type != NPCType.Tourist)
                {
                    if (UnityEngine.Random.value < 0.7f) // 70% participation rate
                    {
                        npc.isPraying = true;
                        npc.state = NPCState.Praying;
                        npc.prayerTimer = PrayerTimeSystem.GetPrayerDuration();
                    }
                }
                
                npcRegistry[i] = npc;
            }
        }
        
        void CullDistantNPCs()
        {
            // Mobile performance: despawn far NPCs, keep data
            var playerPos = (float3)Camera.main.transform.position;
            
            foreach (var kvp in activeNPCs)
            {
                float distance = math.distance(npcRegistry[kvp.Key].position, playerPos);
                if (distance > despawnDistance)
                {
                    kvp.Value.SetActive(false);
                    ObjectPool.ReturnToPool(kvp.Value);
                }
            }
        }
        
        void OnDestroy()
        {
            npcRegistry.Dispose();
            recyclePool.Dispose();
        }
        
        // Accessibility: Screen reader support
        public string GetNPCDescription(int npcId)
        {
            if (npcId >= npcRegistry.Length) return "NPC not found";
            
            var npc = npcRegistry[npcId];
            return $"Maldivian {npc.type}, {npc.state} near island {npc.homeIsland}";
        }
    }
}
