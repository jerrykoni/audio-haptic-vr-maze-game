using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VRBlackoutManager : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Parent transform holding the VR cameras (e.g. OVRCameraRig). If left empty, will search children of this GameObject.")]
    public Transform rigRoot;

    [Tooltip("Optional explicit camera list. If empty, all child Cameras of rigRoot (or self) are used.")]
    public List<Camera> targetCameras = new();

    [Header("Blackout")]
    public bool startBlack = false;
    [Tooltip("Enable a fade instead of instant switch.")]
    public bool useFade = true;
    public float fadeDuration = 0.25f;
    public Color blackoutColor = Color.black;

    [Tooltip("Scale of the fade quad placed in front of HMD.")]
    public float quadScale = 5f;
    [Tooltip("Local Z distance in front of (average) head anchor for the fade quad.")]
    public float quadDistance = 0.15f;

    [Tooltip("Disable (set to 0) culling mask after fade completes for maximum performance.")]
    public bool zeroCullingAfterFade = true;

    private class CamState
    {
        public Camera cam;
        public int originalMask;
        public CameraClearFlags originalClearFlags;
        public Color originalBG;
    }

    private readonly List<CamState> _states = new();
    private bool _isBlack;
    private bool _isAnimating;
    private GameObject _fadeQuad;
    private Material _fadeMat;
    private Coroutine _fadeCo;

    void Awake()
    {
        if (rigRoot == null)
            rigRoot = transform;

        if (targetCameras == null || targetCameras.Count == 0)
        {
            targetCameras = new List<Camera>(rigRoot.GetComponentsInChildren<Camera>(true));
        }

        _states.Clear();
        foreach (var c in targetCameras)
        {
            if (c == null) continue;
            _states.Add(new CamState
            {
                cam = c,
                originalMask = c.cullingMask,
                originalClearFlags = c.clearFlags,
                originalBG = c.backgroundColor
            });
        }

        BuildFadeQuad();

        if (startBlack)
            BlackoutImmediate();
        else
            SetQuadActive(false);
    }

    void BuildFadeQuad()
    {
        if (_fadeQuad != null) return;

        _fadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _fadeQuad.name = "BlackoutFadeQuad";
        DestroyImmediate(_fadeQuad.GetComponent<Collider>());

        // Attempt to position relative to the (first) center eye camera
        Transform refCam = _states.Count > 0 ? _states[0].cam.transform : rigRoot;

        _fadeQuad.transform.SetParent(refCam, false);
        _fadeQuad.transform.SetLocalPositionAndRotation(new Vector3(0, 0, quadDistance), Quaternion.identity);
        _fadeQuad.transform.localScale = Vector3.one * quadScale;

        Shader unlit = Shader.Find("Unlit/Color");
        _fadeMat = new Material(unlit != null ? unlit : Shader.Find("Legacy Shaders/Diffuse"))
        {
            color = new Color(blackoutColor.r, blackoutColor.g, blackoutColor.b, 0f)
        };
        var mr = _fadeQuad.GetComponent<MeshRenderer>();
        mr.material = _fadeMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.allowOcclusionWhenDynamic = false;
        SetQuadActive(false);
    }

    void SetQuadActive(bool active)
    {
        if (_fadeQuad != null && _fadeQuad.activeSelf != active)
            _fadeQuad.SetActive(active);
    }

    public void ToggleBlackout()
    {
        if (_isBlack) Restore(); else Blackout();
    }

    public void Blackout()
    {
        if (_isBlack || _isAnimating) return;
        if (!useFade)
        {
            BlackoutImmediate();
            return;
        }
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine(true));
    }

    public void Restore()
    {
        if (!_isBlack && !_isAnimating) return;
        if (!useFade)
        {
            RestoreImmediate();
            return;
        }
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine(false));
    }

    public void BlackoutImmediate()
    {
        // Make sure any fade is canceled
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _isAnimating = false;

        // Optionally we can keep rendering but easiest is just to zero masks immediately
        foreach (var s in _states)
        {
            s.cam.cullingMask = 0;
            s.cam.clearFlags = CameraClearFlags.SolidColor;
            s.cam.backgroundColor = blackoutColor;
        }

        if (useFade)
        {
            // Show fully opaque quad (so we can fade out later) OR hide if not needed
            if (_fadeMat != null)
            {
                _fadeMat.color = new Color(blackoutColor.r, blackoutColor.g, blackoutColor.b, 1f);
                SetQuadActive(true);
            }
        }
        else
        {
            SetQuadActive(false);
        }

        _isBlack = true;
    }

    public void RestoreImmediate()
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _isAnimating = false;

        foreach (var s in _states)
        {
            s.cam.cullingMask = s.originalMask;
            s.cam.clearFlags = s.originalClearFlags;
            s.cam.backgroundColor = s.originalBG;
        }

        if (_fadeMat != null)
        {
            _fadeMat.color = new Color(blackoutColor.r, blackoutColor.g, blackoutColor.b, 0f);
        }
        SetQuadActive(false);
        _isBlack = false;
    }

    IEnumerator FadeRoutine(bool toBlack)
    {
        _isAnimating = true;

        if (toBlack)
        {
            // Ensure scene still renders during fade-in
            RestoreCameraVisualsIfSuppressed();
            SetQuadActive(true);
        }
        else
        {
            // Before fade-out, restore scene so we can see the fade
            RestoreCameraVisualsIfSuppressed();
            SetQuadActive(true);
            if (_fadeMat != null)
                _fadeMat.color = new Color(blackoutColor.r, blackoutColor.g, blackoutColor.b, 1f);
        }

        float t = 0f;
        float startAlpha = toBlack ? 0f : 1f;
        float endAlpha = toBlack ? 1f : 0f;

        while (t < 1f)
        {
            t += (fadeDuration <= 0f ? 1f : Time.unscaledDeltaTime / fadeDuration);
            float a = Mathf.SmoothStep(startAlpha, endAlpha, Mathf.Clamp01(t));
            if (_fadeMat != null)
            {
                var c = blackoutColor;
                _fadeMat.color = new Color(c.r, c.g, c.b, a);
            }
            yield return null;
        }

        if (toBlack && zeroCullingAfterFade)
        {
            // Now suppress rendering fully (keep quad optional)
            foreach (var s in _states)
            {
                s.cam.cullingMask = 0;
                s.cam.clearFlags = CameraClearFlags.SolidColor;
                s.cam.backgroundColor = blackoutColor;
            }
            // We can hide the quad to avoid redundant overdraw
            SetQuadActive(false);
        }
        else if (!toBlack)
        {
            // Finished restoring
            SetQuadActive(false);
        }

        _isBlack = toBlack;
        _isAnimating = false;
        _fadeCo = null;
    }

    void RestoreCameraVisualsIfSuppressed()
    {
        foreach (var s in _states)
        {
            if (s.cam.cullingMask == 0)
            {
                s.cam.cullingMask = s.originalMask;
                s.cam.clearFlags = s.originalClearFlags;
                s.cam.backgroundColor = s.originalBG;
            }
        }
    }
}