using System;
using System.IO;
using System.Linq;
using SpatialAI.Editor;
using SpatialAI.Unity;
using SpatialAI.XRHands;
using SpatialAI.XRInteraction;
using SpatialAI.XRInteraction.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class Stage5Verification
{
    private static int checks;
    private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static void Run()
    {
        try
        {
            string display=Stage5DisplayVerification.Prepare(false);
            string sim=Stage5DisplayVerification.Import("com.spatialai.xr-hands","HandsSimulation")+"/SpatialHandsSimulation.unity";
            string uvc=Stage5DisplayVerification.Import("com.spatialai.xr-hands","HandsUvc")+"/SpatialUvcXRHands.unity";
            string interactionDirectory=Stage5DisplayVerification.Import("com.spatialai.xr-interaction","HandsInteraction");
            string interaction=interactionDirectory+"/SpatialHandInteraction.unity";
            foreach(string scene in new[]{sim,uvc,interaction})Stage5DisplayVerification.CheckScene(scene);
            Check(SpatialHandTracking.InferenceBackend==SpatialHandInferenceBackend.CPU,"Release defaults CPU");
            Check(UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Any(x=>x.GetType().FullName=="SpatialAI.Cardboard.SpatialCardboardBridge"),"Imported interaction has display bridge");
            var sample=UnityEngine.Object.FindObjectOfType<SpatialHandInteractionSample>();
            var gesturePrefab = sample == null ? null : new SerializedObject(sample).FindProperty("standardGesturesPrefab").objectReferenceValue as GameObject;
            Check(sample!=null&&sample.interactionRigPrefab!=null&&gesturePrefab!=null,"Imported sample preserves both prefab references");
            Check(sample.interactionRigPrefab.GetComponent<SpatialHandInteraction>().leftRay.inputActions!=null,"Imported prefab resolves input actions");
            var gestures=gesturePrefab.GetComponentsInChildren<MonoBehaviour>(true).Where(x=>x.GetType().FullName=="UnityEngine.XR.Hands.Samples.GestureSample.StaticHandGesture").ToArray();
            Check(gestures.Length==6,"Unchanged official component resolves for both hands and three gestures");
            foreach(var gesture in gestures)
                Check(gesture.GetType().GetProperty("handShapeOrPose").GetValue(gesture)!=null,"Standard shape asset resolves");
            Check(!UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Any(x=>x.GetType().Name=="HandTrackingDemo"||x.GetType().Name=="HelloSpatialXR"),"Release sample does not carry legacy Demo behaviour");
            SpatialHandInteractionScene.CreateSimulation();
            const string simulationInteraction="Assets/Scenes/SpatialHandInteractionSimulation.unity";
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(interaction,true),new EditorBuildSettingsScene(uvc,true),new EditorBuildSettingsScene(display,true),new EditorBuildSettingsScene(sim,true),new EditorBuildSettingsScene(simulationInteraction,true)};
            File.WriteAllText("stage5-scenes.json",JsonUtility.ToJson(new Paths{simulation=sim,interaction=interaction,simulationInteraction=simulationInteraction},true));
            CheckGradleIdempotence();AssetDatabase.SaveAssets();
            Debug.Log("SPATIALAI_STAGE5_IMPORT_PASS checks="+checks);
            EditorSceneManager.OpenScene(sim);new GameObject("Stage 5 imported sample probe").AddComponent<Stage5PlayProbe>();
            EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static void CheckGradleIdempotence()
    {
        string root="build/gradle-fixture",lib=root+"/unityLibrary";Directory.CreateDirectory(lib);Directory.CreateDirectory(root+"/launcher");
        File.WriteAllText(root+"/build.gradle","classpath 'com.android.tools.build:gradle:7.1.2'");
        File.WriteAllText(root+"/settings.gradle","pluginManagement {\n}\n");
        File.WriteAllText(root+"/gradle.properties","android.useAndroidX=false\norg.gradle.parallel=true\n");
        string module="android { compileSdkVersion 32\n minSdkVersion 22\n targetSdkVersion 32\n buildToolsVersion '30.0.2'\n}\ndependencies {\n}\n";
        File.WriteAllText(lib+"/build.gradle",module);File.WriteAllText(root+"/launcher/build.gradle",module);
        var hook=new SpatialHandGradleDependencies();hook.OnPostGenerateGradleAndroidProject(lib);
        var first=File.ReadAllText(lib+"/build.gradle");var settings=File.ReadAllText(root+"/settings.gradle");
        hook.OnPostGenerateGradleAndroidProject(lib);
        Check(first==File.ReadAllText(lib+"/build.gradle")&&settings==File.ReadAllText(root+"/settings.gradle"),"Export callback idempotent");
        Check(first.Contains("compileSdkVersion 34")&&first.Contains("com.herohan:UVCAndroid:1.0.13")&&first.Contains("com.google.mediapipe:tasks-vision:1.0.0"),"Dependencies and compile SDK supplied by package");
        Check(File.ReadAllText(root+"/gradle.properties").Contains("android.useAndroidX=true")&&settings.Contains("com.android.tools:r8:8.3.37"),"AndroidX and Unity2022 dexer repair supplied by package");
    }
    [Serializable] public sealed class Paths { public string simulation,interaction,simulationInteraction; }
}
