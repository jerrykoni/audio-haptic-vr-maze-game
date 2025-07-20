using UnityEngine;
using System.Collections;
// Uncomment based on your XR setup:
// using UnityEngine.XR;       
// using Oculus.Platform;      

public class OVRYawFromTarget : MonoBehaviour
{
    [Header("References")]
    public Transform ovrCameraRig;   // Drag your OVRCameraRig root here
    public Transform targetYaw;      // Drag the Transform whose Y you want to match

    [Header("Settings")]
    //[Tooltip("Force recenter before applying yaw?")]
    //public bool recenterOnStart = true;
    [Tooltip("Frames to wait before applying (lets auto-recenter finish)")]
    public int delayFrames = 1;

    void Start()
    {
        if (ovrCameraRig == null || targetYaw == null)
        {
            Debug.LogWarning("[OVRYawFromTarget] Missing references.");
            enabled = false;
            return;
        }

        StartCoroutine(ApplyYawAfterDelay());
    }

    IEnumerator ApplyYawAfterDelay()
    {
        // Wait N end-of-frame ticks
        for (int i = 0; i < delayFrames; i++)
            yield return new WaitForEndOfFrame();

        // Optionally force a fresh recenter
//        if (recenterOnStart)
//        {
//#if OCULUS_SDK
//            OVRManager.display.RecenterPose();
//#else
//            UnityEngine.XR.InputTracking.Recenter();
//#endif
//        }

        // Compute desired yaw from target
        float desiredYaw = targetYaw.eulerAngles.y;

        // Try to rotate the internal TrackingSpace
        Transform trackingSpace = ovrCameraRig.Find("TrackingSpace");
        if (trackingSpace != null)
        {
            // Pre-multiply so existing tracking offsets remain intact
            trackingSpace.localRotation =
                Quaternion.Euler(0f, desiredYaw, 0f) * trackingSpace.localRotation;
        }
        else
        {
            // Fallback: rotate entire rig if no TrackingSpace child is found
            ovrCameraRig.rotation = Quaternion.Euler(0f, desiredYaw, 0f);
        }
    }
}
