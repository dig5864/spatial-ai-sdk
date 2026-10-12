using SpatialAI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpatialAI.Editor
{
    public static class SpatialUvcCalibrationScene
    {
        [MenuItem("Spatial AI/UVC/Create Calibration Capture Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string path = "Assets/SpatialUvcCalibration.unity";
            if (System.IO.File.Exists(path)) { EditorSceneManager.OpenScene(path); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Populate(scene);
            EditorSceneManager.SaveScene(scene, path);
        }
        public static void Populate(Scene scene)
        {
            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var host = new GameObject("UVC Calibration Capture");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<SpatialUvcCalibrationRecorder>();
            Selection.activeGameObject = host;
        }
    }
}
