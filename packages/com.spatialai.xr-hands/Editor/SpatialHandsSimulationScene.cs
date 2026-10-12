using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpatialAI.XRHands.Editor
{
    public static class SpatialHandsSimulationScene
    {
        [MenuItem("Spatial AI/XR Hands/Create Simulation Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string path = "Assets/SpatialHandsSimulation.unity";
            if (File.Exists(path)) { EditorSceneManager.OpenScene(path); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Populate();
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = GameObject.Find("Spatial Hands Simulator");
        }
        public static void Populate()
        {
            var origin = new GameObject("Tracking Origin");
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.SetParent(origin.transform, false);
            camera.transform.localPosition = new Vector3(0, 0.06f, 0);
            camera.transform.LookAt(new Vector3(0, 0.02f, 0.55f));
            camera.nearClipPlane = 0.01f; camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var host = new GameObject("Spatial Hands Simulator");
            var simulator = host.AddComponent<SpatialHandsSimulator>();
            simulator.animate = true;
            host.AddComponent<SpatialHandsSimulationControls>().simulator = simulator;
            var visualizer = new GameObject("XR Hands API Visualizer").AddComponent<SpatialXRHandsVisualizer>();
            visualizer.transform.SetParent(origin.transform, false); visualizer.trackingOrigin = origin.transform;
            var text = new GameObject("Simulation Instructions").AddComponent<TextMesh>();
            text.transform.SetParent(origin.transform, false); text.transform.localPosition = new Vector3(-0.30f, 0.25f, 0.6f);
            text.characterSize = 0.007f; text.fontSize = 32; text.color = Color.white;
            text.text = "SIMULATED XR HANDS\nLeft: cyan   Right: orange\n1 Open  2 Fist  3 Pinch\nL / R Tracking   Space Motion\nInspector: Missing Joint / Rotation";
            text.anchor = TextAnchor.UpperLeft;
        }
    }
}
