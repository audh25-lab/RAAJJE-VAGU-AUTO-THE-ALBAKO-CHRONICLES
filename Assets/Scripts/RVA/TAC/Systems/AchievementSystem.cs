using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RVA.TAC.Cultural;

namespace RVA.TAC.Gameplay
{
    /// <summary>
    /// Achievement tracking with Maldives-specific milestones
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        #region Achievement Data
        [System.Serializable]
        public class Achievement
        {
            public string achievementId;
            public string titleEn;
            public string titleDhivehi;
            public string descriptionEn;
            public string descriptionDhivehi;
            public string iconKey;
            public int points;
            public bool isSecret;
            public bool isUnlocked;
            public System.DateTime unlockTime;
            public string[] prerequisites; // Other achievement IDs required
            public AchievementCategory category;
        }
        
        public enum AchievementCategory
        {
            IslandExploration,
            CulturalMastery,
            CombatProficiency,
            EconomicSuccess,
            MissionCompletion,
            SocialReputation,
            FishingMastery,
            VehicleMastery,
            StealthOperations,
            EnvironmentalProtection
        }
        
        private Dictionary<string, Achievement> achievementDatabase = new Dictionary<string, Achievement>();
        private List<Achievement> unlockedAchievements = new List<Achievement>();
        #endregion

        # region Maldives-Specific Achievements
        private void Awake()
        {
            InitializeAchievements();
        }

