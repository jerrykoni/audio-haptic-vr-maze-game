using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ScanAudioManager : MonoBehaviour
{
    private static GameObject _globallyLastAnnouncedObject;

    [Header("Audio Settings")]
    public AudioSource nameAudioSource;
    public AudioSource hoverAudioSource;
    public AudioSource hoverStayAudioSource;

    [Header("Predefined Audio Clips")]
    public AudioClip hoverSound;
    [Range(0f, 1f)] public float hoverSoundVolume = 1f;
    [Range(0.1f, 3f)] public float hoverSoundPitch = 1f;

    public AudioClip hoverStaySound;
    [Range(0f, 1f)] public float hoverStaySoundVolume = 1f;
    [Range(0.1f, 3f)] public float hoverStaySoundPitch = 1f;

    public AudioClip unhoverSound;
    [Range(0f, 1f)] public float unhoverSoundVolume = 1f;
    [Range(0.1f, 3f)] public float unhoverSoundPitch = 1f;

    [Header("Object Audio Mapping")]
    public ObjectAudioMapping[] objectAudioMappings;

    [Header("Audio Timing")]
    public float hoverToNameDelay = 0.2f;
    public float minTimeBetweenScans = 0.1f;

    [Header("TTS Settings")]
    public bool useTTSFallback = true;
    public float ttsVolume = 0.8f;

    [Header("Cone Edge Positioning")]
    [Tooltip("If true, hover/name/unhover audio will emit from the cone edge (effective tip) instead of object center.")]
    public bool useConeEdgeForSpatializedEvents = true;
    [Tooltip("Reference to the ConeScanner on the same controller (assign in inspector).")]
    public ConeScanner coneScanner;

    // (Optional) Exposed for future adaptive audio (ADDED)
    public float CurrentConeLength => coneScanner != null ? coneScanner.CurrentEffectiveConeLength : 0f;
    public float CurrentConeLengthNormalized => coneScanner != null ? coneScanner.CurrentEffectiveConeLengthNormalized : 0f;

    private Dictionary<string, AudioClip> audioClipMap;
    private GameObject lastScannedObject;
    private GameObject currentDetectedObject;
    private string lastScannedTag;
    private float lastScanTime;
    private Coroutine currentAudioSequence;

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
        if (coneScanner == null)
            coneScanner = GetComponentInParent<ConeScanner>();
    }

    public void OnObjectDetected(GameObject detectedObject)
    {
        if (detectedObject == null) return;
        if (Time.time - lastScanTime < minTimeBetweenScans) return;

        string objectTag = detectedObject.tag;
        if (string.IsNullOrEmpty(objectTag) || objectTag == "Untagged")
            objectTag = detectedObject.name;

        bool shouldAnnounceName = (detectedObject != _globallyLastAnnouncedObject);

        currentDetectedObject = detectedObject;

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
            currentDetectedObject = null;

            if (hoverStayAudioSource != null && hoverStayAudioSource.isPlaying)
                hoverStayAudioSource.Stop();

            PlayUnhoverSound(lostObject);
            OnObjectLostHaptics.Invoke(lostObject);
        }
    }

    #region Core
    void InitializeAudioSystem()
    {
        audioClipMap = new Dictionary<string, AudioClip>();
        foreach (var mapping in objectAudioMappings)
        {
            if (!string.IsNullOrEmpty(mapping.objectTag) && mapping.audioClip != null)
                audioClipMap[mapping.objectTag] = mapping.audioClip;
        }

        if (nameAudioSource == null)
            nameAudioSource = gameObject.AddComponent<AudioSource>();
        if (hoverAudioSource == null)
        {
            GameObject uiAudioObject = new GameObject("UI Audio Source");
            uiAudioObject.transform.SetParent(transform);
            hoverAudioSource = uiAudioObject.AddComponent<AudioSource>();
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

    Vector3 GetEmitPosition(GameObject obj)
    {
        if (!useConeEdgeForSpatializedEvents || coneScanner == null)
            return obj.transform.position;
        return coneScanner.ConeEdgeWorldPosition;
    }

    void HandleNewObjectDetected(GameObject obj, string tag)
    {
        if (currentAudioSequence != null)
            StopCoroutine(currentAudioSequence);
        currentAudioSequence = StartCoroutine(PlayNewObjectAudioSequence(obj, tag));
    }

    void PlayHoverSound(GameObject obj)
    {
        if (hoverSound != null)
        {
            hoverAudioSource.transform.position = GetEmitPosition(obj);
            hoverAudioSource.volume = hoverSoundVolume;
            hoverAudioSource.pitch = hoverSoundPitch;
            hoverAudioSource.PlayOneShot(hoverSound);
        }
    }

    void PlayUnhoverSound(GameObject obj)
    {
        if (unhoverSound != null)
        {
            hoverAudioSource.transform.position = GetEmitPosition(obj);
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
        nameAudioSource.transform.position = GetEmitPosition(obj);

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
            Debug.LogWarning($"No audio clip for tag/name: {tag} and TTS disabled");
        }
    }

    void OnDestroy()
    {
        if (currentAudioSequence != null)
            StopCoroutine(currentAudioSequence);
    }

    public void OnTTSPlaybackComplete() { }
    public void OnTTSPlaybackStarted() { }

    public void UpdateAudioMapping(string tag, AudioClip clip)
    {
        if (audioClipMap == null) InitializeAudioSystem();
        audioClipMap[tag] = clip;
    }
    public void RemoveAudioMapping(string tag)
    {
        if (audioClipMap != null && audioClipMap.ContainsKey(tag))
            audioClipMap.Remove(tag);
    }
    public string[] GetAvailableAudioTags()
    {
        if (audioClipMap == null) return new string[0];
        string[] tags = new string[audioClipMap.Keys.Count];
        audioClipMap.Keys.CopyTo(tags, 0);
        return tags;
    }
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
    #endregion
}