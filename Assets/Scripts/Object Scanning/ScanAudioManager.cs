// Audio manager for handling all scan-related audio
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Audio manager for handling all scan-related audio
public class ScanAudioManager : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource spatialAudioSource;
    public AudioSource uiAudioSource; // For non-spatial feedback

    [Header("Predefined Audio Clips")]
    public AudioClip hoverSound;
    [Range(0f, 1f)]
    public float hoverSoundVolume = 1f;
    [Range(0.1f, 3f)]
    public float hoverSoundPitch = 1f;

    public AudioClip hoverStaySound;
    [Range(0f, 1f)]
    public float hoverStaySoundVolume = 1f;
    [Range(0.1f, 3f)]
    public float hoverStaySoundPitch = 1f;

    public AudioClip unhoverSound;
    [Range(0f, 1f)]
    public float unhoverSoundVolume = 1f;
    [Range(0.1f, 3f)]
    public float unhoverSoundPitch = 1f;

    [Header("Object Audio Mapping")]
    public ObjectAudioMapping[] objectAudioMappings;

    [Header("Audio Timing")]
    public float hoverToNameDelay = 0.2f;
    public float minTimeBetweenScans = 0.1f;

    [Header("TTS Settings")]
    public bool useTTSFallback = true;
    public float ttsVolume = 0.8f;

    // Internal state
    private Dictionary<string, AudioClip> audioClipMap;
    private GameObject lastScannedObject;
    private GameObject currentDetectedObject;
    private string lastScannedTag;
    private float lastScanTime;
    private Coroutine currentAudioSequence;
    private Coroutine hoverStayCoroutine;
    private bool isPlayingNameAudio;
    private bool isObjectCurrentlyDetected;

    // Events for extensibility
    public UnityEvent<string> OnTTSRequested;
    public UnityEvent<GameObject> OnObjectScanned;
    public UnityEvent<GameObject> OnObjectDetectedHaptics;
    public UnityEvent<GameObject> OnObjectLostHaptics;

    [System.Serializable]
    public class ObjectAudioMapping
    {
        public string objectTag;
        public AudioClip audioClip;
    }

    void Awake()
    {
        InitializeAudioSystem();
    }

    void InitializeAudioSystem()
    {
        // Create audio clip dictionary
        audioClipMap = new Dictionary<string, AudioClip>();
        foreach (var mapping in objectAudioMappings)
        {
            if (!string.IsNullOrEmpty(mapping.objectTag) && mapping.audioClip != null)
            {
                audioClipMap[mapping.objectTag] = mapping.audioClip;
            }
        }

        // Setup audio sources if not assigned
        if (spatialAudioSource == null)
        {
            spatialAudioSource = GetComponent<AudioSource>();
            if (spatialAudioSource == null)
            {
                spatialAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (uiAudioSource == null)
        {
            // Create a separate audio source for UI sounds
            GameObject uiAudioObject = new GameObject("UI Audio Source");
            uiAudioObject.transform.SetParent(transform);
            uiAudioSource = uiAudioObject.AddComponent<AudioSource>();
        }

        // Configure spatial audio source
        spatialAudioSource.spatialBlend = 1f; // Full 3D
        spatialAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        spatialAudioSource.minDistance = 1f;
        spatialAudioSource.maxDistance = 20f;

        // Configure UI audio source
        uiAudioSource.spatialBlend = 1f; // Also 3D positioned
        uiAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        uiAudioSource.minDistance = 0.5f;
        uiAudioSource.maxDistance = 10f;

        // Initialize events
        if (OnTTSRequested == null) OnTTSRequested = new UnityEvent<string>();
        if (OnObjectScanned == null) OnObjectScanned = new UnityEvent<GameObject>();
        if (OnObjectDetectedHaptics == null) OnObjectDetectedHaptics = new UnityEvent<GameObject>();
        if (OnObjectLostHaptics == null) OnObjectLostHaptics = new UnityEvent<GameObject>();
    }

    public void OnObjectDetected(GameObject detectedObject)
    {
        if (detectedObject == null) return;

        // Rate limiting
        if (Time.time - lastScanTime < minTimeBetweenScans) return;

        string objectTag = detectedObject.tag;
        bool isNewObject = detectedObject != lastScannedObject;

        currentDetectedObject = detectedObject;
        isObjectCurrentlyDetected = true;

        // Always play hover sound when object is detected
        PlayHoverSound(detectedObject);

        if (isNewObject)
        {
            HandleNewObjectDetected(detectedObject, objectTag);
        }

        // Start or continue hover stay sound
        if (hoverStayCoroutine == null)
        {
            hoverStayCoroutine = StartCoroutine(PlayHoverStaySound(detectedObject));
        }

        lastScannedObject = detectedObject;
        lastScannedTag = objectTag;
        lastScanTime = Time.time;

        OnObjectScanned.Invoke(detectedObject);
        OnObjectDetectedHaptics.Invoke(detectedObject);
    }

    public void OnObjectLost(GameObject lostObject)
    {
        if (lostObject == currentDetectedObject)
        {
            isObjectCurrentlyDetected = false;
            currentDetectedObject = null;

            // Stop hover stay sound
            if (hoverStayCoroutine != null)
            {
                StopCoroutine(hoverStayCoroutine);
                hoverStayCoroutine = null;
            }

            // Stop UI audio source if it's playing hover stay sound
            if (uiAudioSource.isPlaying && uiAudioSource.clip == hoverStaySound)
            {
                uiAudioSource.Stop();
            }

            // Play unhover sound
            PlayUnhoverSound(lostObject);

            OnObjectLostHaptics.Invoke(lostObject);
        }
    }

    void HandleNewObjectDetected(GameObject obj, string tag)
    {
        // Stop any current audio sequence
        if (currentAudioSequence != null)
        {
            StopCoroutine(currentAudioSequence);
        }

        // Start new audio sequence
        currentAudioSequence = StartCoroutine(PlayNewObjectAudioSequence(obj, tag));
    }

    void PlayHoverSound(GameObject obj)
    {
        if (hoverSound != null)
        {
            // Position UI audio source at object location
            uiAudioSource.transform.position = obj.transform.position;

            // Apply volume and pitch settings
            float originalVolume = uiAudioSource.volume;
            float originalPitch = uiAudioSource.pitch;

            uiAudioSource.volume = hoverSoundVolume;
            uiAudioSource.pitch = hoverSoundPitch;
            uiAudioSource.PlayOneShot(hoverSound);

            // Restore original settings
            uiAudioSource.volume = originalVolume;
            uiAudioSource.pitch = originalPitch;
        }
    }

    void PlayUnhoverSound(GameObject obj)
    {
        if (unhoverSound != null)
        {
            // Position UI audio source at object location
            uiAudioSource.transform.position = obj.transform.position;

            // Apply volume and pitch settings
            float originalVolume = uiAudioSource.volume;
            float originalPitch = uiAudioSource.pitch;

            uiAudioSource.volume = unhoverSoundVolume;
            uiAudioSource.pitch = unhoverSoundPitch;
            uiAudioSource.PlayOneShot(unhoverSound);

            // Restore original settings
            uiAudioSource.volume = originalVolume;
            uiAudioSource.pitch = originalPitch;
        }
    }

    IEnumerator PlayHoverStaySound(GameObject obj)
    {
        if (hoverStaySound == null) yield break;

        // Store original audio source settings
        float originalVolume = uiAudioSource.volume;
        float originalPitch = uiAudioSource.pitch;

        while (isObjectCurrentlyDetected && currentDetectedObject == obj)
        {
            // Position UI audio source at object location
            uiAudioSource.transform.position = obj.transform.position;

            // Play hover stay sound if not already playing
            if (!uiAudioSource.isPlaying || uiAudioSource.clip != hoverStaySound)
            {
                uiAudioSource.clip = hoverStaySound;
                uiAudioSource.volume = hoverStaySoundVolume;
                uiAudioSource.pitch = hoverStaySoundPitch;
                uiAudioSource.loop = true;
                uiAudioSource.Play();
            }

            // Update position continuously
            yield return new WaitForSeconds(0.1f);
        }

        // Stop the sound when object is no longer detected
        if (uiAudioSource.isPlaying && uiAudioSource.clip == hoverStaySound)
        {
            uiAudioSource.Stop();
        }

        // Restore original audio source settings
        uiAudioSource.volume = originalVolume;
        uiAudioSource.pitch = originalPitch;
    }

    IEnumerator PlayNewObjectAudioSequence(GameObject obj, string tag)
    {
        // Wait for the delay (hover sound already played)
        yield return new WaitForSeconds(hoverToNameDelay);

        // Play object name audio
        yield return StartCoroutine(PlayObjectNameAudio(obj, tag));

        currentAudioSequence = null;
    }

    IEnumerator PlayObjectNameAudio(GameObject obj, string tag)
    {
        isPlayingNameAudio = true;

        // Position the spatial audio source at the object's location
        spatialAudioSource.transform.position = obj.transform.position;

        // Try to play predefined audio clip
        if (audioClipMap.ContainsKey(tag) && audioClipMap[tag] != null)
        {
            AudioClip clip = audioClipMap[tag];
            spatialAudioSource.clip = clip;
            spatialAudioSource.Play();

            // Wait for the clip to finish
            yield return new WaitForSeconds(clip.length);
        }
        else if (useTTSFallback)
        {
            // Request TTS for missing audio clips
            OnTTSRequested.Invoke(tag);

            // Wait a bit for TTS to potentially play
            yield return new WaitForSeconds(1f);
        }
        else
        {
            Debug.LogWarning($"No audio clip found for tag: {tag} and TTS is disabled");
        }

        isPlayingNameAudio = false;
    }

    void PlayUISound(AudioClip clip, GameObject targetObject = null)
    {
        if (clip != null && uiAudioSource != null)
        {
            // Position at target object if provided
            if (targetObject != null)
            {
                uiAudioSource.transform.position = targetObject.transform.position;
            }
            uiAudioSource.PlayOneShot(clip);
        }
    }

    // Public method for TTS systems to call when they finish playing
    public void OnTTSPlaybackComplete()
    {
        isPlayingNameAudio = false;
    }

    // Public method for TTS systems to indicate they're starting
    public void OnTTSPlaybackStarted()
    {
        isPlayingNameAudio = true;
    }

    // Method to add/update audio mappings at runtime
    public void UpdateAudioMapping(string tag, AudioClip clip)
    {
        if (audioClipMap == null) InitializeAudioSystem();
        audioClipMap[tag] = clip;
    }

    // Method to remove audio mappings
    public void RemoveAudioMapping(string tag)
    {
        if (audioClipMap != null && audioClipMap.ContainsKey(tag))
        {
            audioClipMap.Remove(tag);
        }
    }

    // Get all available audio tags
    public string[] GetAvailableAudioTags()
    {
        if (audioClipMap == null) return new string[0];

        string[] tags = new string[audioClipMap.Keys.Count];
        audioClipMap.Keys.CopyTo(tags, 0);
        return tags;
    }

    // Method to test specific audio clips
    public void TestAudioClip(string tag)
    {
        if (audioClipMap.ContainsKey(tag) && audioClipMap[tag] != null)
        {
            spatialAudioSource.transform.position = transform.position;
            spatialAudioSource.PlayOneShot(audioClipMap[tag]);
        }
        else
        {
            Debug.LogWarning($"No audio clip found for tag: {tag}");
        }
    }

    void OnDestroy()
    {
        if (currentAudioSequence != null)
        {
            StopCoroutine(currentAudioSequence);
        }

        if (hoverStayCoroutine != null)
        {
            StopCoroutine(hoverStayCoroutine);
        }
    }
}