using UnityEngine;
using SpatialAI.Core;
using SpatialAI.Unity;

namespace SpatialAI.Samples
{
    [RequireComponent(typeof(SpatialRuntimeHost))]
    public sealed class HelloSpatial : MonoBehaviour
    {
        private SpatialRuntimeHost host;
        private Camera view;
        private GameObject target;
        private LineRenderer ray;
        private Material targetMaterial, rayMaterial;
        private float headYaw, headPitch, controllerYaw, controllerPitch;
        private bool triggerHeld, hovered;
        private int hitCount;
        private void Start()
        {
            host = GetComponent<SpatialRuntimeHost>();
            view = new GameObject("Simulator Camera").AddComponent<Camera>();
            view.transform.SetParent(transform, false);
            view.transform.localPosition = new Vector3(0, 1.6f, 0);
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.03f, 0.05f, 0.09f);
            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Trigger Target";
            target.transform.SetParent(transform, false);
            target.transform.localPosition = new Vector3(0, 1.6f, 4);
            // Keep one unlit shader to avoid render-pipeline-specific materials.
            var shader = Shader.Find("SpatialAI/SampleUnlit");
            if (shader == null) { Debug.LogError("Import the complete Hello Spatial sample, including its shader."); enabled = false; return; }
            targetMaterial = new Material(shader);
            target.GetComponent<Renderer>().sharedMaterial = targetMaterial;
            ray = new GameObject("Simulated Controller Ray").AddComponent<LineRenderer>();
            ray.transform.SetParent(transform, false);
            ray.positionCount = 2; ray.startWidth = ray.endWidth = 0.012f;
            rayMaterial = new Material(shader); rayMaterial.color = Color.cyan; ray.sharedMaterial = rayMaterial;
        }
        private void LateUpdate()
        {
            if (host == null || host.Session == null || view == null || target == null || ray == null) return;
            if (host.Session.State != SessionState.Running) { ray.enabled = false; return; }
            var frame = host.Session.Frame;
            if (!frame.Head.IsTracked || !frame.Controller.IsTracked) { ray.enabled = false; return; }
            ray.enabled = true;
            view.transform.localRotation = SpatialRuntimeHost.ToUnity(frame.Head.Orientation);
            Vector3 origin = transform.TransformPoint(new Vector3(0.15f, 1.45f, 0.1f));
            Vector3 direction = transform.rotation * SpatialRuntimeHost.ToUnity(frame.Controller.Orientation) * Vector3.forward;
            RaycastHit hit;
            hovered = Physics.Raycast(origin, direction, out hit, 10) && hit.collider.gameObject == target;
            ray.SetPosition(0, origin); ray.SetPosition(1, hovered ? hit.point : origin + direction * 8);
            if (hovered && host.Session.TriggerPressed) hitCount++;
            targetMaterial.color = hovered ? Color.yellow : new Color(0.2f, 0.7f, 0.9f);
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 360, 355), GUI.skin.box);
            GUILayout.Label("Spatial AI SDK | SIMULATOR ONLY");
            GUILayout.Label("No hardware tracking / stereo display");
            if (host == null || host.Session == null) { GUILayout.EndArea(); return; }
            GUILayout.Label("State: " + host.Session.State + " | Hits: " + hitCount);
            GUILayout.Label("Head yaw"); headYaw = GUILayout.HorizontalSlider(headYaw, -90, 90);
            GUILayout.Label("Head pitch"); headPitch = GUILayout.HorizontalSlider(headPitch, -45, 45);
            GUILayout.Label("Controller yaw"); controllerYaw = GUILayout.HorizontalSlider(controllerYaw, -90, 90);
            GUILayout.Label("Controller pitch"); controllerPitch = GUILayout.HorizontalSlider(controllerPitch, -45, 45);
            triggerHeld = GUILayout.RepeatButton(hovered ? "Hold trigger: target selected" : "Hold trigger");
            if (GUILayout.Button("Recenter heading")) host.Session.Recenter();
            if (host.Simulator != null)
                host.Simulator.SetInput(Rotation.FromYawPitch(headYaw, headPitch), Rotation.FromYawPitch(controllerYaw, controllerPitch), triggerHeld);
            GUILayout.Label(host.Session.LastResult.Message);
            GUILayout.EndArea();
        }
        private void OnDestroy()
        {
            if (targetMaterial != null) Destroy(targetMaterial);
            if (rayMaterial != null) Destroy(rayMaterial);
        }
    }
}
