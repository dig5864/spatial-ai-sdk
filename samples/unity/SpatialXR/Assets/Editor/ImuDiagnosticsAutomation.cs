using System;
using System.IO;
using SpatialAI.Cardboard;
using SpatialAI.Samples;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ImuDiagnosticsAutomation
{
    private const string ScenePath = "Assets/Scenes/ImuDiagnostics.unity";
    private static readonly string SdkRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
    private static readonly string RequestPath = Path.Combine(SdkRoot, "build/automation/ImuDiagnosticsBuild.request");
    private static readonly string StatusPath = Path.Combine(SdkRoot, "build/automation/ImuDiagnosticsBuild.status");
    private static readonly string ApkPath = Path.Combine(SdkRoot, "build/samples/android/imu-diagnostics.apk");
    private static double nextCheck;
    private static bool busy;

    static ImuDiagnosticsAutomation() { EditorApplication.update += Poll; }

    [MenuItem("Spatial AI/Create IMU Diagnostics Scene")]
    public static void CreateScene()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Spatial XR IMU Diagnostics");
            var bridge = root.AddComponent<SpatialCardboardBridge>();
            bridge.useGenericViewerForDiagnostics = true;

            var cameraObject = new GameObject("XR Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.025f, 0.055f);
            camera.nearClipPlane = 0.05f;
            camera.stereoTargetEye = StereoTargetEyeMask.Both;
            cameraObject.AddComponent<AudioListener>();
            var driver = cameraObject.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.rotationInput = new InputActionProperty(new InputAction(
                "Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion"));

            var diagnostics = root.AddComponent<ImuDiagnostics>();
            diagnostics.xrCamera = camera;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save IMU diagnostics scene.");
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log("IMU diagnostics scene: " + ScenePath);
    }

    [MenuItem("Spatial AI/Build IMU Diagnostics APK")]
    public static void Build()
    {
        if (!File.Exists(ScenePath)) CreateScene();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Switch the editor to Android before building.");
        var settingsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../ProjectSettings/ProjectSettings.asset"));
        var originalSettings = File.ReadAllBytes(settingsPath);
        var previousId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        var previousName = PlayerSettings.productName;
        var previousOrientation = PlayerSettings.defaultInterfaceOrientation;
        try
        {
            SpatialXRSetup.ConfigureAndroidXR();
            PlayerSettings.companyName = "SpatialAI";
            PlayerSettings.productName = "Spatial XR IMU Diagnostics";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.spatialai.imu.spatialxr");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = ApkPath,
                target = BuildTarget.Android, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("IMU diagnostics Android build failed: " + report.summary.result);
            Debug.Log("IMU diagnostics APK: " + ApkPath);
        }
        finally
        {
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, previousId);
            PlayerSettings.productName = previousName;
            PlayerSettings.defaultInterfaceOrientation = previousOrientation;
            AssetDatabase.SaveAssets();
            File.WriteAllBytes(settingsPath, originalSettings);
        }
    }

    private static void Poll()
    {
        if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(RequestPath)) return;
        busy = true;
        try
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                File.WriteAllText(StatusPath, "switching-to-android");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new Exception("Could not switch Unity build target to Android.");
                return;
            }
            File.Delete(RequestPath);
            File.WriteAllText(StatusPath, "building");
            Build();
            File.WriteAllText(StatusPath, "succeeded\n" + ApkPath);
        }
        catch (Exception error)
        {
            File.WriteAllText(StatusPath, "failed\n" + error);
            Debug.LogException(error);
            if (File.Exists(RequestPath)) File.Delete(RequestPath);
        }
        finally { busy = false; }
    }
}
