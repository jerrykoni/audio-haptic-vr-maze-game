using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneAwareAudioPlayer : MonoBehaviour
{
    public AudioSource audioSource;
    public float delayBeforePlaying = 0f; // Optional delay before playing audio

    void Awake()
    {
        // Ensure we don’t play automatically
        audioSource.playOnAwake = false;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Wait one frame so graphics have time to initialize
        StartCoroutine(PlayAfterFrame());
    }

    private IEnumerator PlayAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        audioSource.PlayDelayed(delayBeforePlaying);
    }
}
