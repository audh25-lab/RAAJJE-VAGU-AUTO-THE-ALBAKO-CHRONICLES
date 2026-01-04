using UnityEngine;
using System.Collections;

/// <summary>
/// AudioSynthesizer is a simplified system for creating variations of existing audio clips.
/// It applies procedural modifications like pitch and volume randomization to a base sound,
/// adding auditory diversity to the game. This refactored version uses object pooling for performance.
/// </summary>
public class AudioSynthesizer : MonoBehaviour
{
    public static AudioSynthesizer Instance;

    [Header("Variation Parameters")]
    [Tooltip("The range of random pitch shift to apply (e.g., 0.1 means +/- 10%).")]
    [SerializeField] private float pitchVariation = 0.1f;
    [Tooltip("The range of random volume variation to apply.")]
    [SerializeField] private float volumeVariation = 0.1f;

    [Header("Pooling")]
    [Tooltip("The tag for the AudioSource pool in the PoolingSystem.")]
    [SerializeField] private string audioSourcePoolTag = "AudioSource";

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Plays a variation of a base audio clip at a specific position using a pooled AudioSource.
    /// </summary>
    /// <param name="clip">The base AudioClip to play.</param>
    /// <param name="position">The world position to play the sound at.</param>
    public void PlaySoundVariation(AudioClip clip, Vector3 position)
    {
        if (clip == null) return;

        // Get an AudioSource from the pool
        GameObject audioObject = PoolingSystem.Instance.SpawnFromPool(audioSourcePoolTag, position, Quaternion.identity);
        if (audioObject == null) return;

        AudioSource audioSource = audioObject.GetComponent<AudioSource>();
        if (audioSource == null) return;

        // Apply random pitch and volume
        audioSource.pitch = 1.0f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.volume = (1.0f + Random.Range(-volumeVariation, volumeVariation)) * AudioManager.Instance.sfxVolume;

        // Play the clip
        audioSource.PlayOneShot(clip);

        // Return the object to the pool after the clip has finished playing
        StartCoroutine(ReturnToPoolAfterClip(audioObject, clip.length));
    }

    private IEnumerator ReturnToPoolAfterClip(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        PoolingSystem.Instance.Despawn(audioSourcePoolTag, obj);
    }
}
