#if SPATIALAI_HAS_CARDBOARD
using SpatialAI.Unity;
using UnityEngine;

namespace SpatialAI.Samples
{
    /// <summary>Image-plane diagnostics; the camera-to-eye transform is not calibrated.</summary>
    public sealed class HandTrackingDemo : MonoBehaviour
    {
        public Camera xrCamera;
        private const float Depth = 1.8f;
        private const float PlaneWidth = 1.0f;
        private const float PlaneHeight = 0.5625f;
        private static readonly int[] Edges = {
            0,1, 1,2, 2,3, 3,4, 0,5, 5,6, 6,7, 7,8,
            0,9, 9,10, 10,11, 11,12, 0,13, 13,14, 14,15, 15,16,
            0,17, 17,18, 18,19, 19,20, 5,9, 9,13, 13,17
        };
        private readonly LineRenderer[,] bones = new LineRenderer[2, Edges.Length / 2];
        private SpatialHandTracking tracking;
        private TextMesh status;
        private long lastSequence = -1;
        private float lastFrameTime;
        private int pinchEvents;
        private readonly bool[] wasPinching = new bool[2];

        private void Start()
        {
            if (xrCamera == null) { enabled = false; return; }
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
            label.transform.localPosition = new Vector3(-0.48f, 0.34f, Depth);
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
                    var valid = hand != null && hand.joints != null && hand.joints.Length >= 21;
                    for (var e = 0; e < Edges.Length / 2; e++)
                    {
                        var line = bones[h, e];
                        line.enabled = valid;
                        if (!valid) continue;
                        line.SetPosition(0, OnPlane(hand.joints[Edges[e * 2]]));
                        line.SetPosition(1, OnPlane(hand.joints[Edges[e * 2 + 1]]));
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
                status.text = "HANDS  " + tracking.State + "\n" +
                    "Seen " + count + "   Pinch " + pinchEvents + "\n" +
                    "Image-plane demo; no camera-to-eye calibration";
            }
        }

        private static Vector3 OnPlane(SpatialHandJoint joint)
        {
            return new Vector3((joint.x - 0.5f) * PlaneWidth,
                (0.5f - joint.y) * PlaneHeight, Depth);
        }
    }
}
#endif
