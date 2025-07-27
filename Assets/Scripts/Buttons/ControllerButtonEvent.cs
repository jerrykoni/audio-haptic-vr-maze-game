using UnityEngine;
using UnityEngine.Events;

public class ControllerButtonEvent : MonoBehaviour
{
    [Header("Choose Button & Controller")]
    [Tooltip("Which OVRInput.Button will trigger the event?")]
    public OVRInput.Button button = OVRInput.Button.One;

    [Tooltip("Which controller(s) to listen on?")]
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Header("Event")]
    [Tooltip("Event invoked on button-down.")]
    public UnityEvent onButtonDown;

    void Update()
    {
        // Check if the specified button was pressed this frame
        if (OVRInput.GetDown(button, controller))
        {
            onButtonDown.Invoke();
        }
    }
}
