using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

namespace RVA.TAC.World
{
    [BurstCompile]
    public class GangSystem : MonoBehaviour
    {
        [Header("Maldivian Gang Archetypes")]
        public int totalGangs = 83;
        public bool enableGangWarfare = true;
        public float territoryUpdateInterval = 60f;
        
        private NativeArray<GangData> gangRegistry;
        private NativeArray<TerritoryNode> territoryMap;
        private Dictionary<int, List<int>> gangMembers;
        
        [BurstCompile]
        public struct GangData
        {
            public int gangId;
            public fixed char name[32];
            public int homeIsland;
            public int influence;
            public GangArchetype archetype;
            public int leaderNPCId;
            public int memberCount;
            public int reputation;
            public bool isActive;
            public int rivalGangId;
            public int allyGangId;
            public float lastActivityTime;
        }
        
        public enum GangArchetype
        {
            TraditionalFishermen,    // Control fishing zones
            ResortMafia,            // Tourist exploitation
            DrugTraffickers,        // Small scale, low profile
            ReligiousExtremists,    // Political influence
            Smugglers,              // Dhoni-based contraband
            YouthGangs,             // Local petty crime
            CorruptOfficials,       // Government connections
            ExpatCriminals          // Foreign organized crime
        }
        
        [BurstCompile]
        public struct TerritoryNode
        {
            public int islandId;
            public int controllingGang;
            public int influenceValue;
            public bool isContested;
            public float2 coordinates;
        }
        
        void Awake()
        {
            gangRegistry = new NativeArray<GangData>(totalGangs, Allocator.Persistent);
            territoryMap = new NativeArray<TerritoryNode>(41, Allocator.Persistent);
            gangMembers = new Dictionary<int, List<int>>();
            
            InitializeGangs();
            DistributeTerritories();
        }
        
        [BurstCompile]
        void InitializeGangs()
        {
            // Procedural generation based on real Maldivian crime patterns
            for (int i = 0; i < totalGangs; i++)
            {
                var gang = new GangData
                {
                    gangId = i,
                    homeIsland = GetGangHomeIsland(i),
                    influence = UnityEngine.Random.Range(10, 100),
                    archetype = GetRegionalArchetype(i),
                    memberCount = UnityEngine.Random.Range(3, 25),
                    reputation = UnityEngine.Random.Range(-50, 50),
                    isActive = true,
                    lastActivityTime = Time.time
                };
                
                // Generate culturally appropriate gang name
                string name = GenerateGangName(gang.archetype, gang.homeIsland);
                SetGangName(ref gang, name);
                
                gangRegistry[i] = gang;
                gangMembers[i] = new List<int>();
            }
            
            // Establish relationships
            GenerateRivalAlliances();
        }
        
        [BurstCompile]
        int GetGangHomeIsland(int gangId)
        {
            // D-tier islands: 40 gangs (high crime due to poverty)
            // C-tier: 28 gangs
            // A-tier: 15 gangs
            
            if (gangId < 40) 
                return UnityEngine.Random.Range(0, 15); // D-tier islands
            else if (gangId < 68) 
                return UnityEngine.Random.Range(15, 30); // C-tier
            else 
                return UnityEngine.Random.Range(30, 41); // A-tier
        }
        
        [BurstCompile]
        GangArchetype GetRegionalArchetype(int gangId)
        {
            int island = GetGangHomeIsland(gangId);
            
            // Island-specific criminal activity patterns
            if (island < 15) // D-tier (poor, isolated)
            {
                float rand = UnityEngine.Random.value;
                if (rand < 0.4f) return GangArchetype.TraditionalFishermen;
                if (rand < 0.7f) return GangArchetype.Smugglers;
                if (rand < 0.85f) return GangArchetype.YouthGangs;
                return GangArchetype.DrugTraffickers;
            }
            else if (island < 30) // C-tier (developing)
            {
                float rand = UnityEngine.Random.value;
                if (rand < 0.35f) return GangArchetype.ResortMafia;
                if (rand < 0.6f) return GangArchetype.YouthGangs;
                if (rand < 0.8f) return GangArchetype.Smugglers;
                return GangArchetype.CorruptOfficials;
            }
            else // A-tier (urban, political)
            {
                float rand = UnityEngine.Random.value;
                if (rand < 0.3f) return GangArchetype.CorruptOfficials;
                if (rand < 0.5f) return GangArchetype.ReligiousExtremists;
                if (rand < 0.75f) return GangArchetype.ExpatCriminals;
                return GangArchetype.ResortMafia;
            }
        }
        
        void SetGangName(ref GangData gang, string name)
        {
            // Copy to fixed buffer (Maldivian names: Dhivehi + English mix)
            for (int i = 0; i < math.min(name.Length, 31); i++)
            {
                gang.name[i] = (char)name[i];
            }
            gang.name[math.min(name.Length, 31)] = '\0';
        }
        
        string GenerateGangName(GangArchetype archetype, int islandId)
        {
            string[] prefixes = { "Kandu", "Dhoni", "Bodu", "Kuda", "Medhu", "Uthuru", "Dheku" };
            string[] suffixes = { "Brotherhood", "Crew", "Family", "Cartel", "Syndicate", "Collective" };
            
            string prefix = prefixes[islandId % prefixes.Length];
            string suffix = suffixes[(int)archetype % suffixes.Length];
            
            return $"{prefix} {suffix}";
        }
        
