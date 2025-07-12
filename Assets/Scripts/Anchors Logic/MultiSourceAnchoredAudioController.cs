using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class AudioSourceGroup
{
    [Header("Audio Configuration")]
    [SerializeField] public AudioSource mainAudioSource;

    [Header("Anchor Points")]
    [SerializeField] public List<Transform> anchorPoints = new();

    [Header("Visual Settings")]
    [SerializeField] public Material lineMaterial;
    [SerializeField] public Color lineColor = Color.white;
    [Range(0.001f, 0.1f)]
    [SerializeField] public float lineWidth = 0.03f;

    [Header("Audio Behavior")]
    [Range(0f, 1f)]
    [SerializeField] public float anchorRatio = 0.5f;
    [Range(0f, 180f)]
    [SerializeField] public float effectiveAngle = 90f;

    // Runtime data
    [System.NonSerialized] public LineRenderer lineRenderer;
    [System.NonSerialized] public HashSet<Transform> deactivatedAnchors = new();
    [System.NonSerialized] public Transform currentBestAnchor;
    [System.NonSerialized] public float currentAngle;
}

public class MultiSourceAnchoredAudioController : MonoBehaviour
{
    [Header("Global Settings")]
    [SerializeField] private Transform listenerTransform;

    [Header("Global Static Audio Settings")]
    [SerializeField] private AudioSource staticNearSource;
    [SerializeField] private AudioSource staticFarSource;
    [Range(0.1f, 1f)]
    [SerializeField] private float nearStaticThreshold = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float maxStaticNearVolume = 0.4f;
    [Range(0f, 1f)]
    [SerializeField] private float maxStaticFarVolume = 0.8f;

    [Header("Audio Source Groups")]
    [SerializeField] private List<AudioSourceGroup> audioGroups = new();

    [Header("Line Rendering")]
    [Tooltip("Visual line Y offset below the listener's Y")]
    [SerializeField] private float lineYOffset = -0.25f;
    [SerializeField] private bool showAllLines = true;
    [SerializeField] private bool showOnlyActiveLines = false;

    // Global runtime data for static audio
    private Transform globalBestAnchor;
    private float globalBestAngle = float.MaxValue;
    private float globalEffectiveAngle;

    private void Start()
    {
        if (listenerTransform == null)
        {
            Debug.LogError("MultiSourceAnchoredAudioController: Missing listener transform.");
            enabled = false;
            return;
        }

        InitializeAudioGroups();
    }

    private void InitializeAudioGroups()
    {
        // Initialize global static audio sources
        InitializeStaticAudioSource(staticNearSource);
        InitializeStaticAudioSource(staticFarSource);

        for (int i = 0; i < audioGroups.Count; i++)
        {
            var group = audioGroups[i];

            if (group.mainAudioSource == null)
            {
                Debug.LogWarning($"Audio Group {i}: Missing main audio source.");
                continue;
            }

            if (group.anchorPoints.Count == 0)
            {
                Debug.LogWarning($"Audio Group {i}: No anchor points assigned.");
                continue;
            }

            // Create and configure LineRenderer for this group
            GameObject lineObj = new GameObject($"AudioGroup_{i}_Line");
            lineObj.transform.SetParent(transform);

            group.lineRenderer = lineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(group.lineRenderer, group);

            // Deactivate all anchors in this group at start
            DeactivateAllAnchorsInGroup(i);
        }
    }

    private void ConfigureLineRenderer(LineRenderer lr, AudioSourceGroup group)
    {
        lr.positionCount = 2;
        lr.startWidth = group.lineWidth;
        lr.endWidth = group.lineWidth;
        lr.useWorldSpace = true;
        lr.receiveShadows = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.allowOcclusionWhenDynamic = false;
        lr.numCornerVertices = 0;
        lr.numCapVertices = 0;

        if (group.lineMaterial != null)
        {
            lr.material = group.lineMaterial;
        }
        else
        {
            // Create a simple colored material if none provided
            lr.material = CreateColoredLineMaterial(group.lineColor);
        }
    }

