using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DelayedAudioPlayer : MonoBehaviour
{
    [Tooltip("If true, the audio can only be played once.")]
    [SerializeField] private bool playOnlyOnce = false;

    private AudioSource audioSource;
    private bool hasPlayed = false;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Pre-loads the audio data into memory to prevent lag on first playback.
    /// Call this during a loading screen or at a time when a small lag spike is acceptable.
    /// </summary>
    public void WarmUpAudio()
    {
        if (audioSource != null && audioSource.clip != null && audioSource.clip.loadState != AudioDataLoadState.Loaded)
        {
            audioSource.clip.LoadAudioData();
        }
    }

    /// <summary>
    /// Unloads the audio data from memory.
    /// Call this when the audio is no longer needed to free up resources.
    /// </summary>
    public void CoolDownAudio()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.clip.UnloadAudioData();
        }
    }

    /// <summary>
    /// Plays the audio source after a specified delay, only if it is not already playing.
    /// If playOnlyOnce is true, it will only play the first time this method is called.
    /// </summary>
    /// <param name="delayBeforePlaying">The delay in seconds before the audio will play.</param>
    public void PlayWithDelay(float delayBeforePlaying)
    {
        if (playOnlyOnce && hasPlayed)
        {
            return;
        }

        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.PlayDelayed(delayBeforePlaying);
            if (playOnlyOnce)
            {
                hasPlayed = true;
            }
        }
    }
}