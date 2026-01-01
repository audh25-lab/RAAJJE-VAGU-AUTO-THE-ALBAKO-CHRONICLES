// BoduberuSystem.cs - RVACONT-004
// Maldivian cultural rhythm mini-game system
// Performance-based rewards & community reputation

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RVA.TAC.GAMEPLAY
{
    [BurstCompile]
    public partial struct BoduberuSystem : ISystem
    {
        private const float BEAT_WINDOW = 0.15f; // Timing leniency (mobile-friendly)
        private const int PERFORMANCE_DURATION = 120; // Seconds
        private const int PARTICIPANT_BONUS = 50; // Per spectator

        private ComponentLookup<BoduberuComponent> boduberuLookup;
        private ComponentLookup<SkillComponent> skillLookup;
        private ComponentLookup<ReputationComponent> reputationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            boduberuLookup = state.GetComponentLookup<BoduberuComponent>();
            skillLookup = state.GetComponentLookup<SkillComponent>();
            reputationLookup = state.GetComponentLookup<ReputationComponent>();
            
            state.RequireForUpdate<BoduberuComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            boduberuLookup.Update(ref state);
            skillLookup.Update(ref state);
            reputationLookup.Update(ref state);
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            // Update active performances
            var performanceJob = new PerformanceUpdateJob
            {
                BoduberuComponents = boduberuLookup,
                SkillComponents = skillLookup,
                DeltaTime = deltaTime,
                CurrentTime = SystemAPI.Time.ElapsedTime,
                PrayerActive = SystemAPI.TryGetSingleton<PrayerTimeComponent>(out var prayer) && prayer.IsPrayerTimeActive
            };
            performanceJob.ScheduleParallel();
            
            // Process rhythm input (mobile touch)
            ProcessRhythmInput(ref state);
            
            // Award performance rewards
            AwardPerformanceRewards(ref state);
        }

        [BurstCompile]
        partial struct PerformanceUpdateJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<SkillComponent> SkillComponents;
            public ComponentLookup<BoduberuComponent> BoduberuComponents;
            public float DeltaTime;
            public float CurrentTime;
            public bool PrayerActive;

            void Execute(Entity performerEntity)
            {
                if (!BoduberuComponents.HasComponent(performerEntity)) return;
                
                var boduberu = BoduberuComponents[performerEntity];
                if (!boduberu.IsPerformanceActive) return;
                
                // Update performance timer
                boduberu.PerformanceTimer += DeltaTime;
                
                // Generate beat pattern (traditional rhythms)
                if (CurrentTime - boduberu.LastBeatTime > boduberu.BeatInterval)
                {
                    GenerateBeatPattern(ref boduberu, CurrentTime);
                    boduberu.LastBeatTime = CurrentTime;
                }
                
                // Check for beat accuracy
                if (boduberu.LastInputTime > 0f)
                {
                    float timeSinceBeat = CurrentTime - boduberu.LastBeatTime;
                    if (timeSinceBeat < BEAT_WINDOW)
                    {
                        // Perfect beat
                        boduberu.PerfectionStreak++;
                        boduberu.TotalScore += 100 * (1 + boduberu.PerfectionStreak * 0.1f);
                        boduberu.LastBeatAccuracy = 1f;
                    }
                    else if (timeSinceBeat < BEAT_WINDOW * 2f)
                    {
                        // Good beat
                        boduberu.PerfectionStreak = 0;
                        boduberu.TotalScore += 50;
                        boduberu.LastBeatAccuracy = 0.5f;
                    }
                    else
                    {
                        // Missed
                        boduberu.PerfectionStreak = 0;
                        boduberu.LastBeatAccuracy = 0f;
                    }
                    
                    // Award skill XP
                    if (SkillComponents.HasComponent(performerEntity))
                    {
                        var skills = SkillComponents[performerEntity];
                        skills.BoduberuXP += 10f * boduberu.LastBeatAccuracy;
                        SkillComponents[performerEntity] = skills;
                    }
                    
                    boduberu.LastInputTime = 0f; // Reset
                }
                
                // End performance
                if (boduberu.PerformanceTimer >= PERFORMANCE_DURATION)
                {
                    boduberu.IsPerformanceActive = false;
                    boduberu.PerformanceComplete = true;
                }
                
                BoduberuComponents[performerEntity] = boduberu;
            }

            private void GenerateBeatPattern(ref BoduberuComponent boduberu, float currentTime)
            {
                // Traditional Boduberu rhythms: complex patterns
                var rng = Unity.Mathematics.Random.CreateFromIndex((uint)(currentTime * 1000));
                
                // Change tempo slightly (dynamic performance)
                boduberu.BeatInterval = rng.NextFloat(0.8f, 1.2f);
                
                // Generate next beat button (simple pattern for mobile)
                boduberu.NextBeatButton = (byte)rng.NextInt(0, 4); // 4 drum types
            }
        }

        private void ProcessRhythmInput(ref SystemState state)
        {
            // Mobile touch input for rhythm game
            foreach (var (input, boduberu) in 
                SystemAPI.Query<RefRO<TouchInputComponent>, RefRW<BoduberuComponent>>())
            {
                if (!boduberu.ValueRO.IsPerformanceActive) continue;
                
                // Detect touch beats (simplified)
                if (input.ValueRO.IsTap)
                {
                    boduberu.ValueRW.LastInputTime = SystemAPI.Time.ElapsedTime;
                    boduberu.ValueRW.TotalInputs++;
                }
            }
        }

        private void AwardPerformanceRewards(ref SystemState state)
        {
            // Award based on performance score
            foreach (var (boduberu, inventory, entity) in 
                SystemAPI.Query<RefRO<BoduberuComponent>, RefRW<InventoryComponent>>()
                .WithEntityAccess())
            {
                if (!boduberu.ValueRO.PerformanceComplete) continue;
                
                // Calculate rewards
                float performanceQuality = boduberu.ValueRO.TotalScore / 10000f; // Normalize
                int participantBonus = boduberu.ValueRO.ParticipantCount * PARTICIPANT_BONUS;
                int totalReward = (int)(performanceQuality * 500) + participantBonus;
                
                // Add currency
                inventory.ValueRW.RufiyaaCurrency += totalReward / 100;
                inventory.ValueRW.LaariCurrency += totalReward % 100;
                
                // Reputation bonus
                if (SystemAPI.HasComponent<ReputationComponent>(entity))
                {
                    var rep = SystemAPI.GetComponent<ReputationComponent>(entity);
                    rep.LocalCommunity += (int)(performanceQuality * 20);
                    SystemAPI.SetComponent(entity, rep);
                }
                
                // Reset for next performance
                SystemAPI.GetComponent<BoduberuComponent>(entity) = new BoduberuComponent
                {
                    IsPerformanceActive = false,
                    PerformanceComplete = false,
                    TotalScore = 0,
                    PerfectionStreak = 0
                };
            }
        }
    }

    [BurstCompile]
    public struct BoduberuComponent : IComponentData
    {
        public bool IsPerformanceActive;
        public bool PerformanceComplete;
        public float PerformanceTimer;
        public float LastBeatTime;
        public float BeatInterval;
        public byte NextBeatButton; // Expected input
        public float LastInputTime;
        public int TotalScore;
        public int PerfectionStreak;
        public float LastBeatAccuracy;
        public int TotalInputs;
        public int ParticipantCount;
    }

    [BurstCompile]
    public struct BoduberuParticipantComponent : IComponentData
    {
        public Entity Performer;
        public float EnjoymentLevel;
    }
}
