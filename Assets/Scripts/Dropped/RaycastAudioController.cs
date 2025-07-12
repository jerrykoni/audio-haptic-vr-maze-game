using UnityEngine;

public class RaycastAudioController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform listenerTransform;     // Point B
    [SerializeField] private AudioSource audioSource;         // Shared audio source

    [Header("Ray Settings")]
    [SerializeField] private float rayDistance = 10f;
    [SerializeField] private LayerMask anchorLayerMask;

    [Header("Audio Settings")]
    [Range(0f, 180f)]
    [SerializeField] private float effectiveAngle = 90f;

    [Range(0f, 1f)]
    [SerializeField] private float anchorRatio = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showRay = true;

    private Transform currentHitAnchor = null;

    private void Update()
    {
        Ray ray = new Ray(listenerTransform.position, listenerTransform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayDistance, anchorLayerMask))
        {
            if (hit.collider.CompareTag("Anchor"))
            {
                currentHitAnchor = hit.transform;

                // Position audio source between listener and anchor
                Vector3 anchorPos = Vector3.Lerp(hit.point, listenerTransform.position, anchorRatio);
                transform.position = anchorPos;

                // Volume falloff based on angle
                Vector3 toAnchor = hit.point - listenerTransform.position;
                toAnchor.y = 0f;
                Vector3 forward = listenerTransform.forward;
                forward.y = 0f;

                float angle = Vector3.Angle(forward.normalized, toAnchor.normalized);
                float t = Mathf.Clamp01(angle / effectiveAngle);
                float volume = Mathf.Lerp(1f, 0f, t);

                audioSource.volume = volume;
                return;
            }
        }

        // No anchor hit — fade out audio
        currentHitAnchor = null;
        audioSource.volume = 0f;
    }

    private void OnDrawGizmos()
    {
        if (!showRay || listenerTransform == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(listenerTransform.position, listenerTransform.forward * rayDistance);
    }
}
