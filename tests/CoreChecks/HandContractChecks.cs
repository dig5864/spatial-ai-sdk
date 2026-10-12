using System;
using SpatialAI.Core;

static class HandContractChecks
{
    public static void Run(Action<bool, string> check)
    {
        const HandDataFields position = HandDataFields.Position;
        check((int)HandJointId.Count == 26, "26 spatial hand joints");
        check(default(HandJointSample).Validate().Success, "absent joint does not require an orientation");
        check(!new HandJointSample(HandDataFields.None, position).Validate().Success, "invalid estimated-only flag rejected");
        check(!new HandJointSample((HandDataFields)32, HandDataFields.None).Validate().Success, "unknown validity bit rejected");
        check(!new HandJointSample(position, position, new HandVector3(float.NaN, 0, 0)).Validate().Success, "nonfinite metric position rejected");
        check(!new HandJointSample(HandDataFields.Rotation, HandDataFields.None).Validate().Success, "zero valid quaternion rejected");
        check(!new HandJointSample(HandDataFields.Radius, HandDataFields.None, radius: 0).Validate().Success, "zero valid radius rejected");
        check(!new HandJointSample(HandDataFields.LinearVelocity, HandDataFields.None,
            linearVelocity: new HandVector3(float.PositiveInfinity, 0, 0)).Validate().Success, "nonfinite linear velocity rejected");
        check(!new HandJointSample(HandDataFields.AngularVelocity, HandDataFields.None,
            angularVelocity: new HandVector3(0, float.NaN, 0)).Validate().Success, "nonfinite angular velocity rejected");
        var joints = new HandJointSample[26];
        joints[0] = new HandJointSample(position, position, new HandVector3(-0.1f, 0.2f, 0.5f));
        var root = joints[0];
        var left = new SpatialHandSample(HandSide.Left, true, root, joints);
        joints[0] = default(HandJointSample);
        check(left.GetJoint(HandJointId.Wrist).Position.Z == 0.5f, "hand snapshot owns joint array");
        check(left.GetJoint(HandJointId.Wrist).Position.X < 0 && left.Root.Position.Y > 0,
            "left/right and upward origin axes represented in metres");
        check(left.GetJoint(HandJointId.IndexTip).Valid == HandDataFields.None, "missing joint stays invalid");
        var right = SpatialHandSample.Untracked(HandSide.Right);
        check(!right.IsTracked && right.Root.Valid == HandDataFields.None, "lost hand invalidates root");
        check(Throws(() => new SpatialHandSample(HandSide.Right, false, root, joints)), "lost hand cannot retain valid root");
        check(Throws(() => new SpatialHandSample(HandSide.Left, true, root, new HandJointSample[21])), "image layout is not spatial layout");
        check(Throws(() => left.GetJoint(HandJointId.Count)), "sentinel not a joint");
        check(Throws(() => new SpatialHandsFrame("uvc", HandFrameSource.MonocularEstimate, 1, 0,
            -1, HandClockDomain.Unknown, 100, HandClockDomain.HostStopwatch, right, left)), "handedness slots cannot be swapped");
        var frame = new SpatialHandsFrame("uvc", HandFrameSource.MonocularEstimate, 1, 0,
            80, HandClockDomain.AndroidElapsedRealtime, 100, HandClockDomain.HostStopwatch, left, right);
        long latency;
        check(!frame.TryGetSourceToReceiveNs(out latency) && latency == -1, "mixed-clock latency unavailable");
        frame = new SpatialHandsFrame("simulation", HandFrameSource.Simulation, 2, 1,
            80, HandClockDomain.HostStopwatch, 100, HandClockDomain.HostStopwatch, left, right);
        check(frame.TryGetSourceToReceiveNs(out latency) && latency == 20, "same-clock receive latency measurable");
        frame = new SpatialHandsFrame("uvc", HandFrameSource.MonocularEstimate, 3, 1,
            -1, HandClockDomain.Unknown, 100, HandClockDomain.HostStopwatch, left, right);
        check(!frame.TryGetSourceToReceiveNs(out latency), "unknown source timestamp never becomes zero latency");
        check(Throws(() => new SpatialHandsFrame("uvc", HandFrameSource.MonocularEstimate, 1, 0,
            0, HandClockDomain.Unknown, 100, HandClockDomain.HostStopwatch, left, right)), "unknown source timestamp must be -1");
        var supported = new HandDataFields[26]; var estimated = new HandDataFields[26];
        supported[0] = estimated[0] = position;
        var capabilities = new SpatialHandCapabilities(position, supported, estimated, position);
        supported[0] = estimated[0] = HandDataFields.None;
        check(capabilities.Validate(left).Success, "capabilities own layout and accept estimated wrist");
        check(!SpatialHandCapabilities.None.Validate(left).Success, "raw camera source declares no metric capability");
        joints[0] = new HandJointSample(position, HandDataFields.None, new HandVector3(0, 0, 0.5f));
        var unlabelled = new SpatialHandSample(HandSide.Left, true, root, joints);
        check(!capabilities.Validate(unlabelled).Success, "estimated position cannot masquerade as measured");
        check(capabilities.Validate(right).Success, "supported hand may be untracked");
    }
    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (ArgumentException) { return true; }
    }
}
