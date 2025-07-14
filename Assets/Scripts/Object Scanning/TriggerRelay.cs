// Relay goes on physGO
using UnityEngine;

public class TriggerRelay : MonoBehaviour
{
    [HideInInspector] public ConeScanner scanner;

    void OnTriggerEnter(Collider other)
        => scanner.HandleTriggerEnter(other);

    void OnTriggerExit(Collider other)
        => scanner.HandleTriggerExit(other);
}