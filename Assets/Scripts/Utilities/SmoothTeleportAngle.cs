using UnityEngine;

/// <summary>
/// This script instantly teleports an OVRCameraRig, ensuring the player's head
/// (CenterEyeAnchor) arrives precisely at the target destination.
/// It accounts for the player's physical offset within their play space.
/// </summary>
public class SmoothTeleportAngle : MonoBehaviour
{
    [Header("Core Components")]
    [Tooltip("The OVRCameraRig you want to move.")]
    public OVRCameraRig cameraRig;

    /// <summary>
    /// Public method to start the instant teleportation to a target's position.
    /// This method can be connected to a Unity Event.
    /// </summary>
    /// <param name="target">The Transform of the GameObject to teleport to.</param>
    public void TeleportToTarget(Transform target)
    {
        if (target == null)
        {
            Debug.LogError("Target for teleportation is not assigned.");
            return;
        }

        if (cameraRig == null)
        {
            Debug.LogError("OVRCameraRig is not assigned.");
            return;
        }

        // --- This is the crucial new logic ---

        // 1. Get a reference to the player's head (CenterEyeAnchor).
        Transform centerEye = cameraRig.centerEyeAnchor;

        // 2. Calculate the player's physical offset from the rig's origin on the horizontal plane.
        Vector3 headOffset = centerEye.position - cameraRig.transform.position;
        headOffset.y = 0; // We only care about the XZ offset for positioning.

        // 3. Calculate the target position for the RIG.
        // This is the destination MINUS the player's physical offset.
        Vector3 targetRigPosition = target.position - headOffset;

        // --- End of new logic ---

        // Instantly move the camera rig to the calculated target position.
        // We keep the rig's current Y position to prevent moving it up or down.
        cameraRig.transform.position = new Vector3(
            targetRigPosition.x,
            cameraRig.transform.position.y,
            targetRigPosition.z
        );

        float headRotationY = centerEye.rotation.eulerAngles.y;
        float targetRotationY = target.rotation.eulerAngles.y;
        float rotationDifferenceY = targetRotationY - headRotationY;
        cameraRig.transform.Rotate(0, rotationDifferenceY, 0);

        Debug.Log("Player teleported accurately to " + target.name);
    }
}