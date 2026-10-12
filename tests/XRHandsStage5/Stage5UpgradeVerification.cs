using System;
using System.IO;
using SpatialAI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class Stage5UpgradeVerification
{
    public static void Run()
    {
        var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.spatialai.sdk");
        if(package.version!="0.2.0-preview.10")throw new Exception("Expected upgrade target preview.10");
        if(AssetDatabase.AssetPathToGUID("Assets/Application.unity")!=File.ReadAllText("upgrade-scene-guid.txt"))throw new Exception("Scene GUID changed");
        if(File.ReadAllText("Assets/Application.unity")!=File.ReadAllText("baseline-scene.txt"))throw new Exception("Existing scene was rewritten by package upgrade");
        EditorSceneManager.OpenScene("Assets/Application.unity");
        if(UnityEngine.Object.FindObjectOfType<SpatialRuntimeHost>()==null)throw new Exception("Existing SDK component no longer resolves");
        if(EditorBuildSettings.scenes.Length!=1||EditorBuildSettings.scenes[0].path!="Assets/Application.unity")throw new Exception("Application Build Settings changed");
        if(PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)!="com.spatialai.validation.upgrade")throw new Exception("Application ID changed");
        Debug.Log("SPATIALAI_STAGE5_UPGRADE_PASS checks=6");
    }
}
