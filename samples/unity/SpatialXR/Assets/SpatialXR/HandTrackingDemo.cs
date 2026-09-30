#if SPATIALAI_HAS_CARDBOARD
using SpatialAI.Unity;
using UnityEngine;

namespace SpatialAI.Samples
{
    /// <summary>RGB hand landmarks projected into eye space with approximate per-hand depth.</summary>
    public sealed class HandTrackingDemo : MonoBehaviour
    {
        public Camera xrCamera;
        public float assumedHandDepthMetres = 0.35f;
        public float palmWidthMetres = 0.075f;
        public float wristToMiddleMetres = 0.10f;
        private static readonly int[] Edges = {
            0,1, 1,2, 2,3, 3,4, 0,5, 5,6, 6,7, 7,8,
            0,9, 9,10, 10,11, 11,12, 0,13, 13,14, 14,15, 15,16,
            0,17, 17,18, 18,19, 19,20, 5,9, 9,13, 13,17
        };
        private static readonly Vector3[] TargetPositions = {
            new Vector3(-0.17f, -0.13f, 0.8f),
            new Vector3(0.17f, -0.13f, 0.8f)
        };
        private readonly LineRenderer[,] bones = new LineRenderer[2, Edges.Length / 2];
        private readonly GameObject[] cursors = new GameObject[2];
        private readonly Material[] targetMaterials = new Material[2];
        private readonly bool[] targetHovered = new bool[2];
        private readonly float[] targetFeedbackUntil = new float[2];
        private SpatialHandTracking tracking;
        private SpatialHandFrame latestFrame;
        private SpatialRgbCalibration calibration;
        private TextMesh status;
        private long lastSequence = -1;
        private float lastFrameTime;
        private int pinchEvents;
        private int selectionEvents;
        private int frameEvents;
        private readonly bool[] wasPinching = new bool[2];
        private readonly float[] handDepth = new float[2];
        private readonly bool[] hasHandDepth = new bool[2];
        private readonly Vector2[] lastWrist = new Vector2[2];
        private readonly Vector3[,] eyeJoints = new Vector3[2, 21];
        private readonly bool[,] eyeJointValid = new bool[2, 21];
        private bool initialized;
        private static Mesh sphereMesh;

