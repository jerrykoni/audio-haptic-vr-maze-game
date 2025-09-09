using UnityEngine;

/// <summary>
/// This script instantly moves an OVRCameraRig to a target's position on the XZ plane.
/// The movement can be initiated by a Unity Event.
/// </summary>
public class InstantTeleport : MonoBehaviour
{
    [Header("Core Components")]
    [Tooltip("The OVRCameraRig you want to move.")]
    public OVRCameraRig cameraRig;

    /// <summary>
    /// Public method to start the instant transfer to a target's position.
    /// This method can be connected to a Unity Event.
    /// </summary>
    /// <param name="target">The Transform of the GameObject to move to.</param>
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

        // Calculate the destination by taking the target's X and Z,
        // but keeping the camera rig's current Y position.
        Vector3 destinationPosition = new Vector3(target.position.x, cameraRig.transform.position.y, target.position.z);

        // Instantly move the camera rig to the destination.
        cameraRig.transform.position = destinationPosition;
    }
}