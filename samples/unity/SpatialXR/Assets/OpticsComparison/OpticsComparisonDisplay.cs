using UnityEngine;

// Draws the identical source image used by the Ximmerse reference application
// into each eye before the active XR runtime applies its distortion mesh.
[RequireComponent(typeof(Camera))]
public sealed class OpticsComparisonDisplay : MonoBehaviour
{
    public Texture2D pattern;

    private void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Application.targetFrameRate = 72;
        if (pattern == null) return;
        pattern.filterMode = FilterMode.Point;
        pattern.wrapMode = TextureWrapMode.Clamp;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(pattern != null ? pattern : source, destination);
    }
}
