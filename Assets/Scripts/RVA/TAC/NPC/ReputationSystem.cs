using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using UnityEngine.Events;

namespace RVA.TAC.Progression
{
    [BurstCompile]
    public class ReputationSystem : MonoBehaviour
    {
        [Header("Maldivian Faction Reputation")]
        public int maxReputation = 100;
        public int minReputation = -100;
        public int startingReputation = 0;
        
        private NativeArray<int> gangReputation;
        private NativeArray<int> civilianReputation;
        private NativeArray<int> policeReputation;
        private NativeArray<int> religiousReputation;
        
        [Header("Prayer & Cultural Impact")]
        public int prayerReputationBonus = 5;
        public int boduberuReputationBonus = 3;
        public int fishingReputationBonus = 2;
        
        [Header("Crime Penalties")]
        public int assaultPenalty = -15;
        public int theftPenalty = -10;
        public int murderPenalty = -50;
        public int drugPenalty = -20;
        public int smugglingPenalty = -25;
        
        public UnityEvent<int> OnReputationChanged;
        public UnityEvent<int> OnGangHostility;
        
        void Awake()
        {
            gangReputation = new NativeArray<int>(83, Allocator.Persistent);
            civilianReputation = new NativeArray<int>(41, Allocator.Persistent);
            policeReputation = new NativeArray<int>(1, Allocator.Persistent);
            religiousReputation = new NativeArray<int>(1, Allocator.Persistent);
            
            InitializeReputation();
        }
        
        [BurstCompile]
        void InitializeReputation()
        {
            // Set starting values
            for (int i = 0; i < 83; i++)
            {
                gangReputation[i] = startingReputation;
            }
            
            for (int i = 0; i < 41; i++)
            {
                civilianReputation[i] = startingReputation + UnityEngine.Random.Range(-10, 10);
            }
            
            policeReputation[0] = startingReputation;
            religiousReputation[0] = startingReputation;
        }
        
        // === Reputation Modifiers ===
        
        public void ModifyGangReputation(int gangId, int amount, string reason = "")
        {
            if (gangId < 0 || gangId >= 83) return;
            
            int oldRep = gangReputation[gangId];
            int newRep = Mathf.Clamp(oldRep + amount, minReputation, maxReputation);
            gangReputation[gangId] = newRep;
            
            Debug.Log($"[RVA:TAC] Gang {gangId} reputation: {oldRep} → {newRep} ({reason})");
            
            OnReputationChanged?.Invoke(gangId);
            
            // Trigger hostility if reputation drops below threshold
            if (newRep < -50 && oldRep >= -50)
            {
                OnGangHostility?.Invoke(gangId);
            }
        }
        
        public void ModifyCivilianReputation(int islandId, int amount, string reason = "")
        {
            if (islandId < 0 || islandId >= 41) return;
            
            int oldRep = civilianReputation[islandId];
            int newRep = Mathf.Clamp(oldRep + amount, minReputation, maxReputation);
            civilianReputation[islandId] = newRep;
            
            Debug.Log($"[RVA:TAC] Island {islandId} civilian rep: {oldRep} → {newRep} ({reason})");
        }
        
        public void ModifyPoliceReputation(int amount, string reason = "")
        {
            int oldRep = policeReputation[0];
            int newRep = Mathf.Clamp(oldRep + amount, minReputation, maxReputation);
            policeReputation[0] = newRep;
            
            Debug.Log($"[RVA:TAC] Police reputation: {oldRep} → {newRep} ({reason})");
        }
        
        public void ModifyReligiousReputation(int amount, string reason = "")
        {
            int oldRep = religiousReputation[0];
            int newRep = Mathf.Clamp(oldRep + amount, minReputation, maxReputation);
            religiousReputation[0] = newRep;
            
            Debug.Log($"[RVA:TAC] Religious reputation: {oldRep} → {newRep} ({reason})");
        }
        
        // === Cultural Activity Bonuses ===
        
        public void OnPlayerPrayerCompleted()
        {
            ModifyReligiousReputation(prayerReputationBonus, "Prayer participation");
            
            // Bonus on current island
            int currentIsland = GetCurrentIsland();
            ModifyCivilianReputation(currentIsland, prayerReputationBonus / 2, "Community prayer");
        }
        
        public void OnPlayerBoduberuParticipated()
        {
            ModifyCivilianReputation(GetCurrentIsland(), boduberuReputationBonus, "Cultural engagement");
            ModifyReligiousReputation(boduberuReputationBonus / 2, "Traditional music support");
        }
        
