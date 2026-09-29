using System;
using System.IO;
using SpatialAI.Cardboard;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OpticsComparisonAutomation
{
    private const string PatternPath = "Assets/OpticsComparison/Pattern_2160.png";
    private const string ScenePath = "Assets/Scenes/OpticsComparison.unity";
    private static readonly string SdkRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
    private static readonly string RequestPath = Path.Combine(SdkRoot, "build/automation/OpticsComparisonBuild.request");
    private static readonly string StatusPath = Path.Combine(SdkRoot, "build/automation/OpticsComparisonBuild.status");
    private static readonly string ApkPath = Path.Combine(SdkRoot, "build/samples/android/optics-comparison.apk");
    private static double nextCheck;
    private static bool busy;

    static OpticsComparisonAutomation() { EditorApplication.update += Poll; }

    [MenuItem("Spatial AI/Create Optics Comparison Scene")]
    public static void CreateScene()
    {
        AssetDatabase.ImportAsset(PatternPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(PatternPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing comparison pattern.");
        importer.textureType = TextureImporterType.Default;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 4096;
        android.format = TextureImporterFormat.RGBA32;
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();
        var pattern = AssetDatabase.LoadAssetAtPath<Texture2D>(PatternPath);
        if (pattern == null || pattern.width != 2160 || pattern.height != 2160)
            throw new InvalidOperationException("Comparison pattern must be 2160 x 2160.");

        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Spatial XR Optics Comparison");
            var bridge = root.AddComponent<SpatialCardboardBridge>();
            bridge.useGenericViewerForDiagnostics = true;
            bridge.useRhinoXLegacyMeshForDiagnostics = true;
            var cameraObject = new GameObject("XR Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.stereoTargetEye = StereoTargetEyeMask.Both;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.1f;
            cameraObject.AddComponent<OpticsComparisonDisplay>().pattern = pattern;
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save optics comparison scene.");
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log("Optics comparison scene: " + ScenePath);
    }

    [MenuItem("Spatial AI/Build Optics Comparison APK")]
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
            PlayerSettings.productName = "Spatial XR Optics Comparison";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.spatialai.optics.spatialxr");
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
                throw new Exception("Optics comparison Android build failed: " + report.summary.result);
            Debug.Log("Optics comparison APK: " + ApkPath);
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
