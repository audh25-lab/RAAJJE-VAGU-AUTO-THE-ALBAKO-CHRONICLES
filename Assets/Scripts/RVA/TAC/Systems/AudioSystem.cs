using UnityEngine;
using System.Collections.Generic;
using RVA.TAC.Cultural;

namespace RVA.TAC.Audio
{
    /// <summary>
    /// High-level audio controller for game-wide soundscape management
    /// </summary>
    public class AudioSystem : MonoBehaviour
    {
        #region State Management
        public enum AudioState
        {
            Normal,
            PrayerMode,
            Combat,
            Stealth,
            Dialogue
        }
        
        private AudioState currentState = AudioState.Normal;
        private AudioState previousState = AudioState.Normal;
        
        private Dictionary<AudioState, float> stateVolumes = new Dictionary<AudioState, float>();
        #endregion

        #region Spatial Audio
        [Header("Maldives Environment Audio")]
        public AudioClip morningAmbient;      // 5:30 AM - peaceful ocean
        public AudioClip middayAmbient;       // 12:00 PM - bustling islands
        public AudioClip eveningAmbient;      // 6:00 PM - sunset atmosphere
        public AudioClip nightAmbient;        // 8:00 PM+ - tranquil night
        public AudioClip monsoonAmbient;      // WeatherSystem integration
        
        private AudioSource ambientSource;
        private AudioSource musicSource;
        #endregion

        #region Cultural Integration
        private PrayerTimeSystem prayerSystem;
        private TimeSystem timeSystem;
        private WeatherSystem weatherSystem;
        
        private bool isMonsoonActive = false;
        private float environmentAudioTimer = 0f;
        private const float ENVIRONMENT_CHECK_INTERVAL = 30f; // Check every 30 seconds
        #endregion

        #region Accessibility
        public bool visualSoundIndicators = true;
        public GameObject soundIndicatorPrefab;
        private Queue<GameObject> indicatorPool = new Queue<GameObject>();
        #endregion

        private void Awake()
        {
            SetupAudioLayers();
            InitializeStateVolumes();
        }

        private void Start()
        {
            // Get system references
            prayerSystem = PrayerTimeSystem.Instance;
            timeSystem = TimeSystem.Instance;
            weatherSystem = WeatherSystem.Instance;
            
            if (prayerSystem != null)
                prayerSystem.OnPrayerTimeStart += HandlePrayerTimeAudio;
                
            if (weatherSystem != null)
                weatherSystem.OnMonsoonStart += HandleMonsoonAudioStart;
                weatherSystem.OnMonsoonEnd += HandleMonsoonAudioEnd;
        }

        private void SetupAudioLayers()
        {
            // Ambient layer
            GameObject ambientObj = new GameObject("AmbientLayer");
            ambientObj.transform.parent = transform;
            ambientSource = ambientObj.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.spatialBlend = 0.0f;
            ambientSource.volume = 0.6f;
            
            // Music layer
            GameObject musicObj = new GameObject("MusicLayer");
            musicObj.transform.parent = transform;
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.spatialBlend = 0.0f;
            musicSource.volume = 0.5f;
        }

        private void InitializeStateVolumes()
        {
            stateVolumes[AudioState.Normal] = 1.0f;
            stateVolumes[AudioState.PrayerMode] = 0.3f;
            stateVolumes[AudioState.Combat] = 1.2f;
            stateVolumes[AudioState.Stealth] = 0.4f;
            stateVolumes[AudioState.Dialogue] = 0.5f;
        }

        private void Update()
        {
            environmentAudioTimer += Time.deltaTime;
            if (environmentAudioTimer >= ENVIRONMENT_CHECK_INTERVAL)
            {
                environmentAudioTimer = 0f;
                UpdateDynamicAudioScape();
            }
            
            UpdateAudioStates();
        }

        /// <summary>
        /// Dynamically adjust ambient audio based on time, weather, and location
        /// </summary>
        private void UpdateDynamicAudioScape()
        {
            if (ambientSource == null) return;
            
            AudioClip targetClip = DetermineAmbientClip();
            
            if (ambientSource.clip != targetClip)
            {
                float fadeTime = 3.0f;
                StartCoroutine(CrossfadeAmbient(targetClip, fadeTime));
            }
        }

        private AudioClip DetermineAmbientClip()
        {
            if (isMonsoonActive && monsoonAmbient != null)
                return monsoonAmbient;
            
            if (timeSystem == null) return morningAmbient;
            
            float currentHour = timeSystem.GetCurrentHour();
            
            return currentHour switch
            {
                >= 5 and < 10 => morningAmbient,
                >= 10 and < 17 => middayAmbient,
                >= 17 and < 20 => eveningAmbient,
                _ => nightAmbient
            };
        }

