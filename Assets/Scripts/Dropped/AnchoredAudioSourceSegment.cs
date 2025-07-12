using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class AnchoredAudioSourceSegment : MonoBehaviour
{
    [SerializeField] private Transform pointStart;
    [SerializeField] private Transform pointB;
    [SerializeField] private AudioSource sharedAudioSource;

    [Range(0f, 1f)]
    [SerializeField] private float audioAnchorRatio = 0.5f;

    [Range(0f, 180f)]
    [SerializeField] private float effectiveAngle = 90f;

    [SerializeField] private Vector3 lineOffset = new(0f, -0.25f, 0f);
    [SerializeField] private Material lineMaterial;

    private LineRenderer lineRenderer;

    // Static tracking
    private static AnchoredAudioSourceSegment bestCandidate = null;
    private static float bestCandidateAngle = float.MaxValue;

    private float signedAngle;
    private float volume;
    private Vector3 anchorPos;

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (pointStart == null || pointB == null || sharedAudioSource == null)
        {
            Debug.LogError("Missing references.");
            enabled = false;
            return;
        }

        // Setup line
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.03f;
        lineRenderer.endWidth = 0.03f;
        lineRenderer.useWorldSpace = true;

        if (lineMaterial != null)
            lineRenderer.material = lineMaterial;

        lineRenderer.receiveShadows = false;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.allowOcclusionWhenDynamic = false;
        lineRenderer.numCornerVertices = 0;
        lineRenderer.numCapVertices = 0;
    }

    private void Update()
    {
        if (sharedAudioSource == null) return;

        // Calculate direction & signed angle
        anchorPos = Vector3.Lerp(pointStart.position, pointB.position, audioAnchorRatio);

        Vector3 direction = pointB.position - pointStart.position;
        direction.y = 0f;
        direction.Normalize();

        Vector3 pointBBackward = -pointB.forward;
        pointBBackward.y = 0f;
        pointBBackward.Normalize();

        signedAngle = Vector3.SignedAngle(direction, pointBBackward, Vector3.up);
        float absAngle = Mathf.Abs(signedAngle);

        // Check if within effective angle
        if (absAngle <= effectiveAngle)
        {
            float t = Mathf.Clamp01(absAngle / effectiveAngle);
            volume = Mathf.Lerp(1f, 0f, t);

            // Compete for control: keep the lowest-angle candidate
            if (absAngle < bestCandidateAngle)
            {
                bestCandidate = this;
                bestCandidateAngle = absAngle;
            }
        }
        else
        {
            volume = 0f;
        }

        // Draw line in-game
        lineRenderer.SetPosition(0, pointStart.position + lineOffset);
        lineRenderer.SetPosition(1, pointB.position + lineOffset);
    }

    private void LateUpdate()
    {
        if (bestCandidate == this)
        {
            sharedAudioSource.transform.position = anchorPos;
            sharedAudioSource.volume = volume;
        }

        // Only one controls per frame. Reset for next frame.
        if (this == bestCandidate)
        {
            bestCandidate = null;
            bestCandidateAngle = float.MaxValue;
        }
    }
}
