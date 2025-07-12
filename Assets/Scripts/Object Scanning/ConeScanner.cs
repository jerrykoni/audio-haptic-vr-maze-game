using System;
using System.Collections.Generic;
using UnityEngine;

public class ConeScanner : MonoBehaviour
{
    [Header("OVR Setup")]
    public OVRCameraRig cameraRig;
    public bool useRightHand = true;

    [Header("Cone Settings")]
    [Range(1f, 89f)] public float scanAngle = 30f;
    public float scanRange = 10f;
    [Range(3, 64)] public int resolution = 16;
    public LayerMask scannableLayer;

    [Header("Orientation")]
    [Tooltip("Rotation angles (X, Y, Z) to orient the cone from forward (Z) axis.")]
    public Vector3 coneRotation = Vector3.zero;    // angles in degrees for continuous adjustment

    [Header("Visuals")]
    public Material coneMaterial;

    // Fires when we gain a new closest object
    public event Action<GameObject> OnObjectDetected;
    // Fires when the cone stops overlapping the previously detected object
    public event Action<GameObject> OnObjectLost;

    // children
    private GameObject visualGO;
    private GameObject physGO;

    // physics state
    private readonly HashSet<GameObject> candidates = new();
    private GameObject currentTarget;
    private float lastAngle, lastRange;

    //Orientation offset
    private Quaternion axisOffset = Quaternion.identity;

    void Awake()
    {
        if (cameraRig == null)
            Debug.LogError("OVRCameraRig not assigned!", this);

        // Calculate axis offset from Euler angles for smooth rotation
        axisOffset = Quaternion.Euler(coneRotation);

        BuildVisualCone();
        BuildPhysicsCone();

        lastAngle = scanAngle;
        lastRange = scanRange;
    }

    void Update()
    {
        var ctrl = GetController();
        if (ctrl == null) return;

        // Recalculate axis offset from Euler angles for real-time adjustment
        axisOffset = Quaternion.Euler(coneRotation);

        // smooth visuals
        visualGO.transform.SetPositionAndRotation(ctrl.position, ctrl.rotation * axisOffset);

        if (!Mathf.Approximately(lastAngle, scanAngle) ||
            !Mathf.Approximately(lastRange, scanRange))
        {
            ScaleCone(visualGO.transform);
        }
    }

    void FixedUpdate()
    {
        var ctrl = GetController();
        if (ctrl == null) return;

        // physics at fixed rate
        physGO.transform.SetPositionAndRotation(ctrl.position, ctrl.rotation * axisOffset);

        if (!Mathf.Approximately(lastAngle, scanAngle) ||
            !Mathf.Approximately(lastRange, scanRange))
        {
            ScaleCone(physGO.transform);
            lastAngle = scanAngle;
            lastRange = scanRange;
        }
    }

    Transform GetController()
        => useRightHand
           ? cameraRig.rightHandAnchor
           : cameraRig.leftHandAnchor;

    // Called via TriggerRelay on physGO
    public void HandleTriggerEnter(Collider other)
    {
        if (IsValid(other.gameObject))
        {
            candidates.Add(other.gameObject);
            UpdateBestTarget();
        }
    }

    public void HandleTriggerExit(Collider other)
    {
        var go = other.gameObject;

        // if this was our current target, signal its loss
        if (go == currentTarget)
        {
            OnObjectLost?.Invoke(currentTarget);
            Debug.LogWarning($"Object lost: {currentTarget.name}");
        }

        // then remove it from candidates and recompute best
        if (candidates.Remove(go))
        {
            UpdateBestTarget();
        }
    }

    bool IsValid(GameObject go)
    {
        int bit = 1 << go.layer;
        return go.isStatic && (scannableLayer.value & bit) != 0;
    }

    void UpdateBestTarget()
    {
        GameObject best = null;
        float bestDist = float.MaxValue;
        Vector3 apex = physGO.transform.position;

        foreach (var go in candidates)
        {
            if (go == null) continue;
            var col = go.GetComponent<Collider>();
            if (col == null) continue;

            Vector3 pt = col.ClosestPoint(apex);
            float dSqr = (pt - apex).sqrMagnitude;
            if (dSqr < bestDist)
            {
                bestDist = dSqr;
                best = go;
            }
        }

        // if the best‐candidate changed, fire events
        if (best != currentTarget)
        {
            currentTarget = best;
            OnObjectDetected?.Invoke(currentTarget);
            Debug.LogWarning($"Object detected: {currentTarget?.name ?? "None"}");
        }
    }

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

        // attach relay so that triggers go back to us
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
}