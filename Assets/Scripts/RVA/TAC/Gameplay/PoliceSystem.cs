// PoliceSystem.cs - RVACONT-004
// Maldives law enforcement: Indian Ocean security context
// Islamic law integration, island-based pursuit

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace RVA.TAC.GAMEPLAY
{
    [BurstCompile]
    public partial struct PoliceSystem : ISystem
    {
        private const float MALDIVES_POLICE_RESPONSE_TIME = 30f; // Island navigation takes time
        private const float ISLAMIC_LAW_MULTIPLIER = 1.5f; // Stricter penalties for certain crimes
        private const float PRAYER_TIME_TOLERANCE = 0.7f; // Reduced enforcement during prayer

        private ComponentLookup<PoliceComponent> policeLookup;
        private ComponentLookup<WantedLevelComponent> wantedLookup;
        private ComponentLookup<LocalTransform> transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            policeLookup = state.GetComponentLookup<PoliceComponent>();
            wantedLookup = state.GetComponentLookup<WantedLevelComponent>();
            transformLookup = state.GetComponentLookup<LocalTransform>();
            
            state.RequireForUpdate<PoliceComponent>();
            state.RequireForUpdate<WantedLevelComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            policeLookup.Update(ref state);
            wantedLookup.Update(ref state);
            transformLookup.Update(ref state);
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            // Police AI patrol & response
            var policeJob = new PoliceAIJob
            {
                PoliceComponents = policeLookup,
                WantedLevels = wantedLookup,
                Transforms = transformLookup,
                DeltaTime = deltaTime,
                PrayerTimeMod = SystemAPI.TryGetSingleton<PrayerTimeComponent>(out var prayer) && prayer.IsPrayerTimeActive
                               ? PRAYER_TIME_TOLERANCE : 1f
            };
            policeJob.ScheduleParallel();
            
            // Process crimes & violations
            ProcessCrimes(ref state);
            
            // Handle arrests & fines
            ProcessArrests(ref state);
        }

        [BurstCompile]
        partial struct PoliceAIJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<WantedLevelComponent> WantedLevels;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            public ComponentLookup<PoliceComponent> PoliceComponents;
            public float DeltaTime;
            public float PrayerTimeMod;

            void Execute(Entity policeEntity, ref LocalTransform transform)
            {
                if (!PoliceComponents.HasComponent(policeEntity)) return;
                
                var police = PoliceComponents[policeEntity];
                
                // Patrol mode or pursuit mode
                if (police.CurrentTarget == Entity.Null)
                {
                    // Patrol nearby islands
                    UpdatePatrol(policeEntity, ref police, ref transform);
                }
                else
                {
                    // Pursue target
                    UpdatePursuit(policeEntity, ref police, ref transform);
                }
                
                PoliceComponents[policeEntity] = police;
            }

            private void UpdatePatrol(Entity policeEntity, ref PoliceComponent police, 
                                     ref LocalTransform transform)
            {
                police.PatrolTimer += DeltaTime;
                
                if (police.PatrolTimer > police.PatrolInterval)
                {
                    // Move to next patrol point (island hopping)
                    float3 nextPatrolPoint = GetNextIslandPatrolPoint(police.PatrolIndex);
                    
                    // Boat movement (all police use boats in Maldives)
                    float3 direction = math.normalize(nextPatrolPoint - transform.Position);
                    transform.Position += direction * police.PatrolSpeed * DeltaTime * PrayerTimeMod;
                    
                    // Check if reached
                    if (math.distance(transform.Position, nextPatrolPoint) < 5f)
                    {
                        police.PatrolIndex = (police.PatrolIndex + 1) % 41; // 41 islands
                        police.PatrolTimer = 0f;
                    }
                }
            }

            private void UpdatePursuit(Entity policeEntity, ref PoliceComponent police,
                                      ref LocalTransform transform)
            {
                if (!WantedLevels.HasComponent(police.CurrentTarget)) return;
                
                var targetTransform = Transforms[police.CurrentTarget];
                float3 direction = math.normalize(targetTransform.Position - transform.Position);
                
                // Pursuit speed (faster during chase)
                float pursuitSpeed = police.PursuitSpeed * PrayerTimeMod;
                transform.Position += direction * pursuitSpeed * DeltaTime;
                
                // Check if caught
                if (math.distance(transform.Position, targetTransform.Position) < 3f)
                {
                    police.IsArresting = true;
                }
            }

            private float3 GetNextIslandPatrolPoint(int patrolIndex)
            {
                // Simple circular patrol of islands
                float angle = (patrolIndex / 41f) * math.PI * 2f;
                float radius = 500f; // Island distribution radius
                return new float3(
                    math.cos(angle) * radius,
                    0f,
                    math.sin(angle) * radius
                );
            }
        }

        private void ProcessCrimes(ref SystemState state)
        {
            // Check for violations
            foreach (var (crime, entity) in 
                SystemAPI.Query<RefRO<CrimeComponent>>().WithEntityAccess())
            {
                var wantedLevel = new WantedLevelComponent
                {
                    HeatLevel = CalculateCrimeSeverity(crime.ValueRO.CrimeType),
                    Stars = 1,
                    LastCrimeTarget = crime.ValueRO.Victim,
                    TimeSinceLastCrime = 0f
                };
                
                // Apply Islamic law multiplier for specific crimes
                if (IsIslamicLawViolation(crime.ValueRO.CrimeType))
                {
                    wantedLevel.HeatLevel *= ISLAMIC_LAW_MULTIPLIER;
                    wantedLevel.Stars = 2; // Instant escalation
                }
                
                state.EntityManager.AddComponentData(entity, wantedLevel);
                state.EntityManager.RemoveComponent<CrimeComponent>(entity);
            }
        }

        private float CalculateCrimeSeverity(CrimeType crimeType)
        {
            return crimeType switch
            {
                CrimeType.PettyTheft => 0.2f,
                CrimeType.Assault => 0.4f,
                CrimeType.Disrespect => 0.15f, // Cultural crime
                CrimeType.IllegalFishing => 0.5f, // Environmental crime
                CrimeType.Smuggling => 0.6f,
                CrimeType.Vandalism => 0.3f,
                CrimeType.PublicDisturbance => 0.25f,
                _ => 0.1f
            };
        }

        private bool IsIslamicLawViolation(CrimeType crimeType)
        {
            // Islamic law specific violations (culturally sensitive)
            return crimeType == CrimeType.Disrespect || 
                   crimeType == CrimeType.PublicDisturbance;
        }

        private void ProcessArrests(ref SystemState state)
        {
            foreach (var (police, entity) in 
                SystemAPI.Query<RefRO<PoliceComponent>>().WithEntityAccess())
            {
                if (!police.ValueRO.IsArresting) continue;
                
                Entity suspect = police.ValueRO.CurrentTarget;
                if (!wantedLookup.HasComponent(suspect)) continue;
                
                // Arrest process
                var wanted = wantedLookup[suspect];
                float fineAmount = CalculateFine(wanted.HeatLevel);
                
                // Apply fine (if player)
                if (SystemAPI.HasComponent<InventoryComponent>(suspect))
                {
                    var inventory = SystemAPI.GetComponent<InventoryComponent>(suspect);
                    int totalLaari = inventory.RufiyaaCurrency * 100 + inventory.LaariCurrency;
                    
                    if (totalLaari >= fineAmount)
                    {
                        // Pay fine
                        totalLaari -= (int)fineAmount;
                        inventory.RufiyaaCurrency = totalLaari / 100;
                        inventory.LaariCurrency = totalLaari % 100;
                        SystemAPI.SetComponent(suspect, inventory);
                        
                        // Clear wanted level (paid fine)
                        state.EntityManager.RemoveComponent<WantedLevelComponent>(suspect);
                    }
                    else
                    {
                        // Can't pay - jail time (game over/wake up mechanic)
                        SystemAPI.AddComponent(suspect, new JailComponent 
                        { 
                            SentenceTime = wanted.HeatLevel * 30f // Seconds
                        });
                    }
                }
                
                // Reset police state
                var policeUpdate = police.ValueRO;
                policeUpdate.IsArresting = false;
                policeUpdate.CurrentTarget = Entity.Null;
                SystemAPI.SetComponent(entity, policeUpdate);
            }
        }

        private float CalculateFine(float heatLevel)
        {
            // Fine in Laari (MVR fines)
            return heatLevel * 1000f; // Scale with crime severity
        }
    }

    [BurstCompile]
    public struct PoliceComponent : IComponentData
    {
        public Entity CurrentTarget;
        public bool IsArresting;
        public float PatrolSpeed;
        public float PursuitSpeed;
        public float PatrolTimer;
        public float PatrolInterval;
        public int PatrolIndex;
    }

    [BurstCompile]
    public struct CrimeComponent : IComponentData
    {
        public CrimeType CrimeType;
        public Entity Victim;
        public float3 Location;
        public float Severity;
    }

    [BurstCompile]
    public struct JailComponent : IComponentData
    {
        public float SentenceTime;
        public float TimeServed;
    }

    public enum CrimeType : byte
    {
        None = 0,
        PettyTheft = 1,
        Assault = 2,
        Disrespect = 3, // To elders/culture
        IllegalFishing = 4,
        Smuggling = 5,
        Vandalism = 6,
        PublicDisturbance = 7, // During prayer
    }
}
