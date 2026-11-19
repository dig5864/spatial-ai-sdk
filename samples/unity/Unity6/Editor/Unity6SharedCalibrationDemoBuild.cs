using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using SpatialAI.Cardboard;
using SpatialAI.Editor;
using SpatialAI.Samples;
using SpatialAI.Unity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Unity6SharedCalibrationDemoBuild
{
    const string ScenePath = "Assets/Scenes/Unity6SharedCalibration.unity";
    const string AppId = "com.spatialai.samples.vuforiauvc";

    [MenuItem("Spatial AI/Unity 6/Build Shared Calibration Validation APK")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ValidateNativeBridge();
        if (SpatialVuforiaTargetCatalog.LoadBuiltIn().Count < 2)
            throw new BuildFailedException("The validation scene needs two built-in targets from the SDK.");
        SpatialAndroidSetup.Configure();
        var previous = EditorSceneManager.GetSceneManagerSetup();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = SpatialDisplayScene.PopulateXR();
        var bridge = UnityEngine.Object.FindAnyObjectByType<SpatialCardboardBridge>();
        bridge.useGenericViewerForDiagnostics = false;
        bridge.useRhinoXReferenceMeshForDiagnostics = false;
        var demo = new GameObject("Shared UVC Calibration Validation").AddComponent<Unity6SharedCalibrationDemo>();
        demo.xrCamera = camera;
        demo.targetShader = Resources.Load<Shader>("Unity6TargetOverlay");
        if (demo.targetShader == null)
            throw new InvalidOperationException("The validation overlay shader was not imported.");

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException("Could not save the shared calibration scene.");

        var output = Environment.GetEnvironmentVariable("SPATIALAI_SHARED_CALIBRATION_APK");
        if (string.IsNullOrWhiteSpace(output)) output = "Builds/Android/Spatial-XR-Unity6-SharedCalibration.apk";
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        string previousId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string previousName = PlayerSettings.productName;
        int previousVersionCode = PlayerSettings.Android.bundleVersionCode;
        string previousSdk = UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath;
        string previousOverride = Environment.GetEnvironmentVariable("SPATIALAI_ANDROID_SDK_ROOT");
        var bundledSdk = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/SDK");
        try
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.productName = "Spatial XR Unity 6 Calibration";
            PlayerSettings.Android.bundleVersionCode = Math.Max(previousVersionCode, 3);
            UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = bundledSdk;
            Environment.SetEnvironmentVariable("SPATIALAI_ANDROID_SDK_ROOT", bundledSdk);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Unity 6 shared calibration build failed.");
            Debug.Log("SPATIALAI_UNITY6_SHARED_CALIBRATION_APK " + output);
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, previousId);
            PlayerSettings.productName = previousName;
            PlayerSettings.Android.bundleVersionCode = previousVersionCode;
            UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = previousSdk;
            Environment.SetEnvironmentVariable("SPATIALAI_ANDROID_SDK_ROOT", previousOverride);
            if (previous.Length > 0 && previous.All(item => !string.IsNullOrEmpty(item.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    static void ValidateNativeBridge()
    {
        var sdk = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.spatialai.sdk");
        using (var aar = ZipFile.OpenRead(Path.Combine(sdk.resolvedPath, "Plugins/Android/spatial-hand-unity.aar")))
        {
            if (aar.GetEntry("jni/arm64-v8a/libSpatialVuforia.so") == null ||
                aar.GetEntry("jni/arm64-v8a/libVuforiaEngine.so") == null)
                throw new BuildFailedException("Install the Vuforia-enabled hand-unity AAR before building this scene.");
        }
    }
}
