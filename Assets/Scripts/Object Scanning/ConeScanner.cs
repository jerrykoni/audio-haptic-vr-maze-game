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
    public LayerMask obstacleLayer;

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

        axisOffset = Quaternion.Euler(coneRotation);
        visualGO.transform.SetPositionAndRotation(attachPoint.position, attachPoint.rotation * axisOffset);

        if (!Mathf.Approximately(lastAngle, scanAngle) || !Mathf.Approximately(lastRange, scanRange))
        {
            ScaleCone(visualGO.transform);
        }
    }

    void FixedUpdate()
    {
        if (attachPoint == null) return;

        physGO.transform.SetPositionAndRotation(attachPoint.position, attachPoint.rotation * axisOffset);

        if (!Mathf.Approximately(lastAngle, scanAngle) || !Mathf.Approximately(lastRange, scanRange))
        {
            ScaleCone(physGO.transform);
            lastAngle = scanAngle;
            lastRange = scanRange;
        }

        // This is now called every fixed update to check for target changes and provide distance updates
        UpdateBestTarget();
    }

    public void HandleTriggerEnter(Collider other)
    {
        if (IsValid(other.gameObject))
        {
            candidates.Add(other.gameObject);
        }
    }

    public void HandleTriggerExit(Collider other)
    {
        // When an object leaves the trigger, it's no longer a candidate.
        // UpdateBestTarget in FixedUpdate will handle the logic of losing the target.
        candidates.Remove(other.gameObject);
    }

    bool IsValid(GameObject go)
    {
        int bit = 1 << go.layer;
        return go.isStatic && (scannableLayer.value & bit) != 0;
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
            Vector3 directionToTarget = pt - apex;
            float dSqr = directionToTarget.sqrMagnitude;

            // Line-of-sight check
            if (Physics.Raycast(apex, directionToTarget.normalized, out RaycastHit hit, scanRange, obstacleLayer))
            {
                // If the raycast hits something on the obstacle layer before it hits our target,
                // then the target is blocked. We can check this by comparing the squared distances.
                if (hit.distance * hit.distance < dSqr)
                {
                    continue; // This target is blocked, so skip to the next one.
                }
            }

            if (dSqr < bestDistSqr)
            {
                bestDistSqr = dSqr;
                best = go;
            }
        }

        // --- The rest of the method remains the same ---

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

        if (currentTarget != null)
        {
            OnObjectUpdated?.Invoke(currentTarget, Mathf.Sqrt(bestDistSqr));
        }
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

        ScaleCone(visualGO.transform);
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

        ScaleCone(physGO.transform);
    }

    void ScaleCone(Transform t)
    {
        float radius = Mathf.Tan(scanAngle * Mathf.Deg2Rad) * scanRange;
        t.localScale = new Vector3(radius, scanRange, radius);
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