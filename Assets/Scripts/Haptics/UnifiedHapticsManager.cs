using System.Collections;
using System.Linq;
using UnityEngine;

public class UnifiedHapticsManager : MonoBehaviour
{
    [System.Serializable]
    public enum HapticController { Left, Right, Both }

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

    [Header("Proximity-Based Haptics")]
    public float minScanStayPulseInterval = 0.1f;
    public float maxScanStayPulseInterval = 0.5f;
    public float maxProximityDistance = 1.0f;

    [Header("Component References")]
    public ScanAudioManager audioManager;
    public ConeScanner coneScanner; // Reference to the scanner

    // Internal state
    private bool isScanning = false;
    private GameObject currentScannedObject;
    private Coroutine wallHapticsCoroutine;
    private Coroutine scanStayHapticsCoroutine;
    private float currentScanDistance = float.MaxValue; // Stores the distance from ConeScanner

    private bool leftWallHapticsActive = false;
    private bool rightWallHapticsActive = false;

    void Start()
    {
        if (audioManager == null) audioManager = FindFirstObjectByType<ScanAudioManager>();
        if (coneScanner == null) coneScanner = FindFirstObjectByType<ConeScanner>();

        // --- MODIFIED: Connect to all three ConeScanner events ---
        if (coneScanner != null)
        {
            coneScanner.OnObjectDetected += OnObjectDetectedHaptics;
            coneScanner.OnObjectLost += OnObjectLostHaptics;
            coneScanner.OnObjectUpdated += OnObjectDistanceUpdate; // Subscribe to continuous updates
        }
        else
        {
            Debug.LogError("UnifiedHapticsManager requires a ConeScanner in the scene.", this);
        }

        wallHapticsCoroutine = StartCoroutine(WallHapticFeedbackLoop());
    }

    // This handler, for the original event, starts the haptic sequence
    void OnObjectDetectedHaptics(GameObject detectedObject)
    {
        // The rest of your original logic is fine here
        if (!isScanning || currentScannedObject != detectedObject)
        {
            if (scanStayHapticsCoroutine != null) StopCoroutine(scanStayHapticsCoroutine);

            isScanning = true;
            currentScannedObject = detectedObject;

            scanStayHapticsCoroutine = StartCoroutine(ScanPulseAndStaySequence());
        }
    }

    // --- NEW: This method receives the continuous distance updates from the new event ---
    void OnObjectDistanceUpdate(GameObject target, float distance)
    {
        // We only care about the distance of the object we are currently locked onto
        if (isScanning && target == currentScannedObject)
        {
            currentScanDistance = distance;
        }
    }

    // This handler, for the original event, stops the haptic sequence
    void OnObjectLostHaptics(GameObject lostObject)
    {
        if (currentScannedObject == lostObject)
        {
            isScanning = false;
            currentScannedObject = null;
            currentScanDistance = float.MaxValue; // Reset distance

            if (scanStayHapticsCoroutine != null)
            {
                StopCoroutine(scanStayHapticsCoroutine);
                scanStayHapticsCoroutine = null;
            }

            if (!leftWallHapticsActive && ShouldUseController(OVRInput.Controller.LTouch))
            {
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
            }
            if (!rightWallHapticsActive && ShouldUseController(OVRInput.Controller.RTouch))
            {
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            }
            StartCoroutine(PlayExitPulseAfterDelay());
        }
    }

    // --- MODIFIED: This coroutine now uses the distance provided by the event system ---
    IEnumerator ScanStayHaptics()
    {
        while (isScanning && currentScannedObject != null)
        {
            // Play brief pulse
            if (!leftWallHapticsActive && ShouldUseController(OVRInput.Controller.LTouch))
                OVRInput.SetControllerVibration(scanStayFrequency, scanStayIntensity, OVRInput.Controller.LTouch);
            if (!rightWallHapticsActive && ShouldUseController(OVRInput.Controller.RTouch))
                OVRInput.SetControllerVibration(scanStayFrequency, scanStayIntensity, OVRInput.Controller.RTouch);

            yield return new WaitForSeconds(0.05f); // Short pulse duration

            // Stop pulse
            if (!leftWallHapticsActive && ShouldUseController(OVRInput.Controller.LTouch))
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
            if (!rightWallHapticsActive && ShouldUseController(OVRInput.Controller.RTouch))
                OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);

            // Calculate wait time based on distance provided by ConeScanner
            float t = Mathf.Clamp01(currentScanDistance / maxProximityDistance);
            float currentInterval = Mathf.Lerp(minScanStayPulseInterval, maxScanStayPulseInterval, t);

            yield return new WaitForSeconds(currentInterval);
        }

