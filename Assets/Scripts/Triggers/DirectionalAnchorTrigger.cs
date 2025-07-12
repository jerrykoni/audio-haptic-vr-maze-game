using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
internal class DirectionalAnchorAction
{
    [Header("Target Configuration")]
    [Tooltip("The audio group index in the controller (0-based)")]
    [Range(0, 10)]
    public int groupIndex = 0;

    [Header("Action Type")]
    [Tooltip("What action to perform when triggered")]
    public AnchorActionType actionType = AnchorActionType.DeactivateSpecific;

    [Header("Specific Anchors (for DeactivateSpecific/ReactivateSpecific only)")]
    [Tooltip("List of specific anchor transforms to control")]
    public List<Transform> targetAnchors = new List<Transform>();
}

internal enum AnchorActionType
{
    DeactivateSpecific,
    ReactivateSpecific,
    DeactivateAllInGroup,
    ReactivateAllInGroup
}

[RequireComponent(typeof(BoxCollider))]
public class DirectionalAnchorTrigger : MonoBehaviour
{
    [Header("Audio Controller")]
    [Tooltip("The universal MultiSourceAnchoredAudioController to modify")]
    [SerializeField] private MultiSourceAnchoredAudioController audioController;

    [Header("Enter Direction Actions")]
    [Tooltip("Actions triggered when player enters from +X direction")]
    [SerializeField] private List<DirectionalAnchorAction> onEnterPositiveX = new List<DirectionalAnchorAction>();

    [Tooltip("Actions triggered when player enters from -X direction")]
    [SerializeField] private List<DirectionalAnchorAction> onEnterNegativeX = new List<DirectionalAnchorAction>();

    [Tooltip("Actions triggered when player enters from +Z direction")]
    [SerializeField] private List<DirectionalAnchorAction> onEnterPositiveZ = new List<DirectionalAnchorAction>();

    [Tooltip("Actions triggered when player enters from -Z direction")]
    [SerializeField] private List<DirectionalAnchorAction> onEnterNegativeZ = new List<DirectionalAnchorAction>();

    [Header("Exit Direction Actions")]
    [Tooltip("Actions triggered when player exits to +X direction")]
    [SerializeField] private List<DirectionalAnchorAction> onExitPositiveX = new List<DirectionalAnchorAction>();

    [Tooltip("Actions triggered when player exits to -X direction")]
    [SerializeField] private List<DirectionalAnchorAction> onExitNegativeX = new List<DirectionalAnchorAction>();

    [Tooltip("Actions triggered when player exits to +Z direction")]
    [SerializeField] private List<DirectionalAnchorAction> onExitPositiveZ = new List<DirectionalAnchorAction>();

    [Tooltip("Actions triggered when player exits to -Z direction")]
    [SerializeField] private List<DirectionalAnchorAction> onExitNegativeZ = new List<DirectionalAnchorAction>();

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    private BoxCollider boxCollider;

    private void Reset()
    {
        // Ensure we have a BoxCollider set as trigger
        if (TryGetComponent<BoxCollider>(out var collider))
        {
            collider.isTrigger = true;
        }
    }

    private void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
        if (!boxCollider.isTrigger)
        {
            Debug.LogWarning("DirectionalAnchorTrigger: BoxCollider must be set to Trigger!");
        }

