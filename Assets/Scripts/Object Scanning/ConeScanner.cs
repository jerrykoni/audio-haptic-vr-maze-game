using System;
using System.Collections.Generic;
using UnityEngine;

public class ConeScanner : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The Transform that the cone will be attached to and follow.")]
    public Transform attachPoint;
    [Tooltip("Headset camera or head anchor transform.")]
    public Transform headsetTransform;

    [Header("Cone Settings")]
    [Range(1f, 89f)] public float scanAngle = 30f;
    public float scanRange = 10f;
    [Range(3, 64)] public int resolution = 16;
    public LayerMask scannableLayer;

    [Header("Obstacles")]
    [Tooltip("Which layers should block the scan beam?")]
    public LayerMask obstacleMask;

    [Header("Orientation")]
    [Tooltip("Local rotation angles (X, Y, Z) to apply as an offset to the attachPoint's rotation.")]
    public Vector3 coneRotation = Vector3.zero;
    public Vector3 headsetConeRotation = new(90f, 0f, 0f);

    [Header("Visuals")]
    public Material coneMaterial;
    [Tooltip("How far from the anchor to start drawing the cone when attached to the headset. This 'cuts' the tip off visually.")]
    public float headsetVisualStartOffset = 0.5f;

    // Scrape Audio (Angular Velocity-Based)
    [Header("Wall Scrape Audio (Angular)")]
    [Tooltip("Looping scrape/scratch clip.")]
    public AudioClip wallScrapeLoop;
    [Tooltip("Min angular speed (deg/sec) required before scrape starts.")]
    public float scrapeMinAngularSpeed = 20f;
    [Tooltip("Angular speed (deg/sec) that maps to max volume/pitch.")]
    public float scrapeMaxAngularSpeed = 180f;
    [Tooltip("Volume at min -> 0; at max -> scrapeMaxVolume.")]
    public float scrapeMaxVolume = 0.8f;
    [Tooltip("Pitch at min angular speed.")]
    public float scrapeMinPitch = 0.85f;
    [Tooltip("Pitch at max angular speed.")]
    public float scrapeMaxPitch = 1.25f;
    [Tooltip("Seconds to fade out after contact / angular speed lost.")]
    public float scrapeFadeOutTime = 0.25f;
    [Tooltip("Normalized (0 = apex, 1 = far end) axis position for scrape audio child.")]
    [Range(0f, 1f)] public float scrapeAudioAxisPosition = 1f;
    [Tooltip("If true, wall scrape audio is muted while a scannable target is currently detected.")]
    public bool suppressScrapeWhileTargetLocked = true;

    // Events
    public event Action<GameObject> OnObjectDetected;
    public event Action<GameObject> OnObjectLost;
    public event Action<GameObject, float> OnObjectUpdated;

    public GameObject CurrentTarget => currentTarget;

    private GameObject visualGO;
    private GameObject physGO;

    private readonly HashSet<GameObject> candidates = new();
    private readonly Collider[] _obstacleCheckCache = new Collider[1];
    private GameObject currentTarget;
    private Quaternion axisOffset = Quaternion.identity;
    private Vector3 defaultConeRotation;
    private Transform defaultAttachPoint;
    private bool isAttachedToHeadset = false;

    // Scrape runtime state
    private AudioSource scrapeAudio;
    private Transform scrapeAudioTransform;
    private int wallContacts = 0;
    private float targetScrapeVolume = 0f;

    // Angular velocity tracking
    private Quaternion lastRot;
    private float smoothedAngularSpeed;
    [Tooltip("Exponential smoothing factor for angular speed (0 = none, higher = faster response).")]
    public float angularSpeedSmoothing = 10f;

    // Effective length for reference (physics cone)
    private float currentConeLength = 0f;

    void Awake()
    {
        if (attachPoint == null)
            Debug.LogError("Attach Point not assigned!", this);
        if (headsetTransform == null)
            Debug.LogError("Headset Transform not assigned!", this);

        defaultAttachPoint = attachPoint;
        defaultConeRotation = coneRotation;

        axisOffset = Quaternion.Euler(coneRotation);
        BuildVisualCone();
        BuildPhysicsCone();

        lastRot = attachPoint != null ? (attachPoint.rotation * axisOffset) : transform.rotation;

        if (wallScrapeLoop != null)
        {
            var audioChild = new GameObject("ScrapeAudio");
            audioChild.transform.SetParent(physGO.transform, false);
            scrapeAudioTransform = audioChild.transform;
            scrapeAudioTransform.localPosition = new Vector3(0f, scrapeAudioAxisPosition, 0f);
            scrapeAudio = audioChild.AddComponent<AudioSource>();
            scrapeAudio.clip = wallScrapeLoop;
            scrapeAudio.loop = true;
            scrapeAudio.playOnAwake = false;
            scrapeAudio.spatialBlend = 1f;
            scrapeAudio.volume = 0f;
        }
    }

    void Update()
    {
        if (attachPoint == null) return;

        Quaternion baseRotation = attachPoint.rotation * axisOffset;

        if (visualGO != null)
        {
            visualGO.transform.SetPositionAndRotation(attachPoint.position, baseRotation);

            float effRange = GetEffectiveRange();

            if (isAttachedToHeadset)
            {
                Vector3 coneForward = visualGO.transform.up;
                visualGO.transform.position += coneForward * headsetVisualStartOffset;
                float visualLength = Mathf.Max(0, effRange - headsetVisualStartOffset);
                ScaleCone(visualGO.transform, visualLength);
            }
            else
            {
                ScaleCone(visualGO.transform, effRange);
            }
        }
    }

    void FixedUpdate()
    {
        if (attachPoint == null) return;

        Quaternion currentRot = attachPoint.rotation * axisOffset;
        physGO.transform.SetPositionAndRotation(attachPoint.position, currentRot);

        float effRange = GetEffectiveRange();
        currentConeLength = effRange;
        ScaleCone(physGO.transform, effRange);

        Quaternion delta = currentRot * Quaternion.Inverse(lastRot);
        delta.ToAngleAxis(out float deltaAngleDeg, out _);
        if (deltaAngleDeg > 180f) deltaAngleDeg = 360f - deltaAngleDeg;
        float angularSpeedDegPerSec = deltaAngleDeg / Time.fixedDeltaTime;

        float lerpFactor = 1f - Mathf.Exp(-angularSpeedSmoothing * Time.fixedDeltaTime);
        smoothedAngularSpeed = Mathf.Lerp(smoothedAngularSpeed, angularSpeedDegPerSec, lerpFactor);

        UpdateBestTarget();
        UpdateScrapeAudioFromAngular(smoothedAngularSpeed);

        if (scrapeAudioTransform != null)
            scrapeAudioTransform.localPosition = new Vector3(0f, Mathf.Clamp01(scrapeAudioAxisPosition), 0f);

        lastRot = currentRot;
    }

    #region Trigger Handling
    public void HandleTriggerEnter(Collider other)
    {
        if (IsObstacle(other.gameObject))
            wallContacts++;

        if (IsValid(other.gameObject))
            candidates.Add(other.gameObject);
    }

    public void HandleTriggerExit(Collider other)
    {
        if (IsObstacle(other.gameObject))
            wallContacts = Mathf.Max(0, wallContacts - 1);

        candidates.Remove(other.gameObject);
    }

    public void HandleTriggerStay(Collider other) { }
    #endregion

    bool IsValid(GameObject go)
    {
        int bit = 1 << go.layer;
        return (scannableLayer.value & bit) != 0;
    }

    bool IsObstacle(GameObject go)
    {
        int bit = 1 << go.layer;
        return (obstacleMask.value & bit) != 0;
    }

    void UpdateBestTarget()
    {
        GameObject best = null;
        float bestDistSqr = float.MaxValue;
        Vector3 apex = physGO.transform.position;

        candidates.RemoveWhere(go => go == null);

        foreach (var go in candidates)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) continue;

            Vector3 pt = col.ClosestPoint(apex);
            float dSqr = (pt - apex).sqrMagnitude;
            if (dSqr < bestDistSqr)
            {
                bestDistSqr = dSqr;
                best = go;
            }
        }

        if (best != currentTarget)
        {
            if (currentTarget != null)
                OnObjectLost?.Invoke(currentTarget);

            currentTarget = best;

            if (currentTarget != null)
                OnObjectDetected?.Invoke(currentTarget);
        }

        if (currentTarget != null)
            OnObjectUpdated?.Invoke(currentTarget, Mathf.Sqrt(bestDistSqr));
    }

    float GetEffectiveRange()
    {
        Vector3 forwardWS = (attachPoint.rotation * axisOffset * Vector3.up).normalized;
        Vector3 origin = attachPoint.position;

        if (IsInsideObstacle(origin))
            return 0f;

        RaycastHit[] hits = Physics.RaycastAll(
            origin, forwardWS, scanRange,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        float minDist = float.MaxValue;
        foreach (var h in hits)
            if (h.distance > 0f && h.distance < minDist)
                minDist = h.distance;

        return (minDist < float.MaxValue) ? minDist : scanRange;
    }

    bool IsInsideObstacle(Vector3 origin)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            0.01f,
            _obstacleCheckCache,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
        return hitCount > 0;
    }

    public void ToggleAttachment()
    {
        isAttachedToHeadset = !isAttachedToHeadset;

        if (isAttachedToHeadset)
        {
            attachPoint = headsetTransform;
            coneRotation = headsetConeRotation;
        }
        else
        {
            attachPoint = defaultAttachPoint;
            coneRotation = defaultConeRotation;
        }

        axisOffset = Quaternion.Euler(coneRotation);
        lastRot = attachPoint.rotation * axisOffset;
    }

    void OnDisable()
    {
        if (currentTarget != null)
            OnObjectLost?.Invoke(currentTarget);
        if (scrapeAudio != null && scrapeAudio.isPlaying)
            scrapeAudio.Stop();
    }

    #region Scrape Audio (Angular)
    void UpdateScrapeAudioFromAngular(float angularSpeedDegPerSec)
    {
        if (scrapeAudio == null || wallScrapeLoop == null)
            return;

        // Suppress while target locked if enabled
        if (suppressScrapeWhileTargetLocked && currentTarget != null)
        {
            FadeOutScrape();
            return;
        }

        bool shouldScrape = wallContacts > 0 && angularSpeedDegPerSec >= scrapeMinAngularSpeed;

        if (shouldScrape)
        {
            float t = Mathf.InverseLerp(scrapeMinAngularSpeed, scrapeMaxAngularSpeed, angularSpeedDegPerSec);
            targetScrapeVolume = scrapeMaxVolume * t;
            float targetPitch = Mathf.Lerp(scrapeMinPitch, scrapeMaxPitch, t);

            if (!scrapeAudio.isPlaying)
            {
                scrapeAudio.volume = 0f;
                scrapeAudio.Play();
            }

            scrapeAudio.volume = Mathf.MoveTowards(
                scrapeAudio.volume,
                targetScrapeVolume,
                Time.fixedDeltaTime * (scrapeMaxVolume / 0.1f));

            scrapeAudio.pitch = Mathf.MoveTowards(
                scrapeAudio.pitch,
                targetPitch,
                Time.fixedDeltaTime * 5f);
        }
        else
        {
            FadeOutScrape();
        }
    }

    void FadeOutScrape()
    {
        if (scrapeAudio == null || !scrapeAudio.isPlaying) return;

        float fadeDelta = (scrapeMaxVolume / Mathf.Max(0.01f, scrapeFadeOutTime)) * Time.fixedDeltaTime;
        scrapeAudio.volume = Mathf.Max(0f, scrapeAudio.volume - fadeDelta);
        if (scrapeAudio.volume <= 0.0001f)
        {
            scrapeAudio.Stop();
            scrapeAudio.volume = 0f;
        }
        targetScrapeVolume = 0f;
    }
    #endregion

    #region Cone Generation
    void BuildVisualCone()
    {
        visualGO = new GameObject("ConeVisual");
        visualGO.transform.SetParent(transform, false);

        var mf = visualGO.AddComponent<MeshFilter>();
        var mr = visualGO.AddComponent<MeshRenderer>();
        mf.mesh = BuildUnitConeMesh();
        mr.material = coneMaterial
            ? coneMaterial
            : new Material(Shader.Find("Unlit/Color"))
            {
                color = new Color(1, 1, 1, 0.15f)
            };

        ScaleCone(visualGO.transform, GetEffectiveRange());
    }

    void BuildPhysicsCone()
    {
        physGO = new GameObject("ConePhysics");
        physGO.transform.SetParent(transform, false);

        var mf = physGO.AddComponent<MeshFilter>();
        var mc = physGO.AddComponent<MeshCollider>();
        mc.convex = true;
        mc.isTrigger = true;
        mf.mesh = BuildUnitConeMesh();
        mc.sharedMesh = mf.mesh;

        var rb = physGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var relay = physGO.AddComponent<TriggerRelay>();
        relay.scanner = this;

        ScaleCone(physGO.transform, GetEffectiveRange());
    }

    void ScaleCone(Transform t, float range)
    {
        float radius = Mathf.Tan(scanAngle * Mathf.Deg2Rad) * range;
        t.localScale = new Vector3(radius, range, radius);
    }

    Mesh BuildUnitConeMesh()
    {
        var mesh = new Mesh();
        var verts = new List<Vector3> { Vector3.zero };
        var tris = new List<int>();

        for (int i = 0; i <= resolution; i++)
        {
            float ang = 2 * Mathf.PI * i / resolution;
            verts.Add(new Vector3(Mathf.Cos(ang), 1, Mathf.Sin(ang)));
        }
        for (int i = 1; i <= resolution; i++)
        {
            tris.Add(0); tris.Add(i); tris.Add(i + 1);
        }
        int bc = verts.Count;
        verts.Add(new Vector3(0, 1, 0));
        for (int i = 1; i <= resolution; i++)
        {
            tris.Add(bc); tris.Add(i + 1); tris.Add(i);
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }
    #endregion
}