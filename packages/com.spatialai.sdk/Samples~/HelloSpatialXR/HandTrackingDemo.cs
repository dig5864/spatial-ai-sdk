#if SPATIALAI_HAS_CARDBOARD
using SpatialAI.Unity;
using UnityEngine;

namespace SpatialAI.Samples
{
    /// <summary>RGB image points in the eye render frame at an assumed camera depth.</summary>
    public sealed class HandTrackingDemo : MonoBehaviour
    {
        public Camera xrCamera;
        public float assumedHandDepthMetres = 0.35f;
        private static readonly int[] Edges = {
            0,1, 1,2, 2,3, 3,4, 0,5, 5,6, 6,7, 7,8,
            0,9, 9,10, 10,11, 11,12, 0,13, 13,14, 14,15, 15,16,
            0,17, 17,18, 18,19, 19,20, 5,9, 9,13, 13,17
        };
        private readonly LineRenderer[,] bones = new LineRenderer[2, Edges.Length / 2];
        private SpatialHandTracking tracking;
        private SpatialRgbCalibration calibration;
        private TextMesh status;
        private long lastSequence = -1;
        private float lastFrameTime;
        private int pinchEvents;
        private readonly bool[] wasPinching = new bool[2];

        private void Start()
        {
            if (xrCamera == null) { enabled = false; return; }
            calibration = SpatialRgbCalibration.LoadRhinoXReference();
            tracking = gameObject.AddComponent<SpatialHandTracking>();
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            for (var hand = 0; hand < 2; hand++)
            {
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
            }
            var label = new GameObject("Hand tracking readout");
            label.transform.SetParent(xrCamera.transform, false);
            label.transform.localPosition = new Vector3(-0.48f, 0.34f, 1.8f);
            status = label.AddComponent<TextMesh>();
            status.anchor = TextAnchor.UpperLeft;
            status.fontSize = 48;
            status.characterSize = 0.008f;
            status.color = Color.white;
        }

        private void LateUpdate()
        {
            if (tracking == null) return;
            var frame = tracking.Latest;
            if (frame != null && frame.sequence != lastSequence)
            {
                lastSequence = frame.sequence;
                lastFrameTime = Time.unscaledTime;
                for (var h = 0; h < 2; h++)
                {
                    var hand = frame.hands != null && h < frame.hands.Length ? frame.hands[h] : null;
                    var valid = hand != null && hand.joints != null && hand.joints.Length >= 21 &&
                        calibration != null && frame.cameraId == calibration.cameraId &&
                        frame.imageWidth == calibration.imageWidth &&
                        frame.imageHeight == calibration.imageHeight;
                    for (var e = 0; e < Edges.Length / 2; e++)
                    {
                        var line = bones[h, e];
                        line.enabled = valid;
                        if (!valid) continue;
                        Vector3 first, second;
                        if (!OnPlane(hand.joints[Edges[e * 2]], frame, out first) ||
                            !OnPlane(hand.joints[Edges[e * 2 + 1]], frame, out second))
                        {
                            line.enabled = false;
                            continue;
                        }
                        line.SetPosition(0, first);
                        line.SetPosition(1, second);
                    }
                    var pinching = valid && hand.Pinching;
                    if (pinching && !wasPinching[h]) pinchEvents++;
                    wasPinching[h] = pinching;
                }
            }
            if (Time.unscaledTime - lastFrameTime > 0.35f)
                for (var h = 0; h < 2; h++)
                    for (var e = 0; e < Edges.Length / 2; e++) bones[h, e].enabled = false;
            if (status != null)
            {
                var count = frame != null && frame.hands != null && Time.unscaledTime - lastFrameTime < 0.35f
                    ? frame.hands.Length : 0;
                var geometry = calibration == null ? "RGB calibration missing" :
                    frame == null ? "Waiting for RGB frame" :
                    frame.cameraId != calibration.cameraId ? "RGB camera ID mismatch: " + frame.cameraId :
                    frame.imageWidth == calibration.imageWidth && frame.imageHeight == calibration.imageHeight
                        ? "RGB -> eye: design extrinsics" :
                        "RGB size " + frame.imageWidth + "x" + frame.imageHeight +
                        " differs from 1440x1080";
                status.text = "HANDS  " + tracking.State + "\n" +
                    "Seen " + count + "   Pinch " + pinchEvents + "\n" +
                    geometry + "\nFPS " + (frame == null ? 0f : frame.resultFps).ToString("F1") +
                    "  CV " + (frame == null ? 0f : frame.conversionMs).ToString("F0") +
                    "ms  AI " + (frame == null ? 0f : frame.inferenceMs).ToString("F0") +
                    "ms  Age " + (frame == null ? -1f : frame.captureToResultMs).ToString("F0") +
                    "ms\nDepth assumed " + assumedHandDepthMetres.ToString("F2") + "m";
            }
        }

        private bool OnPlane(SpatialHandJoint joint, SpatialHandFrame frame, out Vector3 point)
        {
            point = Vector3.zero;
            var depth = Mathf.Max(0.2f, assumedHandDepthMetres);
            return calibration.TryEyePoint(joint, frame.imageWidth, frame.imageHeight, depth, out point);
        }
    }
}
#endif
