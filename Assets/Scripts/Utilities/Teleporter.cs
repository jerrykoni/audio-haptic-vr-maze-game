using UnityEngine;
using UnityEngine.Events; // Not strictly needed for this script, but good practice if you expand event usage

/// <summary>
/// This script transfers a specified GameObject to a target position,
/// considering only the X and Z axes. The Y-axis position of the object is maintained.
/// The transfer can be triggered by a public method, suitable for Unity's Event System.
/// </summary>
public class Teleporter : MonoBehaviour
{
    [Header("Object to Transfer")]
    [Tooltip("The GameObject that will be moved to the destination.")]
    public GameObject objectToTransfer;

    [Header("Transfer Destination")]
    [Tooltip("The Transform representing the destination point for the object.")]
    public Transform transferDestination;

    /// <summary>
    /// This public method initiates the transfer of the specified GameObject.
    /// It can be assigned to events in the Unity Editor, like a button's OnClick event
    /// or a trigger volume's OnEnter event.
    /// </summary>
    public void TransferObjectToPosition()
    {
        if (objectToTransfer == null)
        {
            Debug.LogError("Object to Transfer has not been assigned in the ObjectTransferer script. Please assign the GameObject you want to move.");
            return;
        }

        if (transferDestination == null)
        {
            Debug.LogError("Transfer Destination has not been assigned in the ObjectTransferer script. Please assign a Transform for the destination point.");
            return;
        }

        // Get the current Y position of the object we are moving.
        // This ensures the object's height remains consistent and only its XZ position changes.
        float currentObjectYPosition = objectToTransfer.transform.position.y;

        // Determine the target position.
        // We use the transferDestination's X and Z coordinates,
        // but maintain the objectToTransfer's current Y position.
        Vector3 newTargetPosition = new Vector3(
            transferDestination.position.x,
            currentObjectYPosition, // Preserve the object's current Y-axis value
            transferDestination.position.z
        );

        // Move the objectToTransfer to the calculated target position.
        objectToTransfer.transform.position = newTargetPosition;

        Debug.Log($"Successfully transferred '{objectToTransfer.name}' to X:{newTargetPosition.x}, Y:{newTargetPosition.y}, Z:{newTargetPosition.z}");
    }
}