using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

// Represents a single note in a Boduberu song with cultural accuracy
[System.Serializable]
public class BoduberuNote
{
    [Tooltip("Time in seconds from the start of the song")]
    [Min(0f)]
    public float time;
    
    [Tooltip("Drum track: 0=Lead(Bodu), 1=Mid(Bodu), 2=Backing(Odi), 3=Bell(Onugandu)")]
    [Range(0, 3)]
    public int track;
    
    [Tooltip("Traditional Boduberu pattern marker for cultural verification")]
    public string patternGroup; // e.g., "JEHI", "FURIMANI"
}

// ScriptableObject to define a culturally authentic Boduberu song
[CreateAssetMenu(fileName = "New Boduberu Song", menuName = "RVA/Cultural/Boduberu Song")]
public class BoduberuSongData : ScriptableObject
{
    [Header("Cultural Metadata (Required for RVACULT verification)")]
    public string songName;
    [TextArea] public string culturalDescription; // For community review
    public string islandOfOrigin; // Real Maldivian island
    
    [Header("Audio & Timing")]
    public AudioClip songAudio;
    [Min(60)] public float bpm = 120f; // Boduberu typically 120-140 BPM
    
    [Header("Rhythm Pattern")]
    public List<BoduberuNote> notes;
    
    [Tooltip("Traditional pattern type for cultural validation")]
    public PatternType traditionalPattern;
    
    public enum PatternType { JEHI, FURIMANI, THAARAA, MODERN_FUSION }
    
    private void OnValidate()
    {
        // RVAQA-PERF: Ensure list is sorted for O(1) access during gameplay
        if (notes != null && notes.Count > 1)
        {
            notes.Sort((a, b) => a.time.CompareTo(b.time));
        }
    }
}

// Main system with cultural integration and mobile resilience
public class BoduberuSystem : MonoBehaviour
{
    public static BoduberuSystem Instance { get; private set; }

    // Events with cultural context
    public event Action<int, HitQuality> OnNoteHit; // track + quality
    public event Action<int> OnNoteMiss;
    public event Action OnGameStart;
    public event Action<BoduberuResults> OnGameEnd;
    
    [Header("Cultural Configuration")]
    public BoduberuSongData currentSong;
    [Tooltip("Maldivian standard: ±100ms acceptable window")]
    [Range(0.05f, 0.2f)] public float goodHitThreshold = 0.1f;
    [Tooltip("Master performance: ±50ms perfect window")]
    [Range(0.02f, 0.1f)] public float perfectHitThreshold = 0.05f;
    
    [Header("Mobile & Accessibility")]
    public bool pauseDuringPrayer = true; // CRITICAL: Integrates with PrayerTimeSystem
    public bool autoPlayForAccessibility = false;
    [Tooltip("Visual/audio cues for motor disabilities")]
    public bool enhancedFeedbackMode = false;
    
    // State management
    private AudioSource audioSource;
    private float songStartTime;
    private int nextNoteIndex = 0;
    private bool isPlaying = false;
    private bool isPaused = false;
    
    // Scoring with Maldivian theme
    private int score = 0;
    private int combo = 0;
    private int perfectHits = 0;
    private int totalNotesHit = 0;
    
    // Performance monitoring (RVAQA-PERF)
    private const float MAX_FRAME_TIME = 0.033f; // 30fps = 33ms
    private float lastUpdateTime;
    
    public enum HitQuality { PERFECT, GOOD, MISS }
    
    public struct BoduberuResults
    {
        public int finalScore;
        public int maxCombo;
        public int perfectHitCount;
        public int totalNotes;
        public float accuracy;
        public SongRating rating;
        
        public enum SongRating { ADHIVEHITHE = 0, RAHMATHEE = 1000, MIHAARU = 5000, GOYYA = 10000, BODUBERU_MASTER = 20000 }
    }
    
    private void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad for cultural mini-games
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // RVAQA-MOBILE: Setup audio for call interruption handling
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        
        // Verify audio settings for mobile
        Application.focusChanged += OnAppFocusChanged;
        
