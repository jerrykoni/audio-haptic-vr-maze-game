using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// This script triggers a UnityEvent when three specified buttons are pressed simultaneously on a designated controller.
/// It is designed for use with the Meta XR All-in-One SDK in Unity.
/// </summary>
public class SimultaneousButtonEvent : MonoBehaviour
{
    [Header("Controller and Buttons")]
    [Tooltip("The controller to monitor for button presses.")]
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Tooltip("The first button that must be pressed.")]
    public OVRInput.Button buttonOne = OVRInput.Button.One;

    [Tooltip("The second button that must be pressed.")]
    public OVRInput.Button buttonTwo = OVRInput.Button.Two;

    [Tooltip("The third button that must be pressed (e.g., a trigger).")]
    public OVRInput.Button buttonThree = OVRInput.Button.PrimaryIndexTrigger;

    [Header("Event")]
    [Tooltip("The event that is invoked when all three specified buttons are pressed down at the same time.")]
    public UnityEvent onSimultaneousButtonDown;

    // A private flag to ensure the event only fires once per simultaneous press.
    private bool eventHasBeenFired = false;

    void Update()
    {
        // Check if all three buttons are currently being held down on the specified controller.
        bool allButtonsPressed = OVRInput.Get(buttonOne, controller) &&
                                 OVRInput.Get(buttonTwo, controller) &&
                                 OVRInput.Get(buttonThree, controller);

        // If all buttons are pressed and the event has not already been fired for this press action...
        if (allButtonsPressed && !eventHasBeenFired)
        {
            // ...set the flag to true to prevent it from firing again in the next frame.
            eventHasBeenFired = true;

            // Invoke the public UnityEvent. Any methods assigned in the Inspector will be called.
            onSimultaneousButtonDown?.Invoke();
        }
        // If at least one of the buttons has been released...
        else if (!allButtonsPressed)
        {
            // ...reset the flag so the event can be fired again on the next simultaneous press.
            eventHasBeenFired = false;
        }
    }

    /// <summary>
    /// Public method to manually reset the event fired state.
    /// Can be useful if you need to reset the component's state from another script.
    /// </summary>
    public void ResetState()
    {
        eventHasBeenFired = false;
    }
}