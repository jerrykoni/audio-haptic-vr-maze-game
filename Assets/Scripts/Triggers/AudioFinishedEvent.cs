using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class AudioEventTrigger : MonoBehaviour
{
    private AudioSource audioSource;

    [Tooltip("This event is triggered when the audio clip finishes playing.")]
    public UnityEvent OnAudioFinished;

    // This flag ensures we only start one "listener" coroutine per playback.
    private bool isListening = false;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // This check remains critical. A looping source never "finishes".
        if (audioSource.loop)
        {
            Debug.LogWarning("AudioSource is set to loop. The OnAudioFinished event will never be invoked.", this);
            enabled = false; // Disable the script to prevent unexpected behavior.
        }
    }

    void Update()
    {
        // THE FIX IS HERE:
        // If we are not currently listening, but the audio source IS playing,
        // it means playback was started by "Play On Awake" or another script.
        // We need to start our listener coroutine to catch when it finishes.
        if (!isListening && audioSource.isPlaying)
        {
            isListening = true;
            StartCoroutine(TrackAudioCompletion());
        }
    }

    /// <summary>
    /// Manually starts playing the audio and ensures the event will be triggered on completion.
    /// </summary>
    public void PlayAudioAndTriggerEvent()
    {
        if (enabled == false) return; // Do nothing if disabled (e.g., due to looping).

        // If it's already playing, stop it first to ensure a clean playback from the beginning.
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        audioSource.Play();
        // The Update() method will automatically detect this playback and start the listener.
    }

    private IEnumerator TrackAudioCompletion()
    {
        // Log to confirm the listener has started.
        Debug.Log("Listener started. Waiting for audio to finish.", this.gameObject);

        // Wait until the audio source is no longer playing.
        yield return new WaitUntil(() => !audioSource.isPlaying);

        // Log to confirm the audio has finished.
        Debug.Log("Audio finished. Invoking event.", this.gameObject);

        // Invoke the UnityEvent configured in the Inspector.
        OnAudioFinished.Invoke();

        // Reset the flag so we are ready to listen again for the next playback.
        isListening = false;
    }
}