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

    [Header("Grouping")]
    [Tooltip("Layers whose colliders should be merged and treated as one logical scannable object (e.g. Walls).")]
    public LayerMask mergedScannableLayers;
    [Tooltip("Tag assigned to any merged layer proxy (ensure tag exists in Tag Manager).")]
    public string mergedLayerProxyTag = "Wall";

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
    [Tooltip("Master switch: completely enable/disable wall scrape system.")]
    public bool enableScrapeAudio = true;
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

    // Effective length (physics cone)
    private float currentConeLength = 0f;

    // Track obstacle overlaps explicitly
    private readonly HashSet<Collider> obstacleOverlapCols = new();

    // Merged layer bookkeeping
    private readonly Dictionary<int, int> mergedLayerRefCounts = new();
    private readonly Dictionary<int, HashSet<Collider>> mergedLayerColliderSets = new();
    private readonly Dictionary<int, GameObject> mergedLayerProxies = new();
    private readonly Dictionary<int, Vector3> mergedLayerClosestPoints = new();

    // New: track entry order (larger = more recent)
    private readonly Dictionary<GameObject, ulong> candidateOrder = new();
    private ulong candidateSequence = 0;

    // Proxy marker
    public class MergedLayerProxy : MonoBehaviour
    {
        public int layerIndex;
        public ConeScanner owner;
    }

    // Public accessors for dynamic cone length
    public float CurrentEffectiveConeLength => currentConeLength;
    public float CurrentEffectiveConeLengthNormalized => scanRange > 0f ? currentConeLength / scanRange : 0f;

    // Audio positioning helpers
    public Vector3 ConeApexWorldPosition => (physGO != null) ? physGO.transform.position : (attachPoint != null ? attachPoint.position : transform.position);
    public Vector3 ConeAxisDirection => (attachPoint == null)
        ? transform.up
        : (attachPoint.rotation * axisOffset * Vector3.up).normalized;
    public Vector3 ConeEdgeWorldPosition => ConeApexWorldPosition + ConeAxisDirection * currentConeLength;
    public Vector3 GetConePointAtFraction(float t)
    {
        t = Mathf.Clamp01(t);
        return ConeApexWorldPosition + ConeAxisDirection * (currentConeLength * t);
    }

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

        EnsureScrapeAudio();
    }

    void OnEnable()
    {
        if (physGO != null && attachPoint != null)
            ReacquireOverlaps();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
            EnsureScrapeAudio();
    }
