using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(LineRenderer))]
public class AnchoredAudioSource : MonoBehaviour
{
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [SerializeField] private Transform audioSourceTransform;

    [Range(0f, 1f)]
    [SerializeField] private float audioAnchorRatio = 0.5f;

    [Range(0f, 180f)]
    [SerializeField] private float effectiveAngle = 90f;

    [SerializeField] private Vector3 lineOffset = new(0f, -0.25f, 0f);

    private AudioSource audioSource;
    private LineRenderer lineRenderer;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        lineRenderer = GetComponent<LineRenderer>();

        if (audioSource == null || lineRenderer == null)
        {
            Debug.LogError("AudioSource or LineRenderer component is missing.");
            return;
        }

        // Optimize settings
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.03f;
        lineRenderer.endWidth = 0.03f;
        lineRenderer.useWorldSpace = true;

        // Use a simple shared material (Unlit)
        //lineRenderer.material = new Material(Shader.Find("Sprites/Default")); // Or assign in Inspector for reuse
        //lineRenderer.startColor = Color.magenta;
        //lineRenderer.endColor = Color.magenta;

        // Turn off lighting and shadows
        lineRenderer.receiveShadows = false;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.allowOcclusionWhenDynamic = false;

        // Optional: no corner caps or joins for better performance
        lineRenderer.numCornerVertices = 0;
        lineRenderer.numCapVertices = 0;
    }

    private void Update()
    {
        if (pointA == null || pointB == null || audioSourceTransform == null)
            return;

        // Update audio source position along AB
        audioSourceTransform.position = Vector3.Lerp(pointA.position, pointB.position, audioAnchorRatio);

        // Update AB direction (projected on XZ)
        Vector3 abDirection = (pointB.position - pointA.position);
        abDirection.y = 0f;
        abDirection.Normalize();

        Vector3 pointBBackward = -pointB.forward;
        pointBBackward.y = 0f;
        pointBBackward.Normalize();

        float signedAngle = Vector3.SignedAngle(abDirection, pointBBackward, Vector3.up);
        float t = Mathf.Clamp01(Mathf.Abs(signedAngle) / effectiveAngle);
        float volume = Mathf.Lerp(1f, 0f, t);
        audioSource.volume = volume;

        // Draw the AB line in-game
        lineRenderer.SetPosition(0, pointA.position + lineOffset);
        lineRenderer.SetPosition(1, pointB.position + lineOffset);
    }
}
