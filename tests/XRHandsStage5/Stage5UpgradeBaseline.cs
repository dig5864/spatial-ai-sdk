using System;
using System.IO;
using SpatialAI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class Stage5UpgradeBaseline
{
    public static void Run()
    {
        var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.spatialai.sdk");
        if(package.version!="0.2.0-preview.9")throw new Exception("Expected actual tracked SDK baseline 0.2.0-preview.9");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Application Custom Host").AddComponent<SpatialRuntimeHost>();
        EditorSceneManager.SaveScene(scene,"Assets/Application.unity");
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,"com.spatialai.validation.upgrade");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Application.unity",true)};
        File.WriteAllText("upgrade-scene-guid.txt",AssetDatabase.AssetPathToGUID("Assets/Application.unity"));
        File.Copy("Assets/Application.unity","baseline-scene.txt");AssetDatabase.SaveAssets();
        Debug.Log("SPATIALAI_STAGE5_UPGRADE_BASELINE_PASS version="+package.version);
    }
}
