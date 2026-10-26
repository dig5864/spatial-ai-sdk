using UnityEngine;
using UnityEngine.XR;

namespace SpatialAI.Unity
{
    /// <summary>A display-only consumer. Never starts a camera or hand inference.</summary>
    public sealed class SpatialDisplaySample : MonoBehaviour
    {
        private TextMesh status;
        private int frames;
        private float window, renderFps;
        private void Start()
        {
            Application.targetFrameRate = 60;
            var material = SpatialSampleMaterial.Create(Color.cyan);
            foreach (float x in new[] { -.6f, 0f, .6f })
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(transform, false); cube.transform.localPosition = new Vector3(x, 0, 3);
                cube.transform.localScale = Vector3.one * .3f; cube.GetComponent<Renderer>().sharedMaterial = material;
            }
            var label = new GameObject("Display Status"); label.transform.SetParent(Camera.main.transform, false);
            label.transform.localPosition = new Vector3(-.55f, -.35f, 2.8f);
            status = label.AddComponent<TextMesh>(); status.characterSize = .014f; status.fontSize = 40;
            status.color = Color.white; status.anchor = TextAnchor.UpperLeft;
            Debug.Log("SpatialDisplay: display-only 3DOF sample; hand camera is not started.");
        }
        private void Update()
        {
            frames++; float elapsed = Time.unscaledTime - window;
            if (elapsed < 1) return;
            renderFps = frames / elapsed; frames = 0; window = Time.unscaledTime;
            var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            bool rotationValid = device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation);
            if (status != null) status.text = "DISPLAY / 3DOF\nXR " + (XRSettings.isDeviceActive ? "ON" : "WAIT") +
                "  Render " + renderFps.ToString("F1") + " FPS\nRotation " + (rotationValid ? rotation.eulerAngles.ToString("F0") : "unavailable");
            Debug.Log("SpatialDisplay: xr=" + XRSettings.isDeviceActive + " rotationValid=" + rotationValid + " renderFps=" + renderFps.ToString("F1"));
        }
    }
}
