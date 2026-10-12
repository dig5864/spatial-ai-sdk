using System;
using SpatialAI.Core;
using UnityEngine;

namespace SpatialAI.XRHands
{
    public enum SimulatedHandPose { OpenPalm, Fist, Pinch }
    [Serializable]
    public sealed class SimulatedHandState
    {
        public bool tracked = true;
        public Vector3 position;
        public Vector3 eulerAngles = new Vector3(-90, 0, 0);
        public SimulatedHandPose pose;
        public HandJointId missingJoint = HandJointId.Count;
        public HandJointId missingRotation = HandJointId.Count;
    }

    // Deterministic local skeleton and rigid transforms, independent of cameras.
    // Published snapshots use a unique source session and an explicit host clock.
    public sealed class SimulatedHandsFrameSource : ISpatialHandsFrameSource
    {
        private SpatialHandsFrame latest;
        private long sequence, originRevision;
        public string Id { get; private set; }
        public SpatialHandCapabilities Capabilities { get; private set; }
        public SimulatedHandsFrameSource()
        {
            Id = "simulation:" + Guid.NewGuid().ToString("N");
            var supported = new HandDataFields[26];
            for (int i = 0; i < supported.Length; i++) supported[i] = HandDataFields.Position | HandDataFields.Rotation | HandDataFields.Radius;
            Capabilities = new SpatialHandCapabilities(HandDataFields.Position | HandDataFields.Rotation,
                supported, new HandDataFields[26]);
        }
        public bool TryGetLatestFrame(out SpatialHandsFrame frame) { frame = latest; return frame != null; }
        public void Recenter() { originRevision++; latest = null; }
        public SpatialHandsFrame Publish(SimulatedHandState left, SimulatedHandState right, long timestampNs)
        {
            if (left == null || right == null) throw new ArgumentNullException("hand state");
            latest = new SpatialHandsFrame(Id, HandFrameSource.Simulation, ++sequence, originRevision,
                timestampNs, HandClockDomain.HostStopwatch, timestampNs, HandClockDomain.HostStopwatch,
                Build(HandSide.Left, left), Build(HandSide.Right, right));
            return latest;
        }
        private static SpatialHandSample Build(HandSide side, SimulatedHandState state)
        {
            if (!state.tracked) return SpatialHandSample.Untracked(side);
            var points = new Vector3[26]; var rotations = new Quaternion[26];
            float sign = side == HandSide.Left ? 1 : -1;
            points[0] = Vector3.zero; points[1] = new Vector3(0, 0, 0.035f);
            for (int i = 0; i < rotations.Length; i++) rotations[i] = Quaternion.identity;
            points[2] = new Vector3(sign * 0.035f, 0, 0.02f);
            var thumbDirection = new Vector3(sign * 0.8f, 0, 0.6f);
            for (int i = 3; i <= 5; i++) points[i] = points[i - 1] + thumbDirection * (i == 3 ? 0.030f : i == 4 ? 0.025f : 0.022f);
            float[] widths = { 0.026f, 0.007f, -0.012f, -0.029f };
            float[,] lengths = { { 0.032f, 0.021f, 0.018f }, { 0.038f, 0.025f, 0.018f },
                { 0.033f, 0.021f, 0.018f }, { 0.025f, 0.018f, 0.015f } };
            for (int finger = 0; finger < 4; finger++)
            {
                int start = 6 + finger * 5;
                points[start] = new Vector3(sign * widths[finger], 0, 0.022f);
                points[start + 1] = points[start] + Vector3.forward * 0.038f;
                for (int segment = 0; segment < 3; segment++)
                {
                    float curl = state.pose == SimulatedHandPose.Fist ? 65 + segment * 35 : 0;
                    rotations[start + 1 + segment] = Quaternion.Euler(curl, 0, 0);
                    points[start + 2 + segment] = points[start + 1 + segment] +
                        rotations[start + 1 + segment] * Vector3.forward * lengths[finger, segment];
                }
            }
            if (state.pose == SimulatedHandPose.Fist)
                SolveChain(points, 2, 4, new Vector3(sign * 0.004f, -0.016f, 0.067f));
            if (state.pose == SimulatedHandPose.Pinch)
            {
                var target = new Vector3(sign * 0.023f, -0.018f, 0.079f);
                SolveChain(points, 2, 4, target);
                SolveChain(points, 7, 4, target);
            }
            // +Z points toward the next joint, +Y is the palm normal. On a
            // straight terminal joint inherit the previous bone's orientation.
            for (int i = 2; i < 26; i++)
            {
                bool tip = i == 5 || i >= 6 && (i - 6) % 5 == 4;
                if (tip) rotations[i] = rotations[i - 1];
                else
                {
                    var direction = (points[i + 1] - points[i]).normalized;
                    var up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
                    rotations[i] = Quaternion.LookRotation(direction, up);
                }
            }
            var rootRotation = Quaternion.Euler(state.eulerAngles);
            var joints = new HandJointSample[26];
            for (int i = 0; i < joints.Length; i++)
            {
                if (i == (int)state.missingJoint) continue;
                var valid = HandDataFields.Position | HandDataFields.Rotation | HandDataFields.Radius;
                if (i == (int)state.missingRotation) valid &= ~HandDataFields.Rotation;
                var p = state.position + rootRotation * points[i]; var q = rootRotation * rotations[i];
                joints[i] = new HandJointSample(valid, HandDataFields.None,
                    new HandVector3(p.x, p.y, p.z), new Rotation(q.x, q.y, q.z, q.w), 0.006f);
            }
            var r = rootRotation; var position = state.position;
            var root = new HandJointSample(HandDataFields.Position | HandDataFields.Rotation, HandDataFields.None,
                new HandVector3(position.x, position.y, position.z), new Rotation(r.x, r.y, r.z, r.w));
            return new SpatialHandSample(side, true, root, joints);
        }
        private static void SolveChain(Vector3[] points, int start, int count, Vector3 target)
        {
            var anchor = points[start]; var lengths = new float[count - 1];
            for (int i = 0; i < lengths.Length; i++) lengths[i] = Vector3.Distance(points[start + i], points[start + i + 1]);
            // FABRIK preserves lengths in the curled thumb/index configurations.
            for (int iteration = 0; iteration < 24; iteration++)
            {
                points[start + count - 1] = target;
                for (int i = count - 2; i >= 0; i--)
                    points[start + i] = points[start + i + 1] + (points[start + i] - points[start + i + 1]).normalized * lengths[i];
                points[start] = anchor;
                for (int i = 1; i < count; i++)
                    points[start + i] = points[start + i - 1] + (points[start + i] - points[start + i - 1]).normalized * lengths[i - 1];
            }
        }
    }
}
