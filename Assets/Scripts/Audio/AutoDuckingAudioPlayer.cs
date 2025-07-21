using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class AutoDuckingAudioPlayer : MonoBehaviour
{
    [Header("Ducking Snapshots")]
    [Tooltip("Snapshot with normal volumes")]
    [SerializeField] private AudioMixerSnapshot normalSnapshot;

    [Tooltip("Snapshot with ducked volumes")]
    [SerializeField] private AudioMixerSnapshot duckSnapshot;

    [Tooltip("Time (seconds) to fade into duck snapshot")]
    [SerializeField] private float duckTransitionTime = 0.1f;

    [Tooltip("Time (seconds) to return to normal snapshot")]
    [SerializeField] private float returnTransitionTime = 0.5f;

    private AudioSource _audioSource;
    private bool _wasPlaying;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _wasPlaying = _audioSource.isPlaying;
    }

    void Update()
    {
        // Detect the moment this AudioSource starts playing
        if (_audioSource.isPlaying && !_wasPlaying)
        {
            duckSnapshot.TransitionTo(duckTransitionTime);
            _wasPlaying = true;
        }
        // Detect when it stops, to restore volumes
        else if (!_audioSource.isPlaying && _wasPlaying)
        {
            normalSnapshot.TransitionTo(returnTransitionTime);
            _wasPlaying = false;
        }
    }
}