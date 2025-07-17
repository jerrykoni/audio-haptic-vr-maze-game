// Audio manager for handling all scan-related audio
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Audio manager for handling all scan-related audio
public class ScanAudioManager : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource nameAudioSource;
    public AudioSource hoverAudioSource;
    public AudioSource hoverStayAudioSource;

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
    //private Coroutine hoverStayCoroutine;
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
        if (nameAudioSource == null)
        {
            nameAudioSource = GetComponent<AudioSource>();
            if (nameAudioSource == null)
            {
                nameAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (hoverAudioSource == null)
        {
            // Create a separate audio source for UI sounds
            GameObject uiAudioObject = new GameObject("UI Audio Source");
            uiAudioObject.transform.SetParent(transform);
            hoverAudioSource = uiAudioObject.AddComponent<AudioSource>();
        }

        if (hoverStayAudioSource == null)
        {
            Debug.LogWarning("Hover Stay Audio Source is not assigned.");
        }

        hoverStayAudioSource.loop = false;
        hoverStayAudioSource.playOnAwake = false;
        hoverStayAudioSource.spatialBlend = 1f;
        hoverStayAudioSource.clip = hoverStaySound;

        hoverAudioSource.loop = false;
        hoverAudioSource.playOnAwake = false;

        nameAudioSource.loop = false;
        nameAudioSource.playOnAwake = false;

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
        //if (hoverStayCoroutine == null)
        //{
        //    hoverStayCoroutine = StartCoroutine(PlayHoverStaySound(detectedObject));
        //}

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

            //// Stop hover stay sound
            //if (hoverStayCoroutine != null)
            //{
            //    StopCoroutine(hoverStayCoroutine);
            //    hoverStayCoroutine = null;
            //}

            //// Stop hover audio source if it's playing hover stay sound
            //if (hoverStayAudioSource.isPlaying)
            //{
            //    hoverStayAudioSource.Stop();
            //}

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
            hoverAudioSource.transform.position = obj.transform.position;

            // Apply volume and pitch settings
            float originalVolume = hoverAudioSource.volume;
            float originalPitch = hoverAudioSource.pitch;

            hoverAudioSource.volume = hoverSoundVolume;
            hoverAudioSource.pitch = hoverSoundPitch;
            hoverAudioSource.PlayOneShot(hoverSound);

            // Restore original settings
            hoverAudioSource.volume = originalVolume;
            hoverAudioSource.pitch = originalPitch;
        }
    }

    void PlayUnhoverSound(GameObject obj)
    {
        if (unhoverSound != null)
        {
            // Position UI audio source at object location
            hoverAudioSource.transform.position = obj.transform.position;

            // Apply volume and pitch settings
            float originalVolume = hoverAudioSource.volume;
            float originalPitch = hoverAudioSource.pitch;

            hoverAudioSource.volume = unhoverSoundVolume;
            hoverAudioSource.pitch = unhoverSoundPitch;
            hoverAudioSource.PlayOneShot(unhoverSound);

            // Restore original settings
            hoverAudioSource.volume = originalVolume;
            hoverAudioSource.pitch = originalPitch;
        }
    }

    IEnumerator PlayHoverStaySound(GameObject obj)
    {
        if (hoverStaySound == null)
        {
            Debug.LogWarning("Hover Stay Sound is not assigned.");
            yield break;
        }

        while (isObjectCurrentlyDetected && currentDetectedObject == obj)
        {
            // Position UI audio source at object location
            hoverStayAudioSource.transform.position = obj.transform.position;

            // Play hover stay sound if not already playing
            if (!hoverStayAudioSource.isPlaying)
            {
                //hoverStayAudioSource.volume = hoverStaySoundVolume;
                hoverStayAudioSource.pitch = hoverStaySoundPitch;
                hoverStayAudioSource.Play();
            }

            // Update position continuously
            yield return new WaitForSeconds(0.1f);
        }

        // Stop the sound when object is no longer detected
        if (hoverStayAudioSource.isPlaying)
        {
            hoverStayAudioSource.Stop();
        }
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
        nameAudioSource.transform.position = obj.transform.position;

        // Try to play predefined audio clip
        if (audioClipMap.ContainsKey(tag) && audioClipMap[tag] != null)
        {
            AudioClip clip = audioClipMap[tag];
            nameAudioSource.clip = clip;
            nameAudioSource.Play();

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
        if (clip != null && hoverAudioSource != null)
        {
            // Position at target object if provided
            if (targetObject != null)
            {
                hoverAudioSource.transform.position = targetObject.transform.position;
            }
            hoverAudioSource.PlayOneShot(clip);
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
            nameAudioSource.transform.position = transform.position;
            nameAudioSource.PlayOneShot(audioClipMap[tag]);
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

        //if (hoverStayCoroutine != null)
        //{
        //    StopCoroutine(hoverStayCoroutine);
        //}
    }
}