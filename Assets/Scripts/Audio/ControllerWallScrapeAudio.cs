using UnityEngine;

[DisallowMultipleComponent]
public class ControllerWallScrapeAudio : MonoBehaviour
{
    [Header("Controller References")]
    public Transform leftController;
    public Transform rightController;

    [Header("Wall Detection")]
    [Tooltip("Layers considered 'walls' for scrape detection.")]
    public LayerMask wallLayer;
    [Tooltip("Radius of the detection sphere around each controller.")]
    public float detectionRadius = 0.12f;

    [Header("Velocity Thresholds (m/s)")]
    [Tooltip("Minimum linear speed to begin/maintain scraping.")]
    public float minScrapeVelocity = 0.05f;
    [Tooltip("Velocity mapped to max volume/pitch.")]
    public float maxScrapeVelocity = 1.4f;

    [Header("Audio")]
    [Tooltip("Looping scrape/scratch clip.")]
    public AudioClip scrapeLoopClip;
    [Range(0f, 1f)] public float maxVolume = 0.85f;
    [Tooltip("Pitch when just above min velocity.")]
    public float minPitch = 0.85f;
    [Tooltip("Pitch when at or above max velocity.")]
    public float maxPitch = 1.25f;
    [Tooltip("Seconds to fade in to target volume.")]
    public float fadeInTime = 0.15f;
    [Tooltip("Seconds to fade out when leaving wall or slowing down.")]
    public float fadeOutTime = 0.25f;
    [Tooltip("Extra multiplier applied by proximity (center vs edge of sphere).")]
    public float proximityVolumeWeight = 1.0f;

    [Header("Smoothing")]
    [Tooltip("Exponential smoothing (higher = snappier).")]
    public float velocitySmoothing = 14f;
    [Tooltip("Exponential smoothing for proximity factor.")]
    public float proximitySmoothing = 12f;

    [Header("Debug / Gizmos")]
    public bool drawGizmos = true;
    public Color leftColor = new Color(0.2f, 0.6f, 1f, 0.35f);
    public Color rightColor = new Color(1f, 0.4f, 0.2f, 0.35f);

    private class HandState
    {
        public Transform t;
        public Vector3 lastPos;
        public bool initialized;
        public float smoothedSpeed;
        public float smoothedProximity;
        public AudioSource audio;
        public float currentTargetVolume;
        public float fadeVelocity; // for SmoothDamp if needed (unused now but reserved)
    }

    private HandState _left = new();
    private HandState _right = new();

    // Reusable collider caches to avoid GC
    private readonly Collider[] _overlapCacheLeft = new Collider[16];
    private readonly Collider[] _overlapCacheRight = new Collider[16];

    void Awake()
    {
        InitHand(_left, "LeftScrapeAudio");
        InitHand(_right, "RightScrapeAudio");
    }

    public void SetControllers(Transform left, Transform right)
    {
        leftController = left;
        rightController = right;
    }

    void InitHand(HandState hs, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(transform, false);
        hs.audio = child.AddComponent<AudioSource>();
        hs.audio.playOnAwake = false;
        hs.audio.loop = true;
        hs.audio.spatialBlend = 1f;
        hs.audio.volume = 0f;
        hs.audio.clip = scrapeLoopClip;
    }

    void Update()
    {
        _left.t = leftController;
        _right.t = rightController;

        ProcessHand(_left, _overlapCacheLeft);
        ProcessHand(_right, _overlapCacheRight);
    }

    void ProcessHand(HandState hs, Collider[] cache)
    {
        if (hs.t == null) return;

        Vector3 pos = hs.t.position;

        // Initialize last position
        if (!hs.initialized)
        {
            hs.lastPos = pos;
            hs.smoothedSpeed = 0f;
            hs.smoothedProximity = 0f;
            hs.initialized = true;
        }

        // Velocity
        Vector3 rawVel = (pos - hs.lastPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        float rawSpeed = rawVel.magnitude;
        float speedLerp = 1f - Mathf.Exp(-velocitySmoothing * Time.deltaTime);
        hs.smoothedSpeed = Mathf.Lerp(hs.smoothedSpeed, rawSpeed, speedLerp);
        hs.lastPos = pos;

        // Wall proximity detection
        int hitCount = Physics.OverlapSphereNonAlloc(pos, detectionRadius, cache, wallLayer, QueryTriggerInteraction.Collide);
        float proximity = 0f;
        if (hitCount > 0)
        {
            float closest = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = cache[i];
                if (col == null) continue;
                Vector3 closestPoint = col.ClosestPoint(pos);
                float d = Vector3.Distance(closestPoint, pos);
                if (d < closest) closest = d;
            }
            if (closest <= detectionRadius)
            {
                proximity = 1f - Mathf.Clamp01(closest / detectionRadius);
            }
        }

        float proxLerp = 1f - Mathf.Exp(-proximitySmoothing * Time.deltaTime);
        hs.smoothedProximity = Mathf.Lerp(hs.smoothedProximity, proximity, proxLerp);

        bool shouldScrape = (hs.smoothedSpeed >= minScrapeVelocity) && (hs.smoothedProximity > 0.01f) && scrapeLoopClip != null;

        if (shouldScrape)
        {
            if (!hs.audio.isPlaying)
            {
                hs.audio.clip = scrapeLoopClip; // In case changed at runtime
                hs.audio.volume = 0f;
                hs.audio.pitch = minPitch;
                hs.audio.Play();
            }

            float velT = Mathf.InverseLerp(minScrapeVelocity, maxScrapeVelocity, hs.smoothedSpeed);
            float targetVol = maxVolume * velT;

            if (proximityVolumeWeight > 0f)
                targetVol *= Mathf.Lerp(1f, hs.smoothedProximity, proximityVolumeWeight);

            // Fade in / approach target
            float fadeInRate = (maxVolume / Mathf.Max(0.01f, fadeInTime)) * Time.deltaTime;
            hs.audio.volume = Mathf.MoveTowards(hs.audio.volume, targetVol, fadeInRate);

            float targetPitch = Mathf.Lerp(minPitch, maxPitch, velT);
            hs.audio.pitch = Mathf.MoveTowards(hs.audio.pitch, targetPitch, Time.deltaTime * 5f);
        }
        else
        {
            // Fade out
            if (hs.audio.isPlaying)
            {
                float fadeOutRate = (maxVolume / Mathf.Max(0.01f, fadeOutTime)) * Time.deltaTime;
                hs.audio.volume = Mathf.MoveTowards(hs.audio.volume, 0f, fadeOutRate);
                if (hs.audio.volume <= 0.0001f)
                {
                    hs.audio.Stop();
                    hs.audio.volume = 0f;
                }
            }
        }

        // Keep audio source at controller position
        hs.audio.transform.position = pos;
    }

    void OnValidate()
    {
        if (minScrapeVelocity < 0f) minScrapeVelocity = 0f;
        if (maxScrapeVelocity < minScrapeVelocity + 0.01f)
            maxScrapeVelocity = minScrapeVelocity + 0.01f;
        detectionRadius = Mathf.Max(0.001f, detectionRadius);
        fadeInTime = Mathf.Max(0.01f, fadeInTime);
        fadeOutTime = Mathf.Max(0.01f, fadeOutTime);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        if (leftController != null)
        {
            Gizmos.color = leftColor;
            Gizmos.DrawWireSphere(leftController.position, detectionRadius);
        }
        if (rightController != null)
        {
            Gizmos.color = rightColor;
            Gizmos.DrawWireSphere(rightController.position, detectionRadius);
        }
    }
}