        void GenerateRivalAlliances()
        {
            for (int i = 0; i < totalGangs; i++)
            {
                var gang = gangRegistry[i];
                
                // Archetype-based rivalries
                gang.rivalGangId = FindRivalGang(i);
                gang.allyGangId = FindAllyGang(i);
                
                gangRegistry[i] = gang;
            }
        }
        
        [BurstCompile]
        int FindRivalGang(int gangId)
        {
            var archetype = gangRegistry[gangId].archetype;
            
            // Natural enemy archetypes
            switch (archetype)
            {
                case GangArchetype.TraditionalFishermen:
                    return UnityEngine.Random.Range(0, totalGangs / 3); // Rival: other fishermen gangs
                case GangArchetype.ResortMafia:
                    return UnityEngine.Random.Range(totalGangs / 3, totalGangs * 2 / 3); // Rival: religious extremists
                case GangArchetype.DrugTraffickers:
                    return UnityEngine.Random.Range(0, totalGangs / 2); // Rival: police/youth gangs
                default:
                    return (gangId + UnityEngine.Random.Range(1, 10)) % totalGangs;
            }
        }
        
        [BurstCompile]
        int FindAllyGang(int gangId)
        {
            var archetype = gangRegistry[gangId].archetype;
            
            // Natural alliances
            switch (archetype)
            {
                case GangArchetype.Smugglers:
                    return (gangId + 5) % totalGangs; // Ally: other smugglers
                case GangArchetype.CorruptOfficials:
                    return UnityEngine.Random.Range(totalGangs / 2, totalGangs); // Ally: resort mafia
                default:
                    return (gangId + UnityEngine.Random.Range(3, 8)) % totalGangs;
            }
        }
        
        void DistributeTerritories()
        {
            // Initial territory control based on gang strength
            for (int island = 0; island < 41; island++)
            {
                int strongestGang = GetStrongestGangForIsland(island);
                
                territoryMap[island] = new TerritoryNode
                {
                    islandId = island,
                    controllingGang = strongestGang,
                    influenceValue = gangRegistry[strongestGang].influence,
                    isContested = false,
                    coordinates = GetIslandCoordinates(island)
                };
            }
        }
        
        [BurstCompile]
        int GetStrongestGangForIsland(int island)
        {
            int strongest = 0;
            int maxInfluence = 0;
            
            for (int i = 0; i < totalGangs; i++)
            {
                if (gangRegistry[i].homeIsland == island && gangRegistry[i].influence > maxInfluence)
                {
                    maxInfluence = gangRegistry[i].influence;
                    strongest = i;
                }
            }
            
            return strongest;
        }
        
        [BurstCompile]
        float2 GetIslandCoordinates(int island)
        {
            // Simplified island mapping
            return new float2(island * 2.5f, (island % 7) * 1.2f);
        }
        
        void Update()
        {
            if (enableGangWarfare && Time.time % territoryUpdateInterval < Time.deltaTime)
            {
                UpdateTerritoryControl();
            }
        }
        
        [BurstCompile]
        void UpdateTerritoryControl()
        {
            var job = new TerritoryUpdateJob
            {
                gangData = gangRegistry,
                territoryMap = territoryMap,
                currentTime = Time.time
            };
            
            job.Schedule(41, 1).Complete();
        }
        
        [BurstCompile]
        struct TerritoryUpdateJob : IJobParallelFor
        {
            public NativeArray<GangData> gangData;
            public NativeArray<TerritoryNode> territoryMap;
            public float currentTime;
            
            public void Execute(int island)
            {
                var node = territoryMap[island];
                if (node.isContested) return;
                
                // Check for stronger gangs attempting takeover
                for (int g = 0; g < gangData.Length; g++)
                {
                    if (gangData[g].influence > node.influenceValue + 20)
                    {
                        // 30% chance of takeover attempt
                        if (UnityEngine.Random.value < 0.3f)
                        {
                            node.isContested = true;
                            node.controllingGang = g;
                            node.influenceValue = gangData[g].influence;
                            
                            // Log gang activity
                            gangData[g] = gangData[g];
                        }
                    }
                }
                
                territoryMap[island] = node;
            }
        }
        
        public void RegisterGangMember(int gangId, int npcId)
        {
            if (gangMembers.ContainsKey(gangId))
            {
                gangMembers[gangId].Add(npcId);
                var gang = gangRegistry[gangId];
                gang.memberCount++;
                gangRegistry[gangId] = gang;
            }
        }
        
        public string GetGangInfo(int gangId)
        {
            if (gangId >= totalGangs) return "Invalid gang";
            
            var gang = gangRegistry[gangId];
            string name = "";
            for (int i = 0; i < 32 && gang.name[i] != '\0'; i++)
            {
                name += gang.name[i];
            }
            
            return $"Gang: {name}\n" +
                   $"Archetype: {gang.archetype}\n" +
                   $"Influence: {gang.influence}\n" +
                   $"Members: {gang.memberCount}\n" +
                   $"Island: {gang.homeIsland}";
        }
        
        void OnDestroy()
        {
            gangRegistry.Dispose();
            territoryMap.Dispose();
        }
    }
}
