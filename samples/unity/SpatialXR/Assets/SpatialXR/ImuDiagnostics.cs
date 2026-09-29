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
        private float nextReadoutTime;
        private string lastReadout = "";
        private float frameWindowStart;
        private float measuredFps;
        private float maxFrameMs;
        private float measuredMaxFrameMs;
        private int frameCount;
        private int resetCount;
        private string lastReset = "startup";
        private string poseSource = "none";
        private readonly List<UnityEngine.XR.InputDevice> headDevices = new List<UnityEngine.XR.InputDevice>();
        private GameObject axes;
        private GameObject calibrationTargets;

        private void Start()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 72;
            frameWindowStart = Time.unscaledTime;
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
            frameCount++;
            maxFrameMs = Mathf.Max(maxFrameMs, Time.unscaledDeltaTime * 1000f);
            var frameWindowSeconds = Time.unscaledTime - frameWindowStart;
            if (frameWindowSeconds >= 1f)
            {
                measuredFps = frameCount / frameWindowSeconds;
                measuredMaxFrameMs = maxFrameMs;
                frameWindowStart = Time.unscaledTime;
                frameCount = 0;
                maxFrameMs = 0f;
            }
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
            var resetInput = ReadResetInput(trigger && !previousTrigger);
            if (resetInput != null) ResetReference(rotation, poseAvailable, resetInput);
            previousTrigger = trigger;

            var xrReady = bridge != null && bridge.XrRunning;
            if (xrReady && axes == null && xrCamera != null) CreateWorldAxes();
            if (xrReady && calibrationTargets == null && xrCamera != null) CreateCalibrationTargets();
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

            if (Time.unscaledTime >= nextReadoutTime)
            {
                nextReadoutTime = Time.unscaledTime + 0.1f;
                lastReadout = "SPATIAL AI | IMU CHECK\n" +
                    "XR: " + (xrReady ? "RUNNING" : "WAIT") + "   Pose: " + poseSource + "\n" +
                    "FPS: " + measuredFps.ToString("0.0") + " / 72   Max frame: " +
                    measuredMaxFrameMs.ToString("0.0") + " ms\n" +
                    "Relative head angle (deg)\n" +
                    "Yaw   Y: " + Format(euler.y) + "  turn left/right\n" +
                    "Pitch X: " + Format(euler.x) + "  look up/down\n" +
                    "Roll  Z: " + Format(euler.z) + "  tilt head\n" +
                    "Phone gyro deg/s (device XYZ)\n" +
                    "  " + Format(gyro.x) + "  " + Format(gyro.y) + "  " + Format(gyro.z) + "\n" +
                    "Phone accel g (device XYZ)\n" +
                    "  " + Format(acceleration.x) + "  " + Format(acceleration.y) + "  " + Format(acceleration.z) + "\n" +
                    "Eye XYZ m: " + Format(position.x) + " " + Format(position.y) + " " + Format(position.z) + "\n" +
                    "Tap / trigger / R: ZERO + face axes\n" +
                    "Reset #" + resetCount + " via " + lastReset;
                if (readout != null) readout.text = lastReadout;
            }

            if (Time.unscaledTime >= nextLogTime)
            {
                nextLogTime = Time.unscaledTime + 1f;
                Debug.Log("SpatialAI IMU | " + lastReadout.Replace('\n', ' ') +
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

        private static string ReadResetInput(bool triggerPressed)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
                return "touch (Input System)";
            for (var i = 0; i < Input.touchCount; i++)
                if (Input.GetTouch(i).phase == UnityEngine.TouchPhase.Began) return "touch (Unity)";
            if (Input.GetMouseButtonDown(0) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                return "mouse";
            if (Input.GetKeyDown(KeyCode.R) ||
                (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame))
                return "R key";
            return triggerPressed ? "Cardboard trigger" : null;
        }

        private void ResetReference(Quaternion rotation, bool valid, string source)
        {
            referenceRotation = rotation;
            hasReference = valid;
            resetCount++;
            lastReset = source;
            nextReadoutTime = 0f;
            if (axes != null) PlaceWorldAxes();
            if (calibrationTargets != null) PlaceCalibrationTargets();
            Debug.Log("SpatialAI IMU RESET #" + resetCount + " via " + source +
                "; pose " + (valid ? "captured" : "pending"));
        }

        private void CreateWorldAxes()
        {
            axes = new GameObject("World fixed 3DoF axes");
            PlaceWorldAxes();
            CreateAxis("X", Vector3.right, Color.red);
            CreateAxis("Y", Vector3.up, Color.green);
            CreateAxis("Z", Vector3.forward, Color.blue);
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

        private void CreateCalibrationTargets()
        {
            var shader = Resources.Load<Shader>("CalibrationUnlit");
            if (shader == null)
            {
                Debug.LogError("SpatialAI calibration targets need the CalibrationUnlit shader.");
                return;
            }

            calibrationTargets = new GameObject("World fixed scale and distortion targets");
            PlaceCalibrationTargets();

            var cubeDistances = new[] { 0.6f, 1.0f, 1.3f, 1.6f };
            var cubeSizes = new[] { 0.2f, 0.4f, 0.4f, 0.4f };
            var cubeOffsets = new[]
            {
                new Vector2(-0.35f, -0.15f),
                new Vector2(0.50f, -0.20f),
                new Vector2(-0.70f, -0.20f),
                new Vector2(0.90f, -0.20f)
            };
            var cubeColors = new[]
            {
                new Color(0.3f, 1f, 0.35f),
                new Color(0.15f, 0.85f, 0.95f),
                new Color(1f, 0.7f, 0.1f),
                new Color(0.95f, 0.25f, 0.7f)
            };
            for (var i = 0; i < cubeDistances.Length; i++)
            {
                var offset = cubeOffsets[i];
                var depth = Mathf.Sqrt(cubeDistances[i] * cubeDistances[i] -
                    offset.x * offset.x - offset.y * offset.y);
                var cube = new GameObject((cubeSizes[i] * 100f).ToString("0") +
                    " cm cube at " + cubeDistances[i].ToString("0.0") + " m");
                cube.transform.SetParent(calibrationTargets.transform, false);
                cube.transform.localPosition = new Vector3(offset.x, offset.y, depth);
                cube.AddComponent<MeshFilter>().sharedMesh = CreateCubeMesh(cubeSizes[i]);
                cube.AddComponent<MeshRenderer>().sharedMaterial = new Material(shader) { color = cubeColors[i] };
            }

            var board = new GameObject("20 cm checkerboard at 2.6 m");
            board.transform.SetParent(calibrationTargets.transform, false);
            board.transform.localPosition = new Vector3(0f, 0.1f, 2.6f);
            CreateCheckerTiles(board.transform, shader, 0,
                new Color(0.9f, 0.9f, 0.9f));
            CreateCheckerTiles(board.transform, shader, 1,
                new Color(0.12f, 0.16f, 0.23f));
            Debug.Log("SpatialAI calibration targets: 0.20 m cube at 0.6 m; " +
                "0.40 m cubes at 1.0/1.3/1.6 m; " +
                "4.0 x 2.0 m checkerboard at 2.6 m, 0.20 m cells.");
        }

        private void PlaceCalibrationTargets()
        {
            calibrationTargets.transform.SetPositionAndRotation(
                xrCamera.transform.position, xrCamera.transform.rotation);
        }

        private static Mesh CreateCubeMesh(float size)
        {
            var h = size * 0.5f;
            var mesh = new Mesh { name = (size * 100f).ToString("0") + " cm calibration cube" };
            mesh.vertices = new[]
            {
                new Vector3(-h, -h, -h), new Vector3(h, -h, -h),
                new Vector3(h, h, -h), new Vector3(-h, h, -h),
                new Vector3(-h, -h, h), new Vector3(h, -h, h),
                new Vector3(h, h, h), new Vector3(-h, h, h)
            };
            mesh.triangles = new[]
            {
                0, 3, 2, 0, 2, 1, 4, 5, 6, 4, 6, 7,
                0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5,
                0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void CreateCheckerTiles(Transform parent, Shader shader, int parity, Color color)
        {
            const int columns = 20;
            const int rows = 10;
            const float cell = 0.2f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                if ((row + column) % 2 != parity) continue;
                var x = (column - columns * 0.5f) * cell;
                var y = (row - rows * 0.5f) * cell;
                var first = vertices.Count;
                vertices.Add(new Vector3(x, y, 0f));
                vertices.Add(new Vector3(x + cell, y, 0f));
                vertices.Add(new Vector3(x + cell, y + cell, 0f));
                vertices.Add(new Vector3(x, y + cell, 0f));
                triangles.AddRange(new[] { first, first + 2, first + 1,
                    first, first + 3, first + 2 });
            }
            var mesh = new Mesh { name = parity == 0 ? "Checker light tiles" : "Checker dark tiles" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var tiles = new GameObject(mesh.name);
            tiles.transform.SetParent(parent, false);
            tiles.AddComponent<MeshFilter>().sharedMesh = mesh;
            tiles.AddComponent<MeshRenderer>().sharedMaterial = new Material(shader) { color = color };
        }

        private static string Format(float value) { return value.ToString("+0.0;-0.0;0.0"); }
    }
}
#endif
