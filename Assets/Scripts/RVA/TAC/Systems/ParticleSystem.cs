using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

namespace RVA.TAC.VFX
{
    /// <summary>
    /// Mobile-optimized particle system for tropical Maldives effects
    /// </summary>
    [BurstCompile]
    public class ParticleSystem : MonoBehaviour
    {
        #region Particle Pools
        [System.Serializable]
        public class ParticlePool
        {
            public string effectName;
            public GameObject prefab;
            public int poolSize = 20;
            public bool useGPUInstancing = true;
            
            private Queue<GameObject> availableParticles = new Queue<GameObject>();
            private List<GameObject> activeParticles = new List<GameObject>();
            
            public void Initialize(Transform parent)
            {
                for (int i = 0; i < poolSize; i++)
                {
                    var obj = GameObject.Instantiate(prefab, parent);
                    obj.SetActive(false);
                    availableParticles.Enqueue(obj);
                }
            }
            
            public GameObject GetParticle(Vector3 position, Quaternion rotation)
            {
                if (availableParticles.Count == 0)
                {
                    // Expand pool if needed
                    var obj = GameObject.Instantiate(prefab, parent);
                    obj.SetActive(false);
                    availableParticles.Enqueue(obj);
                }
                
                var particle = availableParticles.Dequeue();
                particle.transform.position = position;
                particle.transform.rotation = rotation;
                particle.SetActive(true);
                activeParticles.Add(particle);
                
                return particle;
            }
            
            public void ReturnParticle(GameObject particle)
            {
                particle.SetActive(false);
                activeParticles.Remove(particle);
                availableParticles.Enqueue(particle);
            }
            
            public void ClearActive()
            {
                foreach (var particle in activeParticles)
                {
                    particle.SetActive(false);
                    availableParticles.Enqueue(particle);
                }
                activeParticles.Clear();
            }
        }
        
        public ParticlePool oceanSprayPool;
        public ParticlePool monsoonRainPool;
        public ParticlePool tropicalFloraPollenPool;
        public ParticlePool fishingSplashPool;
        public ParticlePool boatWakePool;
        #endregion

        #region Mobile Optimization
        [Header("Mobile Performance")]
        public int maxTotalParticles = 200; // Mali-G72 limit
        public bool useGPUInstancing = true;
        public bool useBurstJobs = true;
        public float particleCullDistance = 50f;
        public int targetFrameRate = 30;
        
        private NativeArray<float3> particlePositions;
        private NativeArray<float> particleLifetimes;
        private JobHandle particleUpdateHandle;
        #endregion

        #region Maldives-Specific Effects
        [Header("Maldives Environment")]
        public float oceanSprayHeight = 2f;
        public float monsoonIntensity = 0f;
        public Vector3 windDirection = Vector3.forward;
        public float windStrength = 1f;
        
        private WeatherSystem weatherSystem;
        private OceanSystem oceanSystem;
        #endregion

        #region Active Particles Tracking
        private struct ActiveParticle
        {
            public GameObject gameObject;
            public float lifetime;
            public float maxLifetime;
            public bool isPooled;
        }
        
        private List<ActiveParticle> activeParticles = new List<ActiveParticle>();
        private Queue<ActiveParticle> particlesToRemove = new Queue<ActiveParticle>();
        #endregion

        private void Awake()
        {
            InitializePools();
            SetupNativeArrays();
        }

        private void Start()
        {
            weatherSystem = WeatherSystem.Instance;
            oceanSystem = OceanSystem.Instance;
            
            if (weatherSystem != null)
            {
                weatherSystem.OnMonsoonStart += () => SetMonsoonIntensity(1f);
                weatherSystem.OnMonsoonEnd += () => SetMonsoonIntensity(0f);
            }
        }

        private void InitializePools()
        {
            Transform poolsParent = new GameObject("ParticlePools").transform;
            poolsParent.parent = transform;
            
            oceanSprayPool.Initialize(poolsParent);
            monsoonRainPool.Initialize(poolsParent);
            tropicalFloraPollenPool.Initialize(poolsParent);
            fishingSplashPool.Initialize(poolsParent);
            boatWakePool.Initialize(poolsParent);
        }

