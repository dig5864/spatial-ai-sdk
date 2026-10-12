using System;

namespace SpatialAI.Core
{
    // Stable SDK indices, not XRHandJointID numeric values. All poses use metres in
    // tracking-origin space (+X right, +Y up, +Z forward), before optical reflection.
    public enum HandSide { Left, Right }
    public enum HandJointId
    {
        Wrist, Palm,
        ThumbMetacarpal, ThumbProximal, ThumbDistal, ThumbTip,
        IndexMetacarpal, IndexProximal, IndexIntermediate, IndexDistal, IndexTip,
        MiddleMetacarpal, MiddleProximal, MiddleIntermediate, MiddleDistal, MiddleTip,
        RingMetacarpal, RingProximal, RingIntermediate, RingDistal, RingTip,
        LittleMetacarpal, LittleProximal, LittleIntermediate, LittleDistal, LittleTip,
        Count
    }
    [Flags]
    public enum HandDataFields
    { None = 0, Position = 1, Rotation = 2, Radius = 4, LinearVelocity = 8, AngularVelocity = 16 }
    public enum HandFrameSource { Simulation, MonocularEstimate, HardwareTracking }
    // Android elapsedRealtimeNanos and managed Stopwatch are different clock domains.
    // Never subtract timestamps across domains, or interpret callback time as exposure time.
    public enum HandClockDomain { Unknown, HostStopwatch, AndroidElapsedRealtime, Device }

    public struct HandVector3
    {
        public readonly float X, Y, Z;
        public HandVector3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public bool IsFinite { get { return Finite(X) && Finite(Y) && Finite(Z); } }
        internal static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    // Valid describes this sample; Estimated is a subset of Valid. None means absent,
    // not a zero pose. Velocities are origin-space m/s and rad/s, radius is metres.
    public struct HandJointSample
    {
        public readonly HandVector3 Position, LinearVelocity, AngularVelocity;
        public readonly Rotation Orientation;
        public readonly float Radius;
        public readonly HandDataFields Valid, Estimated;
        public HandJointSample(HandDataFields valid, HandDataFields estimated,
            HandVector3 position = default(HandVector3), Rotation orientation = default(Rotation),
            float radius = 0, HandVector3 linearVelocity = default(HandVector3),
            HandVector3 angularVelocity = default(HandVector3))
        {
            Valid = valid; Estimated = estimated; Position = position; Orientation = orientation;
            Radius = radius; LinearVelocity = linearVelocity; AngularVelocity = angularVelocity;
        }
        public Result Validate()
        {
            const HandDataFields all = HandDataFields.Position | HandDataFields.Rotation |
                HandDataFields.Radius | HandDataFields.LinearVelocity | HandDataFields.AngularVelocity;
            if ((Valid & ~all) != 0 || (Estimated & ~Valid) != 0)
                return Invalid("Unknown fields or estimated fields without valid data.");
            if ((Valid & HandDataFields.Position) != 0 && !Position.IsFinite)
                return Invalid("Position must be finite metres.");
            if ((Valid & HandDataFields.Rotation) != 0 && !Orientation.IsUnit)
                return Invalid("Orientation must be a unit xyzw quaternion.");
            if ((Valid & HandDataFields.Radius) != 0 && (!HandVector3.Finite(Radius) || Radius <= 0))
                return Invalid("Valid radius must be finite and positive metres.");
            if ((Valid & HandDataFields.LinearVelocity) != 0 && !LinearVelocity.IsFinite)
                return Invalid("Linear velocity must be finite m/s.");
            if ((Valid & HandDataFields.AngularVelocity) != 0 && !AngularVelocity.IsFinite)
                return Invalid("Angular velocity must be finite rad/s.");
            return Result.Ok;
        }
        internal static Result Invalid(string message) { return new Result(ErrorCode.InvalidArgument, message); }
    }

    // Immutable snapshots: copy arrays at the worker/main-thread boundary. Root is
    // the wrist pose, independently flagged; root never advertises radius/velocities.
    public sealed class SpatialHandSample
    {
        private readonly HandJointSample[] joints;
        public HandSide Side { get; private set; }
        public bool IsTracked { get; private set; }
        public HandJointSample Root { get; private set; }
        public SpatialHandSample(HandSide side, bool isTracked, HandJointSample root, HandJointSample[] joints)
        {
            if (side != HandSide.Left && side != HandSide.Right) throw new ArgumentOutOfRangeException("side");
            if (joints == null || joints.Length != (int)HandJointId.Count)
                throw new ArgumentException("Exactly 26 joint slots are required.", "joints");
            if (!root.Validate().Success || (root.Valid & ~(HandDataFields.Position | HandDataFields.Rotation)) != 0)
                throw new ArgumentException("Invalid root pose.", "root");
            if (!isTracked && root.Valid != HandDataFields.None)
                throw new ArgumentException("An untracked hand must invalidate its root.", "root");
            foreach (var joint in joints)
                if (!joint.Validate().Success || (!isTracked && joint.Valid != HandDataFields.None))
                    throw new ArgumentException("Invalid joint or stale data on an untracked hand.", "joints");
            Side = side; IsTracked = isTracked; Root = root;
            this.joints = (HandJointSample[])joints.Clone();
        }
        public HandJointSample GetJoint(HandJointId id)
        {
            if ((int)id < 0 || id >= HandJointId.Count) throw new ArgumentOutOfRangeException("id");
            return joints[(int)id];
        }
        public static SpatialHandSample Untracked(HandSide side)
        { return new SpatialHandSample(side, false, default(HandJointSample), new HandJointSample[(int)HandJointId.Count]); }
    }

