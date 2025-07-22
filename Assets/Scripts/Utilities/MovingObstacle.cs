using System;
using System.Collections;
using UnityEngine;

// Add this to ensure a Rigidbody is always present on this object.
[RequireComponent(typeof(Rigidbody))]
public class MovingObstacle : MonoBehaviour
{
    [Header("Movement Targets")]
    [Tooltip("Start/end positions for the obstacle.")]
    public Transform pointA;
    public Transform pointB;

    [Header("Motion Settings")]
    [Tooltip("Units per second of movement.")]
    public float speed = 2f;
    [Tooltip("Pause at each end in seconds.")]
    public float waitAtEndpoints = 0.5f;

    // Events you can subscribe to for window open/close
    public event Action OnWindowOpen;
    public event Action OnWindowClosed;

    private Vector3 _posA;
    private Vector3 _posB;
    private bool _goingToB = true;
    private Rigidbody _rb; // <-- 1. Add a reference for the Rigidbody

    // Awake is called before Start
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>(); // <-- Get the Rigidbody component
        if (!_rb.isKinematic)
        {
            Debug.LogWarning("Rigidbody on MovingObstacle is not set to Kinematic. This is recommended.", this);
        }
    }

    private void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError("Please assign PointA and PointB in the inspector.", this);
            enabled = false;
            return;
        }

        _posA = pointA.position;
        _posB = pointB.position;
        _rb.position = _posA; // Use rb.position instead of transform.position
        StartCoroutine(MoveCycle());
    }

    private IEnumerator MoveCycle()
    {
        while (true)
        {
            Vector3 start = _goingToB ? _posA : _posB;
            Vector3 end = _goingToB ? _posB : _posA;

            // Note: Since we use Time.fixedDeltaTime, we calculate journey over time now.
            float journeyTime = Vector3.Distance(start, end) / speed;
            float elapsedTime = 0f;

            // Trigger open/close events
            if (_goingToB)
                OnWindowClosed?.Invoke();
            else
                OnWindowOpen?.Invoke();

            // Move from start→end
            while (elapsedTime < journeyTime)
            {
                // Use Time.fixedDeltaTime because we are in a FixedUpdate loop
                elapsedTime += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / journeyTime);

                // 2. Use Rigidbody.MovePosition()
                _rb.MovePosition(Vector3.Lerp(start, end, t));

                // 3. Wait for the next physics update
                yield return new WaitForFixedUpdate();
            }

            // Ensure exact endpoint
            _rb.MovePosition(end);
            _goingToB = !_goingToB;

            // Wait at endpoint (this still works as expected)
            yield return new WaitForSeconds(waitAtEndpoints);
        }
    }
}