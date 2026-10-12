using System;
using System.IO;
using SpatialAI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace SpatialAI.Editor
{
    public static class SpatialDisplayScene
    {
        public const string ScenePath = "Assets/Scenes/SpatialDisplay.unity";
        [MenuItem("Spatial AI/Display/Create Display and 3DOF Scene")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            var bridgeType = FindType("SpatialAI.Cardboard.SpatialCardboardBridge");
            if (bridgeType == null) throw new InvalidOperationException("Install the Spatial Cardboard package before creating the display scene.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PopulateXR();
            new GameObject("Display Content").AddComponent<SpatialDisplaySample>();
            Directory.CreateDirectory("Assets/Scenes"); EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("Display scene created. Add it to Build Settings; no hand source or camera capture component is created.");
        }
        public static Camera PopulateXR()
        {
            var bridgeType = FindType("SpatialAI.Cardboard.SpatialCardboardBridge");
            if (bridgeType != null)
            {
            var bridge = new GameObject("Spatial Cardboard Display").AddComponent(bridgeType);
            bridgeType.GetField("useGenericViewerForDiagnostics").SetValue(bridge, true);
            bridgeType.GetField("useRhinoXReferenceMeshForDiagnostics").SetValue(bridge, true);
            }
            else Debug.LogWarning("Spatial display: install/configure the supplied Cardboard loader for stereo Android output. Editor-only hand validation can continue.");
            var camera = new GameObject("XR Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.nearClipPlane = .05f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.015f, .02f, .04f);
            var driver = camera.gameObject.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.rotationInput = new InputActionProperty(new InputAction("Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            return camera;
        }
        private static Type FindType(string name)
        { foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { var type = assembly.GetType(name); if (type != null) return type; } return null; }
    }
}
