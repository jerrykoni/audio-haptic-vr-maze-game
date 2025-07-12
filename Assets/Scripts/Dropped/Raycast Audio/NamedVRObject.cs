using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;
public class NamedVRObject : MonoBehaviour
{
    [Header("Object Identification")]
    [SerializeField] private string objectName = "Unknown Object";
    [SerializeField] private string objectID; // Used for audio clip lookup

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float volume = 1f;
    [SerializeField] private bool playOnHover = true;
    [SerializeField] private bool playOnSelect = false;

    [Header("Cooldown Settings")]
    [SerializeField] private float audioCooldown = 2f; // Prevent spam
    private float lastPlayTime = -999f;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject highlightEffect;
    [SerializeField] private Material hoverMaterial;
    private Material originalMaterial;
    private Renderer objectRenderer;

    [Header("Meta SDK Components")]
    [SerializeField] private RayInteractable rayInteractable;
    [SerializeField] private InteractableUnityEventWrapper eventWrapper;

    private AudioManager audioManager;
    private bool isCurrentlyHovered = false;

    void Start()
    {
        // Find or create AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Configure AudioSource for VR
        ConfigureAudioSource();

        // Get references
        audioManager = AudioManager.Instance;
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
        }

        // Auto-generate objectID if not set
        if (string.IsNullOrEmpty(objectID))
        {
            objectID = objectName.ToLower().Replace(" ", "_");
        }

        // Setup Meta SDK components
        SetupMetaInteraction();
    }

    private void SetupMetaInteraction()
    {
        // Get RayInteractable component
        if (rayInteractable == null)
        {
            rayInteractable = GetComponent<RayInteractable>();
        }

        // Get or create InteractableUnityEventWrapper for event handling
        if (eventWrapper == null)
        {
            eventWrapper = GetComponent<InteractableUnityEventWrapper>();
            if (eventWrapper == null)
            {
                eventWrapper = gameObject.AddComponent<InteractableUnityEventWrapper>();
            }
        }

        // Subscribe to interaction events
        if (eventWrapper != null)
        {
            // Wire up the events in Unity Inspector or via code
            eventWrapper.WhenHover.AddListener(OnHoverStarted);
            eventWrapper.WhenUnhover.AddListener(OnHoverEnded);
            eventWrapper.WhenSelect.AddListener(OnSelected);
            eventWrapper.WhenUnselect.AddListener(OnUnselected);
        }

        // Ensure required components exist
        EnsureRequiredComponents();
    }

    private void EnsureRequiredComponents()
    {
        // Ensure we have a collider for the surface
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }

        // Ensure we have a RayInteractable
        if (rayInteractable == null)
        {
            rayInteractable = gameObject.AddComponent<RayInteractable>();

            // Setup surface - you might need to adjust this based on your surface type
            var surface = GetComponent<PlaneSurface>();
            if (surface == null)
            {
                surface = gameObject.AddComponent<PlaneSurface>();
            }

            // Inject the surface into RayInteractable
            rayInteractable.InjectSurface(surface);
        }
    }

    private void ConfigureAudioSource()
    {
        audioSource.spatialBlend = 0f; // Full 3D audio
        audioSource.volume = volume;
        audioSource.playOnAwake = false;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 10f;
    }

    // Meta SDK Event Handlers
    public void OnHoverStarted()
    {
        isCurrentlyHovered = true;

        if (playOnHover)
        {
            PlayObjectName();
        }

        ShowHoverEffects();
    }

    public void OnHoverEnded()
    {
        isCurrentlyHovered = false;
        HideHoverEffects();
    }

    public void OnSelected()
    {
        if (playOnSelect)
        {
            PlayObjectName();
        }
    }

    public void OnUnselected()
    {
        // Handle selection end if needed
    }

    public void PlayObjectName()
    {
        if (Time.time - lastPlayTime < audioCooldown)
            return;

        if (audioManager != null)
        {
            audioManager.PlayObjectAudio(objectID, audioSource);
            lastPlayTime = Time.time;
        }
        else
        {
            Debug.LogWarning($"AudioManager not found! Cannot play audio for {objectName}");
        }
    }

    private void ShowHoverEffects()
    {
        if (highlightEffect != null)
        {
            highlightEffect.SetActive(true);
        }

        if (hoverMaterial != null && objectRenderer != null)
        {
            objectRenderer.material = hoverMaterial;
        }
    }

    private void HideHoverEffects()
    {
        if (highlightEffect != null)
        {
            highlightEffect.SetActive(false);
        }

        if (originalMaterial != null && objectRenderer != null)
        {
            objectRenderer.material = originalMaterial;
        }
    }

    // Public method to manually trigger audio (useful for debugging or other triggers)
    public void TriggerAudio()
    {
        PlayObjectName();
    }

    // Method to update object name at runtime
    public void SetObjectName(string newName, string newID = null)
    {
        objectName = newName;
        if (!string.IsNullOrEmpty(newID))
        {
            objectID = newID;
        }
        else
        {
            objectID = newName.ToLower().Replace(" ", "_");
        }
    }

    void OnDestroy()
    {
        // Clean up event listeners
        if (eventWrapper != null)
        {
            eventWrapper.WhenHover.RemoveListener(OnHoverStarted);
            eventWrapper.WhenUnhover.RemoveListener(OnHoverEnded);
            eventWrapper.WhenSelect.RemoveListener(OnSelected);
            eventWrapper.WhenUnselect.RemoveListener(OnUnselected);
        }
    }
}