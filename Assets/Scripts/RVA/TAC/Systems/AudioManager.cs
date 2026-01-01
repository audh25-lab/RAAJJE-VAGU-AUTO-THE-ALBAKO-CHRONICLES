using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

namespace RVA.TAC.Audio
{
    /// <summary>
    /// Low-level audio manager optimized for mobile devices with Maldivian cultural soundscapes
    /// </summary>
    [BurstCompile]
    public class AudioManager : MonoBehaviour
    {
        #region Singleton & Persistence
        private static AudioManager instance;
        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                    Debug.LogError("[AudioManager] Instance not initialized");
                return instance;
            }
        }
        #endregion

        #region Maldivian Audio Constants
        private const int MAX_BODUBERU_DRUMS = 8; // Traditional drum ensemble
        private const int OCEAN_AMBIENT_CHANNELS = 4;
        private const float PRAYER_VOLUME_DUCK = 0.3f;
        private const float FISHING_VILLAGE_ACOUSTICS = 0.85f; // Reverb for wooden structures
        #endregion

        #region Mobile Optimization
        [Header("Mobile Performance")]
        public int maxSimultaneousSounds = 16; // Mali-G72 friendly
        public bool enableDynamicCompression = true;
        public float audioUpdateRate = 0.1f; // 10fps update for non-critical audio
        
        private JobHandle audioUpdateHandle;
        private NativeArray<float> volumeArray;
        private float lastUpdateTime;
        #endregion

        #region Audio Pools
        [System.Serializable]
        public class AudioPool
        {
            public string name;
            public AudioSource[] sources;
            public int currentIndex;
            
            public AudioSource GetNext()
            {
                var source = sources[currentIndex];
                currentIndex = (currentIndex + 1) % sources.Length;
                return source;
            }
        }
        
        public AudioPool sfxPool;
        public AudioPool boduberuPool;
        public AudioPool ambientPool;
        #endregion

        #region Cultural Audio References
        public AudioClip[] prayerAdhanClips; // Fajr, Dhuhr, Asr, Maghrib, Isha
        public AudioClip[] oceanWaveClips;
        public AudioClip[] boduberuRhythmClips;
        public AudioClip fishingReelClip;
        public AudioClip dhoniEngineClip;
        #endregion

        #region Accessibility
        public bool subtitlesEnabled = true;
        public float subtitleDisplayDuration = 3.0f;
        #endregion

        private void Awake()
        {
            #region Singleton Setup
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAudioSystem();
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            #endregion
        }

        private void InitializeAudioSystem()
        {
            // Initialize Native Arrays for Burst jobs
            volumeArray = new NativeArray<float>(4, Allocator.Persistent);
            
            // Setup audio pools for performance
            SetupAudioPool(ref sfxPool, "SFX", maxSimultaneousSounds);
            SetupAudioPool(ref boduberuPool, "Boduberu", MAX_BODUBERU_DRUMS);
            SetupAudioPool(ref ambientPool, "Ambient", OCEAN_AMBIENT_CHANNELS);
            
            // Configure audio settings for mobile
            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.numRealVoices = maxSimultaneousSounds;
            config.numVirtualVoices = maxSimultaneousSounds * 2;
            config.speakerMode = AudioSpeakerMode.Stereo;
            AudioSettings.Reset(config);
            
            Debug.Log($"[AudioManager] Initialized with {maxSimultaneousSounds} voices | Cultural:Enabled");
        }

        private void SetupAudioPool(ref AudioPool pool, string name, int size)
        {
            pool = new AudioPool
            {
                name = name,
                sources = new AudioSource[size],
                currentIndex = 0
            };
            
            GameObject poolObject = new GameObject($"Pool_{name}");
            poolObject.transform.parent = transform;
            
            for (int i = 0; i < size; i++)
            {
                GameObject sourceObj = new GameObject($"Source_{i}");
                sourceObj.transform.parent = poolObject.transform;
                pool.sources[i] = sourceObj.AddComponent<AudioSource>();
                pool.sources[i].playOnAwake = false;
                ConfigureSourceForMobile(pool.sources[i]);
            }
        }

        private void ConfigureSourceForMobile(AudioSource source)
        {
            // Mobile-specific optimizations
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 5f;
            source.maxDistance = 50f;
            source.spatialBlend = 0.8f;
            source.dopplerLevel = 0.2f; // Reduced for performance
        }

        /// <summary>
        /// Play spatial audio with Maldivian acoustic modeling
        /// </summary>
        [BurstCompile]
        public void PlayLocalizedSFX(AudioClip clip, Vector3 position, float volume = 1.0f, 
            AudioContext context = AudioContext.General)
        {
            if (clip == null) return;
            
            var source = sfxPool.GetNext();
            source.clip = clip;
            source.volume = ApplyContextualVolume(volume, context);
            source.transform.position = position;
            
            // Apply island-specific acoustics
            ApplyEnvironmentalAudioEffects(source, position, context);
            
            source.Play();
            
            // Trigger subtitles if enabled
            if (subtitlesEnabled && context != AudioContext.Ambient)
            {
                UIManager.Instance?.ShowSubtitle($"🔊 {clip.name}", subtitleDisplayDuration);
            }
        }

        /// <summary>
        /// Play traditional Boduberu drumming with ensemble simulation
        /// </summary>
        public void PlayBoduberuEnsemble(Vector3 position, int intensity = 3)
        {
            intensity = math.clamp(intensity, 1, MAX_BODUBERU_DRUMS);
            
            for (int i = 0; i < intensity; i++)
            {
                var source = boduberuPool.GetNext();
                var clip = boduberuRhythmClips[UnityEngine.Random.Range(0, boduberuRhythmClips.Length)];
                
                source.clip = clip;
                source.volume = 0.6f + (i * 0.1f); // Layered volume
                source.transform.position = position + UnityEngine.Random.insideUnitSphere * 2f;
                source.pitch = 0.95f + (i * 0.02f); // Slight detune for realism
                
                // Humanize timing
                float delay = i * 0.05f;
                source.PlayScheduled(AudioSettings.dspTime + delay);
            }
            
            if (subtitlesEnabled)
            {
                UIManager.Instance?.ShowSubtitle("🥁 Boduberu drums playing", subtitleDisplayDuration);
            }
        }

        /// <summary>
        /// Play prayer call with appropriate reverence and volume ducking
        /// </summary>
        public void PlayPrayerCall(PrayerTimeSystem.PrayerType prayerType)
        {
            int clipIndex = (int)prayerType;
            if (clipIndex >= prayerAdhanClips.Length) return;
            
            var source = ambientPool.GetNext();
            source.clip = prayerAdhanClips[clipIndex];
            source.volume = 0.8f;
            source.spatialBlend = 0.0f; // 2D for spiritual presence
            
            // Duck all other audio
            StartCoroutine(DuckAudioDuringPrayer(source.clip.length));
            
            source.Play();
            
            if (subtitlesEnabled)
            {
                string prayerName = System.Enum.GetName(typeof(PrayerTimeSystem.PrayerType), prayerType);
                UIManager.Instance?.ShowSubtitle($"🕌 {prayerName} prayer call", source.clip.length + 2f);
            }
        }

        private System.Collections.IEnumerator DuckAudioDuringPrayer(float duration)
        {
            float elapsed = 0f;
            float targetDuck = PRAYER_VOLUME_DUCK;
            
            while (elapsed < duration)
            {
                float duckAmount = Mathf.Lerp(1f, targetDuck, elapsed / 2f); // Gradual duck
                AudioListener.volume = duckAmount;
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Restore
            float restoreElapsed = 0f;
            while (restoreElapsed < 2f)
            {
                AudioListener.volume = Mathf.Lerp(targetDuck, 1f, restoreElapsed / 2f);
                restoreElapsed += Time.deltaTime;
                yield return null;
            }
            
            AudioListener.volume = 1f;
        }

        /// <summary>
        /// Apply environmental audio effects based on Maldives geography
        /// </summary>
        private void ApplyEnvironmentalAudioEffects(AudioSource source, Vector3 position, AudioContext context)
        {
            float height = position.y;
            
            // Underwater muffling
            if (height < -1f)
            {
                source.panStereo = 0f;
                source.spatialBlend = 0.5f;
                return;
            }
            
            // Fishing village reverb
            if (context == AudioContext.FishingVillage)
            {
                source.reverbZoneMix = FISHING_VILLAGE_ACOUSTICS;
            }
            
            // Island wind occlusion
            float windFactor = WeatherSystem.Instance?.GetCurrentWindIntensity() ?? 0f;
            source.spread = Mathf.Lerp(0f, 30f, windFactor);
        }

        private float ApplyContextualVolume(float baseVolume, AudioContext context)
        {
            return context switch
            {
                AudioContext.Prayer => baseVolume * 1.2f,
                AudioContext.Underwater => baseVolume * 0.4f,
                AudioContext.Boduberu => baseVolume * 0.8f,
                _ => baseVolume
            };
        }

        [BurstCompile]
        private struct AudioVolumeJob : IJob
        {
            public NativeArray<float> volumes;
            public float masterVolume;
            
            public void Execute()
            {
                for (int i = 0; i < volumes.Length; i++)
                {
                    volumes[i] = math.clamp(volumes[i] * masterVolume, 0f, 1f);
                }
            }
        }

        private void Update()
        {
            // Throttled audio updates for mobile performance
            if (Time.time - lastUpdateTime < audioUpdateRate) return;
            lastUpdateTime = Time.time;
            
            var job = new AudioVolumeJob
            {
                volumes = volumeArray,
                masterVolume = AudioListener.volume
            };
            
            audioUpdateHandle = job.Schedule();
        }

        private void LateUpdate()
        {
            audioUpdateHandle.Complete();
        }

        private void OnDestroy()
        {
            if (volumeArray.IsCreated)
                volumeArray.Dispose();
        }

        #region Enums
        public enum AudioContext
        {
            General,
            Prayer,
            FishingVillage,
            Underwater,
            Boduberu,
            Ambient
        }
        #endregion
    }
}
