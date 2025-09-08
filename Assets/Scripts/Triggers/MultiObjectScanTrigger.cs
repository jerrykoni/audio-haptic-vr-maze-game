using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// This component works with the ConeScanner to trigger events when a specific
/// set of objects have all been successfully scanned. It supports both an
/// immediate event and an optional delayed event.
/// </summary>
public class MultiObjectScanTrigger : MonoBehaviour
{
    [Header("Scanner Reference")]
    [Tooltip("The ConeScanner instance that this trigger should listen to.")]
    [SerializeField]
    private ConeScanner coneScanner;

    [Header("Scan Targets")]
    [Tooltip("The list of specific GameObjects that must be scanned to trigger the event.")]
    [SerializeField]
    private List<GameObject> targetObjects = new List<GameObject>();

    [Header("Events")]
    [Tooltip("This event is invoked immediately once all target objects have been scanned.")]
    public UnityEvent OnAllObjectsScanned;

    [Tooltip("How many seconds to wait after the scan is complete before invoking the delayed event.")]
    [Min(0)]
    public float eventDelay = 1.0f;

    [Tooltip("This event is invoked after the specified delay once all target objects have been scanned.")]
    public UnityEvent OnAllObjectsScannedDelayed;


    // A collection to keep track of the unique target objects that have been scanned.
    private readonly HashSet<GameObject> scannedObjects = new HashSet<GameObject>();

    // A flag to ensure the final event is only triggered once.
    private bool allTargetsScanned = false;

    private void Awake()
    {
        // It's critical that the ConeScanner is assigned in the Inspector.
        if (coneScanner == null)
        {
            Debug.LogError("MultiObjectScanTrigger: The 'Cone Scanner' reference has not been set in the Inspector!", this);
            // Disable this component if the reference is missing to prevent errors.
            this.enabled = false;
        }
    }

    private void OnEnable()
    {
        // Subscribe to the scanner's detection event when this component is enabled.
        if (coneScanner != null)
        {
            coneScanner.OnObjectDetected += HandleObjectDetected;
        }
    }

    private void OnDisable()
    {
        // It's important to unsubscribe from the event when the component is disabled
        // or destroyed to prevent memory leaks and errors.
        if (coneScanner != null)
        {
            coneScanner.OnObjectDetected -= HandleObjectDetected;
        }
    }

    /// <summary>
    /// This method is called by the ConeScanner whenever a new object is detected.
    /// </summary>
    /// <param name="detectedObject">The GameObject that the scanner has detected.</param>
    private void HandleObjectDetected(GameObject detectedObject)
    {
        // If the event has already been triggered, do nothing more.
        if (allTargetsScanned) return;

        // Check if the detected object is one of our targets.
        if (targetObjects.Contains(detectedObject))
        {
            // If the detected object is a valid target and we haven't already
            // logged it as scanned, add it to our set of scanned objects.
            // The HashSet automatically handles preventing duplicates.
            if (scannedObjects.Add(detectedObject))
            {
                Debug.Log($"Target '{detectedObject.name}' scanned! Total scanned: {scannedObjects.Count}/{targetObjects.Count}");

                // Check if the number of unique scanned objects now matches the required number of targets.
                if (scannedObjects.Count == targetObjects.Count)
                {
                    TriggerCompletionEvent();
                }
            }
        }
    }

    /// <summary>
    /// Triggers the final event and starts the coroutine for the delayed event.
    /// </summary>
    private void TriggerCompletionEvent()
    {
        allTargetsScanned = true;
        Debug.Log("All target objects have been successfully scanned! Triggering immediate event.");

        // 1. Invoke the immediate UnityEvent.
        OnAllObjectsScanned?.Invoke();

        // 2. Start the coroutine to handle the delayed event.
        StartCoroutine(DelayedEventRoutine());
    }

    /// <summary>
    /// A coroutine that waits for a specified delay before invoking the delayed event.
    /// </summary>
    private IEnumerator DelayedEventRoutine()
    {
        // Wait for the amount of time specified in the 'eventDelay' variable.
        yield return new WaitForSeconds(eventDelay);

        Debug.Log($"Delay of {eventDelay} seconds finished. Triggering delayed event.");

        // After the delay, invoke the second UnityEvent.
        OnAllObjectsScannedDelayed?.Invoke();
    }

    /// <summary>
    /// Public method to reset the trigger, allowing the events to be fired again.
    /// </summary>
    public void ResetTrigger()
    {
        Debug.Log("MultiObjectScanTrigger has been reset.");

        // Stop any delayed events that haven't fired yet.
        StopAllCoroutines();

        scannedObjects.Clear();
        allTargetsScanned = false;
    }
}