using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// This script waits for a specific controller button press to start the main application.
/// It can either load a new scene or disable a "Press to Start" UI.
/// </summary>
public class StartOnButtonPress : MonoBehaviour
{
    [Header("Start Mechanism")]
    [Tooltip("The name of the scene to load when the start button is pressed. Leave empty if you are disabling a UI object instead.")]
    public string sceneToLoad;

    //[Tooltip("The UI object to disable when the start button is pressed. This is an alternative to loading a new scene.")]
    //public GameObject pressToStartUI;

    [Header("Input Settings")]
    [Tooltip("The primary button to check for starting the application.")]
    public OVRInput.Button startButton = OVRInput.Button.One; // 'A' on the right controller, 'X' on the left

    [Tooltip("Which controller to check for the button press.")]
    public OVRInput.Controller controller = OVRInput.Controller.Active;

    private bool _appStarted = false;

    void Update()
    {
        // Don't do anything if the application has already been started.
        if (_appStarted)
        {
            return;
        }

        // Check if the specified button is pressed on the designated controller.
        if (OVRInput.GetDown(startButton, controller))
        {
            StartApplication();
        }
    }

    /// <summary>
    /// Starts the main application logic.
    /// </summary>
    private void StartApplication()
    {
        _appStarted = true;

        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
        //else if (pressToStartUI != null)
        //{
        //    pressToStartUI.SetActive(false);
        //}
        else
        {
            Debug.LogWarning("No start action defined. Either provide a scene name or a UI object to disable.");
        }
    }
}