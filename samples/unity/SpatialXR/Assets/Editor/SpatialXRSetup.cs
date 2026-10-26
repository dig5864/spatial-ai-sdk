using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEditor.SceneManagement;

public static class SpatialXRSetup
{
    [MenuItem("Spatial AI/Import XR Sample Into Project")]
    public static void ImportSample()
    {
        var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.spatialai.sdk/package.json");
        if (info == null) throw new System.InvalidOperationException("Spatial AI package is not resolved.");
        var source = Path.Combine(info.resolvedPath, "Samples~", "HelloSpatialXR");
        var destination = Path.Combine(Application.dataPath, "SpatialXR");
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        if (Directory.Exists(destination))
        {
            Debug.Log("Spatial XR sample already imported at Assets/SpatialXR; existing edits were preserved.");
            return;
        }
        FileUtil.CopyFileOrDirectory(source, destination);
        AssetDatabase.Refresh();
        Debug.Log("Imported XR sample. Create and save the Cardboard XR scene, then configure the Android or iOS Cardboard loader.");
    }

    [MenuItem("Spatial AI/Configure Cardboard Android XR")]
    public static void ConfigureAndroidXR()
    {
        ConfigureXRFor(BuildTargetGroup.Android);
    }

    [MenuItem("Spatial AI/Configure Cardboard iOS XR")]
    public static void ConfigureIosXR()
    {
        ConfigureXRFor(BuildTargetGroup.iOS);
    }

    private static void ConfigureXRFor(BuildTargetGroup group)
    {
        const string assetPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        if (!AssetDatabase.IsValidFolder("Assets/XR")) AssetDatabase.CreateFolder("Assets", "XR");
        var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(assetPath);
        if (perTarget == null)
        {
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, assetPath);
        }
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        if (!perTarget.HasSettingsForBuildTarget(group))
            perTarget.CreateDefaultSettingsForBuildTarget(group);
        if (!perTarget.HasManagerSettingsForBuildTarget(group))
            perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
        var manager = perTarget.ManagerSettingsForBuildTarget(group);
        if (!XRPackageMetadataStore.AssignLoader(manager, "Google.XR.Cardboard.XRLoader", group))
            throw new System.InvalidOperationException("Could not assign Cardboard XR loader for " + group + ".");
        manager.automaticLoading = true;
        manager.automaticRunning = true;
        perTarget.SettingsForBuildTarget(group).InitManagerOnStart = true;
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(perTarget);
        AssetDatabase.SaveAssets();
        const string scenePath = "Assets/Scenes/HelloSpatialXR.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        Debug.Log("Cardboard " + group + " XR loader configured; verify Player Settings and graphics API before building.");
    }

    [MenuItem("Spatial AI/Build Android XR Sample")]
    public static void BuildAndroidSample()
    {
        ImuPacingComparisonBuild.BuildSdkFrameRateSettings();
    }

    // Shared build path keeps the pacing comparison's scenes and player settings identical.
    public static void BuildAndroidSampleVariant(string outputFileName, string[] extraDefines)
    {
        if (string.IsNullOrEmpty(outputFileName) || Path.GetFileName(outputFileName) != outputFileName)
            throw new System.ArgumentException("Use an APK file name without a directory.", nameof(outputFileName));
        const string scenePath = "Assets/Scenes/HelloSpatialXR.unity";
        if (!File.Exists(scenePath)) throw new FileNotFoundException("Create the XR sample scene first.", scenePath);
        ConfigureAndroidXR();
        const string calibrationPath = "Assets/Scenes/SpatialUvcCalibration.unity";
        if (!File.Exists(calibrationPath))
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new System.OperationCanceledException("Save scenes before generating calibration capture scene.");
            var previousScenes = EditorSceneManager.GetSceneManagerSetup();
            var calibrationScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SpatialAI.Editor.SpatialUvcCalibrationScene.Populate(calibrationScene);
            EditorSceneManager.SaveScene(calibrationScene, calibrationPath);
            if (previousScenes.Length > 0 &&
                System.Array.Exists(previousScenes, scene => scene.isLoaded && scene.isActive) &&
                System.Array.TrueForAll(previousScenes, scene => !string.IsNullOrEmpty(scene.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        PlayerSettings.companyName = "SpatialAI";
        PlayerSettings.productName = "SpatialXRSample";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.spatialai.samples.xr");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "..", "build", "samples", "android", outputFileName));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var scenes=new System.Collections.Generic.List<string>{"Assets/Scenes/ImuDiagnostics.unity",scenePath,"Assets/Scenes/OpticsComparison.unity",calibrationPath};
        // Optional package: base SDK samples still compile without XR Hands installed.
        foreach(var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var factory=assembly.GetType("SpatialAI.XRHands.Editor.SpatialUvcHandsScene");
            if(factory==null)continue;
            factory.GetMethod("Ensure").Invoke(null,null);
            scenes.Add("Assets/Scenes/SpatialUvcXRHands.unity");break;
        }
        foreach(var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var factory=assembly.GetType("SpatialAI.XRInteraction.Editor.SpatialHandInteractionScene");
            if(factory==null)continue;
            factory.GetMethod("Ensure").Invoke(null,null);
            scenes.Add("Assets/Scenes/SpatialHandInteraction.unity");break;
        }
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes.ToArray(), locationPathName = output,
            target = BuildTarget.Android, options = BuildOptions.None,
            extraScriptingDefines = extraDefines ?? new string[0]
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Spatial XR Android build failed: " + report.summary.result);
        Debug.Log("Spatial XR sample APK: " + output);
    }

    [MenuItem("Spatial AI/Build iOS Xcode Sample")]
    public static void BuildIosSample()
    {
        if (Application.platform != RuntimePlatform.OSXEditor)
            throw new System.PlatformNotSupportedException("Export and sign the iOS sample on a macOS machine with iOS Build Support and Xcode.");
        const string scenePath = "Assets/Scenes/HelloSpatialXR.unity";
        if (!File.Exists(scenePath)) throw new FileNotFoundException("Create the XR sample scene first.", scenePath);
        ConfigureIosXR();
        PlayerSettings.companyName = "SpatialAI";
        PlayerSettings.productName = "SpatialXRSample";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.spatialai.samples.xr");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.iOS.targetOSVersionString = "12.0";
        PlayerSettings.iOS.cameraUsageDescription = "Cardboard uses the camera to scan viewer QR parameters.";
        PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "..", "build", "samples", "ios", "spatialxr-xcode"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath }, locationPathName = output,
            target = BuildTarget.iOS, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Spatial XR iOS Xcode export failed: " + report.summary.result);
        Debug.Log("Spatial XR iOS Xcode project: " + output);
    }

    public static void CheckProfileResource()
    {
        SpatialAI.Core.OpticalProfileDescriptor profile;
        var result = SpatialAI.Unity.SpatialDeviceProfiles.Select(
            new SpatialAI.Core.DeviceIdentity { DeviceCode = "RhinoX" }, out profile);
        if (profile == null || profile.profileId != "rhinox-pro-legacy-reference" || result.Success)
            throw new System.Exception("RhinoX reference resource must match and remain unavailable for hardware.");
        Debug.Log("Spatial profile resource loaded and matched; hardware validation rejected as expected: " + result.Message);
    }
}