        private System.Collections.IEnumerator CrossfadeAmbient(AudioClip newClip, float duration)
        {
            float elapsed = 0f;
            float startVolume = ambientSource.volume;
            
            // Fade out
            while (elapsed < duration / 2)
            {
                ambientSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (duration / 2));
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            ambientSource.clip = newClip;
            ambientSource.Play();
            
            // Fade in
            elapsed = 0f;
            while (elapsed < duration / 2)
            {
                ambientSource.volume = Mathf.Lerp(0f, startVolume, elapsed / (duration / 2));
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            ambientSource.volume = startVolume;
        }

        /// <summary>
        /// State-based audio mixing for gameplay contexts
        /// </summary>
        private void UpdateAudioStates()
        {
            float targetMasterVolume = stateVolumes[currentState];
            AudioListener.volume = Mathf.Lerp(AudioListener.volume, targetMasterVolume, Time.deltaTime * 2f);
        }

        public void SetAudioState(AudioState newState)
        {
            if (currentState == newState) return;
            
            previousState = currentState;
            currentState = newState;
            
            Debug.Log($"[AudioSystem] State transition: {previousState} -> {currentState}");
            
            // State-specific behaviors
            switch (newState)
            {
                case AudioState.PrayerMode:
                    HandlePrayerModeEnter();
                    break;
                case AudioState.Combat:
                    HandleCombatEnter();
                    break;
                case AudioState.Stealth:
                    HandleStealthEnter();
                    break;
                case AudioState.Dialogue:
                    HandleDialogueEnter();
                    break;
                default:
                    HandleNormalEnter();
                    break;
            }
        }

        private void HandlePrayerModeEnter()
        {
            // Already handled by PrayerTimeSystem event
        }

        private void HandleCombatEnter()
        {
            // Intensify music, reduce ambient
            if (musicSource != null)
            {
                musicSource.volume = 0.7f;
                // Could trigger combat music track
            }
            
            if (ambientSource != null)
                ambientSource.volume = 0.3f;
        }

        private void HandleStealthEnter()
        {
            // Muffle sounds, increase footstep importance
            AudioListener.volume = 0.4f;
        }

        private void HandleDialogueEnter()
        {
            // Duck non-dialogue audio
            AudioListener.volume = 0.5f;
        }

        private void HandleNormalEnter()
        {
            // Restore all volumes
            if (musicSource != null) musicSource.volume = 0.5f;
            if (ambientSource != null) ambientSource.volume = 0.6f;
            AudioListener.volume = 1.0f;
        }

        private void HandlePrayerTimeAudio(PrayerTimeSystem.PrayerType prayer)
        {
            SetAudioState(AudioState.PrayerMode);
            AudioManager.Instance?.PlayPrayerCall(prayer);
        }

        private void HandleMonsoonAudioStart()
        {
            isMonsoonActive = true;
            environmentAudioTimer = ENVIRONMENT_CHECK_INTERVAL; // Force immediate update
        }

        private void HandleMonsoonAudioEnd()
        {
            isMonsoonActive = false;
            environmentAudioTimer = ENVIRONMENT_CHECK_INTERVAL;
        }

        /// <summary>
        /// Create visual indicator for important sounds (accessibility)
        /// </summary>
        public void ShowSoundIndicator(Vector3 position, string soundType)
        {
            if (!visualSoundIndicators) return;
            
            GameObject indicator = GetIndicatorFromPool();
            indicator.transform.position = position + Vector3.up * 2f;
            indicator.SetActive(true);
            
            var indicatorComp = indicator.GetComponent<SoundIndicator>();
            if (indicatorComp != null)
                indicatorComp.Initialize(soundType);
        }

        private GameObject GetIndicatorFromPool()
        {
            if (indicatorPool.Count > 0)
                return indicatorPool.Dequeue();
            
            return Instantiate(soundIndicatorPrefab);
        }

        public void ReturnIndicatorToPool(GameObject indicator)
        {
            indicator.SetActive(false);
            indicatorPool.Enqueue(indicator);
        }

        #region Public API
        public AudioState GetCurrentAudioState() => currentState;
        
        public void RevertToPreviousState()
        {
            SetAudioState(previousState);
        }
        
        public void SetVisualIndicators(bool enabled)
        {
            visualSoundIndicators = enabled;
        }
        #endregion
    }

    /// <summary>
    /// Helper component for sound indicators
    /// </summary>
    public class SoundIndicator : MonoBehaviour
    {
        public float lifetime = 2.0f;
        private float elapsedTime;
        
        public void Initialize(string soundType)
        {
            // Could set different sprites/colors based on sound type
            elapsedTime = 0f;
        }
        
        private void Update()
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= lifetime)
            {
                AudioSystem.Instance?.ReturnIndicatorToPool(gameObject);
            }
            
            // Float animation
            transform.position += Vector3.up * 0.5f * Time.deltaTime;
        }
    }
}
