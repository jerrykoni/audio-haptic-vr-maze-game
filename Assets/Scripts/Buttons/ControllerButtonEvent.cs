using UnityEngine;
using UnityEngine.Events;

public class ControllerButtonEvent : MonoBehaviour
{
    [Header("Choose Button & Controller")]
    [Tooltip("Which OVRInput.Button will trigger the event?")]
    public OVRInput.Button button = OVRInput.Button.One;

    [Tooltip("Which controller(s) to listen on?")]
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Header("Events")]
    [Tooltip("Event invoked on every button-down.")]
    public UnityEvent onButtonDown;

    [Header("Toggling Event")]
    [Tooltip("A boolean that tracks the current toggle state. True is 'On', False is 'Off'.")]
    private bool toggleState = false;

    [Tooltip("Event invoked on the second press, fourth press, etc. (toggles OFF)")]
    public UnityEvent onToggleOn;

    [Tooltip("Event invoked on the first press, third press, etc. (toggles ON)")]
    public UnityEvent onToggleOff;

    void Update()
    {
        if (OVRInput.GetDown(button, controller))
        {
            onButtonDown?.Invoke();

            if (toggleState)
            {
                onToggleOn?.Invoke();
            }
            else
            {
                onToggleOff?.Invoke();
            }

            toggleState = !toggleState;
        }
    }
}