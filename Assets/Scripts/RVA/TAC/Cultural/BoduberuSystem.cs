using UnityEngine;
using System;
using System.Collections.Generic;

// Represents a single note in a Boduberu song.
[System.Serializable]
public class BoduberuNote
{
    public float time; // Time in seconds from the start of the song.
    public int track; // Which drum/track this note is for (e.g., 0 for left, 1 for middle, 2 for right).
}

// ScriptableObject to define a Boduberu song.
[CreateAssetMenu(fileName = "New Boduberu Song", menuName = "RVA/Boduberu Song")]
public class BoduberuSongData : ScriptableObject
{
    public string songName;
    public AudioClip songAudio;
    public float bpm;
    public List<BoduberuNote> notes;
}

public class BoduberuSystem : MonoBehaviour
{
    public static BoduberuSystem Instance { get; private set; }

    public event Action<int> OnNoteHit;
    public event Action<int> OnNoteMiss;
    public event Action OnGameStart;
    public event Action OnGameEnd;

    [Header("Game Configuration")]
    public BoduberuSongData currentSong;
    public float goodHitThreshold = 0.1f; // Time window for a good hit.
    public float perfectHitThreshold = 0.05f; // Time window for a perfect hit.

    private AudioSource audioSource;
    private float songStartTime;
    private int nextNoteIndex = 0;
    private bool isPlaying = false;

    private int score = 0;
    private int combo = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public void StartGame(BoduberuSongData song)
    {
        if (song == null)
        {
            Debug.LogError("No Boduberu song data provided.");
            return;
        }

        currentSong = song;
        isPlaying = true;
        nextNoteIndex = 0;
        score = 0;
        combo = 0;

        audioSource.clip = currentSong.songAudio;
        audioSource.Play();
        songStartTime = Time.time;

        OnGameStart?.Invoke();
        Debug.Log($"Starting Boduberu mini-game with song: {currentSong.songName}");
    }

    private void Update()
    {
        if (!isPlaying) return;

        float currentTime = Time.time - songStartTime;

        // Check for missed notes
        if (nextNoteIndex < currentSong.notes.Count &&
            currentTime > currentSong.notes[nextNoteIndex].time + goodHitThreshold)
        {
            HandleMiss(currentSong.notes[nextNoteIndex].track);
            nextNoteIndex++;
        }

        // Check for game end
        if (currentTime >= audioSource.clip.length)
        {
            EndGame();
        }
    }

    // This is called by the player's input script.
    public void OnPlayerInput(int track)
    {
        if (!isPlaying) return;

        float currentTime = Time.time - songStartTime;

        // Find the closest note to the current time on the given track
        for (int i = nextNoteIndex; i < currentSong.notes.Count; i++)
        {
            if (currentSong.notes[i].track == track)
            {
                float timeDifference = Mathf.Abs(currentTime - currentSong.notes[i].time);

                if (timeDifference <= goodHitThreshold)
                {
                    HandleHit(timeDifference, track);
                    // We "consume" the note by advancing the index past it,
                    // assuming one note per track at a time. A more complex system might be needed for chords.
                    nextNoteIndex = i + 1;
                    return;
                }
            }
        }
    }

    private void HandleHit(float timeDifference, int track)
    {
        combo++;
        if (timeDifference <= perfectHitThreshold)
        {
            score += 100 * combo;
            Debug.Log($"Perfect Hit on track {track}! Score: {score}");
        }
        else
        {
            score += 50 * combo;
            Debug.Log($"Good Hit on track {track}! Score: {score}");
        }
        OnNoteHit?.Invoke(track);
    }

    private void HandleMiss(int track)
    {
        combo = 0;
        Debug.Log($"Missed note on track {track}.");
        OnNoteMiss?.Invoke(track);
    }

    private void EndGame()
    {
        isPlaying = false;
        Debug.Log($"Boduberu game finished. Final Score: {score}");
        OnGameEnd?.Invoke();
    }
}
