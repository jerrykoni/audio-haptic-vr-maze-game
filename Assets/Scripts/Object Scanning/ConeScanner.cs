using System;
using System.Collections.Generic;
using UnityEngine;

public class ConeScanner : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The Transform that the cone will be attached to and follow.")]
    public Transform attachPoint;

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

    [Header("Visuals")]
    public Material coneMaterial;

    // Fires when we gain a new closest object
    public event Action<GameObject> OnObjectDetected;
    // Fires when the cone stops overlapping the previously detected object
    public event Action<GameObject> OnObjectLost;
    // Fires continuously with the current target and its distance
    public event Action<GameObject, float> OnObjectUpdated;

    private GameObject visualGO;
    private GameObject physGO;

    private readonly HashSet<GameObject> candidates = new();
    private GameObject currentTarget;
    private float lastAngle, lastRange;
    private Quaternion axisOffset = Quaternion.identity;

    void Awake()
    {
        if (attachPoint == null)
            Debug.LogError("Attach Point Transform has not been assigned!", this);

        axisOffset = Quaternion.Euler(coneRotation);
        BuildVisualCone();
        BuildPhysicsCone();
        lastAngle = scanAngle;
        lastRange = scanRange;
    }

    void Update()
    {
        if (attachPoint == null) return;

        visualGO.transform.SetPositionAndRotation(
            attachPoint.position,
            attachPoint.rotation * axisOffset);

        // shrink/extend to hit‐point or max
        float effRange = GetEffectiveRange();
        ScaleCone(visualGO.transform, effRange);
    }

    void FixedUpdate()
    {
        if (attachPoint == null) return;

        physGO.transform.SetPositionAndRotation(
            attachPoint.position,
            attachPoint.rotation * axisOffset);

        float effRange = GetEffectiveRange();
        ScaleCone(physGO.transform, effRange);

        // now run your scanning logic as before…
        UpdateBestTarget();
    }

    public void HandleTriggerEnter(Collider other)
    {
        var go = other.gameObject;
        string layerName = LayerMask.LayerToName(go.layer);
        Debug.Log($"[ConeScanner] TriggerEnter on '{go.name}' (layer={layerName}, static={go.isStatic})");

        if (!IsValid(go))
        {
            Debug.Log($"[ConeScanner] ➖ Ignored '{go.name}' (wrong layer or not static)");
            return;
        }

        candidates.Add(go);
        Debug.Log($"[ConeScanner] ➕ Added '{go.name}'. Count={candidates.Count}");
    }

    public void HandleTriggerExit(Collider other)
    {
        var go = other.gameObject;
        if (candidates.Remove(go))
            Debug.Log($"[ConeScanner] ➖ Removed '{go.name}'. Count={candidates.Count}");
    }

    bool IsValid(GameObject go)
    {
        int bit = 1 << go.layer;
        return (scannableLayer.value & bit) != 0;
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

        // This block handles the original OnObjectDetected and OnObjectLost events.
        // It only fires when the target *changes*. This logic is preserved.
        if (best != currentTarget)
        {
            if (currentTarget != null)
            {
                OnObjectLost?.Invoke(currentTarget);
            }

            currentTarget = best;

            if (currentTarget != null)
            {
                OnObjectDetected?.Invoke(currentTarget);
            }
        }

        // This new block fires OnObjectUpdated every frame there IS a target,
        // providing the continuous distance data needed for the haptics.
        if (currentTarget != null)
        {
            OnObjectUpdated?.Invoke(currentTarget, Mathf.Sqrt(bestDistSqr));
        }
    }

    float GetEffectiveRange()
    {
        // world‐space data
        Vector3 forwardWS = (attachPoint.rotation * axisOffset * Vector3.up).normalized;
        Vector3 origin = attachPoint.position;

        // 1) if buried in geometry, no beam at all
        if (IsInsideObstacle(origin))
            return 0f;

        // 2) gather all forward hits
        RaycastHit[] hits = Physics.RaycastAll(
            origin, forwardWS, scanRange,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        // 3) find nearest exit‐point
        float minDist = float.MaxValue;
        foreach (var h in hits)
            if (h.distance > 0f && h.distance < minDist)
                minDist = h.distance;

        // 4) clamp
        return (minDist < float.MaxValue)
            ? minDist
            : scanRange;
    }

    bool IsInsideObstacle(Vector3 origin)
    {
        // 1 cm sphere to see if we’re buried in any obstacle collider
        Collider[] inside = Physics.OverlapSphere(
            origin,
            0.01f,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
        return inside.Length > 0;
    }

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
        // Y‐axis of your unit cone is its length
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