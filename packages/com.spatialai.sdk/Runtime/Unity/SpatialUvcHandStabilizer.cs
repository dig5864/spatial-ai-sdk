using System;
using UnityEngine;

namespace SpatialAI.Unity
{
    // Image-space identity association, not anatomical inference or world tracking.
    // Keep an acquired label during continuity; expire after 250 ms without detection.
    // Large jumps, camera changes, clock changes and sequence restarts reset identities.
    public sealed class SpatialUvcHandStabilizer
    {
        private sealed class Track
        {
            public int Id;
            public string Label, LastRaw;
            public Vector2 Wrist;
            public long SeenNs;
        }
        private readonly Track[] tracks = new Track[2];
        private int nextId;
        private string camera;
        private int width, height;
        private long lastSequence = -1, lastNs = -1;
        private const long LostNs = 250_000_000;
        private const float GateSquared = .18f * .18f;
        public bool IsAlive(int id, long hostNs)
        {
            foreach (var track in tracks)
                if (track != null && id > 0 && track.Id == id && hostNs >= track.SeenNs && hostNs - track.SeenNs <= LostNs) return true;
            return false;
        }
        public void Reset()
        { tracks[0] = tracks[1] = null; camera = null; lastSequence = lastNs = -1; }
        private static bool Wrist(SpatialHand hand, out Vector2 point)
        {
            point = Vector2.zero;
            if (hand == null || hand.joints == null || hand.joints.Length == 0 || hand.joints[0] == null) return false;
            var p = hand.joints[0];
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y) ||
                p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) return false;
            point = new Vector2(p.x, p.y); return true;
        }
        public SpatialHandFrame Process(SpatialHandFrame input, long hostNs, out string events)
        {
            events = "";
            if (input == null) { Reset(); return null; }
            if (camera != input.cameraId || width != input.imageWidth || height != input.imageHeight ||
                input.sequence <= lastSequence || hostNs < lastNs)
            { Reset(); events = "reset; "; }
            camera = input.cameraId; width = input.imageWidth; height = input.imageHeight;
            lastSequence = input.sequence; lastNs = hostNs;
            for (int t = 0; t < 2; t++) if (tracks[t] != null && hostNs - tracks[t].SeenNs > LostNs)
            { events += "expired track=" + tracks[t].Id + "; "; tracks[t] = null; }
            var raw = input.hands ?? new SpatialHand[0];
            var result = new SpatialHand[raw.Length];
            var points = new Vector2[raw.Length]; var usable = new bool[raw.Length];
            for (int i = 0; i < raw.Length; i++) usable[i] = Wrist(raw[i], out points[i]);
            // Exhaust both assignments: hand array order may change between frames.
            int aBest = -1, bBest = -1; float best = float.MaxValue;
            for (int a = -1; a < 2; a++) for (int b = -1; b < 2; b++)
            {
                if (a >= 0 && a == b || raw.Length < 2 && b >= 0 || raw.Length == 0 && a >= 0) continue;
                float cost = 0; bool valid = true;
                for (int i = 0; i < Math.Min(2, raw.Length); i++)
                {
                    int t = i == 0 ? a : b;
                    if (t < 0) { cost += GateSquared; continue; }
                    if (!usable[i] || tracks[t] == null) { valid = false; break; }
                    float d = (points[i] - tracks[t].Wrist).sqrMagnitude;
                    if (d > GateSquared) { valid = false; break; }
                    cost += d + (tracks[t].Label == raw[i].rawHandedness ? 0 : .000001f);
                }
                if (valid && cost < best) { best = cost; aBest = a; bBest = b; }
            }
            var assigned = new[] { aBest, bBest }; var occupied = new bool[2];
            if (aBest >= 0) occupied[aBest] = true;
            if (bBest >= 0) occupied[bBest] = true;
            for (int i = 0; i < raw.Length; i++)
            {
                var h = raw[i]; if (h == null) continue;
                int t = i < 2 ? assigned[i] : -1;
                if (t < 0 && i < 2 && usable[i] && (h.rawHandedness == "Left" || h.rawHandedness == "Right"))
                {
                    for (int slot = 0; slot < 2; slot++) if (!occupied[slot])
                    { t = slot; occupied[slot] = true; tracks[slot] = new Track { Id = ++nextId,
                        Label = h.rawHandedness, LastRaw = h.rawHandedness };
                        events += "acquired track=" + nextId + " raw=" + h.rawHandedness + "; "; break; }
                }
                var track = t < 0 ? null : tracks[t];
                if (track != null)
                {
                    if (track.LastRaw != h.rawHandedness)
                        events += "label_change track=" + track.Id + " raw=" + h.rawHandedness + " stable=" + track.Label + "; ";
                    track.LastRaw = h.rawHandedness; track.Wrist = points[i]; track.SeenNs = hostNs;
                }
                result[i] = new SpatialHand { joints = h.joints, rawHandedness = h.rawHandedness,
                    rawGesture = h.rawGesture, gestureScore = h.gestureScore, handednessScore = h.handednessScore,
                    hasHandednessScore = h.hasHandednessScore, stabilizedHandedness = track == null ? null : track.Label,
                    trackingId = track == null ? 0 : track.Id };
            }
            return new SpatialHandFrame { cameraId = input.cameraId, imageWidth = input.imageWidth, imageHeight = input.imageHeight,
                cameraSource=input.cameraSource,inputWidth=input.inputWidth,inputHeight=input.inputHeight,
                sequence = input.sequence, timestampNs = input.timestampNs, receivedAtNs = input.receivedAtNs,
                resultFps = input.resultFps, conversionMs = input.conversionMs, inferenceMs = input.inferenceMs,
                captureToResultMs = input.captureToResultMs, gestureSource = input.gestureSource,
                runningMode=input.runningMode, @delegate=input.@delegate,
                receivedHostStopwatchNs=input.receivedHostStopwatchNs, resultAgeAtReceiveMs=input.resultAgeAtReceiveMs, hands = result };
        }
    }
}
