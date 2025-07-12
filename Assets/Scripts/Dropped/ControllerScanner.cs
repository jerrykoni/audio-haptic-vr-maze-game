using Meta.WitAi.TTS; // If using TTS (optional)
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ControllerScanner : MonoBehaviour
{
    [Header("Scanner Settings")]
    public Transform controllerOrigin;
    public float scanRange = 5f;
    public float coneAngle = 30f;
    public LayerMask targetLayer;
    public float scanInterval = 0.1f;

    [Header("Audio")]
    public AudioSource hoverAudioSource; // One-shot reusable audio source for hover
    public float hoverVolumeAtZero = 0.5f;
    public float hoverVolumeAtMax = 1.0f;
    public float hoverDistanceMax = 5f;

    [Header("Haptics")]
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;
    public float strongPulseDuration = 0.1f;
    public float softPulseAmplitude = 0.05f;
    public float softPulseFrequency = 0.05f;

    [Header("TTS (Optional)")]
    public bool useTTS = false;
    //public TTSSpeaker ttsSpeaker; // Optional: Assign if using Wit TTS

    private GameObject lastDetectedObject = null;
    private AudioSource currentClipSource;

    private void Start()
    {
        StartCoroutine(ScanLoop());
    }

    private IEnumerator ScanLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(scanInterval);
        while (true)
        {
            ScanForObjects();
            yield return wait;
        }
    }

    private void ScanForObjects()
    {
        Collider[] hits = Physics.OverlapSphere(controllerOrigin.position, scanRange, targetLayer);
        Transform closest = null;
        float closestDist = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            Vector3 dirToTarget = (hit.transform.position - controllerOrigin.position).normalized;
            float angle = Vector3.Angle(controllerOrigin.forward, dirToTarget);
            if (angle <= coneAngle / 2f)
            {
                float dist = Vector3.Distance(controllerOrigin.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closest = hit.transform;
                    closestDist = dist;
                }
            }
        }

        if (closest != null)
        {
            if (closest.gameObject != lastDetectedObject)
            {
                // New target detected
                HandleNewDetection(closest.gameObject, closestDist);
            }
            else
            {
                // Still detecting same object
                HandleContinuousDetection(closest.gameObject, closestDist);
            }
        }
        else
        {
            ClearDetection();
        }
    }

    private void HandleNewDetection(GameObject detectedObject, float distance)
    {
        lastDetectedObject = detectedObject;

        // --- Audio ---
        StopCurrentAudio();
        AudioClip clip = detectedObject.GetComponent<AudioClipReference>()?.clip;
        if (clip != null)
        {
            currentClipSource = detectedObject.AddComponent<AudioSource>();
            currentClipSource.spatialBlend = 0f;
            currentClipSource.PlayOneShot(clip);
        }
        //else if (useTTS && ttsSpeaker != null)
        //{
        //    string objName = detectedObject.name;
        //    ttsSpeaker.Speak(objName);
        //}

        // --- Hover audio ---
        StartHoverAudio(distance);

        // --- Strong haptic pulse ---
        StartCoroutine(StrongPulse());

        // --- (Optional): add visual feedback here ---
    }

    private void HandleContinuousDetection(GameObject detectedObject, float distance)
    {
        UpdateHoverAudio(distance);
        StartCoroutine(SoftPulse());
    }

    private void ClearDetection()
    {
        if (lastDetectedObject != null)
        {
            StopCurrentAudio();
            StopHoverAudio();
            lastDetectedObject = null;
        }
    }

    private IEnumerator StrongPulse()
    {
        OVRInput.SetControllerVibration(1f, 1f, controller);
        yield return new WaitForSeconds(strongPulseDuration);
        OVRInput.SetControllerVibration(0, 0, controller);
    }

    private IEnumerator SoftPulse()
    {
        OVRInput.SetControllerVibration(softPulseFrequency, softPulseAmplitude, controller);
        yield return new WaitForSeconds(softPulseFrequency);
        OVRInput.SetControllerVibration(0, 0, controller);
    }

    private void StartHoverAudio(float distance)
    {
        if (hoverAudioSource != null && !hoverAudioSource.isPlaying)
        {
            hoverAudioSource.Play();
        }
        UpdateHoverAudio(distance);
    }

    private void UpdateHoverAudio(float distance)
    {
        if (hoverAudioSource == null) return;

        float t = Mathf.Clamp01(distance / hoverDistanceMax);
        hoverAudioSource.volume = Mathf.Lerp(hoverVolumeAtMax, hoverVolumeAtZero, t);
    }

    private void StopHoverAudio()
    {
        if (hoverAudioSource != null)
        {
            hoverAudioSource.Stop();
        }
    }

    private void StopCurrentAudio()
    {
        if (currentClipSource != null)
        {
            currentClipSource.Stop();
            Destroy(currentClipSource);
        }
    }
}
