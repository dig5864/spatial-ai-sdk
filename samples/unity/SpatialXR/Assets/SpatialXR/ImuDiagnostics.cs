#if SPATIALAI_HAS_CARDBOARD
using System.Collections.Generic;
using SpatialAI.Cardboard;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace SpatialAI.Samples
{
    [RequireComponent(typeof(SpatialCardboardBridge))]
    public sealed class ImuDiagnostics : MonoBehaviour
    {
        public Camera xrCamera;

        private SpatialCardboardBridge bridge;
        private TextMesh readout;
        private Quaternion referenceRotation = Quaternion.identity;
        private bool hasReference;
        private bool poseAvailable;
        private bool previousTrigger;
        private float nextLogTime;
        private string poseSource = "none";
        private readonly List<UnityEngine.XR.InputDevice> headDevices = new List<UnityEngine.XR.InputDevice>();
        private GameObject axes;

        private void Start()
        {
            bridge = GetComponent<SpatialCardboardBridge>();
            Input.gyro.enabled = true;
            if (xrCamera == null) return;

            var panel = new GameObject("IMU stereo readout");
            panel.transform.SetParent(xrCamera.transform, false);
            panel.transform.localPosition = new Vector3(-0.68f, 0.45f, 2f);
            readout = panel.AddComponent<TextMesh>();
            readout.anchor = TextAnchor.UpperLeft;
            readout.alignment = TextAlignment.Left;
            readout.fontSize = 64;
            readout.characterSize = 0.008f;
            readout.lineSpacing = 1.2f;
            readout.color = Color.white;
        }

        private void Update()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            Quaternion rotation = Quaternion.identity;
            poseAvailable = TryReadPose(head, out rotation);
            poseSource = poseAvailable ? "XR Head" : "unavailable";
            if (!poseAvailable)
            {
                head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
                poseAvailable = TryReadPose(head, out rotation);
                if (poseAvailable) poseSource = "XR CenterEye";
            }
            if (!poseAvailable)
            {
                headDevices.Clear();
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, headDevices);
                foreach (var device in headDevices)
                {
                    if (!TryReadPose(device, out rotation)) continue;
                    head = device;
                    poseAvailable = true;
                    poseSource = "XR " + device.name;
                    break;
                }
            }
            if (!poseAvailable)
            {
                var hmd = InputSystem.GetDevice<XRHMD>();
                if (hmd != null && hmd.centerEyeRotation != null)
                {
                    rotation = hmd.centerEyeRotation.ReadValue();
                    poseAvailable = Quaternion.Dot(rotation, rotation) > 0.9f;
                    if (poseAvailable) poseSource = "Input System HMD";
                }
            }

            var trigger = bridge != null && bridge.TriggerPressed;
            if (Input.GetMouseButtonDown(0) || (trigger && !previousTrigger))
            {
                hasReference = false;
                if (axes != null) PlaceWorldAxes();
            }
            previousTrigger = trigger;

            var xrReady = bridge != null && bridge.XrRunning;
            if (xrReady && axes == null && xrCamera != null) CreateWorldAxes();
            var gyro = Input.gyro.rotationRateUnbiased * Mathf.Rad2Deg;
            var acceleration = Input.acceleration;
            var euler = Vector3.zero;
            var position = Vector3.zero;
            if (xrReady && poseAvailable)
            {
                if (!hasReference)
                {
                    referenceRotation = rotation;
                    hasReference = true;
                }
                var relative = Quaternion.Inverse(referenceRotation) * rotation;
                var angles = relative.eulerAngles;
                euler = new Vector3(Mathf.DeltaAngle(0, angles.x),
                    Mathf.DeltaAngle(0, angles.y), Mathf.DeltaAngle(0, angles.z));
                if (head.isValid) head.TryGetFeatureValue(XRCommonUsages.centerEyePosition, out position);
            }
            else hasReference = false;

            var message = "SPATIAL AI | IMU CHECK\n" +
                "XR: " + (xrReady ? "RUNNING" : "WAIT") + "   Pose: " + poseSource + "\n" +
                "Relative head angle (deg)\n" +
                "Yaw   Y: " + Format(euler.y) + "  turn left/right\n" +
                "Pitch X: " + Format(euler.x) + "  look up/down\n" +
                "Roll  Z: " + Format(euler.z) + "  tilt head\n" +
                "Phone gyro deg/s (device XYZ)\n" +
                "  " + Format(gyro.x) + "  " + Format(gyro.y) + "  " + Format(gyro.z) + "\n" +
                "Phone accel g (device XYZ)\n" +
                "  " + Format(acceleration.x) + "  " + Format(acceleration.y) + "  " + Format(acceleration.z) + "\n" +
                "Eye XYZ m: " + Format(position.x) + " " + Format(position.y) + " " + Format(position.z) + "\n" +
                "Tap / trigger: zero + face axes";
            if (readout != null) readout.text = message;

            if (Time.unscaledTime >= nextLogTime)
            {
                nextLogTime = Time.unscaledTime + 1f;
                Debug.Log("SpatialAI IMU | " + message.Replace('\n', ' ') +
                    " | Head valid=" + InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid +
                    " CenterEye valid=" + InputDevices.GetDeviceAtXRNode(XRNode.CenterEye).isValid +
                    " HMD count=" + headDevices.Count +
                    " Input HMD=" + (InputSystem.GetDevice<XRHMD>() != null) +
                    " Camera q=" + (xrCamera != null ? xrCamera.transform.rotation.ToString("F3") : "none"));
            }
        }

        private static bool TryReadPose(UnityEngine.XR.InputDevice device, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            return device.isValid &&
                (device.TryGetFeatureValue(XRCommonUsages.centerEyeRotation, out rotation) ||
                 device.TryGetFeatureValue(XRCommonUsages.deviceRotation, out rotation));
        }

        private void CreateWorldAxes()
        {
            axes = new GameObject("World fixed 3DoF axes");
            PlaceWorldAxes();
            CreateAxis("X", Vector3.right, Color.red);
            CreateAxis("Y", Vector3.up, Color.green);
            CreateAxis("Z", Vector3.forward, Color.blue);
            var origin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            origin.name = "Axis origin";
            origin.transform.SetParent(axes.transform, false);
            origin.transform.localScale = Vector3.one * 0.09f;
        }

        private void PlaceWorldAxes()
        {
            var cameraTransform = xrCamera.transform;
            axes.transform.position = cameraTransform.position + cameraTransform.forward * 3f -
                cameraTransform.up * 0.45f;
            axes.transform.rotation = cameraTransform.rotation;
            Debug.Log("SpatialAI IMU world axes placed at " + axes.transform.position.ToString("F3"));
        }

        private void CreateAxis(string name, Vector3 direction, Color color)
        {
            var lineObject = new GameObject(name + " axis");
            lineObject.transform.SetParent(axes.transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, direction * 0.65f);
            line.startWidth = line.endWidth = 0.035f;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) line.material = new Material(shader) { color = color };
            line.startColor = line.endColor = color;

            var label = new GameObject(name + " label");
            label.transform.SetParent(axes.transform, false);
            label.transform.localPosition = direction * 0.75f;
            var text = label.AddComponent<TextMesh>();
            text.text = name;
            text.fontSize = 72;
            text.characterSize = 0.13f;
            text.color = color;
        }

        private static string Format(float value) { return value.ToString("+0.0;-0.0;0.0"); }
    }
}
#endif
