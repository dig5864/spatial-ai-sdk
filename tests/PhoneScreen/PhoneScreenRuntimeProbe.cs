using System.Collections;
using SpatialAI.Cardboard;
using UnityEngine;

// Test APK only. Intent extras affect this application's Surface, never wm size.
public sealed class PhoneScreenRuntimeProbe : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null; yield return null;
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
        {
            int percent = intent.Call<int>("getIntExtra", "screenTestOutputPercent", 100);
            if (percent >= 50 && percent < 100)
            {
                int width = Mathf.RoundToInt(Screen.width * percent / 100f), height = Mathf.RoundToInt(Screen.height * percent / 100f);
                Screen.SetResolution(width, height, true);
                Debug.Log("Spatial screen test: requested output=" + width + "x" + height);
            }
        }
#endif
        yield return new WaitForSeconds(3);
        var bridge = FindObjectOfType<SpatialCardboardBridge>();
        Debug.Log("Spatial screen test: output=" + Screen.width + "x" + Screen.height +
            "; applied=" + (bridge != null && bridge.RhinoXLegacyMeshApplied) + "; " + (bridge == null ? "missing bridge" : bridge.PhoneDisplayStatus));
    }
}
