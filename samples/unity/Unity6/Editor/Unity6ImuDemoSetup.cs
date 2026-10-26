using System;
using System.IO;
using System.Linq;
using SpatialAI.Cardboard;
using SpatialAI.Editor;
using SpatialAI.Samples;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Unity6ImuDemoSetup
{
    public const string ScenePath = "Assets/Scenes/ImuDiagnostics.unity";
    public const string ProductName = "Spatial-XR-Unity6";
    // A versioned request prevents an older loaded generator from consuming it
    // before Unity imports the new frame-rate UI source and reloads assemblies.
    const string Request = "Temp/SpatialUnity6ImuSetup-v2.request";
    const string Status = "Library/SpatialUnity6ImuSetup.status.json";
    const string StartSettings = "ProjectSettings/SpatialUnity6ImuDemo.json";
    static double nextCheck;
    static Unity6ImuDemoSetup()
    {
        EditorApplication.update += Poll;
        EditorApplication.delayCall += RestorePlayStartScene;
    }

    static void RestorePlayStartScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(StartSettings)) return;
        var settings = JsonUtility.FromJson<StartSceneSettings>(File.ReadAllText(StartSettings));
        if (settings == null || !settings.useAsPlayModeStartScene) return;
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(settings.scene);
        if (scene != null) EditorSceneManager.playModeStartScene = scene;
    }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.timeSinceStartup < nextCheck || !File.Exists(Request)) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try
        {
            if (command == "configure") Configure();
            else if (command == "build") Build();
            else throw new ArgumentException("Unknown Unity 6 IMU setup command.");
            File.WriteAllText(Status, JsonUtility.ToJson(new SetupStatus { success = true, editor = Application.unityVersion, productName = PlayerSettings.productName, scene = ScenePath }, true));
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            File.WriteAllText(Status, JsonUtility.ToJson(new SetupStatus { success = false, error = error.ToString() }, true));
        }
    }

    [MenuItem("Spatial AI/Unity 6/Use IMU Default Scene")]
    public static void Configure()
    {
        if (Application.unityVersion.StartsWith("2022.")) throw new InvalidOperationException("Use this setup in Unity 6.");
        SpatialAndroidSetup.Configure();
        // Match the validated SDK IMU baseline; the SDK owns display cadence.
        PlayerSettings.Android.optimizedFramePacing = false;
        PlayerSettings.productName = ProductName;
        if (!File.Exists(ScenePath)) CreateScene();
        EnsureFrameRateControls();
        var previous = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(previous).ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        File.WriteAllText(StartSettings, JsonUtility.ToJson(new StartSceneSettings { scene = ScenePath, useAsPlayModeStartScene = true }, true));
        // Preserve any unsaved work in the user's currently open scenes.
        bool dirty = Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);
        if (!dirty) EditorSceneManager.OpenScene(ScenePath);
        Verify();
        Debug.Log("SPATIALAI_UNITY6_IMU_READY product=" + PlayerSettings.productName + " firstScene=" + ScenePath);
    }

    static void CreateScene()
    {
        var previous = SceneManager.GetActiveScene();
        bool replaceUntitled = previous.IsValid() && string.IsNullOrEmpty(previous.path) && !previous.isDirty;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, replaceUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Spatial XR IMU Diagnostics");
            var bridge = root.AddComponent<SpatialCardboardBridge>();
            bridge.useGenericViewerForDiagnostics = true;
            bridge.useRhinoXReferenceMeshForDiagnostics = true;
            var cameraObject = new GameObject("XR Camera"); cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.nearClipPlane = .05f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.015f, .025f, .055f);
            camera.stereoTargetEye = StereoTargetEyeMask.Both; cameraObject.AddComponent<AudioListener>();
            var driver = cameraObject.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.rotationInput = new InputActionProperty(new InputAction("Head Rotation", InputActionType.Value,
                "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            var diagnostics = root.AddComponent<ImuDiagnostics>(); diagnostics.xrCamera = camera; diagnostics.previewInEditor = true;
            root.AddComponent<SpatialFrameRateDebugControls>();
            Directory.CreateDirectory("Assets/Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save IMU scene.");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (!replaceUntitled) EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.Refresh();
    }

    static void EnsureFrameRateControls()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var diagnostics = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ImuDiagnostics>(true)).ToArray();
            if (diagnostics.Length != 1) throw new Exception("Expected one IMU diagnostics root in " + ScenePath);
            var root = diagnostics[0].gameObject;
            if (root.GetComponent<SpatialFrameRateDebugControls>() == null)
            {
                root.AddComponent<SpatialFrameRateDebugControls>();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("SPATIALAI_UNITY6_FRAME_RATE_UI_READY controls=" + root.GetComponents<SpatialFrameRateDebugControls>().Length);
        }
        finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    public static void Verify()
    {
        if (PlayerSettings.productName != ProductName || EditorBuildSettings.scenes.Length == 0 ||
            EditorBuildSettings.scenes[0].path != ScenePath || !EditorBuildSettings.scenes[0].enabled ||
            EditorSceneManager.playModeStartScene == null || AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) != ScenePath)
            throw new Exception("Unity 6 product name / default IMU scene mismatch.");
        Debug.Log("SPATIALAI_UNITY6_IMU_SETTINGS_PASS");
    }

    [MenuItem("Spatial AI/Unity 6/Build IMU APK")]
    public static void Build()
    {
        Configure();
        string previousSdk = UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath;
        string previousOverride = Environment.GetEnvironmentVariable("SPATIALAI_ANDROID_SDK_ROOT");
        var bundledSdk = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/SDK");
        try
        {
            UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = bundledSdk;
            Environment.SetEnvironmentVariable("SPATIALAI_ANDROID_SDK_ROOT", bundledSdk);
            Directory.CreateDirectory("Builds/Android");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Android/Spatial-XR-Unity6.apk",
                target = BuildTarget.Android, options = BuildOptions.None
            });
            if (result.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Unity 6 IMU APK build failed.");
            Debug.Log("SPATIALAI_UNITY6_IMU_APK " + Path.GetFullPath("Builds/Android/Spatial-XR-Unity6.apk"));
        }
        finally
        {
            UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = previousSdk;
            Environment.SetEnvironmentVariable("SPATIALAI_ANDROID_SDK_ROOT", previousOverride);
        }
    }

    [Serializable] sealed class SetupStatus { public bool success; public string editor, productName, scene, error; }
    [Serializable] sealed class StartSceneSettings { public string scene; public bool useAsPlayModeStartScene; }
}