        private void InitializeAchievements()
        {
            // Island Exploration (41 islands)
            AddAchievement(new Achievement
            {
                achievementId = "ISLANDS_5",
                titleEn = "Island Hopper",
                titleDhivehi = "ޖަޒީރާ ހޯދުންވެރި",
                descriptionEn = "Visit 5 islands",
                descriptionDhivehi = "5 ޖަޒީރަ ދެއްކުން",
                iconKey = "Island",
                points = 10,
                category = AchievementCategory.IslandExploration
            });
            
            AddAchievement(new Achievement
            {
                achievementId = "ISLANDS_20",
                titleEn = "Atoll Explorer",
                titleDhivehi = "އަތޮޅު ހޯދުންވެރި",
                descriptionEn = "Visit 20 islands",
                descriptionDhivehi = "20 ޖަޒީރަ ދެއްކުން",
                iconKey = "Atoll",
                points = 25,
                category = AchievementCategory.IslandExploration,
                prerequisites = new[] { "ISLANDS_5" }
            });
            
            AddAchievement(new Achievement
            {
                achievementId = "ISLANDS_41",
                titleEn = "Maldives Master",
                titleDhivehi = "ދިވެހިރާއްޖޭގެ ވެރި",
                descriptionEn = "Visit all 41 islands",
                descriptionDhivehi = "ހުރިހާ 41 ޖަޒީރައެއް ދެއްކުން",
                iconKey = "MaldivesFlag",
                points = 100,
                category = AchievementCategory.IslandExploration,
                prerequisites = new[] { "ISLANDS_20" },
                isSecret = true
            });
            
            // Cultural Mastery
            AddAchievement(new Achievement
            {
                achievementId = "PRAYER_ATTEND_10",
                titleEn = "Devout Islander",
                titleDhivehi = "ދީންވެރި ޖަޒީރާވެރި",
                descriptionEn = "Attend 10 prayer times",
                descriptionDhivehi = "10 ފަހަރު ނަމާދުގައި ބައިވެރިވުން",
                iconKey = "Mosque",
                points = 20,
                category = AchievementCategory.CulturalMastery
            });
            
            AddAchievement(new Achievement
            {
                achievementId = "BODUBERU_PLAY",
                titleEn = "Rhythm Keeper",
                titleDhivehi = "ރިއަލް ކިއުންތެރިޔާ",
                descriptionEn = "Participate in a Boduberu performance",
                descriptionDhivehi = "ބޮޑުބެރުގައި ބައިވެރިވުން",
                iconKey = "Drum",
                points = 15,
                category = AchievementCategory.CulturalMastery
            });
            
            // Gang Reputation (83 gangs)
            AddAchievement(new Achievement
            {
                achievementId = "GANGS_MET_10",
                titleEn = "Street Networker",
                titleDhivehi = "މަގުގެ ކޮންކެނޭޝަން",
                descriptionEn = "Interact with 10 different gangs",
                descriptionDhivehi = "10 ޖަންގުންގެ މެންބަރުންނާއި ގުޅުން",
                iconKey = "Network",
                points = 20,
                category = AchievementCategory.SocialReputation
            });
            
            AddAchievement(new Achievement
            {
                achievementId = "GANGS_83",
                titleEn = "Gangland Diplomat",
                titleDhivehi = "ޖަންގު ޑިޕްލޯމެޓް",
                descriptionEn = "Discover all 83 gangs",
                descriptionDhivehi = "ހުރިހާ 83 ޖަންގު ހޯދުން",
                iconKey = "Diploma",
                points = 75,
                isSecret = true,
                category = AchievementCategory.SocialReputation
            });
            
            // Mission completion
            AddAchievement(new Achievement
            {
                achievementId = "MISSIONS_10",
                titleEn = "Problem Solver",
                titleDhivehi = "މައްސަލަތައް ހައްލުކުރުންވެރި",
                descriptionEn = "Complete 10 missions",
                descriptionDhivehi = "10 މިޝަން ފުރިހަމަކުރުން",
                iconKey = "Checkmark",
                points = 25,
                category = AchievementCategory.MissionCompletion
            });
            
            // Fishing Mastery
            AddAchievement(new Achievement
            {
                achievementId = "TUNA_100",
                titleEn = "Tuna Tycoon",
                titleDhivehi = "ބުޅަ ބޮޑުވެރި",
                descriptionEn = "Catch 100 tuna fish",
                descriptionDhivehi = "100 ބުޅަ މަސް ހިއްލާން",
                iconKey = "Fish",
                points = 30,
                category = AchievementCategory.FishingMastery
            });
            
            // Building discovery (70 buildings)
            AddAchievement(new Achievement
            {
                achievementId = "BUILDINGS_20",
                titleEn = "Architectural Scout",
                titleDhivehi = "އިމާރާތް ހޯދުންވެރި",
                descriptionEn = "Discover 20 unique buildings",
                descriptionDhivehi = "20 އojަގު އިމާރާތް ހޯދުން",
                iconKey = "Building",
                points = 20,
                category = AchievementCategory.IslandExploration
            });
            
            AddAchievement(new Achievement
            {
                achievementId = "BUILDINGS_70",
                titleEn = "Urban Explorer",
                titleDhivehi = "ޝާރަވެރި ހޯދުންވެރި",
                descriptionEn = "Discover all 70 building types",
                descriptionDhivehi = "ހުރިހާ 70 އިމާރާތް ޓައިޕު ހޯދުން",
                iconKey = "City",
                points = 50,
                isSecret = true,
                category = AchievementCategory.IslandExploration
            });
            
            Debug.Log($"[AchievementSystem] Initialized with {achievementDatabase.Count} achievements across {System.Enum.GetValues(typeof(AchievementCategory)).Length} categories");
        }

        private void AddAchievement(Achievement achievement)
        {
            achievementDatabase[achievement.achievementId] = achievement;
        }

        /// <summary>
        /// Unlock an achievement
        /// </summary>
        public bool UnlockAchievement(string achievementId)
        {
            if (!achievementDatabase.ContainsKey(achievementId))
            {
                Debug.LogWarning($"[AchievementSystem] Achievement not found: {achievementId}");
                return false;
            }
            
            var achievement = achievementDatabase[achievementId];
            
            if (achievement.isUnlocked)
                return false; // Already unlocked
            
            // Check prerequisites
            if (!CanUnlockAchievement(achievement))
            {
                Debug.Log($"[AchievementSystem] Prerequisites not met for: {achievementId}");
                return false;
            }
            
            // Unlock it
            achievement.isUnlocked = true;
            achievement.unlockTime = System.DateTime.Now;
            unlockedAchievements.Add(achievement);
            
            Debug.Log($"[AchievementSystem] Unlocked: {achievement.titleEn} (+{achievement.points} points)");
            
            // Show notification
            UIManager.Instance?.ShowAchievementNotification(achievement);
            
            // Play sound
            AudioManager.Instance?.PlayLocalizedSFX(
                Resources.Load<AudioClip>("Audio/SFX/achievement_unlock"),
                Camera.main.transform.position
            );
            
            // Award points to player profile
            PlayerProfile.Instance?.AddAchievementPoints(achievement.points);
            
            // Check for milestone achievements
            CheckMilestoneAchievements();
            
            // Save immediately
            SaveSystem.Instance?.SaveAchievementData();
            
            return true;
        }

