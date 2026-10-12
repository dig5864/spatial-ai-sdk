using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace SpatialAI.Editor
{
    /// <summary>Portable consumer setup. Never invokes repository build scripts.</summary>
    public static class SpatialAndroidSetup
    {
        [MenuItem("Spatial AI/Setup/Configure Android XR")]
        public static void Configure()
        {
            if (UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.google.xr.cardboard") == null)
                throw new InvalidOperationException("Install the Spatial Cardboard package supplied with this SDK release before configuring Android XR.");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = settings.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 2; settings.ApplyModifiedPropertiesWithoutUndo(); }

            Directory.CreateDirectory("Assets/XR"); AssetDatabase.Refresh();
            const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, path);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var manager = perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(manager, "Google.XR.Cardboard.XRLoader", BuildTargetGroup.Android))
                throw new InvalidOperationException("Cardboard loader assignment failed. Wait for package compilation and retry.");
            manager.automaticLoading = manager.automaticRunning = true;
            perTarget.SettingsForBuildTarget(BuildTargetGroup.Android).InitManagerOnStart = true;
            EditorUtility.SetDirty(manager); EditorUtility.SetDirty(perTarget);

            // Runtime-created sample materials must survive player shader stripping.
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "Unlit/Color", "Sprites/Default", "UI/Default" })
            {
                var shader = Shader.Find(name); if (shader == null || shaders == null) continue;
                bool exists = false;
                for (int i = 0; i < shaders.arraySize; i++) exists |= shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader;
                if (!exists) { shaders.InsertArrayElementAtIndex(shaders.arraySize); shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = shader; }
            }
            graphics.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            Debug.Log("Spatial Android XR configured: ARM64, IL2CPP, API 28/34, GLES3, landscape, Cardboard. Application ID and Build Settings scenes are preserved. Restart the Editor if Unity requests it after changing Active Input Handling.");
        }

        [MenuItem("Spatial AI/Setup/Build Android APK")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scenes = Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled);
            if (scenes.Length == 0) throw new InvalidOperationException("Add at least one scene to Build Settings before building.");
            var output = Environment.GetEnvironmentVariable("SPATIALAI_APK_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = "Builds/Android/spatialai.apk";
            output = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(scenes, scene => scene.path), locationPathName = output,
                target = BuildTarget.Android, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Spatial Android APK build failed: " + report.summary.result);
            Debug.Log("SPATIALAI_CONSUMER_APK " + output);
        }
    }
}
