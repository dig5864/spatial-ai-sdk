#if SPATIALAI_HAS_CARDBOARD
using SpatialAI.Cardboard;
using SpatialAI.Unity;
using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using Pointer = UnityEngine.InputSystem.Pointer;
#endif

namespace SpatialAI.Samples
{
    [RequireComponent(typeof(SpatialCardboardBridge))]
    public sealed class HelloSpatialXR : MonoBehaviour
    {
        public Camera xrCamera;
        public TextAsset uvcCalibrationJson;
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
        private bool phoneHost;
        private SpatialHandTracking phoneHands;
        private float phoneFps;
        private float nextPhoneLog;

        private void Start()
        {
            bridge = GetComponent<SpatialCardboardBridge>();
            phoneHost = SpatialDeviceProfiles.SelectHost(SpatialDeviceProfiles.ReadHostIdentity(), out var host).Success;
            if (phoneHost)
            {
                phoneHands = gameObject.AddComponent<SpatialHandTracking>();
            }
            var hands = gameObject.AddComponent<HandTrackingDemo>();
            hands.xrCamera = xrCamera;
            hands.uvcCalibrationJson = uvcCalibrationJson;
            hands.reflectUvcDisplayX = phoneHost && host.reflectHandDisplayX;
            hands.defaultUvcSwapHandedness = phoneHost && host.uvcSwapHandedness;
            if (target != null) targetMaterial = target.material;
            // Device diagnostics share one stereo panel in HandTrackingDemo.
            bool showPanel = false;
#if UNITY_EDITOR
            showPanel = true;
#endif
            if (showPanel && xrCamera != null)
            {
                var statusObject = new GameObject("Spatial XR status HUD");
                statusObject.transform.SetParent(xrCamera.transform, false);
                statusObject.transform.localPosition = new Vector3(-0.37f, 0.34f, 1.8f);
                statusText = statusObject.AddComponent<TextMesh>();
                statusText.anchor = TextAnchor.UpperLeft;
                statusText.alignment = TextAlignment.Left;
                statusText.fontSize = 48;
                statusText.characterSize = 0.006f;
                statusText.color = Color.white;
                if (phoneHost)
                {
                    statusObject.transform.localPosition = new Vector3(-0.65f, 0.40f, 1.8f);
                    statusText.characterSize = 0.0045f;
                }
            }
        }

        private float opticsHoldStarted = -1f;
        private void Update()
        {
            if (!phoneHost) return;
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            var point = pointer != null ? pointer.position.ReadValue() : Vector2.zero;
            bool pressed = pointer != null && pointer.press.isPressed;
#else
            var point = Input.mousePosition;
            bool pressed = Input.GetMouseButton(0);
#endif
            if (!pressed || point.x > Screen.width * 0.2f || point.y > Screen.height * 0.2f) { opticsHoldStarted = -1f; return; }
            if (opticsHoldStarted < 0f) opticsHoldStarted = Time.unscaledTime;
            if (Time.unscaledTime - opticsHoldStarted > 1f && Application.CanStreamedLevelBeLoaded("OpticsComparison"))
                UnityEngine.SceneManagement.SceneManager.LoadScene("OpticsComparison");
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
            if (statusText == null && !phoneHost) return;
            if (phoneHost)
            {
                phoneFps = Mathf.Lerp(phoneFps, 1f / Mathf.Max(0.001f, Time.unscaledDeltaTime), 0.05f);
                var angles = xrCamera.transform.rotation.eulerAngles;
                var q = xrCamera.transform.rotation;
                var cameraState = phoneHands != null ? phoneHands.State : "initializing";
                var frame = phoneHands != null ? phoneHands.Latest : null;
                if (Time.unscaledTime >= nextPhoneLog)
                {
                    nextPhoneLog = Time.unscaledTime + 3f;
                    Debug.Log("SpatialAI PHONE profile=" + (bridge.MatchedHostProfile != null ? bridge.MatchedHostProfile.profileId : "pending") +
                        " xr=" + bridge.XrRunning + " fps=" + phoneFps.ToString("F1") + " q=" + q.ToString("F3") + " camera=" + cameraState + " captureFps=" + phoneHands.CaptureFps.ToString("F1") + " previewFps=" + phoneHands.PreviewFps.ToString("F1") + " gestures=" + SpatialHandTracking.GestureRecognitionEnabled);
                }
                if (cameraState.Length > 42) cameraState = cameraState.Insert(42, "\n");
                if (statusText != null) statusText.text = "Hold bottom-left: optics test\nXIAOMI 2206123SC | PHONE\n" +
                    "XR " + (bridge.XrRunning ? "ON" : "WAIT") + " | " + phoneFps.ToString("F0") + " FPS\n" +
                    "IMU: Cardboard phone tracking\n" +
                    "Pitch/Yaw/Roll " + Mathf.DeltaAngle(0, angles.x).ToString("F1") + "/" +
                    Mathf.DeltaAngle(0, angles.y).ToString("F1") + "/" + Mathf.DeltaAngle(0, angles.z).ToString("F1") + "\n" +
                    "Q " + q.ToString("F2") + "\n" +
                    "Viewer " + (bridge.UsingGenericViewerParams ? "GENERIC TEST" : bridge.ViewerParamsReady ? "LOADED" : "WAIT") + "\n" +
                    "Camera: EXTERNAL UVC ONLY\n" + cameraState + "\n" +
                    "Gestures " + (SpatialHandTracking.GestureRecognitionEnabled ? "ON" : "OFF") + " | Hands " + (frame != null && frame.hands != null ? frame.hands.Length : 0) +
                    " | AI " + (frame != null ? frame.resultFps.ToString("F1") : "--") + " FPS\n" +
                    "Capture " + phoneHands.CaptureFps.ToString("F1") + " | Preview " + phoneHands.PreviewFps.ToString("F1") + " FPS";
                return;
            }
            var display = bridge != null && bridge.XrRunning ? "ON" : "WAIT";
            var viewer = bridge != null && bridge.ViewerParamsReady
                ? bridge.UsingGenericViewerParams ? "GENERIC TEST" : "LOADED"
                : "MISSING";
            var pose = headPoseAvailable ? "XR feature" : "unavailable";
            var gaze = hovered ? "Target " + (int)(100f * Mathf.Clamp01(hoverSeconds / Mathf.Max(0.1f, dwellSeconds))) + "%" : "Find cyan cube";
            statusText.text = "SPATIAL XR\n" +
                "Display " + display + " Viewer " + viewer + "\n" +
                "Head " + pose + " | " + gaze + "\n" +
                "Hits " + hits + " | " + lastActivation + "\n" +
                "RhinoX mesh " + (bridge != null && bridge.RhinoXLegacyMeshApplied ? "ON" : "OFF");
        }

        private void OnGUI()
        {
            if (phoneHost)
            {
                var style = new GUIStyle(GUI.skin.button) { fontSize = 24 };
                if (Application.CanStreamedLevelBeLoaded("OpticsComparison") &&
                    GUI.Button(new Rect(20, 20, 280, 65), "Optics comparison", style))
                    UnityEngine.SceneManagement.SceneManager.LoadScene("OpticsComparison");
                return;
            }
#if UNITY_EDITOR
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
#endif
        }
    }
}
#endif
