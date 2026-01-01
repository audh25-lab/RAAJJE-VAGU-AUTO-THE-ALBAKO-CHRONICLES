// CameraSystem.cs - Third-Person Camera System for RVA:TAC
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
    public struct CameraData : IComponentData
    {
        public float3 TargetPosition;
        public float3 CurrentPosition;
        public quaternion Rotation;
        public float Distance;
        public float HeightOffset;
        public float TargetZoom;
        public float CurrentZoom;
        public float2 LookInput;
        public float Yaw;
        public float Pitch;
        public float3 LocalLookDir;
        public bool IsOccluded;
        public float3 OcclusionAdjustedPos;
        public float ShakeIntensity;
        public float ShakeDecay;
        public bool IsProning;
        public bool IsInVehicle;
        public Entity FollowedEntity;
        public float BlendSpeed;
        public bool UseSmoothing;
    }

    [BurstCompile]
    public struct CameraObstacleData : IComponentData
    {
        public Entity ObstacleEntity;
        public float3 ObstaclePosition;
        public float ObstacleRadius;
        public bool IsDynamic;
    }

    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [BurstCompile]
    public partial struct CameraSystem : ISystem
    {
        private const float MIN_PITCH = -30f;
        private const float MAX_PITCH = 60f;
        private const float MIN_DISTANCE = 2f;
        private const float MAX_DISTANCE = 15f;
        private const float OCCLUSION_BUFFER = 0.5f;
        private const float PRAYER_ZOOM_ADJUSTMENT = 1.3f;
        
        private PhysicsWorld _physicsWorld;
        private CollisionFilter _occlusionFilter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CameraData>();
            state.RequireForUpdate<PlayerMovementData>();
            
            _physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            _occlusionFilter = new CollisionFilter
            {
                BelongsTo = (uint)CollisionLayer.Camera,
                CollidesWith = (uint)(CollisionLayer.Environment | CollisionLayer.Building)
            };
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var cameraJob = new CameraUpdateJob
            {
                DeltaTime = deltaTime,
                PhysicsWorld = _physicsWorld,
                OcclusionFilter = _occlusionFilter,
                MinPitch = MIN_PITCH,
                MaxPitch = MAX_PITCH,
                MinDistance = MIN_DISTANCE,
                MaxDistance = MAX_DISTANCE,
                OcclusionBuffer = OCCLUSION_BUFFER,
                PrayerZoomAdjustment = PRAYER_ZOOM_ADJUSTMENT
            };
            
            cameraJob.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct CameraUpdateJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            public CollisionFilter OcclusionFilter;
            public float MinPitch;
            public float MaxPitch;
            public float MinDistance;
            public float MaxDistance;
            public float OcclusionBuffer;
            public float PrayerZoomAdjustment;

            void Execute(ref CameraData camera, ref PlayerMovementData player, in PlayerInputData input)
            {
                // Check prayer time for zoom adjustment
                bool isPrayerTime = CheckPrayerTimeActive();
                if (isPrayerTime && camera.TargetZoom < 1f)
                {
                    camera.TargetZoom = math.lerp(camera.TargetZoom, PRAYER_ZOOM_ADJUSTMENT, DeltaTime * 2f);
                }

                // Calculate target rotation from input
                camera.Yaw += input.LookInput.x * 2f;
                camera.Pitch = math.clamp(camera.Pitch - input.LookInput.y, MinPitch, MaxPitch);
                
                camera.Rotation = quaternion.Euler(math.radians(camera.Pitch), math.radians(camera.Yaw), 0);

                // Calculate ideal camera position
                float3 targetPosition = player.Position + new float3(0, camera.HeightOffset, 0);
                float3 lookDir = math.mul(camera.Rotation, new float3(0, 0, -1));
                lookDir = math.normalize(lookDir);
                
                float adjustedDistance = camera.Distance * camera.TargetZoom;
                float3 idealCameraPos = targetPosition - (lookDir * adjustedDistance);

                // Occlusion detection (critical for dense Maldivian cities)
                camera.IsOccluded = false;
                camera.OcclusionAdjustedPos = idealCameraPos;

                RaycastInput rayInput = new RaycastInput
                {
                    Start = targetPosition,
                    End = idealCameraPos,
                    Filter = OcclusionFilter
                };

                if (PhysicsWorld.CastRay(rayInput, out Unity.Physics.RaycastHit hit))
                {
                    camera.IsOccluded = true;
                    float hitDistance = math.distance(targetPosition, hit.Position);
                    camera.OcclusionAdjustedPos = targetPosition - (lookDir * (hitDistance - OcclusionBuffer));
                }

                // Smooth camera movement
                camera.CurrentPosition = math.lerp(camera.CurrentPosition, camera.OcclusionAdjustedPos, 
                                                  DeltaTime * camera.BlendSpeed);

                // Camera shake (for explosions, etc.)
                if (camera.ShakeIntensity > 0.001f)
                {
                    float3 shakeOffset = new float3(
                        Unity.Mathematics.noise.snoise(camera.CurrentPosition * 10f) * camera.ShakeIntensity,
                        Unity.Mathematics.noise.snoise(camera.CurrentPosition * 7f) * camera.ShakeIntensity,
                        Unity.Mathematics.noise.snoise(camera.CurrentPosition * 13f) * camera.ShakeIntensity
                    );
                    camera.CurrentPosition += shakeOffset;
                    camera.ShakeIntensity *= camera.ShakeDecay;
                }

                camera.TargetPosition = targetPosition;
            }

            bool CheckPrayerTimeActive()
            {
                // Check if any prayer time is active
                foreach (var prayer in SystemAPI.Query<PrayerTimeComponent>())
                {
                    if (prayer.IsCurrentlyActive) return true;
                }
                return false;
            }
        }
    }

    public class CameraSystemMonobehaviour : MonoBehaviour
    {
        [Header("Camera Settings")]
        public Camera MainCamera;
        public Transform CameraTarget;
        public float FollowDistance = 8f;
        public float HeightOffset = 2f;
        public float RotationSpeed = 2f;
        public float ZoomSpeed = 5f;
        
        [Header("Mobile Optimization")]
        public bool UseDynamicResolution = true;
        public int TargetFPS = 30;
        public float OcclusionCheckInterval = 0.1f;
        
        [Header("Visual Style")]
        public bool UsePixelArtRendering = true;
        public Material PixelArtPostProcessMaterial;

        private EntityManager _entityManager;
        private Entity _cameraEntity;
        private float _lastOcclusionCheck;
        private RenderTexture _pixelArtRT;

        void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            // Create camera entity
            _cameraEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(_cameraEntity, new CameraData
            {
                Distance = FollowDistance,
                HeightOffset = HeightOffset,
                TargetZoom = 1f,
                CurrentZoom = 1f,
                Yaw = 0,
                Pitch = 20f,
                BlendSpeed = 5f,
                UseSmoothing = true,
                ShakeIntensity = 0,
                ShakeDecay = 0.9f,
                FollowedEntity = Entity.Null,
                IsInVehicle = false
            });
            
            SetupPixelArtRendering();
            SetupDynamicResolution();
        }

        void SetupPixelArtRendering()
        {
            if (!UsePixelArtRendering || !Application.isMobilePlatform) return;
            
            // Create render target for pixel art effect
            int width = Screen.width > 1920 ? 1920 : Screen.width;
            int height = Screen.height > 1080 ? 1080 : Screen.height;
            
            _pixelArtRT = new RenderTexture(width, height, 24);
            _pixelArtRT.filterMode = FilterMode.Point;
            _pixelArtRT.antiAliasing = 1;
            
            MainCamera.targetTexture = _pixelArtRT;
        }

        void SetupDynamicResolution()
        {
            if (!UseDynamicResolution) return;
            
            // Enable dynamic resolution scaling for performance
            QualitySettings.SetQualityLevel(1); // Medium quality
            Application.targetFrameRate = TargetFPS;
        }

        void Update()
        {
            if (!_entityManager.Exists(_cameraEntity) || CameraTarget == null) return;

            var cameraData = _entityManager.GetComponentData<CameraData>(_cameraEntity);
            
            // Update camera transform
            MainCamera.transform.position = cameraData.CurrentPosition;
            MainCamera.transform.rotation = cameraData.Rotation;

            // Update target entity
            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerMovementData>()
                .Build();
            
            if (!playerQuery.IsEmpty)
            {
                var playerEntity = playerQuery.GetSingletonEntity();
                cameraData.FollowedEntity = playerEntity;
                _entityManager.SetComponentData(_cameraEntity, cameraData);
            }

            // Handle occlusion checks
            if (Time.time - _lastOcclusionCheck > OcclusionCheckInterval)
            {
                PerformOcclusionCheck();
                _lastOcclusionCheck = Time.time;
            }

            // Apply pixel art post-processing
            if (UsePixelArtRendering && _pixelArtRT != null)
            {
                ApplyPixelArtEffect();
            }
        }

        void PerformOcclusionCheck()
        {
            var cameraData = _entityManager.GetComponentData<CameraData>(_cameraEntity);
            
            if (cameraData.IsOccluded)
            {
                // Reduce draw distance when occluded
                MainCamera.farClipPlane = 30f;
            }
            else
            {
                MainCamera.farClipPlane = 100f;
            }
        }

        void ApplyPixelArtEffect()
        {
            if (PixelArtPostProcessMaterial == null) return;
            
            // Render to screen with pixel art shader
            Graphics.Blit(_pixelArtRT, null, PixelArtPostProcessMaterial);
        }

        public void TriggerCameraShake(float intensity, float duration)
        {
            if (!_entityManager.Exists(_cameraEntity)) return;
            
            var cameraData = _entityManager.GetComponentData<CameraData>(_cameraEntity);
            cameraData.ShakeIntensity = intensity;
            cameraData.ShakeDecay = math.pow(0.001f, 1f / (duration * 60f)); // Decay over duration
            _entityManager.SetComponentData(_cameraEntity, cameraData);
        }

        public void SetVehicleCamera(bool isInVehicle, float targetZoom = 1f)
        {
            if (!_entityManager.Exists(_cameraEntity)) return;
            
            var cameraData = _entityManager.GetComponentData<CameraData>(_cameraEntity);
            cameraData.IsInVehicle = isInVehicle;
            cameraData.TargetZoom = targetZoom;
            cameraData.HeightOffset = isInVehicle ? 1.5f : 2f;
            _entityManager.SetComponentData(_cameraEntity, cameraData);
        }

        void OnDrawGizmos()
        {
            if (CameraTarget == null) return;
            
            var cameraData = _entityManager.Exists(_cameraEntity) ? 
                _entityManager.GetComponentData<CameraData>(_cameraEntity) : default;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(CameraTarget.position, 0.5f);
            
            if (cameraData.IsOccluded)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(cameraData.TargetPosition, cameraData.OcclusionAdjustedPos);
            }
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(cameraData.TargetPosition, cameraData.CurrentPosition);
            }
        }

        void OnDestroy()
        {
            if (_pixelArtRT != null)
            {
                _pixelArtRT.Release();
            }
        }
    }

    // Pixel Art Post-Process Shader (matching GTA 1/2 visual style)
    public class PixelArtPostProcess : MonoBehaviour
    {
        [SerializeField] private Material postProcessMaterial;
        
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (postProcessMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }
            
            // Set pixel art parameters
            postProcessMaterial.SetFloat("_PixelSize", 4f);
            postProcessMaterial.SetFloat("_Brightness", 1.1f);
            postProcessMaterial.SetFloat("_Contrast", 1.3f);
            postProcessMaterial.SetColor("_ColorTint", new Color(1f, 0.96f, 0.85f)); // Warm tropical tint
            
            Graphics.Blit(source, destination, postProcessMaterial);
        }
    }
}
