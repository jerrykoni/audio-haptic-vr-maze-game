using UnityEngine;
using System.Collections;

public class TeleportPlayerOnLoad : MonoBehaviour
{
    [Tooltip("The target spawn point for the player.")]
    public Transform spawnPoint;

    [Tooltip("A reference to the OVRCameraRig in the scene.")]
    public OVRCameraRig cameraRig;

    void Start()
    {
        // A small delay can sometimes be necessary to ensure the rig is fully initialized
        // before attempting to reposition it.
        StartCoroutine(RepositionAfterDelay(0.1f));
    }

    private IEnumerator RepositionAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Reposition();
    }

    public void Reposition()
    {
        if (spawnPoint == null || cameraRig == null)
        {
            Debug.LogError("Spawn Point or OVRCameraRig reference is not set!");
            return;
        }

        Transform centerEye = cameraRig.centerEyeAnchor;

        // --- Rotation ---
        // 1. Get the current rotation of the headset on the Y axis.
        float headRotationY = centerEye.rotation.eulerAngles.y;

        // 2. Get the target rotation from the spawn point.
        float spawnRotationY = spawnPoint.rotation.eulerAngles.y;

        // 3. Calculate the necessary rotational adjustment for the rig.
        float rotationDifferenceY = spawnRotationY - headRotationY;

        // 4. Apply the rotation to the rig. This rotates the entire tracking space.
        cameraRig.transform.Rotate(0, rotationDifferenceY, 0);


        // --- Position ---
        // 1. Calculate the headset's position offset from the rig's origin *in world space*.
        Vector3 headOffset = centerEye.position - cameraRig.transform.position;
        // We only care about the offset on the horizontal plane.
        headOffset.y = 0;

        // 2. Calculate the target position by subtracting the head's offset from the spawn point.
        Vector3 targetRigPosition = spawnPoint.position - headOffset;

        // 3. Set the rig's position.
        cameraRig.transform.position = targetRigPosition;

        Debug.Log("OVRCameraRig has been repositioned to the spawn point.");
    }
}