#endif

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

        if (enableScrapeAudio)
            UpdateScrapeAudioFromAngular(smoothedAngularSpeed);

        if (enableScrapeAudio && scrapeAudioTransform != null)
            scrapeAudioTransform.localPosition = new Vector3(0f, Mathf.Clamp01(scrapeAudioAxisPosition), 0f);

        lastRot = currentRot;
    }

    #region Trigger Handling
    public void HandleTriggerEnter(Collider other) => ProcessColliderEnter(other);
    public void HandleTriggerExit(Collider other) => ProcessColliderExit(other);
    public void HandleTriggerStay(Collider other) { }

    void ProcessColliderEnter(Collider other)
    {
        GameObject go = other.gameObject;

        if (IsObstacle(go))
        {
            if (obstacleOverlapCols.Add(other))
                wallContacts = obstacleOverlapCols.Count;
        }

        if (!IsValid(go)) return;

        int layer = go.layer;
        if (IsMergedLayer(layer))
        {
            if (!mergedLayerRefCounts.ContainsKey(layer))
                mergedLayerRefCounts[layer] = 0;
            mergedLayerRefCounts[layer]++;

            if (!mergedLayerColliderSets.TryGetValue(layer, out var set))
            {
                set = new HashSet<Collider>();
                mergedLayerColliderSets[layer] = set;
            }
            set.Add(other);

            if (mergedLayerRefCounts[layer] == 1)
            {
                GameObject proxy = GetOrCreateMergedProxy(layer);
                candidates.Add(proxy);
                RegisterCandidate(proxy);
            }
        }
        else
        {
            if (candidates.Add(go))
                RegisterCandidate(go);
        }
    }

    void ProcessColliderExit(Collider other)
    {
        GameObject go = other.gameObject;

        if (IsObstacle(go))
        {
            if (obstacleOverlapCols.Remove(other))
                wallContacts = obstacleOverlapCols.Count;
        }

        if (!IsValid(go)) return;

        int layer = go.layer;
        if (IsMergedLayer(layer))
        {
            if (mergedLayerRefCounts.TryGetValue(layer, out int count))
            {
                count--;
                if (count <= 0)
                {
                    mergedLayerRefCounts[layer] = 0;
                    if (mergedLayerProxies.TryGetValue(layer, out var proxy))
                    {
                        candidates.Remove(proxy);
                        candidateOrder.Remove(proxy);
                    }
                    if (mergedLayerColliderSets.TryGetValue(layer, out var set))
                        set.Clear();
                    mergedLayerClosestPoints.Remove(layer);
                }
                else
                {
                    mergedLayerRefCounts[layer] = count;
                    if (mergedLayerColliderSets.TryGetValue(layer, out var set))
                        set.Remove(other);
                }
            }
        }
        else
        {
            if (candidates.Remove(go))
                candidateOrder.Remove(go);
        }
    }
    #endregion

    void RegisterCandidate(GameObject go)
    {
        // Record order only once
        if (!candidateOrder.ContainsKey(go))
            candidateOrder[go] = ++candidateSequence;
    }

    bool IsValid(GameObject go) => (scannableLayer.value & (1 << go.layer)) != 0;
    bool IsObstacle(GameObject go) => (obstacleMask.value & (1 << go.layer)) != 0;
    bool IsMergedLayer(int layer) => (mergedScannableLayers.value & (1 << layer)) != 0;

    GameObject GetOrCreateMergedProxy(int layer)
    {
        if (mergedLayerProxies.TryGetValue(layer, out var existing))
            return existing;

        var proxy = new GameObject($"MergedLayer_{LayerMaskToName(layer)}");
        proxy.transform.SetParent(transform, false);
        if (!string.IsNullOrWhiteSpace(mergedLayerProxyTag))
        {
            try { proxy.tag = mergedLayerProxyTag; } catch { }
        }

        var marker = proxy.AddComponent<MergedLayerProxy>();
        marker.layerIndex = layer;
        marker.owner = this;
        mergedLayerProxies[layer] = proxy;
        return proxy;
    }

    string LayerMaskToName(int layer)
    {
        string n = LayerMask.LayerToName(layer);
        return string.IsNullOrEmpty(n) ? layer.ToString() : n;
    }

    void UpdateBestTarget()
    {
        GameObject best = null;
        ulong bestOrder = 0;
        float bestDistSqrForTieBreak = float.MaxValue;
        Vector3 apex = physGO != null ? physGO.transform.position : transform.position;

        // Clean up destroyed
        candidates.RemoveWhere(go => go == null);

        foreach (var go in candidates)
        {
            if (!candidateOrder.TryGetValue(go, out var order))
                continue;

            // If order is better, adopt immediately.
            // If equal order (extremely rare) tie-break by distance like before.
            if (order > bestOrder || (order == bestOrder && order != 0))
            {
                float distSqr = GetDistanceSqrToApex(go, apex);
                if (order > bestOrder || distSqr < bestDistSqrForTieBreak)
                {
                    bestOrder = order;
                    bestDistSqrForTieBreak = distSqr;
                    best = go;
                }
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

        // Maintain merged proxy positioning for audio alignment
        if (currentTarget != null)
        {
            var merged = currentTarget.GetComponent<MergedLayerProxy>();
            if (merged != null && mergedLayerClosestPoints.TryGetValue(merged.layerIndex, out var pos))
                currentTarget.transform.position = pos;
        }

        if (currentTarget != null && bestDistSqrForTieBreak < float.MaxValue)
            OnObjectUpdated?.Invoke(currentTarget, Mathf.Sqrt(bestDistSqrForTieBreak));
    }

    float GetDistanceSqrToApex(GameObject go, Vector3 apex)
    {
        var merged = go.GetComponent<MergedLayerProxy>();
        if (merged != null)
        {
            float dSqr = GetMergedLayerClosestPointDistSqr(merged.layerIndex, apex, out Vector3 cp);
            if (!float.IsPositiveInfinity(dSqr))
                mergedLayerClosestPoints[merged.layerIndex] = cp;
            return dSqr;
        }
        var col = go.GetComponent<Collider>();
        if (col == null) return float.MaxValue;
        Vector3 pt = col.ClosestPoint(apex);
        return (pt - apex).sqrMagnitude;
    }

    float GetMergedLayerClosestPointDistSqr(int layer, Vector3 apex, out Vector3 closestPoint)
    {
        closestPoint = apex;
        if (!mergedLayerColliderSets.TryGetValue(layer, out var set) || set.Count == 0)
            return float.PositiveInfinity;

        float best = float.MaxValue;
        foreach (var col in set)
        {
            if (col == null) continue;
            Vector3 pt = col.ClosestPoint(apex);
            float dSqr = (pt - apex).sqrMagnitude;
            if (dSqr < best)
            {
                best = dSqr;
                closestPoint = pt;
            }
        }
        return best;
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

        currentTarget = null;
        candidates.Clear();
        obstacleOverlapCols.Clear();
        wallContacts = 0;

        mergedLayerRefCounts.Clear();
        mergedLayerColliderSets.Clear();
        mergedLayerClosestPoints.Clear();
        foreach (var kvp in mergedLayerProxies)
            if (kvp.Value != null)
                Destroy(kvp.Value);
        mergedLayerProxies.Clear();

        candidateOrder.Clear();
        candidateSequence = 0;

        if (scrapeAudio != null && scrapeAudio.isPlaying)
            scrapeAudio.Stop();
    }

    #region Scrape Audio (Angular)
    void UpdateScrapeAudioFromAngular(float angularSpeedDegPerSec)
    {
        if (!enableScrapeAudio) return;
        if (scrapeAudio == null || wallScrapeLoop == null)
            return;

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

    #region Scrape Audio Enable/Disable
    void EnsureScrapeAudio()
    {
        if (!enableScrapeAudio)
        {
            if (scrapeAudioTransform != null)
            {
                if (scrapeAudio != null && scrapeAudio.isPlaying)
                    scrapeAudio.Stop();
                Destroy(scrapeAudioTransform.gameObject);
            }
            scrapeAudio = null;
            scrapeAudioTransform = null;
            return;
        }

        if (scrapeAudio == null && wallScrapeLoop != null && physGO != null)
        {
            var audioChild = new GameObject("ScrapeAudio");
            audioChild.transform.SetParent(physGO.transform, false);
            scrapeAudioTransform = audioChild.transform;
            scrapeAudioTransform.localPosition = new Vector3(0f, Mathf.Clamp01(scrapeAudioAxisPosition), 0f);
            scrapeAudio = audioChild.AddComponent<AudioSource>();
            scrapeAudio.clip = wallScrapeLoop;
            scrapeAudio.loop = true;
            scrapeAudio.playOnAwake = false;
            scrapeAudio.spatialBlend = 1f;
            scrapeAudio.volume = 0f;
        }
    }

    public void SetScrapeEnabled(bool enabled)
    {
        if (enableScrapeAudio == enabled) return;
        enableScrapeAudio = enabled;
        EnsureScrapeAudio();
        if (enabled)
            ReacquireOverlaps();
    }
    #endregion

    #region Reacquire Overlaps
    void ReacquireOverlaps()
    {
        candidates.Clear();
        obstacleOverlapCols.Clear();
        wallContacts = 0;
        currentTarget = null;
        mergedLayerRefCounts.Clear();
        mergedLayerColliderSets.Clear();
        mergedLayerClosestPoints.Clear();
        foreach (var kvp in mergedLayerProxies)
            if (kvp.Value != null)
                Destroy(kvp.Value);
        mergedLayerProxies.Clear();
        candidateOrder.Clear();
        candidateSequence = 0;

        if (physGO == null) return;

        Vector3 apex = physGO.transform.position;
        Vector3 axisDir = (attachPoint.rotation * axisOffset * Vector3.up).normalized;

        int mask = scannableLayer.value | obstacleMask.value;
        Collider[] hits = Physics.OverlapSphere(apex, scanRange, mask, QueryTriggerInteraction.Collide);

        foreach (var col in hits)
        {
            if (col == null) continue;
            GameObject go = col.gameObject;

            Vector3 center = col.bounds.center;
            Vector3 toCenter = center - apex;
            float dist = toCenter.magnitude;
            if (dist <= 0.0001f) continue;
            if (dist > scanRange) continue;

            float ang = Vector3.Angle(axisDir, toCenter);
            if (ang > scanAngle + 0.5f) continue;

            ProcessColliderEnter(col);
        }

        UpdateBestTarget();
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