        ValidateConfiguration();
    }

    private void ValidateConfiguration()
    {
        if (audioController == null)
        {
            Debug.LogError("DirectionalAnchorTrigger: No audio controller assigned!");
            return;
        }

        var allActions = new List<DirectionalAnchorAction>();
        allActions.AddRange(onEnterPositiveX);
        allActions.AddRange(onEnterNegativeX);
        allActions.AddRange(onEnterPositiveZ);
        allActions.AddRange(onEnterNegativeZ);
        allActions.AddRange(onExitPositiveX);
        allActions.AddRange(onExitNegativeX);
        allActions.AddRange(onExitPositiveZ);
        allActions.AddRange(onExitNegativeZ);

        foreach (var action in allActions)
        {
            if (action.groupIndex >= audioController.GetGroupCount())
            {
                Debug.LogWarning($"DirectionalAnchorTrigger: Action has invalid group index {action.groupIndex}. " +
                               $"Audio controller only has {audioController.GetGroupCount()} groups.");
                continue;
            }

            if ((action.actionType == AnchorActionType.DeactivateSpecific ||
                 action.actionType == AnchorActionType.ReactivateSpecific) &&
                action.targetAnchors.Count == 0)
            {
                Debug.LogWarning("DirectionalAnchorTrigger: Specific anchor action has no target anchors assigned!");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Vector3 boxCenter = transform.TransformPoint(boxCollider.center);
            Vector3 enterDirection = (other.transform.position - boxCenter).normalized;
            Vector3 localEnterDir = transform.InverseTransformDirection(enterDirection);

            if (enableDebugLogs)
            {
                Debug.Log($"DirectionalAnchorTrigger: Player entered trigger '{gameObject.name}'");
            }

            if (Mathf.Abs(localEnterDir.x) > Mathf.Abs(localEnterDir.z))
            {
                if (localEnterDir.x > 0)
                {
                    if (enableDebugLogs) Debug.Log("Entered from +X");
                    ExecuteActions(onEnterPositiveX, "Enter +X");
                }
                else
                {
                    if (enableDebugLogs) Debug.Log("Entered from -X");
                    ExecuteActions(onEnterNegativeX, "Enter -X");
                }
            }
            else
            {
                if (localEnterDir.z > 0)
                {
                    if (enableDebugLogs) Debug.Log("Entered from +Z");
                    ExecuteActions(onEnterPositiveZ, "Enter +Z");
                }
                else
                {
                    if (enableDebugLogs) Debug.Log("Entered from -Z");
                    ExecuteActions(onEnterNegativeZ, "Enter -Z");
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Vector3 boxCenter = transform.TransformPoint(boxCollider.center);
            Vector3 exitDirection = (other.transform.position - boxCenter).normalized;
            Vector3 localExitDir = transform.InverseTransformDirection(exitDirection);

            if (enableDebugLogs)
            {
                Debug.Log($"DirectionalAnchorTrigger: Player exited trigger '{gameObject.name}'");
            }

            if (Mathf.Abs(localExitDir.x) > Mathf.Abs(localExitDir.z))
            {
                if (localExitDir.x > 0)
                {
                    if (enableDebugLogs) Debug.Log("Exited to +X");
                    ExecuteActions(onExitPositiveX, "Exit +X");
                }
                else
                {
                    if (enableDebugLogs) Debug.Log("Exited to -X");
                    ExecuteActions(onExitNegativeX, "Exit -X");
                }
            }
            else
            {
                if (localExitDir.z > 0)
                {
                    if (enableDebugLogs) Debug.Log("Exited to +Z");
                    ExecuteActions(onExitPositiveZ, "Exit +Z");
                }
                else
                {
                    if (enableDebugLogs) Debug.Log("Exited to -Z");
                    ExecuteActions(onExitNegativeZ, "Exit -Z");
                }
            }
        }
    }

    private void ExecuteActions(List<DirectionalAnchorAction> actions, string directionContext)
    {
        if (audioController == null) return;

        foreach (var action in actions)
        {
            switch (action.actionType)
            {
                case AnchorActionType.DeactivateSpecific:
                    foreach (var anchor in action.targetAnchors)
                    {
                        if (anchor != null)
                        {
                            audioController.DeactivateAnchor(action.groupIndex, anchor);
                            if (enableDebugLogs)
                            {
                                Debug.Log($"DirectionalAnchorTrigger [{directionContext}]: Deactivated anchor '{anchor.name}' in group {action.groupIndex}");
                            }
                        }
                    }
                    break;

                case AnchorActionType.ReactivateSpecific:
                    foreach (var anchor in action.targetAnchors)
                    {
                        if (anchor != null)
                        {
                            audioController.ReactivateAnchor(action.groupIndex, anchor);
                            if (enableDebugLogs)
                            {
                                Debug.Log($"DirectionalAnchorTrigger [{directionContext}]: Reactivated anchor '{anchor.name}' in group {action.groupIndex}");
                            }
                        }
                    }
                    break;

                case AnchorActionType.DeactivateAllInGroup:
                    audioController.DeactivateAllAnchorsInGroup(action.groupIndex);
                    if (enableDebugLogs)
                    {
                        Debug.Log($"DirectionalAnchorTrigger [{directionContext}]: Deactivated all anchors in group {action.groupIndex}");
                    }
                    break;

                case AnchorActionType.ReactivateAllInGroup:
                    audioController.ReactivateAllAnchorsInGroup(action.groupIndex);
                    if (enableDebugLogs)
                    {
                        Debug.Log($"DirectionalAnchorTrigger [{directionContext}]: Reactivated all anchors in group {action.groupIndex}");
                    }
                    break;
            }
        }
    }

    // --- Public API Methods for Manual Triggering ---

    /// <summary>
    /// Manually trigger enter actions for a specific direction
    /// </summary>
    public void TriggerEnterDirection(string direction)
    {
        direction = direction.ToLower();
        switch (direction)
        {
            case "+x":
            case "posx":
                ExecuteActions(onEnterPositiveX, "Manual Enter +X");
                break;
            case "-x":
            case "negx":
                ExecuteActions(onEnterNegativeX, "Manual Enter -X");
                break;
            case "+z":
            case "posz":
                ExecuteActions(onEnterPositiveZ, "Manual Enter +Z");
                break;
            case "-z":
            case "negz":
                ExecuteActions(onEnterNegativeZ, "Manual Enter -Z");
                break;
            default:
                Debug.LogWarning($"DirectionalAnchorTrigger: Unknown direction '{direction}'. Use: +X, -X, +Z, -Z");
                break;
        }
    }

    /// <summary>
    /// Manually trigger exit actions for a specific direction
    /// </summary>
    public void TriggerExitDirection(string direction)
    {
        direction = direction.ToLower();
        switch (direction)
        {
            case "+x":
            case "posx":
                ExecuteActions(onExitPositiveX, "Manual Exit +X");
                break;
            case "-x":
            case "negx":
                ExecuteActions(onExitNegativeX, "Manual Exit -X");
                break;
            case "+z":
            case "posz":
                ExecuteActions(onExitPositiveZ, "Manual Exit +Z");
                break;
            case "-z":
            case "negz":
                ExecuteActions(onExitNegativeZ, "Manual Exit -Z");
                break;
            default:
                Debug.LogWarning($"DirectionalAnchorTrigger: Unknown direction '{direction}'. Use: +X, -X, +Z, -Z");
                break;
        }
    }

    /// <summary>
    /// Get reference to the universal audio controller
    /// </summary>
    public MultiSourceAnchoredAudioController AudioController => audioController;
}