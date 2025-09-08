using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Triggers a UnityEvent when the headset is aimed at a specific Transform on the horizontal (XZ) plane.
/// Ignores the Y-axis (height) for triggering.
/// </summary>
public class HeadsetRotationTrigger : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("A reference to the OVRCameraRig's centerEyeAnchor transform.")]
    public Transform centerEyeAnchor;

    [Tooltip("The Transform the user needs to look at to trigger the event.")]
    public Transform targetTransform;

    [Header("Targeting Settings")]
    [Tooltip("The angle of tolerance in degrees on the horizontal plane. The smaller the value, the more precise the user has to be.")]
    [Range(1f, 90f)]
    public float angleThreshold = 10f;

    [Header("Dwell Time")]
    [Tooltip("The time in seconds the user needs to stay looking at the target to trigger the event.")]
    public float dwellTime = 1.5f;

    [Header("Trigger Settings")]
    [Tooltip("If true, the event will only be triggered a single time.")]
    public bool triggerOnlyOnce = true;

    [Header("Event")]
    [Tooltip("The event that will be triggered when the user looks at the target for the specified dwell time.")]
    public UnityEvent OnLookAtTarget;

    private bool isLookingAtTarget = false;
    private float lookAtTimer = 0f;
    private bool eventHasBeenTriggered = false;

    void Update()
    {
        // If the event should only trigger once and it already has, do nothing.
        if (triggerOnlyOnce && eventHasBeenTriggered)
        {
            return;
        }

        // Check for missing references
        if (centerEyeAnchor == null)
        {
            Debug.LogError("Center Eye Anchor is not assigned in the HeadsetRotationTrigger script.");
            return;
        }
        if (targetTransform == null)
        {
            Debug.LogError("Target Transform is not assigned in the HeadsetRotationTrigger script.");
            return;
        }

        // --- MODIFIED CALCULATION FOR XZ PLANE ---

        // Get the headset's forward direction and project it onto the XZ plane by setting y to 0.
        Vector3 headsetForwardXZ = centerEyeAnchor.forward;
        headsetForwardXZ.y = 0;

        // Calculate the direction from the headset to the target and project it onto the XZ plane.
        Vector3 directionToTarget = targetTransform.position - centerEyeAnchor.position;
        directionToTarget.y = 0;

        // --- END OF MODIFICATION ---


        // Calculate the angle between the two flattened vectors.
        // If either vector has a magnitude of zero (e.g., looking straight up/down), the angle is 0.
        float angle = Vector3.Angle(headsetForwardXZ.normalized, directionToTarget.normalized);


        // Check if the angle is within the defined threshold
        if (angle <= angleThreshold)
        {
            // Increment the timer
            lookAtTimer += Time.deltaTime;

            // Check if the timer has reached the dwell time and the event hasn't been triggered yet in this gaze session
            if (lookAtTimer >= dwellTime && !isLookingAtTarget)
            {
                // Set the flag to true to prevent the event from being called repeatedly in this session
                isLookingAtTarget = true;

                // Trigger the event
                OnLookAtTarget.Invoke();

                // If it's a one-time event, set the permanent flag to true
                if (triggerOnlyOnce)
                {
                    eventHasBeenTriggered = true;
                }
            }
        }
        else
        {
            // Reset the timer and the session flag when the user is no longer looking at the target direction
            lookAtTimer = 0f;
            isLookingAtTarget = false;
        }
    }

    /// <summary>
    /// Allows the one-time trigger to be reset from other scripts, so it can be fired again.
    /// </summary>
    public void ResetTrigger()
    {
        eventHasBeenTriggered = false;
        isLookingAtTarget = false;
        lookAtTimer = 0f;
    }

    /// <summary>
    /// Draws a gizmo in the editor to visualize the connection to the target.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (centerEyeAnchor != null && targetTransform != null)
        {
            // Draw a line to the target's actual position
            Gizmos.color = Color.grey;
            Gizmos.DrawLine(centerEyeAnchor.position, targetTransform.position);

            // Draw a brighter line on the XZ plane to visualize the ignored height
            Vector3 centerEyeXZ = new Vector3(centerEyeAnchor.position.x, 0, centerEyeAnchor.position.z);
            Vector3 targetXZ = new Vector3(targetTransform.position.x, 0, targetTransform.position.z);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(centerEyeXZ, targetXZ);
        }
    }
}