        Assert.IsNotNull(audioSource, "AudioSource initialization failed");
    }
    
    private void OnDestroy()
    {
        Application.focusChanged -= OnAppFocusChanged;
    }
    
    private void OnAppFocusChanged(bool hasFocus)
    {
        // RVAIMPL-MOBILE: Handle call interruptions
        if (!hasFocus && isPlaying && !isPaused)
        {
            PauseGame();
        }
    }
    
    public void StartGame(BoduberuSongData song)
    {
        // RVAIMPL-COMP: Comprehensive validation
        if (song == null)
        {
            Debug.LogError("[RVACULT] No Boduberu song data provided. Cultural integrity check failed.");
            return;
        }
        
        if (song.songAudio == null)
        {
            Debug.LogError($"[RVACULT] Song '{song.songName}' missing audio clip.");
            return;
        }
        
        if (song.notes == null || song.notes.Count == 0)
        {
            Debug.LogWarning($"[RVACULT] Song '{song.songName}' has no notes pattern. Traditional authenticity compromised.");
        }
        
        // Validate track indices (Boduberu uses 4 instruments max)
        foreach (var note in song.notes)
        {
            if (note.track < 0 || note.track > 3)
            {
                Debug.LogError($"[RVACULT] Invalid track index {note.track} in '{song.songName}'. Must be 0-3 for traditional ensemble.");
                return;
            }
        }
        
        currentSong = song;
        isPlaying = true;
        isPaused = false;
        nextNoteIndex = 0;
        score = 0;
        combo = 0;
        perfectHits = 0;
        totalNotesHit = 0;
        
        // RVAQA-PERF: Pre-warm audio to avoid lag spike
        audioSource.clip = currentSong.songAudio;
        audioSource.Prepare();
        
        audioSource.Play();
        songStartTime = Time.time - audioSource.time; // Account for any play delay
        
        OnGameStart?.Invoke();
        Debug.Log($"[RVACULT] Starting authentic Boduberu mini-game: {currentSong.songName} from {currentSong.islandOfOrigin}");
        
        // Register with PrayerTimeSystem if available
        if (PrayerTimeSystem.Instance != null && pauseDuringPrayer)
        {
            PrayerTimeSystem.Instance.OnPrayerTimeStart += PauseDuringPrayer;
            PrayerTimeSystem.Instance.OnPrayerTimeEnd += ResumeAfterPrayer;
        }
    }
    
    private void Update()
    {
        if (!isPlaying || isPaused) return;
        
        // RVAQA-PERF: Frame time monitoring
        if (Time.realtimeSinceStartup - lastUpdateTime > MAX_FRAME_TIME)
        {
            Debug.LogWarning($"[RVAQA-PERF] BoduberuSystem frame time exceeded: {(Time.realtimeSinceStartup - lastUpdateTime) * 1000:F2}ms");
        }
        lastUpdateTime = Time.realtimeSinceStartup;
        
        float currentTime = Time.time - songStartTime;
        
        // Check for missed notes with O(1) complexity
        while (nextNoteIndex < currentSong.notes.Count && 
               currentTime > currentSong.notes[nextNoteIndex].time + goodHitThreshold)
        {
            HandleMiss(currentSong.notes[nextNoteIndex].track);
            nextNoteIndex++;
        }
        
        // Check for game end with null safety
        if (audioSource.clip != null && currentTime >= audioSource.clip.length)
        {
            EndGame();
        }
    }
    
    // Called by InputSystem with accessibility support
    public void OnPlayerInput(int track)
    {
        if (!isPlaying || isPaused) return;
        
        // Validate track
        if (track < 0 || track > 3)
        {
            Debug.LogWarning($"[RVAIMPL] Invalid track input: {track}. Must be 0-3.");
            return;
        }
        
        float currentTime = Time.time - songStartTime;
        bool hitProcessed = false;
        
        // O(n) but only scans forward from current index
        // Handles simultaneous notes on different tracks (chords)
        for (int i = nextNoteIndex; i < currentSong.notes.Count && !hitProcessed; i++)
        {
            var note = currentSong.notes[i];
            if (note.track == track)
            {
                float timeDiff = Mathf.Abs(currentTime - note.time);
                
                if (timeDiff <= goodHitThreshold)
                {
                    HandleHit(timeDiff, track);
                    nextNoteIndex = i + 1;
                    hitProcessed = true;
                }
                else if (note.time > currentTime + goodHitThreshold)
                {
                    // Future note, stop scanning
                    break;
                }
            }
        }
        
        // Accessibility: Provide feedback even on miss
        if (!hitProcessed && enhancedFeedbackMode)
        {
            OnNoteMiss?.Invoke(track);
            Debug.Log($"[RVA-ACCESS] Input on track {track} - no note in window (accessibility feedback)");
        }
    }
    
    private void HandleHit(float timeDifference, int track)
    {
        totalNotesHit++;
        combo++;
        
        HitQuality quality = timeDifference <= perfectHitThreshold ? HitQuality.PERFECT : HitQuality.GOOD;
        
        if (quality == HitQuality.PERFECT)
        {
            perfectHits++;
            score += 100 * combo;
        }
        else
        {
            score += 50 * combo;
        }
        
        OnNoteHit?.Invoke(track, quality);
        
        // RVACULT: Log with traditional terminology
        string hitType = quality == HitQuality.PERFECT ? "Gohli" : "Thaavee";
        Debug.Log($"[RVACULT] {hitType} hit on {GetTrackName(track)}! Score: {score} | Combo: {combo}");
    }
    
    private void HandleMiss(int track)
    {
        combo = 0;
        OnNoteMiss?.Invoke(track);
        Debug.Log($"[RVACULT] Missed note on {GetTrackName(track)} - traditional rhythm broken");
    }
    
    private string GetTrackName(int track) => track switch
    {
        0 => "Bodu Beru (Lead Drum)",
        1 => "Bodu Beru (Mid)",
        2 => "Odi (Backing)",
        3 => "Onugandu (Bell)",
        _ => "Unknown"
    };
    
    private void PauseDuringPrayer()
    {
        if (isPlaying && !isPaused)
        {
            PauseGame();
            Debug.Log("[RVACULT] Boduberu paused for prayer time - cultural respect protocol");
        }
    }
    
    private void ResumeAfterPrayer()
    {
        if (isPlaying && isPaused)
        {
            ResumeGame();
            Debug.Log("[RVACULT] Boduberu resumed after prayer");
        }
    }
    
    public void PauseGame()
    {
        if (!isPlaying || isPaused) return;
        
        isPaused = true;
        audioSource.Pause();
        Time.timeScale = 0f; // Pause gameplay but not UI
        
        Debug.Log("[RVAIMPL-MOBILE] Game paused");
    }
    
    public void ResumeGame()
    {
        if (!isPlaying || !isPaused) return;
        
        isPaused = false;
        audioSource.UnPause();
        Time.timeScale = 1f;
        
        // Recalculate start time to account for pause duration
        songStartTime = Time.time - audioSource.time;
    }
    
    private void EndGame()
    {
        isPlaying = false;
        isPaused = false;
        Time.timeScale = 1f;
        
        // Calculate results
        var results = new BoduberuResults
        {
            finalScore = score,
            maxCombo = combo,
            perfectHitCount = perfectHits,
            totalNotes = currentSong?.notes?.Count ?? 0,
            accuracy = (currentSong?.notes?.Count ?? 0) > 0 ? 
                (float)totalNotesHit / currentSong.notes.Count : 0f
        };
        
        // Determine rating
        results.rating = score switch
        {
            >= 20000 => BoduberuResults.SongRating.BODUBERU_MASTER,
            >= 10000 => BoduberuResults.SongRating.GOYYA,
            >= 5000 => BoduberuResults.SongRating.MIHAARU,
            >= 1000 => BoduberuResults.SongRating.RAHMATHEE,
            _ => BoduberuResults.SongRating.ADHIVEHITHE
        };
        
        OnGameEnd?.Invoke(results);
        
        // Save high score via SaveSystem
        if (SaveSystem.Instance != null && currentSong != null)
        {
            string key = $"Boduberu_HighScore_{currentSong.name}_{currentSong.bpm}";
            int currentHigh = SaveSystem.Instance.GetPlayerPrefInt(key, 0);
            if (score > currentHigh)
            {
                SaveSystem.Instance.SetPlayerPrefInt(key, score, true);
                Debug.Log($"[RVAIMPL] New high score saved: {score}");
            }
        }
        
        // Unregister prayer callbacks
        if (PrayerTimeSystem.Instance != null)
        {
            PrayerTimeSystem.Instance.OnPrayerTimeStart -= PauseDuringPrayer;
            PrayerTimeSystem.Instance.OnPrayerTimeEnd -= ResumeAfterPrayer;
        }
        
        Debug.Log($"[RVACULT] Boduberu performance complete. Rating: {results.rating} | Final Score: {score}");
    }
    
    // RVAQA-COMPAT: Cleanup for scene transitions
    public void ForceStop()
    {
        isPlaying = false;
        isPaused = false;
        Time.timeScale = 1f;
        
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
        
        // Unregister all callbacks to prevent memory leaks
        if (PrayerTimeSystem.Instance != null)
        {
            PrayerTimeSystem.Instance.OnPrayerTimeStart -= PauseDuringPrayer;
            PrayerTimeSystem.Instance.OnPrayerTimeEnd -= ResumeAfterPrayer;
        }
        
        Debug.Log("[RVAIMPL] BoduberuSystem force stopped");
    }
}
