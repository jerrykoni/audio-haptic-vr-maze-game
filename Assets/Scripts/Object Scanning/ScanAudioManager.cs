using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ScanAudioManager : MonoBehaviour
{
    // A static variable shared by ALL instances of this script.
    // This tracks the last object whose name was announced, globally.
    private static GameObject _globallyLastAnnouncedObject;

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

    // Internal state (local to this instance)
    private Dictionary<string, AudioClip> audioClipMap;
    private GameObject lastScannedObject;
    private GameObject currentDetectedObject;
    private string lastScannedTag;
    private float lastScanTime;
    private Coroutine currentAudioSequence;

    // --- REMOVED: The two unused variables ---
    // private bool isPlayingNameAudio;
    // private bool isObjectCurrentlyDetected;

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

    public void OnObjectDetected(GameObject detectedObject)
    {
        if (detectedObject == null) return;
        if (Time.time - lastScanTime < minTimeBetweenScans) return;

        string objectTag = detectedObject.tag;

        bool shouldAnnounceName = (detectedObject != _globallyLastAnnouncedObject);

        currentDetectedObject = detectedObject;

        // --- REMOVED: Assignment to isObjectCurrentlyDetected ---
        // isObjectCurrentlyDetected = true;

        PlayHoverSound(detectedObject);

        if (shouldAnnounceName)
        {
            HandleNewObjectDetected(detectedObject, objectTag);
            _globallyLastAnnouncedObject = detectedObject;
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
            // --- REMOVED: Assignment to isObjectCurrentlyDetected ---
            // isObjectCurrentlyDetected = false;
            currentDetectedObject = null;

            if (hoverStayAudioSource != null && hoverStayAudioSource.isPlaying)
            {
                hoverStayAudioSource.Stop();
            }

            PlayUnhoverSound(lostObject);
            OnObjectLostHaptics.Invoke(lostObject);
        }
    }

    #region Unchanged Methods
    void InitializeAudioSystem()
    {
        audioClipMap = new Dictionary<string, AudioClip>();
        foreach (var mapping in objectAudioMappings)
        {
            if (!string.IsNullOrEmpty(mapping.objectTag) && mapping.audioClip != null)
            {
                audioClipMap[mapping.objectTag] = mapping.audioClip;
            }
        }

        if (nameAudioSource == null)
        {
            nameAudioSource = gameObject.AddComponent<AudioSource>();
        }
        if (hoverAudioSource == null)
        {
            GameObject uiAudioObject = new GameObject("UI Audio Source");
            uiAudioObject.transform.SetParent(transform);
            hoverAudioSource = uiAudioObject.AddComponent<AudioSource>();
        }
        if (hoverStayAudioSource == null)
        {
            Debug.LogWarning("Hover Stay Audio Source is not assigned.");
        }

        if (hoverStayAudioSource != null)
        {
            hoverStayAudioSource.loop = false;
            hoverStayAudioSource.playOnAwake = false;
            hoverStayAudioSource.spatialBlend = 1f;
            hoverStayAudioSource.clip = hoverStaySound;
        }

        hoverAudioSource.loop = false;
        hoverAudioSource.playOnAwake = false;
        nameAudioSource.loop = false;
        nameAudioSource.playOnAwake = false;

        if (OnTTSRequested == null) OnTTSRequested = new UnityEvent<string>();
        if (OnObjectScanned == null) OnObjectScanned = new UnityEvent<GameObject>();
        if (OnObjectDetectedHaptics == null) OnObjectDetectedHaptics = new UnityEvent<GameObject>();
        if (OnObjectLostHaptics == null) OnObjectLostHaptics = new UnityEvent<GameObject>();
    }

    void HandleNewObjectDetected(GameObject obj, string tag)
    {
        if (currentAudioSequence != null)
        {
            StopCoroutine(currentAudioSequence);
        }
        currentAudioSequence = StartCoroutine(PlayNewObjectAudioSequence(obj, tag));
    }

    void PlayHoverSound(GameObject obj)
    {
        if (hoverSound != null)
        {
            hoverAudioSource.transform.position = obj.transform.position;
            hoverAudioSource.volume = hoverSoundVolume;
            hoverAudioSource.pitch = hoverSoundPitch;
            hoverAudioSource.PlayOneShot(hoverSound);
        }
    }

    void PlayUnhoverSound(GameObject obj)
    {
        if (unhoverSound != null)
        {
            hoverAudioSource.transform.position = obj.transform.position;
            hoverAudioSource.volume = unhoverSoundVolume;
            hoverAudioSource.pitch = unhoverSoundPitch;
            hoverAudioSource.PlayOneShot(unhoverSound);
        }
    }

    IEnumerator PlayNewObjectAudioSequence(GameObject obj, string tag)
    {
        yield return new WaitForSeconds(hoverToNameDelay);
        yield return StartCoroutine(PlayObjectNameAudio(obj, tag));
        currentAudioSequence = null;
    }

    IEnumerator PlayObjectNameAudio(GameObject obj, string tag)
    {
        // --- REMOVED: Assignment to isPlayingNameAudio ---
        // isPlayingNameAudio = true;
        nameAudioSource.transform.position = obj.transform.position;

        if (audioClipMap.ContainsKey(tag) && audioClipMap[tag] != null)
        {
            AudioClip clip = audioClipMap[tag];
            nameAudioSource.clip = clip;
            nameAudioSource.Play();
            yield return new WaitForSeconds(clip.length);
        }
        else if (useTTSFallback)
        {
            OnTTSRequested.Invoke(tag);
            yield return new WaitForSeconds(1f);
        }
        else
        {
            Debug.LogWarning($"No audio clip found for tag: {tag} and TTS is disabled");
        }
        // --- REMOVED: Assignment to isPlayingNameAudio ---
        // isPlayingNameAudio = false;
    }

    void OnDestroy()
    {
        if (currentAudioSequence != null)
        {
            StopCoroutine(currentAudioSequence);
        }
    }

    // --- MODIFIED: Methods kept as hooks for external systems, but internal logic removed ---
    public void OnTTSPlaybackComplete() { /* isPlayingNameAudio = false; */ }
    public void OnTTSPlaybackStarted() { /* isPlayingNameAudio = true; */ }

    public void UpdateAudioMapping(string tag, AudioClip clip) { if (audioClipMap == null) InitializeAudioSystem(); audioClipMap[tag] = clip; }
    public void RemoveAudioMapping(string tag) { if (audioClipMap != null && audioClipMap.ContainsKey(tag)) audioClipMap.Remove(tag); }
    public string[] GetAvailableAudioTags() { if (audioClipMap == null) return new string[0]; string[] tags = new string[audioClipMap.Keys.Count]; audioClipMap.Keys.CopyTo(tags, 0); return tags; }
    public void TestAudioClip(string tag) { if (audioClipMap.ContainsKey(tag) && audioClipMap[tag] != null) { nameAudioSource.transform.position = transform.position; nameAudioSource.PlayOneShot(audioClipMap[tag]); } else { Debug.LogWarning($"No audio clip found for tag: {tag}"); } }
    #endregion
}