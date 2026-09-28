using System;
using System.Diagnostics;

namespace SpatialAI.Core
{
    [Flags]
    public enum Capability
    {
        None = 0, HeadOrientation = 1, ControllerOrientation = 2, Trigger = 4,
        StereoDisplay = 8, Camera = 16, HandTracking = 32, SpatialAI = 64
    }

    public enum SessionState { Stopped, Starting, Running, Suspended, Error, Disposed }
    public enum ErrorCode { None, Unsupported, InvalidArgument, NotRunning, BackendFailure, NativeLibraryMissing }

    public struct Result
    {
        public readonly ErrorCode Code;
        public readonly string Message;
        public bool Success { get { return Code == ErrorCode.None; } }
        public Result(ErrorCode code, string message) { Code = code; Message = message ?? ""; }
        public static Result Ok { get { return new Result(ErrorCode.None, ""); } }
    }

    // Unity-compatible left-handed tracking coordinates: +X right, +Y up, +Z forward.
    // Quaternions are xyzw. Positions, when introduced, must be metres.
    public struct Rotation
    {
        public readonly float X, Y, Z, W;
        public Rotation(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
        public static Rotation Identity { get { return new Rotation(0, 0, 0, 1); } }
        public bool IsUnit
        {
            get
            {
                float norm = X * X + Y * Y + Z * Z + W * W;
                return !float.IsNaN(norm) && !float.IsInfinity(norm) && Math.Abs(norm - 1f) < 0.001f;
            }
        }
        public Rotation Inverse() { return new Rotation(-X, -Y, -Z, W); }
        public static Rotation operator *(Rotation a, Rotation b)
        {
            return new Rotation(a.W*b.X + a.X*b.W + a.Y*b.Z - a.Z*b.Y,
                a.W*b.Y - a.X*b.Z + a.Y*b.W + a.Z*b.X,
                a.W*b.Z + a.X*b.Y - a.Y*b.X + a.Z*b.W,
                a.W*b.W - a.X*b.X - a.Y*b.Y - a.Z*b.Z);
        }
        public static Rotation FromYawPitch(float yawDegrees, float pitchDegrees)
        {
            double yaw = yawDegrees * Math.PI / 360, pitch = pitchDegrees * Math.PI / 360;
            return new Rotation(0, (float)Math.Sin(yaw), 0, (float)Math.Cos(yaw)) *
                new Rotation((float)Math.Sin(pitch), 0, 0, (float)Math.Cos(pitch));
        }
        public float YawDegrees
        {
            get { return (float)(Math.Atan2(2 * (W * Y + X * Z), 1 - 2 * (X * X + Y * Y)) * 180 / Math.PI); }
        }
    }

    public struct PoseSample
    {
        public readonly Rotation Orientation;
        public readonly long TimestampNs;
        public readonly bool IsTracked;
        public PoseSample(Rotation orientation, long timestampNs, bool isTracked)
        { Orientation = orientation; TimestampNs = timestampNs; IsTracked = isTracked; }
    }

    public struct RuntimeFrame
    {
        public readonly PoseSample Head, Controller;
        public readonly bool TriggerHeld;
        public RuntimeFrame(PoseSample head, PoseSample controller, bool triggerHeld)
        { Head = head; Controller = controller; TriggerHeld = triggerHeld; }
    }

    public interface IClock { long NowNs { get; } }
    public sealed class MonotonicClock : IClock
    {
        public long NowNs { get { return (long)(Stopwatch.GetTimestamp() * (1000000000.0 / Stopwatch.Frequency)); } }
    }

    // Single-threaded contract. The Unity host invokes these methods on its main thread.
    // A hardware implementation must transfer immutable samples from its worker threads.
    public interface IRuntimeBackend : IDisposable
    {
        string Id { get; }
        bool IsSimulated { get; }
        Capability Capabilities { get; }
        Result Start();
        void Stop();
        Result ReadFrame(out RuntimeFrame frame);
        Result Recenter();
    }
}
