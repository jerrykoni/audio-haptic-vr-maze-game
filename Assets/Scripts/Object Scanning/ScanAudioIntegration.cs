using UnityEngine;
public class ScanAudioIntegration : MonoBehaviour
{
    [Header("Component References")]
    public ConeScanner scanner;
    public ScanAudioManager audioManager;

    [Header("Auto-Find Components")]
    public bool autoFindComponents = true;

    void Start()
    {
        if (autoFindComponents)
        {
            if (scanner == null)
                scanner = GetComponent<ConeScanner>();

            if (audioManager == null)
                audioManager = FindFirstObjectByType<ScanAudioManager>();
        }

        // Connect the scanner to the audio manager  
        if (scanner != null && audioManager != null)
        {
            scanner.OnObjectDetected += audioManager.OnObjectDetected; // Use += to subscribe to the event
            scanner.OnObjectLost += audioManager.OnObjectLost; // Connect the OnObjectLost event
            Debug.Log("Scanner connected to Audio Manager");
        }
        else
        {
            Debug.LogError("Scanner or Audio Manager not found! Please assign them in the inspector.");
        }
    }

    void OnDestroy()
    {
        // Clean up event listeners  
        if (scanner != null && audioManager != null)
        {
            scanner.OnObjectDetected -= audioManager.OnObjectDetected; // Use -= to unsubscribe from the event
            scanner.OnObjectLost -= audioManager.OnObjectLost; // Unsubscribe from the OnObjectLost event
        }
    }
}