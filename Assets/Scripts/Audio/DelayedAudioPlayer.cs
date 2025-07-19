using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DelayedAudioPlayer : MonoBehaviour
{
    private AudioSource audioSource;

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
    /// </summary>
    /// <param name="delayBeforePlaying">The delay in seconds before the audio will play.</param>
    public void PlayWithDelay(float delayBeforePlaying)
    {
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.PlayDelayed(delayBeforePlaying);
        }
    }
}