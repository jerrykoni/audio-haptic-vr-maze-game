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
    private Vector3 defaultConeRotation;
    private Transform defaultAttachPoint;      // remembers the controller
    private bool isAttachedToHeadset = false;  // current state

    void Awake()
    {
        if (attachPoint == null)
            Debug.LogError("Attach Point not assigned!", this);
        if (headsetTransform == null)
            Debug.LogError("Headset Transform not assigned!", this);

        // Remember initial values 
        defaultAttachPoint = attachPoint;
        defaultConeRotation = coneRotation;

        axisOffset = Quaternion.Euler(coneRotation);
        BuildVisualCone();
        BuildPhysicsCone();
        lastAngle = scanAngle;
        lastRange = scanRange;
    }

    void Update()
    {
        if (attachPoint == null) return;

        // Base rotation is always set first
        Quaternion baseRotation = attachPoint.rotation * axisOffset;

        // --- Visual Cone Logic ---
        if (visualGO != null)
        {
            // Set position and rotation from attach point
            visualGO.transform.SetPositionAndRotation(attachPoint.position, baseRotation);

            // Determine the cone's full length based on raycast hits
            float effRange = GetEffectiveRange();

            // If attached to headset, apply the visual offset
            if (isAttachedToHeadset)
            {
                // The direction the cone's tip points (its local Y-axis)
                Vector3 coneForward = visualGO.transform.up;

                // Move the cone's pivot forward by the offset amount
                visualGO.transform.position += coneForward * headsetVisualStartOffset;

                // Shorten the cone's length by the same amount so the base stays in place
                float visualLength = Mathf.Max(0, effRange - headsetVisualStartOffset);
                ScaleCone(visualGO.transform, visualLength);
            }
            else
            {
                // If not on headset, scale normally
                ScaleCone(visualGO.transform, effRange);
            }
        }
    }


    void FixedUpdate()
    {
        if (attachPoint == null) return;

        // --- Physics Cone Logic (remains unchanged) ---
        physGO.transform.SetPositionAndRotation(
            attachPoint.position,
            attachPoint.rotation * axisOffset);

        float effRange = GetEffectiveRange();
        ScaleCone(physGO.transform, effRange);

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
        candidates.Remove(other.gameObject);
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
        Collider[] inside = Physics.OverlapSphere(
            origin,
            0.01f,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
        return inside.Length > 0;
    }


    /// <summary>
    /// Call this (e.g. via your button event) to flip between controller & headset.
    /// </summary>
    public void ToggleAttachment()
    {
        isAttachedToHeadset = !isAttachedToHeadset; // Invert the state

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

        // Update the rotation offset for the new attachment
        axisOffset = Quaternion.Euler(coneRotation);
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