        scanStayHapticsCoroutine = null;
    }

    void OnDestroy()
    {
        // --- MODIFIED: Clean up all event listeners ---
        if (coneScanner != null)
        {
            coneScanner.OnObjectDetected -= OnObjectDetectedHaptics;
            coneScanner.OnObjectLost -= OnObjectLostHaptics;
            coneScanner.OnObjectUpdated -= OnObjectDistanceUpdate;
        }

        // Stop all haptics
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);

        // Stop coroutines
        if (wallHapticsCoroutine != null) StopCoroutine(wallHapticsCoroutine);
        if (scanStayHapticsCoroutine != null) StopCoroutine(scanStayHapticsCoroutine);
    }

    // The rest of your script (PlayScanPulse, WallHapticFeedbackLoop, etc.)
    // does not need to be changed.
    #region Unchanged Methods
    void PlayScanPulse()
    {
        if (!leftWallHapticsActive && ShouldUseController(OVRInput.Controller.LTouch))
            OVRInput.SetControllerVibration(scanPulseFrequency, scanPulseIntensity, OVRInput.Controller.LTouch);
        if (!rightWallHapticsActive && ShouldUseController(OVRInput.Controller.RTouch))
            OVRInput.SetControllerVibration(scanPulseFrequency, scanPulseIntensity, OVRInput.Controller.RTouch);
        StartCoroutine(StopPulseAfterDelay(scanPulseDuration));
    }

    IEnumerator StopPulseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!leftWallHapticsActive && ShouldUseController(OVRInput.Controller.LTouch))
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        if (!rightWallHapticsActive && ShouldUseController(OVRInput.Controller.RTouch))
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
    }

    IEnumerator ScanPulseAndStaySequence()
    {
        PlayScanPulse();
        yield return new WaitForSeconds(scanPulseDuration);
        yield return StartCoroutine(ScanStayHaptics());
    }

    IEnumerator PlayExitPulseAfterDelay()
    {
        yield return new WaitForSeconds(0.021f);
        PlayScanPulse();
    }

    bool ShouldUseController(OVRInput.Controller controller)
    {
        switch (scanController)
        {
            case HapticController.Left: return controller == OVRInput.Controller.LTouch;
            case HapticController.Right: return controller == OVRInput.Controller.RTouch;
            case HapticController.Both: return true;
            default: return true;
        }
    }

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

        Collider[] nearbyWalls = Physics.OverlapSphere(handAnchor.position, detectionRadius, wallLayer);
        Collider[] nearbyScannables = Physics.OverlapSphere(handAnchor.position, detectionRadius)
            .Where(c => IsScannableObject(c.gameObject))
            .ToArray();
        var allNearbyObjects = nearbyWalls.Concat(nearbyScannables).ToArray();

        if (allNearbyObjects.Length > 0)
        {
            float minDistance = allNearbyObjects
                .Select(collider => Vector3.Distance(collider.ClosestPoint(handAnchor.position), handAnchor.position))
                .Min();
            float intensity = Mathf.Clamp01(1f - (minDistance / maxHapticDistance));
            OVRInput.SetControllerVibration(wallHapticFrequency, intensity, controller);
            return true;
        }
        else if (!isScanning || !ShouldUseController(controller))
        {
            OVRInput.SetControllerVibration(0, 0, controller);
        }
        return false;
    }

    bool IsScannableObject(GameObject obj)
    {
        if (audioManager != null)
        {
            string[] availableTags = audioManager.GetAvailableAudioTags();
            return availableTags.Contains(obj.tag);
        }
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
    #endregion
}