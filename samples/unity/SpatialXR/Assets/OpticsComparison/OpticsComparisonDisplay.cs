using UnityEngine;

// Draws the identical source image used by the Ximmerse reference application
// into each eye before the active XR runtime applies its distortion mesh.
[RequireComponent(typeof(Camera))]
public sealed class OpticsComparisonDisplay : MonoBehaviour
{
    public Texture2D pattern;
    private float menuUntil;
    private float backHoldStarted = -1f;

    private void Start()
    {
        menuUntil = Time.unscaledTime + 4f;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Application.targetFrameRate = SpatialAI.Unity.SpatialDeviceProfiles.SelectHost(
            SpatialAI.Unity.SpatialDeviceProfiles.ReadHostIdentity(), out var host).Success ? host.targetFrameRate : 72;
        if (pattern == null) return;
        pattern.filterMode = FilterMode.Point;
        pattern.wrapMode = TextureWrapMode.Clamp;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) menuUntil = Time.unscaledTime + 4f;
        var point = Input.mousePosition;
        if (!Input.GetMouseButton(0) || point.x > Screen.width * 0.2f || point.y > Screen.height * 0.2f) { backHoldStarted = -1f; return; }
        if (backHoldStarted < 0f) backHoldStarted = Time.unscaledTime;
        if (Time.unscaledTime - backHoldStarted > 1f && Application.CanStreamedLevelBeLoaded("HelloSpatialXR"))
            UnityEngine.SceneManagement.SceneManager.LoadScene("HelloSpatialXR");
    }

    private void OnGUI()
    {
        if (Time.unscaledTime > menuUntil) return;
        var style = new GUIStyle(GUI.skin.button) { fontSize = 24 };
        if (GUI.Button(new Rect(20, 20, 280, 65), "Back to camera demo", style) &&
            Application.CanStreamedLevelBeLoaded("HelloSpatialXR"))
            UnityEngine.SceneManagement.SceneManager.LoadScene("HelloSpatialXR");
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(pattern != null ? pattern : source, destination);
    }
}