    public sealed class SpatialHandsFrame
    {
        public string SourceId { get; private set; }
        public HandFrameSource Source { get; private set; }
        // Sequence resets only when SourceId or OriginRevision changes.
        public long Sequence { get; private set; }
        // OriginRevision changes on recenter/calibration; do not blend across revisions.
        public long OriginRevision { get; private set; }
        public long SourceTimestampNs { get; private set; }
        public long ReceivedTimestampNs { get; private set; }
        public HandClockDomain SourceClock { get; private set; }
        public HandClockDomain ReceivedClock { get; private set; }
        public SpatialHandSample Left { get; private set; }
        public SpatialHandSample Right { get; private set; }
        public SpatialHandsFrame(string sourceId, HandFrameSource source, long sequence, long originRevision,
            long sourceTimestampNs, HandClockDomain sourceClock, long receivedTimestampNs,
            HandClockDomain receivedClock, SpatialHandSample left, SpatialHandSample right)
        {
            if (string.IsNullOrEmpty(sourceId) || !Enum.IsDefined(typeof(HandFrameSource), source) ||
                sequence < 0 || originRevision < 0) throw new ArgumentException("Invalid frame identity.");
            if (!Enum.IsDefined(typeof(HandClockDomain), sourceClock) ||
                !Enum.IsDefined(typeof(HandClockDomain), receivedClock) ||
                (sourceClock == HandClockDomain.Unknown ? sourceTimestampNs != -1 : sourceTimestampNs < 0) ||
                (receivedClock != HandClockDomain.HostStopwatch && receivedClock != HandClockDomain.AndroidElapsedRealtime) ||
                receivedTimestampNs < 0)
                throw new ArgumentException("Timestamps require explicit clock domains; unknown source time is -1.");
            if (left == null || right == null || left.Side != HandSide.Left || right.Side != HandSide.Right)
                throw new ArgumentException("Explicit left and right samples are required, including untracked hands.");
            SourceId = sourceId; Source = source; Sequence = sequence; OriginRevision = originRevision;
            SourceTimestampNs = sourceTimestampNs; SourceClock = sourceClock;
            ReceivedTimestampNs = receivedTimestampNs; ReceivedClock = receivedClock; Left = left; Right = right;
        }
        public bool TryGetSourceToReceiveNs(out long latencyNs)
        {
            latencyNs = -1;
            if (SourceClock == HandClockDomain.Unknown || SourceClock != ReceivedClock ||
                ReceivedTimestampNs < SourceTimestampNs) return false;
            latencyNs = ReceivedTimestampNs - SourceTimestampNs;
            return true;
        }
    }

    // Source capability is distinct from per-frame validity. A supported joint may
    // be absent in this frame. Estimated fields require provenance on every valid sample.
    public sealed class SpatialHandCapabilities
    {
        private readonly HandDataFields[] supported, estimated;
        public HandDataFields RootSupported { get; private set; }
        public HandDataFields RootEstimated { get; private set; }
        public SpatialHandCapabilities(HandDataFields rootSupported, HandDataFields[] supported, HandDataFields[] estimated,
            HandDataFields rootEstimated = HandDataFields.None)
        {
            if ((rootSupported & ~(HandDataFields.Position | HandDataFields.Rotation)) != 0 || supported == null ||
                estimated == null || supported.Length != (int)HandJointId.Count || estimated.Length != supported.Length ||
                (rootEstimated & ~rootSupported) != 0)
                throw new ArgumentException("Invalid capability layout.");
            for (int i = 0; i < supported.Length; i++)
                if (((int)supported[i] & ~31) != 0 || (estimated[i] & ~supported[i]) != 0)
                    throw new ArgumentException("Invalid capability fields.");
            RootSupported = rootSupported; RootEstimated = rootEstimated;
            this.supported = (HandDataFields[])supported.Clone(); this.estimated = (HandDataFields[])estimated.Clone();
        }
        private static int Index(HandJointId id)
        {
            if ((int)id < 0 || id >= HandJointId.Count) throw new ArgumentOutOfRangeException("id");
            return (int)id;
        }
        public HandDataFields GetSupported(HandJointId id) { return supported[Index(id)]; }
        public HandDataFields GetEstimated(HandJointId id) { return estimated[Index(id)]; }
        public Result Validate(SpatialHandSample hand)
        {
            if (hand == null || (hand.Root.Valid & ~RootSupported) != 0 ||
                (hand.Root.Estimated & ~RootEstimated) != 0 ||
                (hand.Root.Valid & RootEstimated & ~hand.Root.Estimated) != 0)
                return HandJointSample.Invalid("Root exceeds source capabilities.");
            for (int i = 0; i < supported.Length; i++)
            {
                var joint = hand.GetJoint((HandJointId)i);
                if ((joint.Valid & ~supported[i]) != 0 || (joint.Estimated & ~estimated[i]) != 0 ||
                    (joint.Valid & estimated[i] & ~joint.Estimated) != 0)
                    return HandJointSample.Invalid("Joint fields exceed capabilities or omit estimation provenance.");
            }
            return Result.Ok;
        }
        // Raw MediaPipe image landmarks are not a spatial source; no metric capability yet.
        public static SpatialHandCapabilities None
        { get { return new SpatialHandCapabilities(HandDataFields.None, new HandDataFields[26], new HandDataFields[26]); } }
    }

    // Main-thread, nonblocking read of the latest immutable snapshot. Source lifecycle
    // and XRHandSubsystem ownership are implemented in later stages.
    public interface ISpatialHandsFrameSource
    {
        string Id { get; }
        SpatialHandCapabilities Capabilities { get; }
        bool TryGetLatestFrame(out SpatialHandsFrame frame);
    }
}
