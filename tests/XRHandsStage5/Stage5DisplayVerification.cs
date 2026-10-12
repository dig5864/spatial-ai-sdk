using System;
using System.IO;
using System.Linq;
using SpatialAI.Editor;
using SpatialAI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;

public static class Stage5DisplayVerification
{
    private static int checks;
    public static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static string Import(string package,string sample)
    {
        var info=UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/"+package);
        Check(info!=null&&info.source.ToString()=="LocalTarball","Consumer resolves a local release tarball: "+package);
        string source=Path.Combine(info.resolvedPath,"Samples~",sample);
        string target="Assets/Samples/"+package+"/"+info.version+"/"+sample;
        foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories))
        {
            var destination=Path.Combine(target,file.Substring(source.Length+1));Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if(!File.Exists(destination))File.Copy(file,destination);
        }
        AssetDatabase.Refresh();return target.Replace('\\','/');
    }
    public static void CheckScene(string path)
    {
        EditorSceneManager.OpenScene(path);
        foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach(var transform in root.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)==0,"No missing scripts: "+path+"/"+transform.name);
    }
    public static string Prepare(bool displayOnly)
    {
        const string keep="Assets/KeepApplicationScene.unity";
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Application scene preserved");EditorSceneManager.SaveScene(scene,keep);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(keep,true)};
        string application=displayOnly?"com.spatialai.validation.stage5.display":"com.spatialai.validation.stage5";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,application);
        SpatialAndroidSetup.Configure();SpatialAndroidSetup.Configure();
        Check(PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)==application,"Setup preserves application identifier");
        Check(EditorBuildSettings.scenes.Length==1&&EditorBuildSettings.scenes[0].path==keep,"Setup preserves application scenes");
        Check(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).SequenceEqual(new[]{GraphicsDeviceType.OpenGLES3}),"GLES3 baseline");
        Check(PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)==ScriptingImplementation.IL2CPP,"IL2CPP");
        Check(PlayerSettings.Android.targetArchitectures==AndroidArchitecture.ARM64&&PlayerSettings.Android.minSdkVersion==(AndroidSdkVersions)28,"ARM64 / API28");
        var directory=Import("com.spatialai.sdk","Display3DOF");string display=directory+"/SpatialDisplay.unity";
        CheckScene(display);Check(UnityEngine.Object.FindObjectOfType<SpatialDisplaySample>()!=null,"Display sample imports");
        Check(UnityEngine.Object.FindObjectOfType<SpatialHandTracking>()==null,"Display sample contains no hand camera owner");
        Check(Camera.main.GetComponent<TrackedPoseDriver>().trackingType==TrackedPoseDriver.TrackingType.RotationOnly,"Display consumes standard 3DOF rotation");
        Check(UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Any(x=>x.GetType().FullName=="SpatialAI.Cardboard.SpatialCardboardBridge"),"Display has the supplied XR bridge");
        SpatialDisplayScene.Create();
        string original=File.ReadAllText(SpatialDisplayScene.ScenePath);SpatialDisplayScene.Create();
        Check(File.ReadAllText(SpatialDisplayScene.ScenePath)==original,"Create preserves existing display scene edits");
        Check(Directory.GetFiles("Assets","SpatialXRNativeDependencies.cs",SearchOption.AllDirectories).Length==0,"No repository native build scripts required");
        Check(Directory.GetFiles("Assets","SpatialXRAndroidGradle.cs",SearchOption.AllDirectories).Length==0,"No Demo Gradle callback copied");
        return display;
    }
    public static void Run()
    {
        try
        {
            // The SDK release installs companion packages; a display scene still creates no hand camera or inference owner.
            var display=Prepare(true);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(display,true)};AssetDatabase.SaveAssets();
            Debug.Log("SPATIALAI_STAGE5_DISPLAY_PASS checks="+checks);EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
