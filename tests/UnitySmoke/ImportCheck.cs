using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SpatialAI.Core;
using SpatialAI.Unity;
using SpatialAI.Samples;

public static class ImportCheck
{
    public static void Run()
    {
        if (Shader.Find("SpatialAI/SampleUnlit") == null)
            throw new System.Exception("Sample shader was not imported.");
        using (var session = new SpatialSession(new SimulatorBackend()))
            if (!session.Start().Success || !session.Tick().Success)
                throw new System.Exception("Core runtime cannot run in the installed editor.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Spatial AI Simulator");
        root.AddComponent<SpatialRuntimeHost>();
        root.AddComponent<HelloSpatial>();
        if (!EditorSceneManager.SaveScene(root.scene, "Assets/HelloSpatial.unity"))
            throw new System.Exception("Could not save sample scene.");
        Debug.Log("SPATIAL_IMPORT_CHECK_PASS");
    }
}