        private bool CanUnlockAchievement(Achievement achievement)
        {
            if (achievement.prerequisites == null || achievement.prerequisites.Length == 0)
                return true;
            
            foreach (var prereqId in achievement.prerequisites)
            {
                if (!achievementDatabase.ContainsKey(prereqId) || !achievementDatabase[prereqId].isUnlocked)
                    return false;
            }
            
            return true;
        }

        private void CheckMilestoneAchievements()
        {
            int totalPoints = PlayerProfile.Instance?.GetTotalAchievementPoints() ?? 0;
            
            // Milestone achievements
            if (totalPoints >= 100 && !IsAchievementUnlocked("MILESTONE_100"))
                UnlockAchievement("MILESTONE_100");
            
            if (totalPoints >= 250 && !IsAchievementUnlocked("MILESTONE_250"))
                UnlockAchievement("MILESTONE_250");
            
            if (totalPoints >= 500 && !IsAchievementUnlocked("MILESTONE_500"))
                UnlockAchievement("MILESTONE_500");
        }

        /// <summary>
        /// Check if player meets criteria for hidden achievements
        /// </summary>
        private void CheckHiddenAchievementConditions()
        {
            // Example: Secret achievement for earning 1,000,000 Rufiyaa
            if (EconomySystem.Instance?.GetTotalEarnings() >= 1000000)
            {
                UnlockAchievement("MILLIONAIRE");
            }
            
            // Secret achievement for visiting all islands without fast travel
            if (IslandGenerator.Instance?.GetDiscoveredIslandCount() >= 41)
            {
                UnlockAchievement("PURIST_EXPLORER");
            }
        }

        public bool IsAchievementUnlocked(string achievementId)
        {
            return achievementDatabase.ContainsKey(achievementId) && achievementDatabase[achievementId].isUnlocked;
        }

        public List<Achievement> GetUnlockedAchievements() => unlockedAchievements;

        public List<Achievement> GetAchievementsByCategory(AchievementCategory category)
        {
            return achievementDatabase.Values.Where(a => a.category == category).ToList();
        }

        public int GetTotalPoints()
        {
            return unlockedAchievements.Sum(a => a.points);
        }

        public float GetCompletionPercentage()
        {
            return (float)unlockedAchievements.Count / achievementDatabase.Count * 100f;
        }

        /// <summary>
        /// For testing: unlock all achievements
        /// </summary>
        public void UnlockAllAchievements()
        {
            foreach (var achievement in achievementDatabase.Values)
            {
                if (!achievement.isUnlocked)
                {
                    UnlockAchievement(achievement.achievementId);
                }
            }
        }

        #region Public API
        public Dictionary<string, Achievement> GetAllAchievements() => achievementDatabase;
        public Achievement GetAchievement(string achievementId) => achievementDatabase.ContainsKey(achievementId) ? achievementDatabase[achievementId] : null;
        public int GetUnlockedCount() => unlockedAchievements.Count;
        public int GetTotalCount() => achievementDatabase.Count;
        #endregion
    }

    /// <summary>
    /// Player profile for persistent achievement data
    /// </summary>
    public class PlayerProfile
    {
        private static PlayerProfile instance;
        public static PlayerProfile Instance
        {
            get
            {
                if (instance == null)
                    instance = new PlayerProfile();
                return instance;
            }
        }
        
        private int achievementPoints = 0;
        
        public void AddAchievementPoints(int points)
        {
            achievementPoints += points;
        }
        
        public int GetTotalAchievementPoints() => achievementPoints;
    }
}
