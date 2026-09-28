using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SpatialAI.Unity;

namespace SpatialAI.Samples.Editor
{
    public static class CreateSampleScene
    {
        [MenuItem("Spatial AI/Create Simulator Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Spatial AI Simulator");
            root.AddComponent<SpatialRuntimeHost>();
            root.AddComponent<HelloSpatial>();
            Selection.activeGameObject = root;
            Debug.Log("Save the scene and press Play. Use the sliders and trigger button. All poses are simulated.");
        }
    }
}
