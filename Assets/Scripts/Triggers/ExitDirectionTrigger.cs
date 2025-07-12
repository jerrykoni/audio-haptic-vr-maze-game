using UnityEngine;
using UnityEngine.Events;

public class DirectionalTrigger : MonoBehaviour
{
    [Header("Enter Axis Events")]
    [SerializeField] private UnityEvent onEnterPositiveX;
    [SerializeField] private UnityEvent onEnterNegativeX;
    [SerializeField] private UnityEvent onEnterPositiveZ;
    [SerializeField] private UnityEvent onEnterNegativeZ;

    [Header("Exit Axis Events")]
    [SerializeField] private UnityEvent onExitPositiveX;
    [SerializeField] private UnityEvent onExitNegativeX;
    [SerializeField] private UnityEvent onExitPositiveZ;
    [SerializeField] private UnityEvent onExitNegativeZ;

    private BoxCollider boxCollider;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
        if (!boxCollider.isTrigger)
        {
            Debug.LogWarning("BoxCollider must be set to Trigger!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Vector3 boxCenter = transform.TransformPoint(boxCollider.center);
            Vector3 enterDirection = (other.transform.position - boxCenter).normalized;
            Vector3 localEnterDir = transform.InverseTransformDirection(enterDirection);

            if (Mathf.Abs(localEnterDir.x) > Mathf.Abs(localEnterDir.z))
            {
                if (localEnterDir.x > 0)
                {
                    Debug.Log("Entered from +X");
                    onEnterPositiveX.Invoke();
                }
                else
                {
                    Debug.Log("Entered from -X");
                    onEnterNegativeX.Invoke();
                }
            }
            else
            {
                if (localEnterDir.z > 0)
                {
                    Debug.Log("Entered from +Z");
                    onEnterPositiveZ.Invoke();
                }
                else
                {
                    Debug.Log("Entered from -Z");
                    onEnterNegativeZ.Invoke();
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Vector3 boxCenter = transform.TransformPoint(boxCollider.center);
            Vector3 exitDirection = (other.transform.position - boxCenter).normalized;
            Vector3 localExitDir = transform.InverseTransformDirection(exitDirection);

            if (Mathf.Abs(localExitDir.x) > Mathf.Abs(localExitDir.z))
            {
                if (localExitDir.x > 0)
                {
                    Debug.Log("Exited to +X");
                    onExitPositiveX.Invoke();
                }
                else
                {
                    Debug.Log("Exited to -X");
                    onExitNegativeX.Invoke();
                }
            }
            else
            {
                if (localExitDir.z > 0)
                {
                    Debug.Log("Exited to +Z");
                    onExitPositiveZ.Invoke();
                }
                else
                {
                    Debug.Log("Exited to -Z");
                    onExitNegativeZ.Invoke();
                }
            }
        }
    }
}