        private void SetupNativeArrays()
        {
            particlePositions = new NativeArray<float3>(maxTotalParticles, Allocator.Persistent);
            particleLifetimes = new NativeArray<float>(maxTotalParticles, Allocator.Persistent);
        }

        private void Update()
        {
            UpdateActiveParticles();
            
            if (useBurstJobs && activeParticles.Count > 10)
            {
                UpdateParticlesWithJobs();
            }
            else
            {
                UpdateParticlesStandard();
            }
            
            // Cleanup
            ProcessRemovalQueue();
        }

        private void UpdateActiveParticles()
        {
            for (int i = 0; i < activeParticles.Count; i++)
            {
                var particle = activeParticles[i];
                particle.lifetime -= Time.deltaTime;
                
                if (particle.lifetime <= 0)
                {
                    particlesToRemove.Enqueue(particle);
                    activeParticles.RemoveAt(i);
                    i--;
                }
                else
                {
                    activeParticles[i] = particle;
                }
            }
        }

        private void UpdateParticlesStandard()
        {
            foreach (var particle in activeParticles)
            {
                if (particle.gameObject == null) continue;
                
                // Apply wind
                var velocity = particle.gameObject.GetComponent<Rigidbody>()?.velocity ?? Vector3.zero;
                velocity += windDirection * windStrength * Time.deltaTime;
                
                // Cull distant particles
                float distance = Vector3.Distance(particle.gameObject.transform.position, Camera.main.transform.position);
                if (distance > particleCullDistance)
                {
                    particle.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateParticlesWithJobs()
        {
            // Prepare data for job
            for (int i = 0; i < math.min(activeParticles.Count, maxTotalParticles); i++)
            {
                particlePositions[i] = activeParticles[i].gameObject.transform.position;
                particleLifetimes[i] = activeParticles[i].lifetime;
            }
            
            // Schedule job
            var updateJob = new ParticleUpdateJob
            {
                positions = particlePositions,
                lifetimes = particleLifetimes,
                deltaTime = Time.deltaTime,
                windDir = new float3(windDirection.x, windDirection.y, windDirection.z),
                windStrength = windStrength,
                particleCount = math.min(activeParticles.Count, maxTotalParticles)
            };
            
            particleUpdateHandle = updateJob.Schedule(math.min(activeParticles.Count, maxTotalParticles), 64);
        }

        private void LateUpdate()
        {
            particleUpdateHandle.Complete();
            
            // Apply job results
            for (int i = 0; i < math.min(activeParticles.Count, maxTotalParticles); i++)
            {
                if (particleLifetimes[i] <= 0) continue;
                
                activeParticles[i].gameObject.transform.position = particlePositions[i];
            }
        }

        private void ProcessRemovalQueue()
        {
            while (particlesToRemove.Count > 0)
            {
                var particle = particlesToRemove.Dequeue();
                
                if (particle.isPooled)
                {
                    ReturnToPool(particle);
                }
                else
                {
                    Destroy(particle.gameObject);
                }
            }
        }

        #region Effect Emitters
        /// <summary>
        /// Emit ocean spray at given position
        /// </summary>
        public void EmitOceanSpray(Vector3 position, int intensity = 5)
        {
            intensity = math.clamp(intensity, 1, 10);
            
            for (int i = 0; i < intensity; i++)
            {
                var offset = UnityEngine.Random.insideUnitCircle * 0.5f;
                var spawnPos = position + new Vector3(offset.x, 0, offset.y);
                
                var particle = oceanSprayPool.GetParticle(spawnPos, Quaternion.identity);
                
                var rb = particle.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = new Vector3(
                        UnityEngine.Random.Range(-2f, 2f),
                        UnityEngine.Random.Range(1f, 3f),
                        UnityEngine.Random.Range(-2f, 2f)
                    );
                }
                
                activeParticles.Add(new ActiveParticle
                {
                    gameObject = particle,
                    lifetime = UnityEngine.Random.Range(1f, 2f),
                    maxLifetime = 2f,
                    isPooled = true
                });
            }
        }

        /// <summary>
        /// Emit monsoon rain particles
        /// </summary>
        public void EmitMonsoonRain(Vector3 center, float radius = 20f)
        {
            if (monsoonIntensity <= 0f) return;
            
            int particleCount = Mathf.RoundToInt(monsoonIntensity * 5);
            
            for (int i 
