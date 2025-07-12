using System.Collections;
using UnityEngine;
using System.Linq;

public class HandWallHaptics : MonoBehaviour
{
    [Header("References")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;

    [Header("Haptics Settings")]
    public float detectionRadius = 0.5f;
    public float maxHapticDistance = 0.5f;
    public float hapticCheckInterval = 0.1f;
    public float hapticFrequency = 0.1f; // Fixed frequency
    public LayerMask wallLayer;

    private void Start()
    {
        StartCoroutine(HapticFeedbackLoop());
    }

    private IEnumerator HapticFeedbackLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(hapticCheckInterval);

        while (true)
        {
            ProcessHandHaptics(leftHandAnchor, OVRInput.Controller.LTouch);
            ProcessHandHaptics(rightHandAnchor, OVRInput.Controller.RTouch);
            yield return wait;
        }
    }

    private void ProcessHandHaptics(Transform handAnchor, OVRInput.Controller controller)
    {
        Collider[] nearbyWalls = Physics.OverlapSphere(handAnchor.position, detectionRadius, wallLayer);

        if (nearbyWalls.Length > 0)
        {
            float minDistance = nearbyWalls
                .Select(collider => Vector3.Distance(collider.ClosestPoint(handAnchor.position), handAnchor.position))
                .Min();

            float intensity = Mathf.Clamp01(1f - (minDistance / maxHapticDistance));
            OVRInput.SetControllerVibration(hapticFrequency, intensity, controller);
        }
        else
        {
            // Stop vibration if no walls are nearby
            OVRInput.SetControllerVibration(0, 0, controller);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (leftHandAnchor != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(leftHandAnchor.position, detectionRadius);
        }

        if (rightHandAnchor != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(rightHandAnchor.position, detectionRadius);
        }
    }
}
