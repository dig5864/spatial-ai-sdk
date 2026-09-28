#if SPATIALAI_HAS_CARDBOARD
using SpatialAI.Cardboard;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace SpatialAI.Samples.Editor
{
    public static class CreateSpatialXRScene
    {
        [MenuItem("Spatial AI/Create Cardboard XR Sample Scene")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Spatial AI XR Sample");
            var bridge = root.AddComponent<SpatialCardboardBridge>();
            bridge.useGenericViewerForDiagnostics = true;
            bridge.useRhinoXLegacyMeshForDiagnostics = true;
            var sample = root.AddComponent<HelloSpatialXR>();
            sample.enableGazeDwell = true;
            sample.dwellSeconds = 1.5f;
            var cameraObject = new GameObject("XR Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.6f, 0);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.backgroundColor = new Color(0.02f, 0.03f, 0.08f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            ConfigureCamera(camera);
            sample.xrCamera = camera;

            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Gaze target";
            target.transform.position = new Vector3(0, 1.6f, 3);
            var shader = Shader.Find("Unlit/Color");
            if (shader != null) target.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = Color.cyan };
            sample.target = target.GetComponent<Renderer>();

            var rayObject = new GameObject("Gaze ray");
            rayObject.transform.SetParent(root.transform, false);
            var ray = rayObject.AddComponent<LineRenderer>();
            ray.positionCount = 2;
            ray.startWidth = ray.endWidth = 0.008f;
            if (shader != null) ray.sharedMaterial = new Material(shader) { color = Color.green };
            sample.gazeRay = ray;
            Selection.activeGameObject = root;
            Debug.Log("Save the scene, enable Cardboard XR Plugin for Android in XR Plug-in Management, then build. RhinoX optics remain unverified.");
        }

        public static void CreateAndSave()
        {
            Create();
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/HelloSpatialXR.unity");
        }

        [MenuItem("Spatial AI/Repair XR Sample Camera")]
        public static void RepairCameraInOpenScene()
        {
            var sample = Object.FindObjectOfType<HelloSpatialXR>();
            if (sample == null || sample.xrCamera == null)
                throw new System.InvalidOperationException("Open HelloSpatialXR.unity before repairing its camera.");
            ConfigureCamera(sample.xrCamera);
            EditorSceneManager.MarkSceneDirty(sample.gameObject.scene);
            Debug.Log("XR sample camera now uses the Input System Tracked Pose Driver. Save the scene.");
        }

        private static void ConfigureCamera(Camera camera)
        {
            camera.gameObject.tag = "MainCamera";
            var poseDriver = camera.GetComponent<TrackedPoseDriver>();
            if (poseDriver == null) poseDriver = camera.gameObject.AddComponent<TrackedPoseDriver>();
            poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
            poseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            poseDriver.rotationInput = new InputActionProperty(new InputAction(
                "Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion"));
            EditorUtility.SetDirty(poseDriver);
        }
    }
}
#endif
