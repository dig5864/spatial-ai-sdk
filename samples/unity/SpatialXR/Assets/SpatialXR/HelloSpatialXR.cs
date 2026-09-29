#if SPATIALAI_HAS_CARDBOARD
using SpatialAI.Cardboard;
using UnityEngine;
using UnityEngine.XR;

namespace SpatialAI.Samples
{
    [RequireComponent(typeof(SpatialCardboardBridge))]
    public sealed class HelloSpatialXR : MonoBehaviour
    {
        public Camera xrCamera;
        public Renderer target;
        public LineRenderer gazeRay;
        public bool enableGazeDwell = true;
        public float dwellSeconds = 1.5f;
        private SpatialCardboardBridge bridge;
        private Material targetMaterial;
        private TextMesh statusText;
        private int hits;
        private bool headPoseAvailable;
        private bool hovered;
        private bool dwellTriggered;
        private bool targetPlaced;
        private float hoverSeconds;
        private float feedbackUntil;
        private string lastActivation = "none";

        private void Start()
        {
            bridge = GetComponent<SpatialCardboardBridge>();
            var hands = gameObject.AddComponent<HandTrackingDemo>();
            hands.xrCamera = xrCamera;
            if (target != null) targetMaterial = target.material;
            if (xrCamera != null)
            {
                var statusObject = new GameObject("Spatial XR status HUD");
                statusObject.transform.SetParent(xrCamera.transform, false);
                statusObject.transform.localPosition = new Vector3(-0.36f, 0.22f, 1.8f);
                statusText = statusObject.AddComponent<TextMesh>();
                statusText.anchor = TextAnchor.UpperLeft;
                statusText.alignment = TextAlignment.Left;
                statusText.fontSize = 48;
                statusText.characterSize = 0.012f;
                statusText.color = Color.white;
            }
        }

        private void LateUpdate()
        {
            headPoseAvailable = false;
            hovered = false;
            if (bridge == null || !bridge.XrRunning || xrCamera == null || target == null)
            {
                if (gazeRay != null) gazeRay.enabled = false;
                UpdateStatus();
                return;
            }
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            Quaternion orientation;
            headPoseAvailable = head.isValid &&
                (head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out orientation) ||
                 head.TryGetFeatureValue(CommonUsages.deviceRotation, out orientation));
            // Gaze remains usable when the XR input provider has no pose feature.
            if (!targetPlaced)
            {
                target.transform.position = xrCamera.transform.position + xrCamera.transform.forward * 3f;
                targetPlaced = true;
            }
            var origin = xrCamera.transform.position;
            var direction = xrCamera.transform.forward;
            RaycastHit hit;
            hovered = Physics.Raycast(origin, direction, out hit, 10f) && hit.collider.GetComponent<Renderer>() == target;
            if (gazeRay != null)
            {
                gazeRay.enabled = true;
                gazeRay.SetPosition(0, origin);
                gazeRay.SetPosition(1, hovered ? hit.point : origin + direction * 8f);
            }
            if (hovered)
            {
                hoverSeconds += Time.deltaTime;
                if (bridge.TriggerPressed)
                {
                    RegisterHit("viewer trigger");
                    dwellTriggered = true;
                }
                else if (enableGazeDwell && !dwellTriggered && hoverSeconds >= dwellSeconds)
                {
                    RegisterHit("gaze dwell");
                    dwellTriggered = true;
                }
            }
            else
            {
                hoverSeconds = 0;
                dwellTriggered = false;
            }
            if (targetMaterial != null)
                targetMaterial.color = Time.time < feedbackUntil ? Color.green : hovered ? Color.yellow : Color.cyan;
            UpdateStatus();
        }

        private void RegisterHit(string activation)
        {
            hits++;
            lastActivation = activation;
            feedbackUntil = Time.time + 0.5f;
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;
            var display = bridge != null && bridge.XrRunning ? "ON" : "WAIT";
            var viewer = bridge != null && bridge.ViewerParamsReady
                ? bridge.UsingGenericViewerParams ? "GENERIC TEST" : "LOADED"
                : "MISSING";
            var pose = headPoseAvailable ? "XR feature" : "unavailable";
            var gaze = hovered ? "Target " + (int)(100f * Mathf.Clamp01(hoverSeconds / Mathf.Max(0.1f, dwellSeconds))) + "%" : "Find cyan cube";
            statusText.text = "SPATIAL XR\n" +
                "Display " + display + "  Viewer " + viewer + "\n" +
                "Head " + pose + "  " + gaze + "\n" +
                "Hits " + hits + "  Last " + lastActivation + "\n" +
                "RhinoX Pro Sharp mesh: " + (bridge != null && bridge.RhinoXLegacyMeshApplied ? "ON" : "OFF") + " (unverified)";
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 520, 260), GUI.skin.box);
            GUILayout.Label("Spatial AI | Cardboard XR baseline");
            GUILayout.Label("XR display: " + (bridge != null && bridge.XrRunning ? "running" : "unavailable") +
                " | Head pose available: " + headPoseAvailable + " | Hits: " + hits);
            GUILayout.Label("Viewer params: " + (bridge != null && bridge.ViewerParamsReady ?
                bridge.UsingGenericViewerParams ? "generic diagnostic, NOT RhinoX calibration" : "loaded" : "missing"));
            GUILayout.Label("Aim at the cyan cube; yellow means hover, green means hit. Hold gaze for " + dwellSeconds + " seconds or use Cardboard trigger.");
            if (bridge != null && bridge.Identity != null)
                GUILayout.Label("Phone device: " + bridge.Identity.DisplayName);
            if (bridge != null && bridge.MatchedProfile != null)
                GUILayout.Label("Matched optics: " + bridge.MatchedProfile.profileId);
            if (bridge != null && !bridge.ProfileResult.Success)
                GUILayout.Label("Optics status: " + bridge.ProfileResult.Message);
            GUILayout.Label("RhinoX Pro Sharp mesh: " + (bridge != null && bridge.RhinoXLegacyMeshApplied ? "applied" : "not applied") + "; visual calibration pending.");
            GUILayout.Label("Head pose availability is not a sensor health check.");
            GUILayout.EndArea();
        }
    }
}
#endif
