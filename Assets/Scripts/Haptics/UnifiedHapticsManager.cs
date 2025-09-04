using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnifiedHapticsManager : MonoBehaviour
{
    // --- SINGLETON PATTERN ---
    public static UnifiedHapticsManager Instance { get; private set; }

    [Header("Hand References")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;

    [Header("Component References")]
    [Tooltip("The ConeScanner on the Left Controller.")]
    public ConeScanner leftConeScanner;
    [Tooltip("The ConeScanner on the Right Controller.")]
    public ConeScanner rightConeScanner;
    [Tooltip("The ScanAudioManager on the Left Controller.")]
    public ScanAudioManager leftAudioManager;
    [Tooltip("The ScanAudioManager on the Right Controller.")]
    public ScanAudioManager rightAudioManager;

    [Header("Wall Haptics Settings")]
    public float detectionRadius = 0.5f;
    [Tooltip("Distance (from hand to wall surface) over which intensity ramps from 0 to full.")]
    public float maxHapticDistance = 0.5f;
    public float hapticCheckInterval = 0.1f;
    public float wallHapticFrequency = 0.1f;
    public LayerMask wallLayer;
    [Space(6)]
    [Tooltip("Maximum vibration intensity when fully 'penetrated' (after easing).")]
    [Range(0f, 1f)] public float wallMaxIntensity = 1f;
    [Tooltip("Easing exponent. 1 = linear. >1 slows early growth (ease-in). <1 speeds early growth.")]
    [Min(0.01f)] public float wallIntensityRampExponent = 2f;
    [Tooltip("Seconds to go from 0 to max when entering a wall (after exponent applied).")]
    [Min(0.01f)] public float wallHapticRiseTime = 0.3f;
    [Tooltip("Seconds to fade back to 0 after leaving a wall.")]
    [Min(0.01f)] public float wallHapticFallTime = 0.12f;
    [Tooltip("If true, intensity is forced to 0 when below this threshold.")]
    public float wallHapticsMinCutoff = 0.01f;

    [Header("Scanning Haptics Settings")]
    public float scanPulseFrequency = 0.5f;
    public float scanPulseIntensity = 0.8f;
    public float scanPulseDuration = 0.1f;
    public float scanStayFrequency = 0.2f;
    public float scanStayIntensity = 0.4f;

    [Header("Proximity-Based Haptics")]
    public float minScanStayPulseInterval = 0.1f;
    public float maxScanStayPulseInterval = 0.5f;
    public float maxProximityDistance = 1.0f;

    [Header("Audio Settings")]
    public int audioSourcePoolSize = 8;

    private List<AudioSource> _hoverAudioSourcePool;
    private int _poolIndex = 0;

    private Dictionary<GameObject, Coroutine> _activeScanCoroutines = new Dictionary<GameObject, Coroutine>();
    private Dictionary<GameObject, float> _objectDistances = new Dictionary<GameObject, float>();

    private Coroutine _wallHapticsCoroutine;
    private bool _leftWallHapticsActive = false;
    private bool _rightWallHapticsActive = false;

    // Cached (smoothed) wall intensities per hand
    private float _leftWallIntensity = 0f;
    private float _rightWallIntensity = 0f;

    // A pre-allocated array for physics queries to avoid generating garbage memory.
    private readonly Collider[] _nearbyWallsCache = new Collider[16];

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        InitializeAudioPool();

        // Subscribe to specific handlers for each controller
        if (leftConeScanner != null)
        {
            leftConeScanner.OnObjectDetected += HandleLeftObjectDetected;
            leftConeScanner.OnObjectLost += HandleLeftObjectLost;
            leftConeScanner.OnObjectUpdated += HandleObjectDistanceUpdate;
        }
        else Debug.LogError("Left ConeScanner is not assigned!", this);

        if (rightConeScanner != null)
        {
            rightConeScanner.OnObjectDetected += HandleRightObjectDetected;
            rightConeScanner.OnObjectLost += HandleRightObjectLost;
            rightConeScanner.OnObjectUpdated += HandleObjectDistanceUpdate;
        }
        else Debug.LogError("Right ConeScanner is not assigned!", this);

        _wallHapticsCoroutine = StartCoroutine(WallHapticFeedbackLoop());
    }

    void OnDestroy()
    {
        if (leftConeScanner != null)
        {
            leftConeScanner.OnObjectDetected -= HandleLeftObjectDetected;
            leftConeScanner.OnObjectLost -= HandleLeftObjectLost;
            leftConeScanner.OnObjectUpdated -= HandleObjectDistanceUpdate;
        }
        if (rightConeScanner != null)
        {
            rightConeScanner.OnObjectDetected -= HandleRightObjectDetected;
            rightConeScanner.OnObjectLost -= HandleRightObjectLost;
            rightConeScanner.OnObjectUpdated -= HandleObjectDistanceUpdate;
        }
        StopAllCoroutines();
        StopAllVibrations();
    }

    private void HandleLeftObjectDetected(GameObject detectedObject)
    {
        if (!_leftWallHapticsActive)
            StartCoroutine(VibrationCoroutine(OVRInput.Controller.LTouch, scanPulseFrequency, scanPulseIntensity, scanPulseDuration));

        if (!_activeScanCoroutines.ContainsKey(detectedObject))
        {
            _objectDistances[detectedObject] = float.MaxValue;
            Coroutine newCoroutine = StartCoroutine(ScanSequence(detectedObject));
            _activeScanCoroutines[detectedObject] = newCoroutine;
        }
    }

    private void HandleRightObjectDetected(GameObject detectedObject)
    {
        if (!_rightWallHapticsActive)
            StartCoroutine(VibrationCoroutine(OVRInput.Controller.RTouch, scanPulseFrequency, scanPulseIntensity, scanPulseDuration));

        if (!_activeScanCoroutines.ContainsKey(detectedObject))
        {
            _objectDistances[detectedObject] = float.MaxValue;
            Coroutine newCoroutine = StartCoroutine(ScanSequence(detectedObject));
            _activeScanCoroutines[detectedObject] = newCoroutine;
        }
    }

    private void HandleLeftObjectLost(GameObject lostObject)
    {
        bool isStillDetectedByRight = (rightConeScanner != null && rightConeScanner.CurrentTarget == lostObject);
        if (!isStillDetectedByRight && _activeScanCoroutines.ContainsKey(lostObject))
        {
            StopCoroutine(_activeScanCoroutines[lostObject]);
            _activeScanCoroutines.Remove(lostObject);
            _objectDistances.Remove(lostObject);
        }
    }

    private void HandleRightObjectLost(GameObject lostObject)
    {
        bool isStillDetectedByLeft = (leftConeScanner != null && leftConeScanner.CurrentTarget == lostObject);
        if (!isStillDetectedByLeft && _activeScanCoroutines.ContainsKey(lostObject))
        {
            StopCoroutine(_activeScanCoroutines[lostObject]);
            _activeScanCoroutines.Remove(lostObject);
            _objectDistances.Remove(lostObject);
        }
    }

    private void HandleObjectDistanceUpdate(GameObject target, float distance)
    {
        if (_objectDistances.ContainsKey(target))
        {
            _objectDistances[target] = distance;
        }
    }

    private IEnumerator ScanSequence(GameObject targetObject)
    {
        yield return new WaitForSeconds(scanPulseDuration);

        while (_activeScanCoroutines.ContainsKey(targetObject))
        {
            PlayStayPulse(targetObject);
            PlayPooledHoverSound(targetObject);

            float currentDistance = _objectDistances.ContainsKey(targetObject) ? _objectDistances[targetObject] : float.MaxValue;
            float t = Mathf.Clamp01(currentDistance / maxProximityDistance);
            float currentInterval = Mathf.Lerp(minScanStayPulseInterval, maxScanStayPulseInterval, t);
            yield return new WaitForSeconds(currentInterval);
        }
    }

    private void PlayStayPulse(GameObject targetObject)
    {
        if (leftConeScanner != null && leftConeScanner.CurrentTarget == targetObject)
        {
            if (!_leftWallHapticsActive)
                StartCoroutine(VibrationCoroutine(OVRInput.Controller.LTouch, scanStayFrequency, scanStayIntensity, 0.05f));
        }

        if (rightConeScanner != null && rightConeScanner.CurrentTarget == targetObject)
        {
            if (!_rightWallHapticsActive)
                StartCoroutine(VibrationCoroutine(OVRInput.Controller.RTouch, scanStayFrequency, scanStayIntensity, 0.05f));
        }
    }

    private void PlayPooledHoverSound(GameObject targetObject)
    {
        if (_hoverAudioSourcePool == null || _hoverAudioSourcePool.Count == 0 || targetObject == null) return;

        AudioSource sourceToPlay = _hoverAudioSourcePool[_poolIndex];
        sourceToPlay.transform.position = targetObject.transform.position;
        sourceToPlay.Play();
        _poolIndex = (_poolIndex + 1) % _hoverAudioSourcePool.Count;
    }

    #region Helper and Corrected Methods
    void InitializeAudioPool()
    {
        _hoverAudioSourcePool = new List<AudioSource>();
        GameObject poolParent = new("HoverAudioPool");
        poolParent.transform.SetParent(this.transform);

        ScanAudioManager templateManager = leftAudioManager;
        if (templateManager == null)
        {
            Debug.LogError("Cannot initialize audio pool, Left ScanAudioManager is not assigned!");
            return;
        }

        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            GameObject sourceGO = new($"PooledAudioSource_{i}");
            sourceGO.transform.SetParent(poolParent.transform);
            AudioSource source = sourceGO.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1.0f;
            if (templateManager.hoverStaySound != null)
            {
                source.clip = templateManager.hoverStaySound;
                source.volume = templateManager.hoverStaySoundVolume;
                source.pitch = templateManager.hoverStaySoundPitch;
            }
            _hoverAudioSourcePool.Add(source);
        }
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

    IEnumerator WallHapticFeedbackLoop()
    {
        WaitForSeconds wait = new(hapticCheckInterval);
        while (true)
        {
            _leftWallHapticsActive = ProcessHandHaptics(leftHandAnchor, OVRInput.Controller.LTouch, ref _leftWallIntensity);
            _rightWallHapticsActive = ProcessHandHaptics(rightHandAnchor, OVRInput.Controller.RTouch, ref _rightWallIntensity);
            yield return wait;
        }
    }

    // Modified: Added smoothing + adjustable max + easing exponent.
    bool ProcessHandHaptics(Transform handAnchor, OVRInput.Controller controller, ref float currentSmoothedIntensity)
    {
        if (handAnchor == null) return false;

        int numColliders = Physics.OverlapSphereNonAlloc(handAnchor.position, detectionRadius, _nearbyWallsCache, wallLayer);

        if (numColliders > 0)
        {
            // Distance to closest wall surface
            float minDistance = _nearbyWallsCache
                .Take(numColliders)
                .Select(c => Vector3.Distance(c.ClosestPoint(handAnchor.position), handAnchor.position))
                .Min();

            // Normalize (0 = touching / inside, 1 = at or beyond maxHapticDistance)
            float normalized = Mathf.Clamp01(1f - (minDistance / Mathf.Max(0.0001f, maxHapticDistance)));

            // Ease-in / shaping
            if (wallIntensityRampExponent != 1f)
                normalized = Mathf.Pow(normalized, wallIntensityRampExponent);

            float targetIntensity = wallMaxIntensity * normalized;

            // Time-based smoothing: different rise/fall speeds
            float timeConstant = (targetIntensity > currentSmoothedIntensity) ? wallHapticRiseTime : wallHapticFallTime;
            float maxDelta = (wallMaxIntensity * hapticCheckInterval) / Mathf.Max(0.0001f, timeConstant);
            currentSmoothedIntensity = Mathf.MoveTowards(currentSmoothedIntensity, targetIntensity, maxDelta);

            // Cutoff small residuals
            if (currentSmoothedIntensity < wallHapticsMinCutoff)
                currentSmoothedIntensity = 0f;

            OVRInput.SetControllerVibration(wallHapticFrequency, currentSmoothedIntensity, controller);
            return currentSmoothedIntensity > 0f;
        }
        else
        {
            // No wall; decay toward 0
            if (currentSmoothedIntensity > 0f)
            {
                float maxDelta = (wallMaxIntensity * hapticCheckInterval) / Mathf.Max(0.0001f, wallHapticFallTime);
                currentSmoothedIntensity = Mathf.MoveTowards(currentSmoothedIntensity, 0f, maxDelta);
                if (currentSmoothedIntensity < wallHapticsMinCutoff)
                    currentSmoothedIntensity = 0f;

                if (currentSmoothedIntensity > 0f)
                {
                    OVRInput.SetControllerVibration(wallHapticFrequency, currentSmoothedIntensity, controller);
                    return true;
                }
            }

            bool isThisHandScanning =
                (controller == OVRInput.Controller.LTouch && leftConeScanner != null && leftConeScanner.CurrentTarget != null) ||
                (controller == OVRInput.Controller.RTouch && rightConeScanner != null && rightConeScanner.CurrentTarget != null);

            if (!isThisHandScanning)
            {
                OVRInput.SetControllerVibration(0, 0, controller);
            }
            return false;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (leftHandAnchor != null) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(leftHandAnchor.position, detectionRadius); }
        if (rightHandAnchor != null) { Gizmos.color = Color.blue; Gizmos.DrawWireSphere(rightHandAnchor.position, detectionRadius); }
    }
    #endregion
}