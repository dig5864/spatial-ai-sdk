#if UNITY_EDITOR
using System;
using System.IO;
using SpatialAI.Cardboard;
using SpatialAI.Core;
using SpatialAI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PhoneScreenVerification
{
    public static void Build()
    {
        SpatialAndroidSetup.Configure();
        PlayerSettings.companyName = "SpatialAI";
        PlayerSettings.productName = "Screen Adaptation Test";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.spatialai.validation.screen");
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SpatialDisplayScene.PopulateXR();
        new GameObject("Display Content").AddComponent<SpatialAI.Unity.SpatialDisplaySample>();
        new GameObject("Screen Runtime Probe").AddComponent<PhoneScreenRuntimeProbe>();
        string scenePath = "Assets/ScreenAdaptation.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        var bridge = UnityEngine.Object.FindObjectOfType<SpatialCardboardBridge>();
        if (bridge == null || !bridge.useRhinoXReferenceMeshForDiagnostics) throw new Exception("Missing phone reference display bridge.");
        var sample = new PhoneScreenCalibration { phoneKey = "json-roundtrip", widthMm = 144, heightMm = 64.8f, measured = true };
        var roundtrip = JsonUtility.FromJson<PhoneScreenCalibration>(JsonUtility.ToJson(sample));
        if (!roundtrip.IsValid || !roundtrip.measured || roundtrip.widthMm != sample.widthMm) throw new Exception("Screen JSON contract failed.");
        var rig = new ScreenRigProfile { rigId = "json-rig", eyeRectsMm = new[] {-50f,-20f,40f,40f,10f,-20f,40f,40f} };
        var asset = new TextAsset(JsonUtility.ToJson(rig));
        var loaded = SpatialAI.Unity.SpatialPhoneScreen.LoadRig(asset);
        if (loaded == null || !loaded.IsValid || loaded.opticsValidated) throw new Exception("Rig JSON contract failed.");
        UnityEngine.Object.DestroyImmediate(asset);
        string sdk = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        string apk = Path.Combine(sdk, "build/samples/android/spatial-screen-adaptation.apk");
        Directory.CreateDirectory(Path.GetDirectoryName(apk));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scenePath },
            locationPathName = apk, target = BuildTarget.Android, options = BuildOptions.Development });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Screen adaptation APK build failed.");
        Debug.Log("SPATIALAI_PHONE_SCREEN_BUILD_PASS " + apk);
    }
}
#endif
