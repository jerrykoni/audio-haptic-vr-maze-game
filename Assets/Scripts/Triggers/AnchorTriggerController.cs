using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
internal class AnchorControlGroup
{
    [Header("Target Configuration")]
    [Tooltip("The audio group index in the controller (0-based)")]
    [Range(0, 10)]
    public int groupIndex = 0;

    [Header("Anchors to Control")]
    [Tooltip("List of anchor transforms to deactivate/reactivate")]
    public List<Transform> targetAnchors = new List<Transform>();

    [Header("Behavior Settings")]
    [Tooltip("Should anchors be deactivated when player enters? (false = activate on enter)")]
    public bool deactivateOnEnter = true;

    [Tooltip("Should anchors be reactivated when player enters? (overrides deactivateOnEnter)")]
    public bool reactivateOnEnter = false;
}

[RequireComponent(typeof(Collider))]
public class AnchorTriggerController : MonoBehaviour
{
    [Header("Audio Controller")]
    [Tooltip("The universal MultiSourceAnchoredAudioController to modify")]
    [SerializeField] private MultiSourceAnchoredAudioController audioController;

    [Header("Anchor Control Groups")]
    [Tooltip("List of anchor control configurations")]
    [SerializeField] private List<AnchorControlGroup> anchorGroups = new List<AnchorControlGroup>();

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    private void Reset()
    {
        // Ensure the collider is set as a trigger
        if (TryGetComponent<Collider>(out var collider))
        {
            collider.isTrigger = true;
        }
    }

    private void Start()
    {
        ValidateConfiguration();
    }

    private void ValidateConfiguration()
    {
        if (audioController == null)
        {
            Debug.LogError("AnchorTriggerController: No audio controller assigned!");
            return;
        }

        for (int i = 0; i < anchorGroups.Count; i++)
        {
            var group = anchorGroups[i];

            if (group.groupIndex >= audioController.GetGroupCount())
            {
                Debug.LogWarning($"AnchorTriggerController: Anchor group {i} has invalid group index {group.groupIndex}. " +
                               $"Audio controller only has {audioController.GetGroupCount()} groups.");
                continue;
            }

            if (group.targetAnchors.Count == 0)
            {
                Debug.LogWarning($"AnchorTriggerController: Anchor group {i} has no target anchors assigned.");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"AnchorTriggerController: Player entered trigger '{gameObject.name}'");
            }

            ProcessAnchorGroups(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"AnchorTriggerController: Player exited trigger '{gameObject.name}'");
            }

            ProcessAnchorGroups(false);
        }
    }

    private void ProcessAnchorGroups(bool isEntering)
    {
        if (audioController == null) return;

        foreach (var group in anchorGroups)
        {
            if (group.targetAnchors.Count == 0)
                continue;

            if (isEntering)
            {
                // Handle enter behavior
                if (group.reactivateOnEnter)
                {
                    // Reactivate anchors on enter (overrides deactivateOnEnter)
                    ProcessAnchorGroup(group, false); // false = reactivate
                }
                else
                {
                    // Use normal deactivateOnEnter behavior
                    ProcessAnchorGroup(group, group.deactivateOnEnter);
                }
            }
            // No exit behavior - only trigger actions are handled
        }
    }

    private void ProcessAnchorGroup(AnchorControlGroup group, bool deactivate)
    {
        foreach (var anchor in group.targetAnchors)
        {
            if (anchor == null) continue;

            if (deactivate)
            {
                audioController.DeactivateAnchor(group.groupIndex, anchor);

                if (enableDebugLogs)
                {
                    Debug.Log($"AnchorTriggerController: Deactivated anchor '{anchor.name}' in group {group.groupIndex}");
                }
            }
            else
            {
                audioController.ReactivateAnchor(group.groupIndex, anchor);

                if (enableDebugLogs)
                {
                    Debug.Log($"AnchorTriggerController: Reactivated anchor '{anchor.name}' in group {group.groupIndex}");
                }
            }
        }
    }

    // --- Public API Methods (Non-redundant) ---

    /// <summary>
    /// Manually trigger the configured anchor behavior as if player entered
    /// </summary>
    public void TriggerEnterBehavior()
    {
        ProcessAnchorGroups(true);
    }

    /// <summary>
    /// Manually trigger the configured anchor behavior as if player exited
    /// </summary>
    public void TriggerExitBehavior()
    {
        ProcessAnchorGroups(false);
    }

    /// <summary>
    /// Get reference to the audio controller (for direct access if needed)
    /// </summary>
    public MultiSourceAnchoredAudioController AudioController => audioController;

    /// <summary>
    /// Get the number of configured anchor control groups
    /// </summary>
    public int ConfiguredGroupCount => anchorGroups.Count;
}