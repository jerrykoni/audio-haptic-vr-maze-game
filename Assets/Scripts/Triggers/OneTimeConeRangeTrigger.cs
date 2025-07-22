using UnityEngine;

[RequireComponent(typeof(Collider))]
public class OneTimeConeRangeTrigger : MonoBehaviour
{
    [Tooltip("ConeScanner to modify once.")]
    public ConeScanner scanner;

    [Tooltip("New scanRange applied on first entry.")]
    public float triggeredRange = 5f;

    void Awake()
    {
        // Ensure collider is a trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"{name}: Collider was not a trigger—enabled it now.", this);
        }

        // Validate scanner reference
        if (scanner == null)
            Debug.LogError($"{name}: No ConeScanner assigned!", this);
    }

    void OnTriggerEnter(Collider other)
    {
        // Only run once, only on player
        if (scanner == null || !other.CompareTag("Player"))
            return;

        // Apply the one-time change
        scanner.scanRange = triggeredRange;

        // Optionally disable this script or destroy the volume
        // this.enabled = false;
        // Destroy(this);
        // Destroy(gameObject);
    }
}
