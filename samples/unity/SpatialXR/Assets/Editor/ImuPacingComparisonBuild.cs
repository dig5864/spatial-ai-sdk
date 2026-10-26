using UnityEditor;
using UnityEngine;

// One build for both policies; runtime parameters are applied through the SDK.
public static class ImuPacingComparisonBuild
{
    [MenuItem("Spatial AI/IMU Pacing/Build SDK Frame Rate Settings")]
    public static void BuildSdkFrameRateSettings()
    {
        var previous = PlayerSettings.Android.optimizedFramePacing;
        try
        {
            PlayerSettings.Android.optimizedFramePacing = false;
            Debug.Log("SpatialAI IMU-PACING BUILD revision=sdk-frame-rate-v1 policy=runtime-sdk-config optimizedFramePacing=false");
            SpatialXRSetup.BuildAndroidSampleVariant("spatialxr-sdk-frame-rate-settings.apk", new string[0]);
        }
        finally { PlayerSettings.Android.optimizedFramePacing = previous; AssetDatabase.SaveAssets(); }
    }
}