        public void OnPlayerFishingAssistance(int islandId)
        {
            ModifyCivilianReputation(islandId, fishingReputationBonus, "Fishing assistance");
            
            // Find local fisherman gangs on that island
            for (int i = 0; i < 83; i++)
            {
                if (IsFishermanGangOnIsland(i, islandId))
                {
                    ModifyGangReputation(i, fishingReputationBonus, "Fishing cooperation");
                }
            }
        }
        
        // === Crime Reporting ===
        
        public void ReportCrime(CrimeType crime, int islandId = -1)
        {
            switch (crime)
            {
                case CrimeType.Assault:
                    ModifyPoliceReputation(assaultPenalty / 2, "Assault reported");
                    if (islandId >= 0) ModifyCivilianReputation(islandId, assaultPenalty, "Violence on island");
                    break;
                    
                case CrimeType.Theft:
                    ModifyPoliceReputation(theftPenalty / 2, "Theft reported");
                    if (islandId >= 0) ModifyCivilianReputation(islandId, theftPenalty, "Theft on island");
                    break;
                    
                case CrimeType.Murder:
                    ModifyPoliceReputation(murderPenalty / 2, "Murder reported");
                    if (islandId >= 0) ModifyCivilianReputation(islandId, murderPenalty, "Murder on island");
                    
                    // Severe gang reputation hit across all gangs
                    for (int i = 0; i < 83; i++)
                    {
                        ModifyGangReputation(i, murderPenalty / 3, "Murder causes instability");
                    }
                    break;
                    
                case CrimeType.DrugRelated:
                    ModifyPoliceReputation(drugPenalty / 2, "Drug activity reported");
                    ModifyReligiousReputation(drugPenalty, "Drug use violation");
                    break;
                    
                case CrimeType.Smuggling:
                    ModifyPoliceReputation(smugglingPenalty / 2, "Smuggling reported");
                    // Some gangs may approve
                    for (int i = 0; i < 83; i++)
                    {
                        if (gangRegistry[i].archetype == GangArchetype.Smugglers)
                        {
                            ModifyGangReputation(i, Mathf.Abs(smugglingPenalty) / 2, "Smuggling respect");
                        }
                    }
                    break;
            }
        }
        
        public enum CrimeType
        {
            Assault,
            Theft,
            Murder,
            DrugRelated,
            Smuggling
        }
        
        // === Reputation Checks ===
        
        public int GetGangReputation(int gangId)
        {
            return (gangId >= 0 && gangId < 83) ? gangReputation[gangId] : 0;
        }
        
        public int GetCivilianReputation(int islandId)
        {
            return (islandId >= 0 && islandId < 41) ? civilianReputation[islandId] : 0;
        }
        
        public int GetPoliceReputation()
        {
            return policeReputation[0];
        }
        
        public int GetReligiousReputation()
        {
            return religiousReputation[0];
        }
        
        // === Reputation-Based Behaviors ===
        
        public bool IsGangFriendly(int gangId)
        {
            return GetGangReputation(gangId) > 25;
        }
        
        public bool IsGangHostile(int gangId)
        {
            return GetGangReputation(gangId) < -25;
        }
        
        public bool IsGangNeutral(int gangId)
        {
            int rep = GetGangReputation(gangId);
            return rep >= -25 && rep <= 25;
        }
        
        public bool CanEnterIslandSafely(int islandId)
        {
            int civilianRep = GetCivilianReputation(islandId);
            return civilianRep > -30; // Below -30, locals may attack on sight
        }
        
        public bool IsPoliceLookingForPlayer()
        {
            return GetPoliceReputation() < -40;
        }
        
        public bool IsReligiousCommunitySupportive()
        {
            return GetReligiousReputation() > 30;
        }
        
        // Helper methods
        int GetCurrentIsland()
        {
            // Would integrate with IslandGenerator
            return 0; // Placeholder
        }
        
        bool IsFishermanGangOnIsland(int gangId, int islandId)
        {
            // Would integrate with GangSystem
            return UnityEngine.Random.value < 0.3f; // Placeholder
        }
        
        void OnDestroy()
        {
            gangReputation.Dispose();
            civilianReputation.Dispose();
            policeReputation.Dispose();
            religiousReputation.Dispose();
        }
        
        // Accessibility: Reputation status for screen readers
        public string GetReputationSummary()
        {
            int currentIsland = GetCurrentIsland();
            return $"Police: {GetPoliceReputation()}, " +
                   $"Religious: {GetReligiousReputation()}, " +
                   $"Island {currentIsland} Civilians: {GetCivilianReputation(currentIsland)}";
        }
    }
}
