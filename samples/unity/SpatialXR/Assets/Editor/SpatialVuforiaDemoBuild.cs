using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class SpatialVuforiaDemoBuild
{
    public static void Build()
    {
        const string scene = "Assets/Scenes/HelloSpatialXR.unity";
        var output = Environment.GetEnvironmentVariable("SPATIAL_VUFORIA_APK");
        if (string.IsNullOrEmpty(output)) throw new Exception("SPATIAL_VUFORIA_APK is required");
        string oldId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        string oldName = PlayerSettings.productName;
        var sdkOverride = Environment.GetEnvironmentVariable("SPATIAL_VUFORIA_ANDROID_SDK");
        var oldSdk = EditorPrefs.GetString("AndroidSdkRoot", "");
        try
        {
            if (!string.IsNullOrEmpty(sdkOverride)) EditorPrefs.SetString("AndroidSdkRoot", sdkOverride);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.spatialai.samples.vuforiauvc");
            PlayerSettings.productName = "Spatial Vuforia UVC Test";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { scene }, locationPathName = output, target = BuildTarget.Android,
                options = BuildOptions.Development, extraScriptingDefines = new[] { "SPATIAL_VUFORIA_DEMO" }
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Unity build failed: " + report.summary.result);
            Debug.Log("SPATIAL_VUFORIA_APK=" + output);
        }
        finally {
            if (!string.IsNullOrEmpty(sdkOverride)) EditorPrefs.SetString("AndroidSdkRoot", oldSdk);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, oldId); PlayerSettings.productName = oldName;
        }
    }
}
