// PlayerController.cs - Mobile-Optimized Third-Person Controller for RVA:TAC
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Physics;
using Unity.Physics.Systems;

namespace RAAJJE_VAGU_AUTO
{
    [BurstCompile]
    public struct PlayerMovementData : IComponentData
    {
        public float3 Position;
        public float3 Velocity;
        public float3 Forward;
        public quaternion Rotation;
        public float MoveSpeed;
        public float RunSpeed;
        public float JumpForce;
        public bool IsGrounded;
        public bool IsRunning;
        public bool IsSwimming;
        public bool IsOnBoat;
        public float Health;
        public float Stamina;
        public uint LastPrayerCheckTime;
        public bool IsPrayerRespecting;
    }

    [BurstCompile]
    public struct PlayerInputData : IComponentData
    {
        public float2 MoveInput;
        public float2 LookInput;
        public bool JumpPressed;
        public bool RunPressed;
        public bool ActionPressed;
        public bool AttackPressed;
        public bool IsTouching;
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [BurstCompile]
    public partial struct PlayerControllerSystem : ISystem
    {
        private const float GROUND_CHECK_DISTANCE = 0.15f;
        private const float WATER_CHECK_DISTANCE = 0.5f;
        private const float PRAYER_RESPECT_RADIUS = 50f;
        private const float STAMINA_REGEN_RATE = 5f;
        private const float STAMINA_DEPLETE_RATE = 15f;
        private const float WATER_HEIGHT_THRESHOLD = -0.5f;
        
        private ComponentLookup<PrayerTimeComponent> _prayerLookup;
        private ComponentLookup<PhysicsVelocity> _physicsVelocityLookup;
        private ComponentLookup<PhysicsMass> _physicsMassLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovementData>();
            state.RequireForUpdate<PlayerInputData>();
            state.RequireForUpdate<PrayerTimeComponent>();
            
            _prayerLookup = state.GetComponentLookup<PrayerTimeComponent>(true);
            _physicsVelocityLookup = state.GetComponentLookup<PhysicsVelocity>(false);
            _physicsMassLookup = state.GetComponentLookup<PhysicsMass>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var currentTime = (uint)SystemAPI.Time.ElapsedTime;
            
            _prayerLookup.Update(ref state);
            _physicsVelocityLookup.Update(ref state);
            _physicsMassLookup.Update(ref state);

            // Check for active prayer times
            bool isPrayerTime = false;
            bool isFridayPrayer = false;
            foreach (var prayer in SystemAPI.Query<PrayerTimeComponent>())
            {
                if (prayer.IsCurrentlyActive)
                {
                    isPrayerTime = true;
                    isFridayPrayer = prayer.PrayerType == PrayerType.Friday;
                    break;
                }
            }

            var job = new PlayerMovementJob
            {
                DeltaTime = deltaTime,
                CurrentTime = currentTime,
                IsPrayerTime = isPrayerTime,
                IsFridayPrayer = isFridayPrayer,
                PrayerRadius = PRAYER_RESPECT_RADIUS,
                WaterHeightThreshold = WATER_HEIGHT_THRESHOLD,
                PrayerLookup = _prayerLookup,
                PhysicsVelocityLookup = _physicsVelocityLookup,
                PhysicsMassLookup = _physicsMassLookup
            };
            
            job.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct PlayerMovementJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTime;
            public bool IsPrayerTime;
            public bool IsFridayPrayer;
            public float PrayerRadius;
            public float WaterHeightThreshold;
            
            [ReadOnly] public ComponentLookup<PrayerTimeComponent> PrayerLookup;
            public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            [ReadOnly] public ComponentLookup<PhysicsMass> PhysicsMassLookup;

            void Execute(Entity player, ref PlayerMovementData movement, ref PlayerInputData input, in LocalToWorld transform)
            {
                // Handle prayer time cultural behavior
                if (IsPrayerTime && CurrentTime - movement.LastPrayerCheckTime > 60)
                {
                    CheckPrayerProximity(ref movement, transform.Position);
                    movement.LastPrayerCheckTime = CurrentTime;
                }

                // Ground and water detection
                UpdateGroundedState(ref movement, transform.Position);
                UpdateWaterState(ref movement, transform.Position);

                // Calculate movement direction
                float3 moveDirection = new float3(input.MoveInput.x, 0, input.MoveInput.y);
                float inputMagnitude = math.length(moveDirection);
                
                if (inputMagnitude > 0.01f)
                {
                    moveDirection = math.normalize(moveDirection);
                    moveDirection = math.rotate(transform.Rotation, moveDirection);
                }

                // Apply prayer time slowdown
                float speedMultiplier = movement.IsPrayerRespecting ? 0.4f : 1f;
                if (movement.IsSwimming) speedMultiplier *= 0.3f;
                if (movement.IsOnBoat) speedMultiplier *= 0.8f;

                // Movement based on input
                if (inputMagnitude > 0.1f)
                {
                    HandleMovement(ref movement, ref input, moveDirection, speedMultiplier);
                }

                // Jump handling
                if (input.JumpPressed && movement.IsGrounded && !movement.IsSwimming)
                {
                    HandleJump(ref movement);
                }

                // Stamina management
                UpdateStamina(ref movement, inputMagnitude);

                // Apply physics movement
                ApplyPhysicsMovement(player, ref movement, transform.Position);
            }

