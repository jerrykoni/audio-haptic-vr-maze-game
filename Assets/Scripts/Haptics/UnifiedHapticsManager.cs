// Unified haptics manager that handles both scanning and hand-wall haptics
using System.Collections;
using System.Linq;
using UnityEngine;

public class UnifiedHapticsManager : MonoBehaviour
{
    [System.Serializable]
    public enum HapticController
    {
        Left,
        Right,
        Both
    }

    [Header("Hand References")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;

    [Header("Wall Haptics Settings")]
    public float detectionRadius = 0.5f;
    public float maxHapticDistance = 0.5f;
    public float hapticCheckInterval = 0.1f;
    public float wallHapticFrequency = 0.1f;
    public LayerMask wallLayer;

    [Header("Scanning Haptics Settings")]
    public HapticController scanController = HapticController.Both;
    public float scanPulseFrequency = 0.5f;
    public float scanPulseIntensity = 0.8f;
    public float scanPulseDuration = 0.1f;
    public float scanStayFrequency = 0.2f;
    public float scanStayIntensity = 0.4f;
    public float scanStayPulseInterval = 0.3f;

    [Header("Component References")]
    public ScanAudioManager audioManager;

    // Internal state
    private bool isScanning = false;
    private GameObject currentScannedObject;
    private Coroutine wallHapticsCoroutine;
    private Coroutine scanStayHapticsCoroutine;

    // Track wall haptics state for each controller
    private bool leftWallHapticsActive = false;
    private bool rightWallHapticsActive = false;

    void Start()
    {
        // Auto-find audio manager if not assigned
        if (audioManager == null)
        {
            audioManager = FindFirstObjectByType<ScanAudioManager>();
        }

        // Connect to audio manager events
        if (audioManager != null)
        {
            audioManager.OnObjectDetectedHaptics.AddListener(OnObjectDetectedHaptics);
            audioManager.OnObjectLostHaptics.AddListener(OnObjectLostHaptics);
        }

        // Start wall haptics loop
        wallHapticsCoroutine = StartCoroutine(WallHapticFeedbackLoop());
    }

    void OnObjectDetectedHaptics(GameObject detectedObject)
    {
        // Play strong pulse for object detection
        PlayScanPulse();

        // Start continuous scanning haptics
        if (!isScanning || currentScannedObject != detectedObject)
        {
            if (scanStayHapticsCoroutine != null)
            {
                StopCoroutine(scanStayHapticsCoroutine);
            }

            isScanning = true;
            currentScannedObject = detectedObject;
            scanStayHapticsCoroutine = StartCoroutine(ScanStayHaptics());
        }
    }

    void OnObjectLostHaptics(GameObject lostObject)
    {
        if (currentScannedObject == lostObject)
        {
            isScanning = false;
            currentScannedObject = null;

            if (scanStayHapticsCoroutine != null)
            {
                StopCoroutine(scanStayHapticsCoroutine);
                scanStayHapticsCoroutine = null;
            }

            // Clear scan haptics when not wall haptics active
            if (!leftWallHapticsActive && ShouldUseController(OVRInput.Controller.LTouch))
            {
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
            }
            if (!rightWallHapticsActive && ShouldUseController(OVRInput.Controller.RTouch))
            {
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            }
        }
    }

    void PlayScanPulse()
    {
        // Play strong pulse on selected controllers
        if (ShouldUseController(OVRInput.Controller.LTouch))
        {
            OVRInput.SetControllerVibration(scanPulseFrequency, scanPulseIntensity, OVRInput.Controller.LTouch);
        }
        if (ShouldUseController(OVRInput.Controller.RTouch))
        {
            OVRInput.SetControllerVibration(scanPulseFrequency, scanPulseIntensity, OVRInput.Controller.RTouch);
        }

        // Stop the pulse after the specified duration
        StartCoroutine(StopPulseAfterDelay(scanPulseDuration));
    }

    IEnumerator StopPulseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Only stop if not doing wall haptics
        if (!leftWallHapticsActive && ShouldUseController(OVRInput.Controller.LTouch))
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        }
        if (!rightWallHapticsActive && ShouldUseController(OVRInput.Controller.RTouch))
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
        }
    }

    IEnumerator ScanStayHaptics()
    {
        WaitForSeconds wait = new WaitForSeconds(scanStayPulseInterval);

        while (isScanning && currentScannedObject != null)
        {
            // Play soft continuous pulse on selected controllers, but only if wall haptics aren't active
            if (!leftWallHapticsActive && ShouldUseController(OVRInput.Controller.LTouch))
            {
                OVRInput.SetControllerVibration(scanStayFrequency, scanStayIntensity, OVRInput.Controller.LTouch);
            }
            if (!rightWallHapticsActive && ShouldUseController(OVRInput.Controller.RTouch))
            {
                OVRInput.SetControllerVibration(scanStayFrequency, scanStayIntensity, OVRInput.Controller.RTouch);
            }

            yield return new WaitForSeconds(0.1f);

            // Brief pause between pulses, but only if not doing wall haptics
            if (!leftWallHapticsActive && ShouldUseController(OVRInput.Controller.LTouch))
            {
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
            }
            if (!rightWallHapticsActive && ShouldUseController(OVRInput.Controller.RTouch))
            {
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            }

            yield return wait;
        }

        scanStayHapticsCoroutine = null;
    }

    bool ShouldUseController(OVRInput.Controller controller)
    {
        switch (scanController)
        {
            case HapticController.Left:
                return controller == OVRInput.Controller.LTouch;
            case HapticController.Right:
                return controller == OVRInput.Controller.RTouch;
            case HapticController.Both:
                return true;
            default:
                return true;
        }
    }

    // Wall haptics functionality (integrated from HandWallHaptics)
    IEnumerator WallHapticFeedbackLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(hapticCheckInterval);
        while (true)
        {
            leftWallHapticsActive = ProcessHandHaptics(leftHandAnchor, OVRInput.Controller.LTouch);
            rightWallHapticsActive = ProcessHandHaptics(rightHandAnchor, OVRInput.Controller.RTouch);
            yield return wait;
        }
    }

    bool ProcessHandHaptics(Transform handAnchor, OVRInput.Controller controller)
    {
        if (handAnchor == null) return false;

        // Check for walls and scannable objects
        Collider[] nearbyWalls = Physics.OverlapSphere(handAnchor.position, detectionRadius, wallLayer);

        // Also check for scannable objects in the scene
        Collider[] nearbyScannables = Physics.OverlapSphere(handAnchor.position, detectionRadius)
            .Where(c => IsScannableObject(c.gameObject))
            .ToArray();

        // Combine both wall and scannable objects
        var allNearbyObjects = nearbyWalls.Concat(nearbyScannables).ToArray();

        if (allNearbyObjects.Length > 0)
        {
            float minDistance = allNearbyObjects
                .Select(collider => Vector3.Distance(collider.ClosestPoint(handAnchor.position), handAnchor.position))
                .Min();

            float intensity = Mathf.Clamp01(1f - (minDistance / maxHapticDistance));

            // Wall haptics always take priority over scan haptics
            OVRInput.SetControllerVibration(wallHapticFrequency, intensity, controller);
            return true;
        }
        else if (!isScanning || !ShouldUseController(controller))
        {
            // Stop vibration if no objects are nearby and not scanning on this controller
            OVRInput.SetControllerVibration(0, 0, controller);
        }

        return false;
    }

    bool IsScannableObject(GameObject obj)
    {
        // Check if object has tags that are scannable
        if (audioManager != null)
        {
            string[] availableTags = audioManager.GetAvailableAudioTags();
            return availableTags.Contains(obj.tag);
        }

        // Fallback: check common scannable tags
        return obj.CompareTag("Wall") || obj.CompareTag("Chair") || obj.CompareTag("Door") ||
               obj.CompareTag("Table") || obj.CompareTag("Window");
    }

    void OnDrawGizmosSelected()
    {
        if (leftHandAnchor != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(leftHandAnchor.position, detectionRadius);
        }
        if (rightHandAnchor != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(rightHandAnchor.position, detectionRadius);
        }
    }

    void OnDestroy()
    {
        // Clean up event listeners
        if (audioManager != null)
        {
            audioManager.OnObjectDetectedHaptics.RemoveListener(OnObjectDetectedHaptics);
            audioManager.OnObjectLostHaptics.RemoveListener(OnObjectLostHaptics);
        }

        // Stop all haptics
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);

        // Stop coroutines
        if (wallHapticsCoroutine != null)
        {
            StopCoroutine(wallHapticsCoroutine);
        }
        if (scanStayHapticsCoroutine != null)
        {
            StopCoroutine(scanStayHapticsCoroutine);
        }
    }
}