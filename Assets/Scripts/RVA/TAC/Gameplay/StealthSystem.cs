// StealthSystem.cs - RVACONT-004
// Maldives adaptation: Island hiding spots, marine concealment
// Police awareness based on Islamic law observance

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace RVA.TAC.GAMEPLAY
{
    [BurstCompile]
    public partial struct StealthSystem : ISystem
    {
        private const float ISLAND_COVER_BONUS = 0.3f; // Palm trees, rocks
        private const float WATER_COVER_BONUS = 0.2f; // Underwater hiding
        private const float PRAYER_TIME_BONUS = 0.25f; // "Good citizen" bonus
        private const float WANTED_LEVEL_HEAT_DECAY = 0.98f; // Slow decay in Maldives (small islands)

        private ComponentLookup<StealthComponent> stealthLookup;
        private ComponentLookup<LocalTransform> transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            stealthLookup = state.GetComponentLookup<StealthComponent>();
            transformLookup = state.GetComponentLookup<LocalTransform>();
            
            state.RequireForUpdate<StealthComponent>();
            state.RequireForUpdate<WantedLevelComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            stealthLookup.Update(ref state);
            transformLookup.Update(ref state);
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            // Update detection levels
            var detectionJob = new DetectionUpdateJob
            {
                StealthComponents = stealthLookup,
                Transforms = transformLookup,
                DeltaTime = deltaTime,
                PrayerTimeBonus = SystemAPI.TryGetSingleton<PrayerTimeComponent>(out var prayer) && prayer.IsPrayerTimeActive
                                 ? PRAYER_TIME_BONUS : 0f
            };
            detectionJob.ScheduleParallel();
            
            // Update wanted level (heat)
            var wantedJob = new WantedLevelUpdateJob
            {
                WantedLevels = state.GetComponentLookup<WantedLevelComponent>(),
                DeltaTime = deltaTime,
                DecayRate = WANTED_LEVEL_HEAT_DECAY
            };
            wantedJob.ScheduleParallel();
            
            // Process hiding spots
            ProcessHidingSpots(ref state);
        }

        [BurstCompile]
        partial struct DetectionUpdateJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<StealthComponent> StealthComponents;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            public float DeltaTime;
            public float PrayerTimeBonus;

            void Execute(Entity observer, ref DetectionComponent detection)
            {
                if (!StealthComponents.HasComponent(detection.TargetEntity)) return;
                
                var targetStealth = StealthComponents[detection.TargetEntity];
                var targetTransform = Transforms[detection.TargetEntity];
                var observerTransform = Transforms[observer];
                
                // Distance check
                float distance = math.distance(targetTransform.Position, observerTransform.Position);
                if (distance > detection.MaxDetectionRange) return;
                
                // Calculate visibility
                float visibility = 1f - targetStealth.VisibilityMultiplier;
                visibility -= PrayerTimeBonus; // Good behavior bonus
                visibility -= CalculateCoverBonus(targetTransform.Position);
                
                visibility = math.clamp(visibility, 0.1f, 1f);
                
                // Detection accumulation
                float detectionRate = (1f / distance) * visibility * detection.DetectionSpeed;
                detection.CurrentDetection = math.min(1f, detection.CurrentDetection + detectionRate * DeltaTime);
                
                // If fully detected, trigger awareness
                if (detection.CurrentDetection >= 0.95f)
                {
                    targetStealth.IsDetected = true;
                    StealthComponents[detection.TargetEntity] = targetStealth;
                }
            }

            private float CalculateCoverBonus(float3 position)
            {
                float coverBonus = 0f;
                
                // Palm tree cover (common in Maldives)
                if (IsNearFoliage(position))
                {
                    coverBonus += ISLAND_COVER_BONUS;
                }
                
                // Water concealment (diving underwater)
                if (position.y < -1.5f)
                {
                    coverBonus += WATER_COVER_BONUS;
                }
                
                // Building shadow (urban areas)
                if (IsInBuildingShadow(position))
                {
                    coverBonus += 0.15f;
                }
                
                return coverBonus;
            }

            private bool IsNearFoliage(float3 position)
            {
                // Check for flora in radius
                // (Would query flora system)
                return Unity.Mathematics.Random.CreateFromIndex((uint)position.GetHashCode()).NextFloat() < 0.3f;
            }

            private bool IsInBuildingShadow(float3 position)
            {
                // Check building proximity
                return position.y < 2f && math.lengthsq(position) < 100f; // Simplified
            }
        }

        [BurstCompile]
        partial struct WantedLevelUpdateJob : IJobEntity
        {
            public ComponentLookup<WantedLevelComponent> WantedLevels;
            public float DeltaTime;
            public float DecayRate;

            void Execute(Entity entity)
            {
                var wanted = WantedLevels[entity];
                if (wanted.HeatLevel <= 0) return;
                
                // Decay based on time and location
                float decayMultiplier = IsOnHomeIsland(entity) ? 1.2f : 1f;
                wanted.HeatLevel = math.max(0, wanted.HeatLevel * math.pow(DecayRate, DeltaTime * decayMultiplier));
                
                // Update wanted level stars
                wanted.Stars = (byte)(wanted.HeatLevel / 0.25f);
                
                WantedLevels[entity] = wanted;
            }

            private bool IsOnHomeIsland(Entity entity)
            {
                // Check if on player's home island (safe zone)
                return SystemAPI.HasComponent<HomeIslandTag>(entity);
            }
        }

        private void ProcessHidingSpots(ref SystemState state)
        {
            // Maldives-specific hiding mechanics
            foreach (var (stealth, transform) in 
                SystemAPI.Query<RefRW<StealthComponent>, RefRO<LocalTransform>>())
            {
                bool isInHidingSpot = false;
                
                // Check if in water (underwater hiding)
                if (transform.ValueRO.Position.y < -2f)
                {
                    isInHidingSpot = true;
                    stealth.ValueRW.VisibilityMultiplier = 0.3f; // 70% hidden
                }
                // Check if in dense foliage (palm grove)
                else if (IsInFoliageCluster(transform.ValueRO.Position))
                {
                    isInHidingSpot = true;
                    stealth.ValueRW.VisibilityMultiplier = 0.4f;
                }
                // Check if in building (private property)
                else if (SystemAPI.HasComponent<InsideBuildingTag>(state.SystemHandle))
                {
                    isInHidingSpot = true;
                    stealth.ValueRW.VisibilityMultiplier = 0.1f; // Very hidden
                }
                
                stealth.ValueRW.IsInHidingSpot = isInHidingSpot;
            }
        }

        private bool IsInFoliageCluster(float3 position)
        {
            // Query flora density
            return Unity.Mathematics.Random.CreateFromIndex((uint)position.GetHashCode()).NextFloat() < 0.4f;
        }
    }

    [BurstCompile]
    public struct StealthComponent : IComponentData
    {
        public float VisibilityMultiplier; // 1.0 = fully visible
        public bool IsInHidingSpot;
        public bool IsDetected;
        public float LastActionTime; // Recent actions increase visibility
    }

    [BurstCompile]
    public struct DetectionComponent : IComponentData
    {
        public Entity TargetEntity;
        public float MaxDetectionRange;
        public float CurrentDetection; // 0 to 1
        public float DetectionSpeed;
    }

    [BurstCompile]
    public struct WantedLevelComponent : IComponentData
    {
        public float HeatLevel; // 0 to 1
        public byte Stars; // 0-4 stars
        public Entity LastCrimeTarget;
        public float TimeSinceLastCrime;
    }

    [BurstCompile]
    public struct HomeIslandTag : IComponentData { }
    [BurstCompile]
    public struct InsideBuildingTag : IComponentData { }
}
