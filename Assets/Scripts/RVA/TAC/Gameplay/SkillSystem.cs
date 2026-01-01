// SkillSystem.cs - RVACONT-004
// Maldivian cultural skills: Fishing mastery, Boduberu drumming, Dhivehi language
// Progression system with Islamic values integration

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RVA.TAC.GAMEPLAY
{
    [BurstCompile]
    public partial struct SkillSystem : ISystem
    {
        private const float PRAYER_TIME_XP_BONUS = 1.25f; // Skill gain bonus during prayer
        private const int MAX_SKILL_LEVEL = 10;
        private const int XP_PER_LEVEL = 1000;

        private ComponentLookup<SkillComponent> skillLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            skillLookup = state.GetComponentLookup<SkillComponent>();
            state.RequireForUpdate<SkillComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            skillLookup.Update(ref state);
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            // Calculate XP gains from activities
            var xpJob = new XPGainJob
            {
                Skills = skillLookup,
                DeltaTime = deltaTime,
                PrayerBonus = SystemAPI.TryGetSingleton<PrayerTimeComponent>(out var prayer) && prayer.IsPrayerTimeActive 
                            ? PRAYER_TIME_XP_BONUS : 1f
            };
            xpJob.ScheduleParallel();
            
            // Process skill level ups
            var levelJob = new SkillLevelUpJob
            {
                Skills = skillLookup,
                MaxLevel = MAX_SKILL_LEVEL,
                XpPerLevel = XP_PER_LEVEL
            };
            levelJob.ScheduleParallel();
            
            // Apply skill bonuses to gameplay
            ApplySkillBonuses(ref state);
        }

        [BurstCompile]
        partial struct XPGainJob : IJobEntity
        {
            public ComponentLookup<SkillComponent> Skills;
            public float DeltaTime;
            public float PrayerBonus;

            void Execute(Entity entity, in ActivityComponent activity)
            {
                if (!Skills.HasComponent(entity)) return;
                
                var skills = Skills[entity];
                float xpGained = 0f;
                
                switch (activity.ActivityType)
                {
                    case ActivityType.Fishing:
                        xpGained = activity.Duration * 5f * PrayerBonus;
                        skills.FishingXP += xpGained;
                        break;
                    case ActivityType.BoduberuPerformance:
                        xpGained = activity.Duration * 8f * PrayerBonus;
                        skills.BoduberuXP += xpGained;
                        break;
                    case ActivityType.Diving:
                        xpGained = activity.Duration * 6f;
                        skills.DivingXP += xpGained;
                        break;
                    case ActivityType.DhivehiConversation:
                        xpGained = activity.Duration * 3f * PrayerBonus;
                        skills.DhivehiLanguageXP += xpGained;
                        break;
                    case ActivityType.Prayer:
                        xpGained = activity.Duration * 10f * PrayerBonus; // High XP for prayer
                        skills.PietyXP += xpGained;
                        break;
                }
                
                Skills[entity] = skills;
            }
        }

        [BurstCompile]
        partial struct SkillLevelUpJob : IJobEntity
        {
            public ComponentLookup<SkillComponent> Skills;
            public int MaxLevel;
            public int XpPerLevel;

            void Execute(Entity entity)
            {
                var skills = Skills[entity];
                
                // Check each skill for level up
                TryLevelUp(ref skills.FishingLevel, ref skills.FishingXP);
                TryLevelUp(ref skills.BoduberuLevel, ref skills.BoduberuXP);
                TryLevelUp(ref skills.DivingLevel, ref skills.DivingXP);
                TryLevelUp(ref skills.DhivehiLanguageLevel, ref skills.DhivehiLanguageXP);
                TryLevelUp(ref skills.PietyLevel, ref skills.PietyXP);
                
                Skills[entity] = skills;
            }

            private void TryLevelUp(ref byte level, ref float xp)
            {
                if (level >= MaxLevel) return;
                
                int requiredXp = level * XpPerLevel;
                if (xp >= requiredXp)
                {
                    xp -= requiredXp;
                    level = (byte)math.min(level + 1, MaxLevel);
                }
            }
        }

        private void ApplySkillBonuses(ref SystemState state)
        {
            // Fishing skill: better catch rates
            foreach (var (skill, fishing) in 
                SystemAPI.Query<RefRO<SkillComponent>, RefRO<FishingComponent>>())
            {
                var bonus = CalculateFishingBonus(skill.ValueRO.FishingLevel);
                // Apply to fishing system (would modify fishing component)
            }
            
            // Boduberu skill: better performance rewards
            foreach (var (skill, boduberu) in 
                SystemAPI.Query<RefRO<SkillComponent>, RefRO<BoduberuComponent>>())
            {
                var bonus = CalculateBoduberuBonus(skill.ValueRO.BoduberuLevel);
                // Apply to Boduberu system
            }
            
            // Dhivehi skill: unlock dialogue options
            foreach (var skill in SystemAPI.Query<RefRO<SkillComponent>>())
            {
                if (skill.ValueRO.DhivehiLanguageLevel >= 5)
                {
                    // Unlock advanced NPC interactions
                    SystemAPI.SetComponentEnabled<DhivehiSpeakerTag>(state.SystemHandle, true);
                }
            }
        }

        private float CalculateFishingBonus(byte level)
        {
            return 1f + (level * 0.15f); // 150% bonus at max level
        }

        private float CalculateBoduberuBonus(byte level)
        {
            return 1f + (level * 0.2f); // 200% bonus at max level
        }

        // Public API for other systems
        public static byte GetFishingLevel(in SkillComponent skills)
        {
            return skills.FishingLevel;
        }

        public static byte GetBoduberuLevel(in SkillComponent skills)
        {
            return skills.BoduberuLevel;
        }
    }

    [BurstCompile]
    public struct SkillComponent : IComponentData
    {
        // Fishing - primary Maldivian skill
        public byte FishingLevel;
        public float FishingXP;
        
        // Cultural skills
        public byte BoduberuLevel;
        public float BoduberuXP;
        
        public byte DivingLevel;
        public float DivingXP;
        
        public byte DhivehiLanguageLevel; // Local language
        public float DhivehiLanguageXP;
        
        public byte PietyLevel; // Religious devotion
        public float PietyXP;
    }

    [BurstCompile]
    public struct ActivityComponent : IComponentData
    {
        public ActivityType ActivityType;
        public float Duration; // Seconds
        public bool IsActive;
    }

    public enum ActivityType : byte
    {
        None = 0,
        Fishing = 1,
        BoduberuPerformance = 2,
        Diving = 3,
        DhivehiConversation = 4,
        Prayer = 5,
        Swimming = 6,
        Boating = 7,
    }

    [BurstCompile]
    public struct BoduberuSkillComponent : IComponentData { }
    [BurstCompile]
    public struct DhivehiSpeakerTag : IComponentData, IEnableableComponent { }
}
