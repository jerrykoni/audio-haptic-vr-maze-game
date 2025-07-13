using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class ScanAudioManager : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioClip hoverSound;
    public AudioClip hoverStaySound;
    public AudioClip unhoverSound;
    
    [Header("Audio Source")]
    public AudioSource uiAudioSource;
    
    [Header("Detection Settings")]
    private GameObject currentDetectedObject;
    private bool isObjectCurrentlyDetected = false;
    private Coroutine hoverStayCoroutine;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnObjectLostHaptics;
    
    void Start()
    {
        if (uiAudioSource == null)
        {
            uiAudioSource = GetComponent<AudioSource>();
            if (uiAudioSource == null)
            {
                uiAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }
    
    public void OnObjectDetected(GameObject detectedObject)
    {
        if (detectedObject != currentDetectedObject)
        {
            currentDetectedObject = detectedObject;
            isObjectCurrentlyDetected = true;
            
            // Play hover sound
            if (hoverSound != null)
            {
                uiAudioSource.transform.position = detectedObject.transform.position;
                uiAudioSource.PlayOneShot(hoverSound);
            }
            
            // Start hover stay sound coroutine if available
            if (hoverStaySound != null && hoverStayCoroutine == null)
            {
                hoverStayCoroutine = StartCoroutine(PlayHoverStaySound());
            }
        }
    }
    
    public void OnObjectLost(GameObject lostObject)
    {
        if (lostObject == currentDetectedObject)
        {
            isObjectCurrentlyDetected = false;
            currentDetectedObject = null;

            // Stop hover stay sound
            if (hoverStayCoroutine != null)
            {
                StopCoroutine(hoverStayCoroutine);
                hoverStayCoroutine = null;
            }

            // Stop UI audio source if it's playing hover stay sound
            if (uiAudioSource.isPlaying && uiAudioSource.clip == hoverStaySound)
            {
                uiAudioSource.Stop();
            }

            // Play unhover sound
            if (unhoverSound != null)
            {
                uiAudioSource.transform.position = lostObject.transform.position;
                uiAudioSource.PlayOneShot(unhoverSound);
            }

            OnObjectLostHaptics.Invoke(lostObject);
        }
    }
    
    private IEnumerator PlayHoverStaySound()
    {
        while (isObjectCurrentlyDetected && hoverStaySound != null)
        {
            if (!uiAudioSource.isPlaying)
            {
                uiAudioSource.clip = hoverStaySound;
                uiAudioSource.Play();
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
}