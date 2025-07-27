using System.Collections;
using System.Collections.Generic; // Required for List
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

    // --- NEW: Audio Pooling Settings ---
    [Header("Audio Settings")]
    [Tooltip("Number of audio sources to pool for the hover sound to prevent clicking artifacts.")]
    public int audioSourcePoolSize = 8;

    [Header("Component References")]
    public ScanAudioManager audioManager;
    public ConeScanner coneScanner;

    // --- MODIFIED: Replaced single source with a pool ---
    private List<AudioSource> _hoverAudioSourcePool;
    private int _poolIndex = 0;

    private Coroutine _wallHapticsCoroutine;
    private Coroutine _scanHapticsCoroutine;

    private GameObject _currentScannedObject;
    private float _currentScanDistance = float.MaxValue;
    private bool _leftWallHapticsActive = false;
    private bool _rightWallHapticsActive = false;

    void Start()
    {
        if (audioManager == null) audioManager = FindFirstObjectByType<ScanAudioManager>();
        if (coneScanner == null) coneScanner = FindFirstObjectByType<ConeScanner>();

        // --- REBUILT: Initialize the audio source pool ---
        InitializeAudioPool();

        if (coneScanner != null)
        {
            coneScanner.OnObjectDetected += HandleObjectDetected;
            coneScanner.OnObjectLost += HandleObjectLost;
            coneScanner.OnObjectUpdated += HandleObjectDistanceUpdate;
        }
        else
        {
            Debug.LogError("UnifiedHapticsManager requires a ConeScanner in the scene.", this);
        }

        _wallHapticsCoroutine = StartCoroutine(WallHapticFeedbackLoop());
    }

    // --- NEW: Method to set up the pool of audio sources ---
    void InitializeAudioPool()
    {
        _hoverAudioSourcePool = new List<AudioSource>();
        GameObject poolParent = new GameObject("HoverAudioPool");
        poolParent.transform.SetParent(this.transform);

        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            GameObject sourceGO = new GameObject($"PooledAudioSource_{i}");
            sourceGO.transform.SetParent(poolParent.transform);
            AudioSource source = sourceGO.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1.0f; // 3D Sound

            if (audioManager != null && audioManager.hoverStaySound != null)
            {
                source.clip = audioManager.hoverStaySound;
                source.volume = audioManager.hoverStaySoundVolume;
                source.pitch = audioManager.hoverStaySoundPitch;
            }
            _hoverAudioSourcePool.Add(source);
        }
    }


    void OnDestroy()
    {
        if (coneScanner != null)
        {
            coneScanner.OnObjectDetected -= HandleObjectDetected;
            coneScanner.OnObjectLost -= HandleObjectLost;
            coneScanner.OnObjectUpdated -= HandleObjectDistanceUpdate;
        }
        StopAllCoroutines();
        StopAllVibrations();
    }


    private void HandleObjectDetected(GameObject detectedObject)
    {
        if (_currentScannedObject == detectedObject) return;
        StopScanningHapticsAndAudio();
        _currentScannedObject = detectedObject;
        _scanHapticsCoroutine = StartCoroutine(ScanSequence());
    }

    private void HandleObjectLost(GameObject lostObject)
    {
        if (_currentScannedObject == lostObject)
        {
            StopScanningHapticsAndAudio();
            _currentScannedObject = null;
            _currentScanDistance = float.MaxValue;
            PlayPulse(scanPulseFrequency, scanPulseIntensity, scanPulseDuration);
        }
    }

    private void HandleObjectDistanceUpdate(GameObject target, float distance)
    {
        if (target == _currentScannedObject)
        {
            _currentScanDistance = distance;
        }
    }

    private void StopScanningHapticsAndAudio()
    {
        if (_scanHapticsCoroutine != null)
        {
            StopCoroutine(_scanHapticsCoroutine);
            _scanHapticsCoroutine = null;
        }

        // --- MODIFIED: Stop all sources in the pool ---
        // This is only needed for an abrupt, total stop (like losing the object).
        if (_hoverAudioSourcePool != null)
        {
            foreach (var source in _hoverAudioSourcePool)
            {
                source.Stop();
            }
        }

        if (!_leftWallHapticsActive && ShouldUseController(OVRInput.Controller.LTouch))
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);

        if (!_rightWallHapticsActive && ShouldUseController(OVRInput.Controller.RTouch))
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
    }

    // --- MODIFIED: ScanSequence now calls the new pooling method ---
    private IEnumerator ScanSequence()
    {
        PlayPulse(scanPulseFrequency, scanPulseIntensity, scanPulseDuration);
        //if (audioManager != null) audioManager.PlayScanPulseSound();

        yield return new WaitForSeconds(scanPulseDuration);

        while (_currentScannedObject != null)
        {
            PlayPulse(scanStayFrequency, scanStayIntensity, 0.05f);

            // --- REPLACED FADE LOGIC WITH POOLING LOGIC ---
            PlayPooledHoverSound();

            float t = Mathf.Clamp01(_currentScanDistance / maxProximityDistance);
            float currentInterval = Mathf.Lerp(minScanStayPulseInterval, maxScanStayPulseInterval, t);

            yield return new WaitForSeconds(currentInterval);
        }
    }

    // --- NEW: Method to play sound from the audio pool ---
    private void PlayPooledHoverSound()
    {
        // Basic checks to ensure everything is set up
        if (_hoverAudioSourcePool == null || _hoverAudioSourcePool.Count == 0 || _currentScannedObject == null)
            return;

        // Get the next available audio source from the pool
        AudioSource sourceToPlay = _hoverAudioSourcePool[_poolIndex];

        // Position it and play it. No stopping, no fading.
        sourceToPlay.transform.position = _currentScannedObject.transform.position;
        sourceToPlay.Play();

        // Move to the next source for the next time, wrapping around if necessary
        _poolIndex = (_poolIndex + 1) % _hoverAudioSourcePool.Count;
    }

    /// <summary>
    /// Toggles the active controller for scanning haptics, cycling between Right and Both.
    /// This allows changing the haptic feedback mode at runtime.
    /// </summary>
    public void ToggleHapticControllers()
    {
        if (scanController == HapticController.Right)
        {
            scanController = HapticController.Both;
        }
        else // This covers the 'Both' case and will switch it back to 'Right'.
        {
            scanController = HapticController.Right;
        }
    }

    private void PlayPulse(float frequency, float intensity, float duration)
    {
        if (!_leftWallHapticsActive && ShouldUseController(OVRInput.Controller.LTouch))
            StartCoroutine(VibrationCoroutine(OVRInput.Controller.LTouch, frequency, intensity, duration));
        if (!_rightWallHapticsActive && ShouldUseController(OVRInput.Controller.RTouch))
            StartCoroutine(VibrationCoroutine(OVRInput.Controller.RTouch, frequency, intensity, duration));
    }

    private IEnumerator VibrationCoroutine(OVRInput.Controller controller, float frequency, float intensity, float duration)
    {
        OVRInput.SetControllerVibration(frequency, intensity, controller);
        yield return new WaitForSeconds(duration);
        if ((controller == OVRInput.Controller.LTouch && !_leftWallHapticsActive) ||
            (controller == OVRInput.Controller.RTouch && !_rightWallHapticsActive))
        {
            OVRInput.SetControllerVibration(0, 0, controller);
        }
    }

    private void StopAllVibrations()
    {
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
    }

    bool ShouldUseController(OVRInput.Controller controller)
    {
        return scanController switch
        {
            HapticController.Left => controller == OVRInput.Controller.LTouch,
            HapticController.Right => controller == OVRInput.Controller.RTouch,
            HapticController.Both => true,
            _ => true,
        };
    }

    IEnumerator WallHapticFeedbackLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(hapticCheckInterval);
        while (true)
        {
            _leftWallHapticsActive = ProcessHandHaptics(leftHandAnchor, OVRInput.Controller.LTouch);
            _rightWallHapticsActive = ProcessHandHaptics(rightHandAnchor, OVRInput.Controller.RTouch);
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
        else if (_currentScannedObject == null || !ShouldUseController(controller))
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
}