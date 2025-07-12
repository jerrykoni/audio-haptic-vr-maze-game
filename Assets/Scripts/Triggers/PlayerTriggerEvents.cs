using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class PlayerTriggerEvents : MonoBehaviour
{
    [Tooltip("Event triggered when the player enters the collider.")]
    public UnityEvent OnPlayerEnter;

    [Tooltip("Event triggered when the player exits the collider.")]
    public UnityEvent OnPlayerExit;

    private void Reset()
    {
        // Ensure the collider is set as a trigger
        if (TryGetComponent<Collider>(out var collider))
        {
            collider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            OnPlayerEnter?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            OnPlayerExit?.Invoke();
        }
    }
}