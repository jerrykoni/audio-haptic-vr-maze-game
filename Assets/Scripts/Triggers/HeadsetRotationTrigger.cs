using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Triggers a UnityEvent when the headset is rotated to a specific direction for a certain duration.
/// Can be configured to trigger only once or multiple times.
/// </summary>
public class HeadsetRotationTrigger : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("A reference to the OVRCameraRig's centerEyeAnchor transform.")]
    public Transform centerEyeAnchor;

    [Header("Target Direction")]
    [Tooltip("The direction the user needs to look at to trigger the event.")]
    public Vector3 targetDirection = Vector3.forward;

    [Tooltip("The angle of tolerance in degrees. The smaller the value, the more precise the user has to be.")]
    [Range(1f, 90f)]
    public float angleThreshold = 10f;

    [Header("Dwell Time")]
    [Tooltip("The time in seconds the user needs to stay looking at the target direction to trigger the event.")]
    public float dwellTime = 1.5f;

    [Header("Trigger Settings")]
    [Tooltip("If true, the event will only be triggered a single time.")]
    public bool triggerOnlyOnce = true;

    [Header("Event")]
    [Tooltip("The event that will be triggered when the user looks at the target direction for the specified dwell time.")]
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

        if (centerEyeAnchor == null)
        {
            Debug.LogError("Center Eye Anchor is not assigned in the HeadsetRotationTrigger script.");
            return;
        }

        // Normalize the target direction to ensure its length is 1
        Vector3 normalizedTargetDirection = targetDirection.normalized;

        // Get the forward direction of the headset
        Vector3 headsetForwardDirection = centerEyeAnchor.forward;

        // Calculate the angle between the headset's forward direction and the target direction
        float angle = Vector3.Angle(headsetForwardDirection, normalizedTargetDirection);

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
    /// Draws a gizmo in the editor to visualize the target direction.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (centerEyeAnchor != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(centerEyeAnchor.position, targetDirection.normalized * 2);
        }
    }
}