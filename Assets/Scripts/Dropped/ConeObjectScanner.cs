using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConeObjectScanner : MonoBehaviour
{
    [Header("Cone Configuration")]
    [SerializeField] private float coneRange = 10f;
    [SerializeField] private float coneAngle = 45f;
    [SerializeField] private LayerMask targetLayers = -1;
    [SerializeField] private string[] targetTags = { "Static", "Interactable" };

    [Header("Performance Settings")]
    [SerializeField] private float scanInterval = 0.1f; // Scan every 100ms for better performance
    [SerializeField] private int maxObjectsToCheck = 50; // Limit objects checked per frame

    [Header("Detection Settings")]
    [SerializeField] private bool useColliderBasedDetection = true;
    [SerializeField] private float colliderDistanceTolerance = 0.1f; // Distance tolerance for collider intersection

    [Header("Visual Debug")]
    [SerializeField] private bool showDebugVisualization = true;
    [SerializeField] private bool showRuntimeVisualization = true;
    [SerializeField] private int visualizationSegments = 20;
    [SerializeField] private Material coneMaterial;
    [SerializeField] private Material hitMaterial;
    [SerializeField] private Material centerMaterial;
    [SerializeField] private float lineWidth = 0.02f;

    [Header("Results")]
    [SerializeField] private GameObject closestObject;
    [SerializeField] private float closestDistance;

    // Performance optimization variables
    private Collider[] nearbyColliders;
    private List<GameObject> validTargets = new List<GameObject>();
    private List<DetectionResult> detectionResults = new List<DetectionResult>();
    private Coroutine scanCoroutine;
    private Transform cachedTransform;
    private Collider scannerCollider;
    private Vector3 forward;
    private Vector3 position;

    // Runtime visualization components
    private LineRenderer[] coneLines;
    private LineRenderer hitLine;
    private GameObject visualizationParent;

    // Events for external scripts
    public System.Action<GameObject> OnClosestObjectChanged;
    public System.Action<GameObject, float> OnObjectDetected;

    // Detection result structure
    [System.Serializable]
    public struct DetectionResult
    {
        public GameObject target;
        public float distance;
        public Vector3 closestPointOnTarget;
        public Vector3 closestPointOnScanner;
        public bool isIntersecting;

        public DetectionResult(GameObject target, float distance, Vector3 closestPointOnTarget, Vector3 closestPointOnScanner, bool isIntersecting)
        {
            this.target = target;
            this.distance = distance;
            this.closestPointOnTarget = closestPointOnTarget;
            this.closestPointOnScanner = closestPointOnScanner;
            this.isIntersecting = isIntersecting;
        }
    }

    private void Awake()
    {
        cachedTransform = transform;
        scannerCollider = GetComponent<Collider>();

        // If no collider exists, create a sphere collider for detection
        //if (scannerCollider == null)
        //{
        //    GameObject detectionHelper = new GameObject("DetectionHelper");
        //    detectionHelper.transform.SetParent(transform);
        //    detectionHelper.transform.localPosition = Vector3.zero;
        //    detectionHelper.transform.localRotation = Quaternion.identity;

        //    scannerCollider = detectionHelper.AddComponent<SphereCollider>();
        //    scannerCollider.radius = 0.1f; // Small radius for point-like detection
        //    scannerCollider.isTrigger = true;
        //}

        nearbyColliders = new Collider[maxObjectsToCheck];
        SetupRuntimeVisualization();
    }

    private void OnEnable()
    {
        StartScanning();
        SetVisualizationActive(showRuntimeVisualization);
    }

    private void OnDisable()
    {
        StopScanning();
        SetVisualizationActive(false);
    }

    public void StartScanning()
    {
        if (scanCoroutine != null)
            StopCoroutine(scanCoroutine);

        scanCoroutine = StartCoroutine(ScanForObjects());
    }

    public void StopScanning()
    {
        if (scanCoroutine != null)
        {
            StopCoroutine(scanCoroutine);
            scanCoroutine = null;
        }
    }

    private IEnumerator ScanForObjects()
    {
        while (true)
        {
            ScanCone();
            yield return new WaitForSeconds(scanInterval);
        }
    }

    private void ScanCone()
    {
        position = cachedTransform.position;
        forward = cachedTransform.forward;

        // Use OverlapSphere for initial broad-phase detection
        int hitCount = Physics.OverlapSphereNonAlloc(position, coneRange, nearbyColliders, targetLayers);

        GameObject newClosestObject = null;
        float newClosestDistance = float.MaxValue;

        validTargets.Clear();
        detectionResults.Clear();

        // Check each nearby collider
        for (int i = 0; i < hitCount; i++)
        {
            Collider targetCollider = nearbyColliders[i];
            if (targetCollider == null || targetCollider.gameObject == gameObject) continue;

            // Check if object has valid tag
            if (!HasValidTag(targetCollider.gameObject)) continue;

            // Check if object is static (optimization for Quest 2)
            if (!targetCollider.gameObject.isStatic && !IsValidDynamicObject(targetCollider.gameObject)) continue;

            // Perform collider-based detection
            DetectionResult result = PerformColliderDetection(targetCollider);

            if (result.target != null)
            {
                // Check if the detection point is within the cone
                Vector3 detectionPoint = useColliderBasedDetection ? result.closestPointOnTarget : targetCollider.bounds.center;

                if (IsPointInCone(detectionPoint))
                {
                    // Additional raycast check to ensure line of sight
                    if (HasLineOfSight(position, detectionPoint, result.distance))
                    {
                        validTargets.Add(targetCollider.gameObject);
                        detectionResults.Add(result);

                        if (result.distance < newClosestDistance)
                        {
                            newClosestDistance = result.distance;
                            newClosestObject = targetCollider.gameObject;
                        }
                    }
                }
            }
        }

        // Update closest object if changed
        if (newClosestObject != closestObject)
        {
            closestObject = newClosestObject;
            closestDistance = newClosestDistance;
            OnClosestObjectChanged?.Invoke(closestObject);
        }

        // Fire detection event
        if (closestObject != null)
        {
            OnObjectDetected?.Invoke(closestObject, closestDistance);
        }
    }

    private DetectionResult PerformColliderDetection(Collider targetCollider)
    {
        if (!useColliderBasedDetection)
        {
            // Fallback to bounds center detection  
            float fallbackDistance = Vector3.Distance(position, targetCollider.bounds.center);
            return new DetectionResult(
                targetCollider.gameObject,
                fallbackDistance,
                targetCollider.bounds.center,
                position,
                false
            );
        }

        // 1) Find scanner's nearest point towards target's center  
        Vector3 ptScanner = scannerCollider.ClosestPoint(targetCollider.bounds.center);

        // 2) Find target's nearest point towards scanner's center  
        Vector3 ptTarget = targetCollider.ClosestPoint(scannerCollider.bounds.center);

        // Calculate surface-to-surface distance  
        float distance = Vector3.Distance(ptScanner, ptTarget);

        // Check if colliders are intersecting (distance is very small or zero)  
        bool isIntersecting = distance <= colliderDistanceTolerance;

        return new DetectionResult(
            targetCollider.gameObject,
            distance,
            ptTarget,
            ptScanner,
            isIntersecting
        );
    }

    private bool IsPointInCone(Vector3 point)
    {
        Vector3 dirToPoint = (point - position).normalized;
        float angle = Vector3.Angle(forward, dirToPoint);

        // Check if within cone angle
        return angle <= coneAngle * 0.5f;
    }

    private bool HasValidTag(GameObject obj)
    {
        if (targetTags.Length == 0) return true;

        foreach (string tag in targetTags)
        {
            if (obj.CompareTag(tag))
                return true;
        }
        return false;
    }

    private bool IsValidDynamicObject(GameObject obj)
    {
        // Add custom logic here for dynamic objects you want to detect
        // For example, objects with specific components or in specific states
        return obj.GetComponent<Rigidbody>() != null && obj.GetComponent<Rigidbody>().isKinematic;
    }

    private bool HasLineOfSight(Vector3 start, Vector3 end, float maxDistance)
    {
        Vector3 direction = (end - start).normalized;

        // Use a slightly shorter ray to account for object bounds
        if (Physics.Raycast(start, direction, out RaycastHit hit, maxDistance + 0.1f, targetLayers))
        {
            return Vector3.Distance(hit.point, end) < 1f; // Allow small tolerance
        }

        return true;
    }

    // Public getters for external scripts
    public GameObject GetClosestObject() => closestObject;
    public float GetClosestDistance() => closestDistance;
    public List<GameObject> GetAllValidTargets() => new List<GameObject>(validTargets);
    public List<DetectionResult> GetDetectionResults() => new List<DetectionResult>(detectionResults);

    // Runtime configuration methods
    public void SetConeRange(float range)
    {
        coneRange = Mathf.Max(0.1f, range);
    }

    public void SetConeAngle(float angle)
    {
        coneAngle = Mathf.Clamp(angle, 1f, 179f);
    }

    public void SetScanInterval(float interval)
    {
        scanInterval = Mathf.Max(0.01f, interval);
    }

    public void SetUseColliderBasedDetection(bool useColliderDetection)
    {
        useColliderBasedDetection = useColliderDetection;
    }

    public void SetColliderDistanceTolerance(float tolerance)
    {
        colliderDistanceTolerance = Mathf.Max(0f, tolerance);
    }

    public void SetVisualizationActive(bool active)
    {
        showRuntimeVisualization = active;
        if (visualizationParent != null)
            visualizationParent.SetActive(active);
    }

    private void SetupRuntimeVisualization()
    {
        // Create parent object for all visualization components
        visualizationParent = new GameObject("ConeVisualization");
        visualizationParent.transform.SetParent(transform);
        visualizationParent.transform.localPosition = Vector3.zero;
        visualizationParent.transform.localRotation = Quaternion.identity;

        // Create line renderers for cone edges and base circle
        int totalLines = visualizationSegments + 1; // cone edges + center line
        coneLines = new LineRenderer[totalLines];

        // Create material if not provided
        if (coneMaterial == null)
        {
            coneMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        // Setup cone edge lines
        for (int i = 0; i < visualizationSegments; i++)
        {
            GameObject lineObj = new GameObject($"ConeLine_{i}");
            lineObj.transform.SetParent(visualizationParent.transform);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = coneMaterial;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = true;

            coneLines[i] = lr;
        }

        // Setup center line
        GameObject centerLineObj = new GameObject("CenterLine");
        centerLineObj.transform.SetParent(visualizationParent.transform);

        LineRenderer centerLr = centerLineObj.AddComponent<LineRenderer>();
        centerLr.material = centerMaterial;
        centerLr.startWidth = lineWidth * 1.5f;
        centerLr.endWidth = lineWidth * 1.5f;
        centerLr.positionCount = 2;
        centerLr.useWorldSpace = true;

        coneLines[visualizationSegments] = centerLr;

        // Setup hit line
        GameObject hitLineObj = new GameObject("HitLine");
        hitLineObj.transform.SetParent(visualizationParent.transform);

        hitLine = hitLineObj.AddComponent<LineRenderer>();
        hitLine.material = hitMaterial;
        hitLine.startWidth = lineWidth * 2f;
        hitLine.endWidth = lineWidth * 2f;
        hitLine.positionCount = 2;
        hitLine.useWorldSpace = true;
        hitLine.enabled = false;
    }

    // Debug visualization
    private void OnDrawGizmos()
    {
        if (!showDebugVisualization) return;

        Vector3 pos = transform.position;
        Vector3 fwd = transform.forward;

        // Draw cone
        Gizmos.color = coneMaterial != null ? coneMaterial.color : Color.yellow;
        DrawCone(pos, fwd, coneRange, coneAngle);

        // Draw line to closest object and show detection points
        if (closestObject != null)
        {
            Gizmos.color = hitMaterial != null ? hitMaterial.color : Color.red;

            // Find the detection result for the closest object
            DetectionResult closestResult = default;
            foreach (var result in detectionResults)
            {
                if (result.target == closestObject)
                {
                    closestResult = result;
                    break;
                }
            }

            if (closestResult.target != null)
            {
                // Draw line between closest points
                Gizmos.DrawLine(closestResult.closestPointOnScanner, closestResult.closestPointOnTarget);

                // Draw spheres at detection points
                Gizmos.DrawWireSphere(closestResult.closestPointOnTarget, 0.1f);
                Gizmos.DrawWireSphere(closestResult.closestPointOnScanner, 0.05f);

                // Change color if intersecting
                if (closestResult.isIntersecting)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(closestResult.closestPointOnTarget, 0.2f);
                }
            }
            else
            {
                // Fallback to simple line
                Gizmos.DrawLine(pos, closestObject.transform.position);
                Gizmos.DrawWireSphere(closestObject.transform.position, 0.2f);
            }
        }
    }

    private void DrawCone(Vector3 origin, Vector3 direction, float range, float angle)
    {
        float halfAngle = angle * 0.5f;

        // Calculate cone base radius
        float radius = range * Mathf.Tan(halfAngle * Mathf.Deg2Rad);

        // Draw cone lines
        Vector3 forward = direction.normalized * range;
        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized * radius;
        Vector3 up = Vector3.Cross(right, direction).normalized * radius;

        // Draw cone outline
        for (int i = 0; i <= visualizationSegments; i++)
        {
            float currentAngle = (i / (float)visualizationSegments) * 360f;
            float nextAngle = ((i + 1) / (float)visualizationSegments) * 360f;

            Vector3 currentPoint = origin + forward +
                (right * Mathf.Cos(currentAngle * Mathf.Deg2Rad)) +
                (up * Mathf.Sin(currentAngle * Mathf.Deg2Rad));

            Vector3 nextPoint = origin + forward +
                (right * Mathf.Cos(nextAngle * Mathf.Deg2Rad)) +
                (up * Mathf.Sin(nextAngle * Mathf.Deg2Rad));

            // Draw lines from origin to cone base
            Gizmos.DrawLine(origin, currentPoint);

            // Draw cone base circle
            if (i < visualizationSegments)
                Gizmos.DrawLine(currentPoint, nextPoint);
        }

        // Draw center line
        Gizmos.DrawLine(origin, origin + forward);
    }

    private void Update()
    {
        // Update runtime visualization
        if (showRuntimeVisualization && coneLines != null)
        {
            UpdateRuntimeVisualization();
        }
    }

    private void UpdateRuntimeVisualization()
    {
        Vector3 pos = cachedTransform.position;
        Vector3 fwd = cachedTransform.forward;
        float halfAngle = coneAngle * 0.5f;

        // Calculate cone base radius
        float radius = coneRange * Mathf.Tan(halfAngle * Mathf.Deg2Rad);

        // Get perpendicular vectors for cone calculation
        Vector3 right = Vector3.Cross(fwd, Vector3.up).normalized;
        Vector3 up = Vector3.Cross(right, fwd).normalized;

        // Update cone edge lines
        for (int i = 0; i < visualizationSegments; i++)
        {
            float angle = (i / (float)visualizationSegments) * 360f;

            Vector3 conePoint = pos + fwd * coneRange +
                (right * Mathf.Cos(angle * Mathf.Deg2Rad) * radius) +
                (up * Mathf.Sin(angle * Mathf.Deg2Rad) * radius);

            coneLines[i].SetPosition(0, pos);
            coneLines[i].SetPosition(1, conePoint);
        }

        // Update center line
        coneLines[visualizationSegments].SetPosition(0, pos);
        coneLines[visualizationSegments].SetPosition(1, pos + fwd * coneRange);

        // Update hit line
        if (closestObject != null)
        {
            hitLine.enabled = true;

            // Find the detection result for the closest object
            DetectionResult closestResult = default;
            foreach (var result in detectionResults)
            {
                if (result.target == closestObject)
                {
                    closestResult = result;
                    break;
                }
            }

            if (closestResult.target != null && useColliderBasedDetection)
            {
                // Use closest points for visualization
                hitLine.SetPosition(0, closestResult.closestPointOnScanner);
                hitLine.SetPosition(1, closestResult.closestPointOnTarget);
            }
            else
            {
                // Fallback to simple line
                hitLine.SetPosition(0, pos);
                hitLine.SetPosition(1, closestObject.transform.position);
            }
        }
        else
        {
            hitLine.enabled = false;
        }
    }

    private void OnDestroy()
    {
        // Clean up visualization objects
        if (visualizationParent != null)
        {
            DestroyImmediate(visualizationParent);
        }
    }
}