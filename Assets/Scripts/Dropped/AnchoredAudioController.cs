using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(LineRenderer))]
public class AnchoredAudioController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform listenerTransform;
    [SerializeField] private List<Transform> anchorPoints = new();

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float anchorRatio = 0.5f;

    [Range(0f, 180f)]
    [SerializeField] private float effectiveAngle = 90f;

    [Header("Static Noise Settings")]
    [Tooltip("Static noise fades in before full loss (staticNear) and takes over after (staticFar)")]
    [SerializeField] private AudioSource staticNearSource;
    [SerializeField] private AudioSource staticFarSource;

    [Range(0.1f, 1f)]
    [SerializeField] private float nearStaticThreshold = 0.8f; // 0.8 means 80% of effectiveAngle

    [Range(0f, 1f)]
    [SerializeField] private float maxStaticNearVolume = 0.4f;

    [Range(0f, 1f)]
    [SerializeField] private float maxStaticFarVolume = 0.8f;

    [Header("Line Settings")]
    [Tooltip("Visual line Y offset below the listener's Y")]
    [SerializeField] private float lineYOffset = -0.25f;

    private AudioSource audioSource;
    private LineRenderer lineRenderer;
    private HashSet<Transform> deactivatedAnchors = new();

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        lineRenderer = GetComponent<LineRenderer>();

        if (listenerTransform == null || anchorPoints.Count == 0)
        {
            Debug.LogError("AnchoredAudioController: Missing listener or anchor points.");
            enabled = false;
            return;
        }

        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.03f;
        lineRenderer.endWidth = 0.03f;
        lineRenderer.useWorldSpace = true;

        lineRenderer.receiveShadows = false;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.allowOcclusionWhenDynamic = false;
        lineRenderer.numCornerVertices = 0;
        lineRenderer.numCapVertices = 0;

        if (staticNearSource != null)
        {
            staticNearSource.loop = true;
            staticNearSource.playOnAwake = false;
            staticNearSource.volume = 0f;
            staticNearSource.Play();
            staticNearSource.Pause(); // start paused
        }

        if (staticFarSource != null)
        {
            staticFarSource.loop = true;
            staticFarSource.playOnAwake = false;
            staticFarSource.volume = 0f;
            staticFarSource.Play();
            staticFarSource.Pause(); // start paused
        }
    }

    private void Update()
    {
        float bestAngle = float.MaxValue;
        Transform bestAnchor = null;

        float targetY = listenerTransform.position.y;
        Vector3 listenerPos = new Vector3(listenerTransform.position.x, targetY, listenerTransform.position.z);

        Vector3 forwardFlat = new Vector3(listenerTransform.forward.x, 0f, listenerTransform.forward.z).normalized;
        Vector3 listenerBack = -forwardFlat;

        foreach (var anchor in anchorPoints)
        {
            if (anchor == null || deactivatedAnchors.Contains(anchor)) continue;

            Vector3 anchorPos = new Vector3(anchor.position.x, targetY, anchor.position.z);
            Vector3 dir = listenerPos - anchorPos;
            dir.y = 0f;
            if (dir != Vector3.zero) dir.Normalize();

            float angle = Vector3.Angle(dir, listenerBack);

            if (angle < bestAngle && angle <= effectiveAngle)
            {
                bestAngle = angle;
                bestAnchor = anchor;
            }
        }

        float songVolume = 1f;
        float nearVolume = 0f;
        float farVolume = 0f;

        float t = Mathf.Clamp01(bestAngle / effectiveAngle);

        if (bestAnchor != null)
        {
            Vector3 alignedAnchor = new Vector3(bestAnchor.position.x, targetY, bestAnchor.position.z);
            Vector3 alignedListener = new Vector3(listenerTransform.position.x, targetY, listenerTransform.position.z);

            Vector3 flatPos = Vector3.Lerp(alignedAnchor, alignedListener, anchorRatio);
            Vector3 finalPos = new Vector3(flatPos.x, listenerTransform.position.y, flatPos.z);
            transform.position = finalPos;

            // --- Custom Static + Main Song Fade Logic ---
            float tStart = nearStaticThreshold;
            float tMid = tStart + (1f - tStart) * 0.5f;
            float tEnd = 1f;

            //float songVolume = 1f;
            //float nearVolume = 0f;
            //float farVolume = 0f;

            if (t < tStart)
            {
                // Full song, no static
                songVolume = 1f;
                staticFarSource.Pause();
                staticNearSource.Pause();
            }
            else if (t < tMid)
            {           
                float phaseT = Mathf.InverseLerp(tStart, tMid, t);
                songVolume = Mathf.Lerp(1f, 0f, phaseT);
                nearVolume = Mathf.Lerp(0f, maxStaticNearVolume, phaseT);
                staticNearSource.UnPause();
                
                staticFarSource.Pause();
            }
            else if (t < tEnd)
            {
                float phaseT = Mathf.InverseLerp(tMid, tEnd, t);
                songVolume = 0f;
                nearVolume = Mathf.Lerp(maxStaticNearVolume, 0f, phaseT);
                farVolume = Mathf.Lerp(0f, maxStaticFarVolume, phaseT);
                staticNearSource.UnPause();
                
                staticFarSource.UnPause();
                
            }
            //else // t == 1
            //{
            //    songVolume = 0f;
            //    farVolume = maxStaticFarVolume;
            //    //staticFarSource.UnPause();
                
            //    staticNearSource.Pause();
            //}

            //// --- Static Near ---
            //if (staticNearSource != null)
            //{
            //    staticNearSource.volume = nearVolume;

            //    if (nearVolume > 0f)
            //    {
            //        if (!staticNearSource.isPlaying)
            //        {
            //            staticNearSource.UnPause();
            //            Debug.LogWarning("StaticNearSource Play!");
            //        }
            //    }
            //    else if (staticNearSource.isPlaying)
            //    {
            //        staticNearSource.Pause();
            //        Debug.LogWarning("StaticNearSource Pause!");
            //    }
            //}

            //// --- Static Far ---
            //if (staticFarSource != null)
            //{
            //    staticFarSource.volume = farVolume;

            //    if (farVolume > 0f)
            //    {
            //        if (!staticFarSource.isPlaying)
            //        {
            //            staticFarSource.UnPause();
            //            Debug.LogWarning("StaticFarSource Play!");
            //        }
            //    }
            //    else if (staticFarSource.isPlaying)
            //    {
            //        staticFarSource.Pause();
            //        Debug.LogWarning("StaticFarSource Pause!");
            //    }
            //}

            //audioSource.volume = songVolume;
            //staticNearSource.volume = nearVolume;
            //staticFarSource.volume = farVolume;

            lineRenderer.enabled = true;
            Vector3 lineOffsetVec = new Vector3(0f, lineYOffset, 0f);
            lineRenderer.SetPosition(0, alignedAnchor + lineOffsetVec);
            lineRenderer.SetPosition(1, alignedListener + lineOffsetVec);
        }
        else
        {
            lineRenderer.enabled = false;
            //audioSource.volume = 0f;
            //staticFarSource.UnPause();
            //staticFarSource.volume = maxStaticFarVolume;
            songVolume = 0f;
            farVolume = maxStaticFarVolume;
            //staticFarSource.UnPause();

            //staticNearSource.Pause();
        }

        audioSource.volume = songVolume;
        staticNearSource.volume = nearVolume;
        staticFarSource.volume = farVolume;


    }


    // --- Anchor Management ---
    public void DeactivateAnchor(Transform anchor)
    {
        if (anchor != null && anchorPoints.Contains(anchor))
        {
            deactivatedAnchors.Add(anchor);
        }
    }

    public void ReactivateAnchor(Transform anchor)
    {
        if (anchor != null)
        {
            deactivatedAnchors.Remove(anchor);
        }
    }

    public bool IsAnchorActive(Transform anchor)
    {
        return anchor != null && anchorPoints.Contains(anchor) && !deactivatedAnchors.Contains(anchor);
    }
}