            private void CheckPrayerProximity(ref PlayerMovementData movement, float3 position)
            {
                foreach (var prayer in PrayerLookup)
                {
                    if (prayer.IsCurrentlyActive)
                    {
                        float distance = math.distance(position, prayer.Location);
                        if (distance < PrayerRadius)
                        {
                            movement.IsPrayerRespecting = true;
                            return;
                        }
                    }
                }
                movement.IsPrayerRespecting = false;
            }

            private void UpdateGroundedState(ref PlayerMovementData movement, float3 position)
            {
                float3 rayStart = position + new float3(0, 0.1f, 0);
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, GROUND_CHECK_DISTANCE))
                {
                    movement.IsGrounded = true;
                    movement.IsSwimming = false;
                    
                    // Check if standing on boat
                    if (hit.collider.CompareTag("Boat"))
                        movement.IsOnBoat = true;
                    else
                        movement.IsOnBoat = false;
                }
                else
                {
                    movement.IsGrounded = false;
                    movement.IsOnBoat = false;
                }
            }

            private void UpdateWaterState(ref PlayerMovementData movement, float3 position)
            {
                if (position.y < WaterHeightThreshold)
                {
                    movement.IsSwimming = true;
                    movement.IsGrounded = false;
                }
            }

            private void HandleMovement(ref PlayerMovementData movement, ref PlayerInputData input, 
                                      float3 moveDirection, float speedMultiplier)
            {
                float targetSpeed = input.RunPressed && movement.Stamina > 20f ? 
                                    movement.RunSpeed : movement.MoveSpeed;
                
                float3 targetVelocity = moveDirection * targetSpeed * speedMultiplier;
                
                // Smooth acceleration
                movement.Velocity = math.lerp(movement.Velocity, targetVelocity, DeltaTime * 8f);
                movement.IsRunning = input.RunPressed;

                // Update rotation
                if (math.length(moveDirection) > 0.01f)
                {
                    quaternion targetRotation = quaternion.LookRotation(moveDirection, math.up());
                    movement.Rotation = math.slerp(movement.Rotation, targetRotation, DeltaTime * 10f);
                }
            }

            private void HandleJump(ref PlayerMovementData movement)
            {
                movement.Velocity.y = movement.JumpForce;
                movement.Stamina -= 10f;
            }

            private void UpdateStamina(ref PlayerMovementData movement, float inputMagnitude)
            {
                if (movement.IsRunning && inputMagnitude > 0.1f)
                    movement.Stamina -= STAMINA_DEPLETE_RATE * DeltaTime;
                else
                    movement.Stamina += STAMINA_REGEN_RATE * DeltaTime;
                
                movement.Stamina = math.clamp(movement.Stamina, 0f, 100f);
            }

            private void ApplyPhysicsMovement(Entity player, ref PlayerMovementData movement, float3 currentPosition)
            {
                if (PhysicsVelocityLookup.HasComponent(player) && PhysicsMassLookup.HasComponent(player))
                {
                    var velocity = PhysicsVelocityLookup[player];
                    var mass = PhysicsMassLookup[player];
                    
                    velocity.Linear = movement.Velocity;
                    PhysicsVelocityLookup[player] = velocity;
                }
            }
        }
    }

    // MonoBehaviour wrapper for traditional components
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float runSpeed = 8f;
        [SerializeField] private float jumpForce = 6f;
        [SerializeField] private float maxHealth = 100f;
        
        private EntityManager _entityManager;
        private Entity _playerEntity;

        void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            var entity = _entityManager.CreateEntity();
            _playerEntity = entity;
            
            _entityManager.AddComponentData(entity, new PlayerMovementData
            {
                Position = transform.position,
                MoveSpeed = moveSpeed,
                RunSpeed = runSpeed,
                JumpForce = jumpForce,
                Health = maxHealth,
                Stamina = 100f,
                LastPrayerCheckTime = 0,
                IsPrayerRespecting = false
            });
            
            _entityManager.AddComponentData(entity, new PlayerInputData());
            
            // Add physics components
            _entityManager.AddComponentData(entity, new PhysicsVelocity());
            _entityManager.AddComponentData(entity, new PhysicsMass { InverseMass = 1f });
        }

        void Update()
        {
            if (_entityManager.Exists(_playerEntity))
            {
                var movement = _entityManager.GetComponentData<PlayerMovementData>(_playerEntity);
                transform.position = movement.Position;
                transform.rotation = movement.Rotation;
            }
        }

        void OnDestroy()
        {
            if (_entityManager != null && _entityManager.Exists(_playerEntity))
            {
                _entityManager.DestroyEntity(_playerEntity);
            }
        }
    }
}
