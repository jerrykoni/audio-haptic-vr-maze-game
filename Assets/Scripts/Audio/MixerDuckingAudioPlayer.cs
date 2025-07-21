// MixerDuckingAudioPlayer.cs

using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class MixerDuckingAudioPlayer : MonoBehaviour
{
    [Tooltip("If true, this clip only plays once.")]
    [SerializeField] private bool playOnlyOnce = false;

    [Header("Ducking Snapshots")]
    [Tooltip("Snapshot with normal volumes")]
    [SerializeField] private AudioMixerSnapshot normalSnapshot;

    [Tooltip("Snapshot with target ducked volumes")]
    [SerializeField] private AudioMixerSnapshot duckSnapshot;

    [Tooltip("Time (seconds) to fade into duck snapshot")]
    [SerializeField] private float duckTransitionTime = 0.1f;

    [Tooltip("Time (seconds) to return to normal snapshot")]
    [SerializeField] private float returnTransitionTime = 0.5f;

    private AudioSource audioSource;
    private bool hasPlayed = false;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Plays the clip after the given delay, ducks other mixer groups,
    /// then restores them when playback finishes.
    /// </summary>
    /// <param name="delayBeforePlaying">Seconds to wait before playing.</param>
    public void PlayWithDelay(float delayBeforePlaying)
    {
        if (playOnlyOnce && hasPlayed) return;
        if (audioSource.isPlaying) return;

        StartCoroutine(PlayAndDuck(delayBeforePlaying));

        if (playOnlyOnce)
            hasPlayed = true;
    }

    private IEnumerator PlayAndDuck(float delay)
    {
        // Transition mixer into ducked state
        duckSnapshot.TransitionTo(duckTransitionTime);

        // Play audio after the specified delay
        audioSource.PlayDelayed(delay);

        // Wait out delay + clip length
        float clipLength = audioSource.clip?.length ?? 0f;
        yield return new WaitForSeconds(delay + clipLength);

        // Return mixer to normal
        normalSnapshot.TransitionTo(returnTransitionTime);
    }
}
