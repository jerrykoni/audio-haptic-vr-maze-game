using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// This script smoothly teleports a GameObject to the OVRCameraRig's position on the XZ plane.
/// The teleportation can be triggered by a UnityEvent.
/// </summary>
public class SmoothObjectTransfer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The OVRCameraRig to which the object will be teleported.")]
    public OVRCameraRig cameraRig;

    [Tooltip("The GameObject that will be moved.")]
    public GameObject objectToMove;

    [Header("Movement Settings")]
    [Tooltip("The duration of the smooth teleport in seconds.")]
    public float teleportDuration = 1.0f;

    private Coroutine _teleportCoroutine;

    /// <summary>
    /// Public method to initiate the smooth teleport. This can be assigned to a UnityEvent.
    /// </summary>
    public void StartTeleport()
    {
        if (_teleportCoroutine != null)
        {
            StopCoroutine(_teleportCoroutine);
        }
        _teleportCoroutine = StartCoroutine(SmoothTeleport());
    }

    private IEnumerator SmoothTeleport()
    {
        if (cameraRig == null || objectToMove == null)
        {
            Debug.LogError("CameraRig or ObjectToMove is not assigned.");
            yield break;
        }

        Vector3 startPosition = objectToMove.transform.position;
        // The centerEyeAnchor represents the position of the player's head. [7]
        Transform targetTransform = cameraRig.centerEyeAnchor;

        float elapsedTime = 0f;

        while (elapsedTime < teleportDuration)
        {
            // Calculate the target position on the XZ plane, keeping the object's original Y position.
            Vector3 targetPosition = new Vector3(targetTransform.position.x, startPosition.y, targetTransform.position.z);

            // Smoothly interpolate the position using Vector3.Lerp.
            objectToMove.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / teleportDuration);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure the object reaches the final target position precisely.
        Vector3 finalTargetPosition = new Vector3(targetTransform.position.x, startPosition.y, targetTransform.position.z);
        objectToMove.transform.position = finalTargetPosition;
    }
}