        private static GameObject CreateVisualSphere(string name, Material material)
        {
            if (sphereMesh == null)
            {
                const int longitude = 24;
                const int latitude = 12;
                var vertices = new Vector3[(longitude + 1) * (latitude + 1)];
                var triangles = new int[longitude * latitude * 6];
                for (var y = 0; y <= latitude; y++)
                {
                    var polar = Mathf.PI * y / latitude;
                    for (var x = 0; x <= longitude; x++)
                    {
                        var azimuth = 2f * Mathf.PI * x / longitude;
                        vertices[y * (longitude + 1) + x] = new Vector3(
                            0.5f * Mathf.Sin(polar) * Mathf.Cos(azimuth),
                            0.5f * Mathf.Cos(polar),
                            0.5f * Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    }
                }
                for (var y = 0; y < latitude; y++)
                    for (var x = 0; x < longitude; x++)
                    {
                        var a = y * (longitude + 1) + x;
                        var b = a + longitude + 1;
                        var t = (y * longitude + x) * 6;
                        triangles[t] = a;
                        triangles[t + 1] = a + 1;
                        triangles[t + 2] = b;
                        triangles[t + 3] = b;
                        triangles[t + 4] = a + 1;
                        triangles[t + 5] = b + 1;
                    }
                sphereMesh = new Mesh { name = "SpatialAI hand target sphere" };
                sphereMesh.vertices = vertices;
                sphereMesh.triangles = triangles;
                sphereMesh.RecalculateNormals();
            }
            var visual = new GameObject(name);
            visual.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            return visual;
        }

        private void Start()
        {
            if (xrCamera == null) { enabled = false; return; }
            Debug.Log("SpatialAI hand demo init: calibration");
            calibration = SpatialRgbCalibration.LoadRhinoXReference();
            Debug.Log("SpatialAI hand demo init: tracker");
            tracking = GetComponent<SpatialHandTracking>();
            if (tracking == null) tracking = gameObject.AddComponent<SpatialHandTracking>();
            tracking.FrameUpdated += OnHandFrame;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var targetShader = Shader.Find("Unlit/Color");
            if (targetShader == null) targetShader = shader;
            if (shader == null || targetShader == null)
            {
                Debug.LogError("SpatialAI hand demo requires an included unlit shader.");
                enabled = false;
                return;
            }
            for (var hand = 0; hand < 2; hand++)
            {
                Debug.Log("SpatialAI hand demo init: visuals " + hand);
                var material = new Material(shader);
                material.color = hand == 0 ? Color.green : Color.cyan;
                for (var edge = 0; edge < Edges.Length / 2; edge++)
                {
                    var segment = new GameObject("Hand " + hand + " bone " + edge);
                    segment.transform.SetParent(xrCamera.transform, false);
                    var line = segment.AddComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    line.positionCount = 2;
                    line.startWidth = line.endWidth = 0.006f;
                    line.sharedMaterial = material;
                    line.enabled = false;
                    bones[hand, edge] = line;
                }
                var cursor = CreateVisualSphere("Hand " + hand + " pinch pointer", material);
                cursor.transform.SetParent(xrCamera.transform, false);
                cursor.transform.localScale = Vector3.one * 0.025f;
                cursor.SetActive(false);
                cursors[hand] = cursor;
            }
            for (var i = 0; i < TargetPositions.Length; i++)
            {
                Debug.Log("SpatialAI hand demo init: target " + i);
                var material = new Material(targetShader);
                material.color = Color.cyan;
                var target = CreateVisualSphere("Pinch target " + (i + 1), material);
                target.transform.SetParent(xrCamera.transform, false);
                target.transform.localPosition = TargetPositions[i];
                target.transform.localScale = Vector3.one * 0.13f;
                targetMaterials[i] = material;
            }
            var label = new GameObject("Hand tracking readout");
            label.transform.SetParent(xrCamera.transform, false);
            label.transform.localPosition = new Vector3(0.05f, 0.34f, 1.8f);
            status = label.AddComponent<TextMesh>();
            status.anchor = TextAnchor.UpperLeft;
            status.fontSize = 48;
            status.characterSize = 0.006f;
            status.color = Color.white;
            initialized = true;
            Debug.Log("SpatialAI hand interaction demo ready");
        }

        private void OnEnable()
        {
            if (tracking != null) tracking.FrameUpdated += OnHandFrame;
        }

        private void OnDisable()
        {
            if (tracking != null) tracking.FrameUpdated -= OnHandFrame;
            latestFrame = null;
            for (var h = 0; h < cursors.Length; h++)
                if (cursors[h] != null) cursors[h].SetActive(false);
        }

        private void OnHandFrame(SpatialHandFrame frame)
        {
            latestFrame = frame;
            frameEvents++;
        }

        private static string HandGestureText(SpatialHandFrame frame, string handedness)
        {
            if (frame == null || frame.hands == null) return handedness + ": --";
            for (var i = 0; i < frame.hands.Length; i++)
            {
                var hand = frame.hands[i];
                if (hand == null || hand.rawHandedness != handedness) continue;
                return handedness + ": " + hand.Gesture + " " +
                    hand.gestureScore.ToString("F2") + (hand.Pinching ? " PINCH" : "");
            }
            return handedness + ": --";
        }

        private void LateUpdate()
        {
            if (!initialized || tracking == null) return;
            var frame = latestFrame;
            if (frame != null && frame.sequence != lastSequence)
            {
                lastSequence = frame.sequence;
                lastFrameTime = Time.unscaledTime;
                for (var i = 0; i < targetHovered.Length; i++) targetHovered[i] = false;
                for (var h = 0; h < 2; h++)
                {
                    var hand = frame.hands != null && h < frame.hands.Length ? frame.hands[h] : null;
                    var valid = hand != null && hand.joints != null && hand.joints.Length >= 21 &&
                        hand.joints[0] != null &&
                        calibration != null && frame.cameraId == calibration.cameraId &&
                        frame.imageWidth == calibration.imageWidth &&
                        frame.imageHeight == calibration.imageHeight;
                    if (valid)
                    {
                        float measuredDepth;
                        var wrist = new Vector2(hand.joints[0].x, hand.joints[0].y);
                        if (calibration.TryEstimateApproximateHandDepth(hand.joints,
                            frame.imageWidth, frame.imageHeight, palmWidthMetres,
                            wristToMiddleMetres, out measuredDepth))
                        {
                            var sameHand = hasHandDepth[h] &&
                                (wrist - lastWrist[h]).sqrMagnitude < 0.09f;
                            handDepth[h] = sameHand ? Mathf.Lerp(handDepth[h], measuredDepth, 0.55f)
                                : measuredDepth;
                            hasHandDepth[h] = true;
                        }
                        else if (!hasHandDepth[h]) handDepth[h] = Mathf.Max(0.2f, assumedHandDepthMetres);
                        lastWrist[h] = wrist;
                        for (var j = 0; j < 21; j++)
                            eyeJointValid[h, j] = calibration.TryEyePoint(hand.joints[j],
                                frame.imageWidth, frame.imageHeight, handDepth[h], out eyeJoints[h, j]);
                    }
                    else hasHandDepth[h] = false;
                    for (var e = 0; e < Edges.Length / 2; e++)
                    {
                        var line = bones[h, e];
                        var first = Edges[e * 2];
                        var second = Edges[e * 2 + 1];
                        line.enabled = valid && eyeJointValid[h, first] && eyeJointValid[h, second];
                        if (!line.enabled) continue;
                        line.SetPosition(0, eyeJoints[h, first]);
                        line.SetPosition(1, eyeJoints[h, second]);
                    }
                    var hovered = -1;
                    cursors[h].SetActive(false);
                    if (valid && eyeJointValid[h, 4] && eyeJointValid[h, 8])
                    {
                        var pinchPoint = (eyeJoints[h, 4] + eyeJoints[h, 8]) * 0.5f;
                        if (pinchPoint.z > 0.05f)
                        {
                            var scale = TargetPositions[0].z / pinchPoint.z;
                            var pointer = new Vector3(pinchPoint.x * scale,
                                pinchPoint.y * scale, TargetPositions[0].z - 0.08f);
                            cursors[h].transform.localPosition = pointer;
                            cursors[h].SetActive(true);
                            for (var i = 0; i < TargetPositions.Length; i++)
                            {
                                var difference = pointer - TargetPositions[i];
                                if (difference.x * difference.x + difference.y * difference.y < 0.08f * 0.08f)
                                {
                                    hovered = i;
                                    targetHovered[i] = true;
                                    break;
                                }
                            }
                        }
                    }
                    var pinching = valid && hand.Pinching;
                    if (pinching && !wasPinching[h])
                    {
                        pinchEvents++;
                        if (hovered >= 0)
                        {
                            selectionEvents++;
                            targetFeedbackUntil[hovered] = Time.unscaledTime + 0.6f;
                        }
                        Debug.Log("SpatialAI HAND_PINCH hand=" + h + " target=" + hovered +
                            " pinch=" + pinchEvents + " select=" + selectionEvents +
                            " depth=" + handDepth[h].ToString("F2") + "m");
                    }
                    wasPinching[h] = pinching;
                }
            }
            if (Time.unscaledTime - lastFrameTime > 0.35f)
            {
                for (var h = 0; h < 2; h++)
                {
                    if (cursors[h] != null) cursors[h].SetActive(false);
                    for (var e = 0; e < Edges.Length / 2; e++) bones[h, e].enabled = false;
                }
                for (var i = 0; i < targetHovered.Length; i++) targetHovered[i] = false;
            }
            for (var i = 0; i < targetMaterials.Length; i++)
                if (targetMaterials[i] != null)
                    targetMaterials[i].color = Time.unscaledTime < targetFeedbackUntil[i] ? Color.green :
                        targetHovered[i] ? Color.yellow : Color.cyan;
            if (status != null)
            {
                var count = frame != null && frame.hands != null && Time.unscaledTime - lastFrameTime < 0.35f
                    ? frame.hands.Length : 0;
                var freshFrame = count > 0 ? frame : null;
                status.text = "GESTURES | " + (frame == null ? tracking.State :
                    frame.resultFps.ToString("F1") + " FPS") + "\n" +
                    "Hands " + count + "  Pinch " + pinchEvents + "  Select " + selectionEvents + "\n" +
                    HandGestureText(freshFrame, "Left") + "\n" +
                    HandGestureText(freshFrame, "Right") + "\n" +
                    "AI " + (frame == null ? 0f : frame.inferenceMs).ToString("F0") +
                    "ms Age " + (frame == null ? -1f : frame.captureToResultMs).ToString("F0") + "ms\n" +
                    "Point at orb, then pinch";
            }
        }
    }
}
#endif
