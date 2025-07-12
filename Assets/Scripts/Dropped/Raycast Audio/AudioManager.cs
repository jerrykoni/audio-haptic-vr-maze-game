using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Clip Database")]
    [SerializeField] private ObjectAudioClip[] audioClips;

    [Header("Fallback Settings")]
    [SerializeField] private AudioClip defaultClip;
    [SerializeField] private bool logMissingClips = true;

    private System.Collections.Generic.Dictionary<string, AudioClip> audioDatabase;

    [System.Serializable]
    public class ObjectAudioClip
    {
        public string objectID;
        public AudioClip audioClip;
        [TextArea(2, 3)]
        public string description; // For organization in inspector
    }

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioDatabase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeAudioDatabase()
    {
        audioDatabase = new System.Collections.Generic.Dictionary<string, AudioClip>();

        foreach (var audioClipData in audioClips)
        {
            if (!string.IsNullOrEmpty(audioClipData.objectID) && audioClipData.audioClip != null)
            {
                audioDatabase[audioClipData.objectID] = audioClipData.audioClip;
            }
        }

        Debug.Log($"AudioManager initialized with {audioDatabase.Count} audio clips");
    }

    public void PlayObjectAudio(string objectID, AudioSource audioSource)
    {
        if (audioDatabase.TryGetValue(objectID, out AudioClip clip))
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
        else
        {
            if (logMissingClips)
            {
                Debug.LogWarning($"Audio clip not found for object ID: {objectID}");
            }

            // Play default clip if available
            if (defaultClip != null)
            {
                audioSource.clip = defaultClip;
                audioSource.Play();
            }
        }
    }

    public void AddAudioClip(string objectID, AudioClip clip)
    {
        audioDatabase[objectID] = clip;
    }

    public bool HasAudioClip(string objectID)
    {
        return audioDatabase.ContainsKey(objectID);
    }
}