using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using SpatialAI.Unity;

namespace SpatialAI.Editor
{
    public sealed class SpatialBuildCheck : IProcessSceneWithReport
    {
        public int callbackOrder { get { return 0; } }
        [MenuItem("Spatial AI/Check Setup")]
        public static void CheckSetup()
        {
            Debug.Log("Spatial AI SDK 0.2.0-preview.4 | Editor " + Application.unityVersion +
                " | Target " + EditorUserBuildSettings.activeBuildTarget +
                " | Simulator and optional Cardboard baseline available. Unity 6 and Tuanjie hardware certification is pending. " +
                "Import Hello Spatial XR for a Cardboard test; enable its loader separately. RhinoX optics are not applied.");
        }
        public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
        {
            if (report == null) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var host in root.GetComponentsInChildren<SpatialRuntimeHost>(true))
                {
                    if (host.backend != BackendSelection.Simulator)
                        throw new BuildFailedException("Spatial AI SDK: hardware adapter " + host.backend + " is not implemented. Select Simulator for preview builds.");
                    Debug.LogWarning("Spatial AI SDK: this build contains simulated tracking and no stereo XR output.");
                }
        }
    }
}