    private Material CreateColoredLineMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        return mat;
    }

    private void InitializeStaticAudioSource(AudioSource staticSource)
    {
        if (staticSource == null) return;

        staticSource.loop = true;
        staticSource.playOnAwake = false;
        staticSource.volume = 0f;
        staticSource.Play();
        staticSource.Pause();
    }

    private void Update()
    {
        ProcessAllAudioGroups();
    }

    private void ProcessAllAudioGroups()
    {
        float targetY = listenerTransform.position.y;
        Vector3 listenerPos = new Vector3(listenerTransform.position.x, targetY, listenerTransform.position.z);
        Vector3 forwardFlat = new Vector3(listenerTransform.forward.x, 0f, listenerTransform.forward.z).normalized;
        Vector3 listenerBack = -forwardFlat;

        // Reset global best anchor tracking
        globalBestAnchor = null;
        globalBestAngle = float.MaxValue;
        globalEffectiveAngle = 0f;

        // First pass: Find the global best anchor across all groups and process individual groups
        foreach (var group in audioGroups)
        {
            if (group.mainAudioSource == null) continue;

            ProcessAudioGroup(group, listenerPos, listenerBack, targetY);

            // Update global best anchor if this group has a better one
            if (group.currentBestAnchor != null && group.currentAngle < globalBestAngle)
            {
                globalBestAnchor = group.currentBestAnchor;
                globalBestAngle = group.currentAngle;
                globalEffectiveAngle = group.effectiveAngle;
            }
        }

        // Second pass: Update global static audio based on the best anchor from any group
        UpdateGlobalStaticAudio();
    }

    private void ProcessAudioGroup(AudioSourceGroup group, Vector3 listenerPos, Vector3 listenerBack, float targetY)
    {
        float bestAngle = float.MaxValue;
        Transform bestAnchor = null;

        // Find the best anchor for this group
        foreach (var anchor in group.anchorPoints)
        {
            if (anchor == null || group.deactivatedAnchors.Contains(anchor)) continue;

            Vector3 anchorPos = new Vector3(anchor.position.x, targetY, anchor.position.z);
            Vector3 dir = listenerPos - anchorPos;
            dir.y = 0f;
            if (dir != Vector3.zero) dir.Normalize();

            float angle = Vector3.Angle(dir, listenerBack);

            if (angle < bestAngle && angle <= group.effectiveAngle)
            {
                bestAngle = angle;
                bestAnchor = anchor;
            }
        }

        group.currentBestAnchor = bestAnchor;
        group.currentAngle = bestAngle;

        // Update audio source position and line rendering
        UpdateAudioSourcePosition(group, bestAnchor, targetY);
        UpdateLineRenderer(group, bestAnchor, targetY);

        // Update only the main audio volume for this group
        UpdateMainAudioVolume(group, bestAngle);
    }

    private void UpdateAudioSourcePosition(AudioSourceGroup group, Transform bestAnchor, float targetY)
    {
        if (bestAnchor != null)
        {
            Vector3 alignedAnchor = new Vector3(bestAnchor.position.x, targetY, bestAnchor.position.z);
            Vector3 alignedListener = new Vector3(listenerTransform.position.x, targetY, listenerTransform.position.z);

            Vector3 flatPos = Vector3.Lerp(alignedAnchor, alignedListener, group.anchorRatio);
            Vector3 finalPos = new Vector3(flatPos.x, listenerTransform.position.y, flatPos.z);

            group.mainAudioSource.transform.position = finalPos;
        }
    }

    private void UpdateLineRenderer(AudioSourceGroup group, Transform bestAnchor, float targetY)
    {
        if (group.lineRenderer == null) return;

        bool shouldShowLine = bestAnchor != null &&
                             (showAllLines || (showOnlyActiveLines && group.currentAngle <= group.effectiveAngle));

        group.lineRenderer.enabled = shouldShowLine;

        if (shouldShowLine)
        {
            Vector3 alignedAnchor = new Vector3(bestAnchor.position.x, targetY, bestAnchor.position.z);
            Vector3 alignedListener = new Vector3(listenerTransform.position.x, targetY, listenerTransform.position.z);
            Vector3 lineOffsetVec = new Vector3(0f, lineYOffset, 0f);

            group.lineRenderer.SetPosition(0, alignedAnchor + lineOffsetVec);
            group.lineRenderer.SetPosition(1, alignedListener + lineOffsetVec);
        }
    }

    private void UpdateMainAudioVolume(AudioSourceGroup group, float bestAngle)
    {
        if (bestAngle == float.MaxValue)
        {
            // No valid anchor found for this group
            group.mainAudioSource.volume = 0f;
            return;
        }

        float t = Mathf.Clamp01(bestAngle / group.effectiveAngle);

        // Simple fade based on angle - only affects main audio
        // Static audio is handled globally
        float songVolume = 1f - t;
        group.mainAudioSource.volume = songVolume;
    }

    private void UpdateGlobalStaticAudio()
    {
        if (globalBestAnchor == null || globalBestAngle == float.MaxValue)
        {
            // No valid anchors found anywhere - play far static
            UpdateStaticAudioSource(staticNearSource, 0f);
            UpdateStaticAudioSource(staticFarSource, maxStaticFarVolume);
            return;
        }

        float t = Mathf.Clamp01(globalBestAngle / globalEffectiveAngle);

        // Global Static + Main Song Fade Logic
        float tStart = nearStaticThreshold;
        float tMid = tStart + (1f - tStart) * 0.5f;
        float tEnd = 1f;

        float nearVolume = 0f;
        float farVolume = 0f;

        if (t < tStart)
        {
            // Within good range - no static
            nearVolume = 0f;
            farVolume = 0f;
        }
        else if (t < tMid)
        {
            // Transition to near static
            float phaseT = Mathf.InverseLerp(tStart, tMid, t);
            nearVolume = Mathf.Lerp(0f, maxStaticNearVolume, phaseT);
            farVolume = 0f;
        }
        else if (t < tEnd)
        {
            // Transition from near static to far static
            float phaseT = Mathf.InverseLerp(tMid, tEnd, t);
            nearVolume = Mathf.Lerp(maxStaticNearVolume, 0f, phaseT);
            farVolume = Mathf.Lerp(0f, maxStaticFarVolume, phaseT);
        }
        else // t == 1
        {
            // Completely out of range - only far static
            nearVolume = 0f;
            farVolume = maxStaticFarVolume;
        }

        UpdateStaticAudioSource(staticNearSource, nearVolume);
        UpdateStaticAudioSource(staticFarSource, farVolume);
    }

    private void UpdateStaticAudioSource(AudioSource staticSource, float targetVolume)
    {
        if (staticSource == null) return;

        staticSource.volume = targetVolume;

        if (targetVolume > 0f)
        {
            if (!staticSource.isPlaying) staticSource.UnPause();
        }
        else if (staticSource.isPlaying)
        {
            staticSource.Pause();
        }
    }

    // --- Public API Methods ---

    public void DeactivateAnchor(int groupIndex, Transform anchor)
    {
        if (IsValidGroupIndex(groupIndex) && anchor != null &&
            audioGroups[groupIndex].anchorPoints.Contains(anchor))
        {
            audioGroups[groupIndex].deactivatedAnchors.Add(anchor);
        }
    }

    public void ReactivateAnchor(int groupIndex, Transform anchor)
    {
        if (IsValidGroupIndex(groupIndex) && anchor != null)
        {
            audioGroups[groupIndex].deactivatedAnchors.Remove(anchor);
        }
    }

    public bool IsAnchorActive(int groupIndex, Transform anchor)
    {
        if (!IsValidGroupIndex(groupIndex)) return false;

        var group = audioGroups[groupIndex];
        return anchor != null && group.anchorPoints.Contains(anchor) &&
               !group.deactivatedAnchors.Contains(anchor);
    }

    public void DeactivateAllAnchorsInGroup(int groupIndex)
    {
        if (!IsValidGroupIndex(groupIndex)) return;

        var group = audioGroups[groupIndex];
        foreach (var anchor in group.anchorPoints)
        {
            if (anchor != null) group.deactivatedAnchors.Add(anchor);
        }
    }

    public void ReactivateAllAnchorsInGroup(int groupIndex)
    {
        if (IsValidGroupIndex(groupIndex))
        {
            audioGroups[groupIndex].deactivatedAnchors.Clear();
        }
    }

    public Transform GetCurrentBestAnchor(int groupIndex)
    {
        return IsValidGroupIndex(groupIndex) ? audioGroups[groupIndex].currentBestAnchor : null;
    }

    public float GetCurrentAngle(int groupIndex)
    {
        return IsValidGroupIndex(groupIndex) ? audioGroups[groupIndex].currentAngle : float.MaxValue;
    }

    public int GetGroupCount()
    {
        return audioGroups.Count;
    }

    private bool IsValidGroupIndex(int index)
    {
        return index >= 0 && index < audioGroups.Count;
    }

    // --- Editor Helpers ---

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnDrawGizmosSelected()
    {
        if (listenerTransform == null) return;

        foreach (var group in audioGroups)
        {
            if (group.anchorPoints.Count == 0) continue;

            Gizmos.color = group.lineColor;

            foreach (var anchor in group.anchorPoints)
            {
                if (anchor == null) continue;

                // Draw anchor point
                Gizmos.DrawWireSphere(anchor.position, 0.5f);

                // Draw effective angle cone
                Vector3 listenerPos = listenerTransform.position;
                Vector3 forward = listenerTransform.forward;
                Vector3 toAnchor = (anchor.position - listenerPos).normalized;

                float angle = Vector3.Angle(-forward, toAnchor);
                if (angle <= group.effectiveAngle)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(listenerPos, anchor.position);
                }
                else
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(listenerPos, anchor.position);
                }
            }
        }
    }
}