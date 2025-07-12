using UnityEngine;

public class ScannerResponder : MonoBehaviour
{
    public ControllerScanner scanner;

    //private void OnEnable()
    //{
    //    if (scanner != null)
    //    {
    //        scanner.OnNewObjectScanned += HandleScan;
    //    }
    //}

    //private void OnDisable()
    //{
    //    if (scanner != null)
    //    {
    //        scanner.OnNewObjectScanned -= HandleScan;
    //    }
    //}

    private void HandleScan(GameObject obj)
    {
        Debug.LogWarning("Detected new object: " + obj.name);
        // You could also play a sound, TTS, or show UI here.
    }
}
